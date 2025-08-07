import * as cdk from 'aws-cdk-lib';
import * as lambda from 'aws-cdk-lib/aws-lambda';
import * as iam from 'aws-cdk-lib/aws-iam';
import * as s3 from 'aws-cdk-lib/aws-s3';
import * as ec2 from 'aws-cdk-lib/aws-ec2';
import * as logs from 'aws-cdk-lib/aws-logs';
import { Construct } from 'constructs';

export interface EsizzlePdfManipulationStackProps extends cdk.StackProps {
  environment: string;
  config: {
    dbHost: string;
    dbName: string;
    dbUser: string;
    s3Bucket: string;
    apiUrl: string;
  };
}

export class EsizzlePdfManipulationStack extends cdk.Stack {
  constructor(scope: Construct, id: string, props: EsizzlePdfManipulationStackProps) {
    super(scope, id, props);

    // VPC Configuration (assuming existing VPC)
    const vpc = ec2.Vpc.fromLookup(this, 'DefaultVPC', {
      isDefault: true
    });

    // Security Group for Lambda
    const lambdaSecurityGroup = new ec2.SecurityGroup(this, 'LambdaSecurityGroup', {
      vpc,
      description: 'Security group for PDF manipulation Lambda',
      allowAllOutbound: true
    });

    // Add egress rules for database and S3 access
    lambdaSecurityGroup.addEgressRule(
      ec2.Peer.anyIpv4(),
      ec2.Port.tcp(3306), // MySQL
      'Allow MySQL database access'
    );

    lambdaSecurityGroup.addEgressRule(
      ec2.Peer.anyIpv4(),
      ec2.Port.tcp(443), // HTTPS
      'Allow HTTPS outbound traffic'
    );

    // S3 Bucket reference (assuming existing bucket)
    const documentsBucket = s3.Bucket.fromBucketName(
      this,
      'DocumentsBucket',
      props.config.s3Bucket
    );

    // IAM Role for Lambda
    const lambdaRole = new iam.Role(this, 'PdfProcessorLambdaRole', {
      assumedBy: new iam.ServicePrincipal('lambda.amazonaws.com'),
      description: 'IAM role for PDF processor Lambda function',
      managedPolicies: [
        iam.ManagedPolicy.fromAwsManagedPolicyName('service-role/AWSLambdaVPCAccessExecutionRole'),
        iam.ManagedPolicy.fromAwsManagedPolicyName('service-role/AWSLambdaBasicExecutionRole')
      ]
    });

    // S3 permissions for Lambda
    documentsBucket.grantReadWrite(lambdaRole);
    
    // Additional S3 permissions for specific paths
    lambdaRole.addToPolicy(new iam.PolicyStatement({
      effect: iam.Effect.ALLOW,
      actions: [
        's3:GetObject',
        's3:PutObject',
        's3:DeleteObject',
        's3:ListBucket'
      ],
      resources: [
        documentsBucket.bucketArn,
        `${documentsBucket.bucketArn}/*`
      ]
    }));

    // CloudWatch Logs permissions
    lambdaRole.addToPolicy(new iam.PolicyStatement({
      effect: iam.Effect.ALLOW,
      actions: [
        'logs:CreateLogGroup',
        'logs:CreateLogStream',
        'logs:PutLogEvents'
      ],
      resources: ['*']
    }));

    // Lambda Layer for PyMuPDF and dependencies
    const pymupdfLayer = new lambda.LayerVersion(this, 'PyMuPDFLayer', {
      layerVersionName: `esizzle-pymupdf-layer-${props.environment}`,
      code: lambda.Code.fromAsset('../lambda/layers/pymupdf-layer'),
      compatibleRuntimes: [lambda.Runtime.PYTHON_3_9],
      description: 'PyMuPDF and PDF processing dependencies'
    });

    // Lambda Layer for common utilities
    const utilsLayer = new lambda.LayerVersion(this, 'UtilsLayer', {
      layerVersionName: `esizzle-utils-layer-${props.environment}`,
      code: lambda.Code.fromAsset('../lambda/layers/utils-layer'),
      compatibleRuntimes: [lambda.Runtime.PYTHON_3_9],
      description: 'Common utilities and database drivers'
    });

    // Main PDF Processor Lambda Function
    const pdfProcessorLambda = new lambda.Function(this, 'PdfProcessorLambda', {
      functionName: `esizzle-pdf-processor-${props.environment}`,
      runtime: lambda.Runtime.PYTHON_3_9,
      handler: 'main.lambda_handler',
      code: lambda.Code.fromAsset('../lambda/pdf-processor'),
      timeout: cdk.Duration.minutes(15), // Maximum Lambda timeout
      memorySize: 3008, // Maximum memory for PDF processing
      role: lambdaRole,
      vpc,
      vpcSubnets: {
        subnetType: ec2.SubnetType.PRIVATE_WITH_EGRESS
      },
      securityGroups: [lambdaSecurityGroup],
      layers: [pymupdfLayer, utilsLayer],
      environment: {
        // Database configuration
        DB_HOST: props.config.dbHost,
        DB_NAME: props.config.dbName,
        DB_USER: props.config.dbUser,
        DB_PASSWORD_SECRET_NAME: `esizzle-db-password-${props.environment}`,
        
        // S3 configuration
        S3_BUCKET: props.config.s3Bucket,
        
        // API configuration
        API_BASE_URL: props.config.apiUrl,
        PROGRESS_CALLBACK_URL: `${props.config.apiUrl}/api/processing/progress`,
        
        // Environment
        ENVIRONMENT: props.environment,
        
        // Logging
        LOG_LEVEL: props.environment === 'prod' ? 'INFO' : 'DEBUG',
        
        // Processing configuration
        MAX_PROCESSING_TIME: '840', // 14 minutes in seconds
        TEMP_DIR: '/tmp',
        
        // Feature flags
        ENABLE_RASTERIZATION: 'true',
        ENABLE_PROGRESS_CALLBACKS: 'true'
      },
      deadLetterQueue: undefined, // We'll handle errors in the function
      reservedConcurrentExecutions: 50 // Limit concurrent executions
    });

    // CloudWatch Log Group with retention
    new logs.LogGroup(this, 'PdfProcessorLogGroup', {
      logGroupName: `/aws/lambda/${pdfProcessorLambda.functionName}`,
      retention: props.environment === 'prod' 
        ? logs.RetentionDays.THREE_MONTHS 
        : logs.RetentionDays.ONE_WEEK,
      removalPolicy: cdk.RemovalPolicy.DESTROY
    });

    // Lambda Alias for versioning
    const lambdaAlias = new lambda.Alias(this, 'PdfProcessorAlias', {
      aliasName: props.environment,
      version: pdfProcessorLambda.currentVersion,
      description: `PDF processor Lambda alias for ${props.environment} environment`
    });

    // Additional Lambda for health checks and status monitoring
    const healthCheckLambda = new lambda.Function(this, 'HealthCheckLambda', {
      functionName: `esizzle-pdf-health-check-${props.environment}`,
      runtime: lambda.Runtime.PYTHON_3_9,
      handler: 'health.lambda_handler',
      code: lambda.Code.fromAsset('../lambda/health-check'),
      timeout: cdk.Duration.minutes(1),
      memorySize: 256,
      role: lambdaRole,
      vpc,
      vpcSubnets: {
        subnetType: ec2.SubnetType.PRIVATE_WITH_EGRESS
      },
      securityGroups: [lambdaSecurityGroup],
      environment: {
        DB_HOST: props.config.dbHost,
        DB_NAME: props.config.dbName,
        DB_USER: props.config.dbUser,
        DB_PASSWORD_SECRET_NAME: `esizzle-db-password-${props.environment}`,
        S3_BUCKET: props.config.s3Bucket,
        ENVIRONMENT: props.environment
      }
    });

    // Output important values
    new cdk.CfnOutput(this, 'PdfProcessorLambdaArn', {
      value: pdfProcessorLambda.functionArn,
      description: 'ARN of the PDF processor Lambda function',
      exportName: `${id}-PdfProcessorLambdaArn`
    });

    new cdk.CfnOutput(this, 'PdfProcessorLambdaName', {
      value: pdfProcessorLambda.functionName,
      description: 'Name of the PDF processor Lambda function',
      exportName: `${id}-PdfProcessorLambdaName`
    });

    new cdk.CfnOutput(this, 'HealthCheckLambdaArn', {
      value: healthCheckLambda.functionArn,
      description: 'ARN of the health check Lambda function',
      exportName: `${id}-HealthCheckLambdaArn`
    });

    new cdk.CfnOutput(this, 'LambdaSecurityGroupId', {
      value: lambdaSecurityGroup.securityGroupId,
      description: 'Security Group ID for Lambda functions',
      exportName: `${id}-LambdaSecurityGroupId`
    });

    // Tags
    cdk.Tags.of(this).add('Project', 'ESizzle-PDF-Manipulation');
    cdk.Tags.of(this).add('Environment', props.environment);
    cdk.Tags.of(this).add('Component', 'Lambda-Infrastructure');
  }
}
