# 🚀 PDF Processor Lambda - Local Debugging Guide

This guide shows you how to run and debug the AWS Lambda PDF processing function locally without deploying to AWS.

## 📋 Prerequisites

- **Python 3.9+** (compatible with AWS Lambda runtime)
- **pip** for installing dependencies
- **Access to local MySQL database** (for full testing)
- **Local Esizzle API running** (for callback testing)

## 🔧 Setup Instructions

### 1. Install Dependencies

```bash
cd /Users/shall/dev/Git/shall-ffn/esizzle/lambda/pdf-processor
pip install -r requirements.txt
```

### 2. Configure Environment

```bash
# Copy the environment template
cp .env.template .env

# Edit .env with your local settings
nano .env
```

**Key Configuration:**
```bash
# Database (update with your local MySQL settings)
DB_HOST=localhost
DB_USER=root
DB_PASSWORD=your_password
DB_NAME=loanmaster

# API (your local Esizzle API)
API_BASE_URL=http://localhost:5000

# S3 (for real S3 testing, or use mocks)
S3_BUCKET_NAME=your-test-bucket
```

## 🏃‍♂️ Running Locally

### Basic Usage (With Mocks)

```bash
# Run with default test payload and all mocks enabled
python local_debug.py

# Run with debug logging
python local_debug.py --debug
```

### Custom Test Payload

```bash
# Run with your own test payload
python local_debug.py --test-payload test_payload.json

# Edit test_payload.json to match your test scenario
```

### Real Services Testing

```bash
# Use real database and S3 (no mocks)
python local_debug.py --no-mocks

# Use real database but mock S3
python local_debug.py --mock-s3
```

## 🧪 Testing Scenarios

### 1. Simple Document Processing (No Splits)
```json
{
  "documentId": 123,
  "sessionId": "test-simple-001",
  "operation": "split_document", 
  "bookmarks": [],
  "metadata": {
    "userId": 21496,
    "bucketPrefix": "ffn"
  }
}
```

### 2. Single Break (Index Only)
```json
{
  "bookmarks": [
    {
      "bookmarkId": 1,
      "pageIndex": 0,
      "documentTypeId": 42,
      "documentTypeName": "Agreement"
    }
  ]
}
```

### 3. Multiple Breaks (Document Splitting)
```json
{
  "bookmarks": [
    {
      "bookmarkId": 1,
      "pageIndex": 0,
      "documentTypeId": 42,
      "documentTypeName": "Agreement"
    },
    {
      "bookmarkId": 2,
      "pageIndex": 5,
      "documentTypeId": 43,
      "documentTypeName": "Amendment"
    }
  ]
}
```

## 🔍 Debug Output

The debug script provides comprehensive logging:

### Console Output
```
2025-01-11 14:49:25 - INFO - PDF Processor Lambda started
2025-01-11 14:49:25 - INFO - Processing session debug-session-001
2025-01-11 14:49:25 - INFO - MOCK: Downloading PDF 123 from bucket prefix ffn
2025-01-11 14:49:25 - INFO - PDF validation successful: 10 pages
2025-01-11 14:49:25 - INFO - Generated 3 split ranges
2025-01-11 14:49:25 - INFO - Processing split: pages 0-4
2025-01-11 14:49:25 - INFO - MOCK: Created Image record 1123
```

### Log Files
- **`lambda_debug.log`** - Detailed execution log
- **`lambda_result.json`** - Final result output

## 🎯 Integration Testing

### Test with Real API

1. **Start your local Esizzle API**:
   ```bash
   cd /path/to/esizzle-api
   dotnet run
   ```

2. **Run Lambda with API callbacks**:
   ```bash
   python local_debug.py --mock-s3 --mock-db
   ```

3. **Check API logs** for status updates and result linking

### Test with Real Database

1. **Ensure MySQL is running** with the `loanmaster` database
2. **Update `.env`** with correct database credentials
3. **Run with database enabled**:
   ```bash
   python local_debug.py --mock-s3 --mock-api
   ```

## 🐛 Debugging Tips

### 1. Enable Verbose Logging
```bash
python local_debug.py --debug
```

### 2. Check Import Issues
```bash
# Test individual modules
python -c "import lambda_function; print('Main function OK')"
python -c "import s3_operations; print('S3 operations OK')"
python -c "import database_operations; print('Database OK')"
python -c "import api_callbacks; print('API callbacks OK')"
```

### 3. Validate Environment
```bash
# Check environment variables
python -c "import os; print('DB_HOST:', os.environ.get('DB_HOST'))"
```

### 4. Test Database Connection
```python
from database_operations import validate_database_connection
print("Database OK:", validate_database_connection())
```

### 5. Test API Connection
```python
from api_callbacks import validate_api_connectivity
print("API OK:", validate_api_connectivity())
```

## 📁 Output Files

After running, check these files:

- **`lambda_debug.log`** - Complete execution log
- **`lambda_result.json`** - Lambda function result
- **`.env`** - Your local configuration

## 🚨 Common Issues

### Import Errors
```bash
# Make sure you're in the correct directory
cd /Users/shall/dev/Git/shall-ffn/esizzle/lambda/pdf-processor

# Install dependencies
pip install -r requirements.txt
```

### Database Connection Issues
- Check MySQL is running: `mysql -u root -p`
- Verify database exists: `USE loanmaster;`
- Update `.env` with correct credentials

### S3 Access Issues
- Use `--mock-s3` flag to bypass S3
- Check AWS credentials if using real S3
- Verify bucket name in `.env`

### API Callback Issues
- Ensure Esizzle API is running on `localhost:5000`
- Use `--mock-api` to bypass API calls
- Check API logs for incoming requests

## 🎯 Expected Output

**Successful Run:**
```json
{
  "statusCode": 200,
  "body": {
    "status": "completed",
    "sessionId": "debug-session-001",
    "processedDocuments": 3,
    "results": [
      {
        "originalImageId": 123,
        "resultImageId": 1123,
        "startPage": 0,
        "endPage": 4,
        "documentTypeName": "Agreement"
      }
    ]
  }
}
```

This local debugging setup allows you to **test the complete Lambda workflow** without deploying to AWS, making development and debugging much faster! 🚀
