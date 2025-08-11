# 🚀 AWS SAM Local Development Guide

This guide shows you how to use **AWS SAM CLI** to run and debug your Lambda function locally in a **realistic AWS environment**.

## 📋 Prerequisites

### Install AWS SAM CLI
```bash
# macOS (using Homebrew)
brew install aws-sam-cli

# Verify installation
sam --version
```

### Install Docker
```bash
# Docker is required for SAM local
# Download from: https://www.docker.com/products/docker-desktop
# Or install via Homebrew
brew install --cask docker
```

## 🔧 Setup Instructions

### 1. Configure Environment
```bash
cd /Users/shall/dev/Git/shall-ffn/esizzle/lambda/pdf-processor

# Edit env.json with your local database settings
nano env.json
```

**Key Settings:**
```json
{
  "PdfProcessorFunction": {
    "DB_HOST": "host.docker.internal",        // Docker access to host
    "DB_PASSWORD": "your_actual_password",    // Your MySQL password
    "API_BASE_URL": "http://host.docker.internal:5000"  // Your esizzle-api
  }
}
```

### 2. Install Python Dependencies
```bash
# SAM will use requirements.txt
pip3 install -r requirements.txt
```

## 🚀 Running with SAM Local

### **Option 1: Direct Lambda Invocation**
```bash
# Test with sample payload
sam local invoke PdfProcessorFunction \
  --event test_payload.json \
  --env-vars env.json

# With debug logging
sam local invoke PdfProcessorFunction \
  --event test_payload.json \
  --env-vars env.json \
  --debug
```

### **Option 2: API Gateway Simulation**
```bash
# Start local API Gateway + Lambda
sam local start-api --env-vars env.json --port 3000

# Test via HTTP (in another terminal)
curl -X POST http://localhost:3000/pdf-processor \
  -H "Content-Type: application/json" \
  -d @test_payload.json
```

### **Option 3: Lambda Function URL**
```bash
# Start Lambda function endpoint
sam local start-lambda --env-vars env.json --port 3001

# Your esizzle-api can call: http://localhost:3001/2015-03-31/functions/PdfProcessorFunction/invocations
```

## 🔄 Integration with Your esizzle-api

### **Update LambdaService for SAM Local**
Add to your `appsettings.json`:
```json
{
  "Lambda": {
    "UseLocalMode": true,
    "LocalUrl": "http://localhost:3001/2015-03-31/functions/PdfProcessorFunction/invocations"
  }
}
```

### **Full Workflow Test**
```bash
# Terminal 1: Start SAM local Lambda
sam local start-lambda --env-vars env.json --port 3001

# Terminal 2: Start your esizzle-api
cd /path/to/esizzle-api
dotnet run

# Terminal 3: Start your frontend
cd /path/to/esizzle-frontend
npm run dev

# Now test: Frontend → API → SAM Local Lambda → API → Frontend
```

## 🐛 Debugging with SAM

### **1. Enable Debug Mode**
```bash
sam local invoke PdfProcessorFunction \
  --event test_payload.json \
  --env-vars env.json \
  --debug \
  --debugger-path ./debugpy \
  --debug-args "-m debugpy --listen 0.0.0.0:5678 --wait-for-client"
```

### **2. VS Code Debugging**
Create `.vscode/launch.json`:
```json
{
  "version": "0.2.0",
  "configurations": [
    {
      "name": "SAM Local Debug",
      "type": "python",
      "request": "attach",
      "port": 5678,
      "host": "localhost",
      "pathMappings": [
        {
          "localRoot": "${workspaceFolder}",
          "remoteRoot": "/var/task"
        }
      ]
    }
  ]
}
```

### **3. Step-by-Step Debugging**
```bash
# 1. Start SAM in debug mode
sam local invoke PdfProcessorFunction \
  --event test_payload.json \
  --env-vars env.json \
  --debug-port 5678

# 2. Attach VS Code debugger (F5)
# 3. Set breakpoints in your Python code
# 4. Step through execution
```

## 📊 SAM Advantages Over Custom Script

| Feature | Custom Script | AWS SAM |
|---------|---------------|---------|
| **Environment** | Simulated | Real Lambda runtime |
| **Dependencies** | Local Python | Lambda layers |
| **Debugging** | Basic logging | Full IDE debugging |
| **API Integration** | Manual HTTP | Built-in API Gateway |
| **AWS Services** | Mocked | Real AWS SDK calls |
| **Deployment** | Manual | `sam deploy` |

## 🎯 Common SAM Commands

```bash
# Build the application
sam build

# Test function locally
sam local invoke PdfProcessorFunction --event test_payload.json

# Start API locally
sam local start-api --port 3000

# Start Lambda endpoint
sam local start-lambda --port 3001

# Deploy to AWS
sam deploy --guided

# Generate sample events
sam local generate-event apigateway aws-proxy
```

## 🔧 Configuration Files

### **template.yaml**
- Defines Lambda function configuration
- Environment variables
- IAM permissions (for deployment)
- API Gateway routes

### **env.json** 
- Local environment variables
- Database connection settings
- API callback URLs

### **test_payload.json**
- Sample Lambda invocation payload
- Matches your IndexingController format

## 🚨 Troubleshooting

### **Docker Issues**
```bash
# Make sure Docker is running
docker ps

# Pull Lambda runtime image
docker pull public.ecr.aws/lambda/python:3.9
```

### **Database Connection**
```bash
# Test database connectivity from Docker
docker run --rm -it mysql:8 mysql -h host.docker.internal -u root -p
```

### **API Callback Issues**
```bash
# Verify your esizzle-api is accessible
curl http://host.docker.internal:5000/api/health

# Check SAM logs
sam local invoke --debug
```

## 🎉 Why SAM is Better

1. **🔄 Real Lambda Environment**: Runs in actual Lambda runtime
2. **🐛 Professional Debugging**: Full IDE integration
3. **📡 API Gateway Simulation**: Test HTTP endpoints
4. **🚀 Easy Deployment**: `sam deploy` to AWS
5. **⚡ Hot Reloading**: Code changes auto-reload
6. **📊 CloudWatch Logs**: Realistic logging experience

**SAM gives you the closest experience to production Lambda without deploying to AWS!** 🚀
