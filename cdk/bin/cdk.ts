#!/usr/bin/env node
import 'source-map-support/register';
import * as cdk from 'aws-cdk-lib';
import { EsizzlePdfManipulationStack } from '../lib/esizzle-pdf-manipulation-stack';

const app = new cdk.App();

// Get environment configuration
const account = process.env.CDK_DEFAULT_ACCOUNT;
const region = process.env.CDK_DEFAULT_REGION || 'us-east-1';

// Environment-specific configuration
const envConfig = {
  dev: {
    dbHost: process.env.DEV_DB_HOST || 'localhost',
    dbName: process.env.DEV_DB_NAME || 'LoanMaster_Dev',
    dbUser: process.env.DEV_DB_USER || 'esizzle_api',
    s3Bucket: process.env.DEV_S3_BUCKET || 'esizzle-documents-dev',
    apiUrl: process.env.DEV_API_URL || 'https://dev-api.esizzle.com'
  },
  staging: {
    dbHost: process.env.STAGING_DB_HOST || 'staging-db.esizzle.com',
    dbName: process.env.STAGING_DB_NAME || 'LoanMaster_Staging',
    dbUser: process.env.STAGING_DB_USER || 'esizzle_api',
    s3Bucket: process.env.STAGING_S3_BUCKET || 'esizzle-documents-staging',
    apiUrl: process.env.STAGING_API_URL || 'https://staging-api.esizzle.com'
  },
  prod: {
    dbHost: process.env.PROD_DB_HOST || 'prod-db.esizzle.com',
    dbName: process.env.PROD_DB_NAME || 'LoanMaster',
    dbUser: process.env.PROD_DB_USER || 'esizzle_api',
    s3Bucket: process.env.PROD_S3_BUCKET || 'esizzle-documents',
    apiUrl: process.env.PROD_API_URL || 'https://api.esizzle.com'
  }
};

// Determine environment
const environment = process.env.ENVIRONMENT || 'dev';
const config = envConfig[environment as keyof typeof envConfig] || envConfig.dev;

new EsizzlePdfManipulationStack(app, `EsizzlePdfManipulation-${environment}`, {
  env: { account, region },
  environment,
  config,
  tags: {
    Environment: environment,
    Project: 'ESizzle-PDF-Manipulation',
    ManagedBy: 'CDK'
  }
});
