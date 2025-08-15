#!/usr/bin/env python3
"""
Local Debug Script for PDF Processor Lambda

This script allows you to run and debug the Lambda function locally
without deploying to AWS. It simulates the Lambda environment and
provides debugging capabilities.

Usage:
    python local_debug.py
    python local_debug.py --test-payload test_payload.json
    python local_debug.py --mock-s3 --mock-db

Author: Esizzle Development Team
Date: January 2025
"""

import os
import sys
import json
import logging
import argparse
from pathlib import Path
from typing import Dict, Any, Optional
from unittest.mock import Mock, patch

# Add current directory to Python path for imports
current_dir = Path(__file__).parent
sys.path.insert(0, str(current_dir))

# Set up logging for debug mode
logging.basicConfig(
    level=logging.DEBUG,
    format='%(asctime)s - %(name)s - %(levelname)s - %(message)s',
    handlers=[
        logging.StreamHandler(sys.stdout),
        logging.FileHandler('lambda_debug.log')
    ]
)

logger = logging.getLogger(__name__)

class LocalLambdaContext:
    """Mock Lambda context for local testing"""
    
    def __init__(self):
        self.function_name = "pdf-processor-local"
        self.function_version = "$LATEST"
        self.invoked_function_arn = "arn:aws:lambda:us-east-1:123456789012:function:pdf-processor-local"
        self.memory_limit_in_mb = 512
        self.remaining_time_in_millis = 300000  # 5 minutes
        self.aws_request_id = "local-debug-request-123"
        self.log_group_name = "/aws/lambda/pdf-processor-local"
        self.log_stream_name = "2025/01/11/[$LATEST]local-debug"

def setup_local_environment():
    """Set up local environment variables for testing"""
    
    # Default environment variables for local testing
    default_env = {
        'S3_BUCKET_NAME': 'esizzle-documents-dev',
        'DB_HOST': 'localhost',
        'DB_USER': 'root',
        'DB_PASSWORD': 'password',
        'DB_NAME': 'loanmaster',
        'DB_PORT': '3306',
        'API_BASE_URL': 'http://localhost:5000',
        'API_SERVICE_TOKEN': 'local-debug-token',
        'API_TIMEOUT': '30',
        'API_RETRY_ATTEMPTS': '3'
    }
    
    # Load from .env file if it exists
    env_file = current_dir / '.env'
    if env_file.exists():
        logger.info(f"Loading environment from {env_file}")
        with open(env_file, 'r') as f:
            for line in f:
                line = line.strip()
                if line and not line.startswith('#') and '=' in line:
                    key, value = line.split('=', 1)
                    default_env[key.strip()] = value.strip()
    
    # Set environment variables
    for key, value in default_env.items():
        if key not in os.environ:
            os.environ[key] = value
            logger.debug(f"Set {key}={value}")

def create_test_payload() -> Dict[str, Any]:
    """Create a test payload for local debugging"""
    
    return {
        "documentId": 123,
        "sessionId": "local-debug-session-001",
        "operation": "split_document",
        "bookmarks": [
            {
                "bookmarkId": 1,
                "pageIndex": 0,
                "documentTypeId": 42,
                "documentTypeName": "Agreement",
                "documentDate": "2024-08-11",
                "comments": "First document"
            },
            {
                "bookmarkId": 2,
                "pageIndex": 5,
                "documentTypeId": 43,
                "documentTypeName": "Amendment",
                "documentDate": "2024-08-11",
                "comments": "Second document"
            }
        ],
        "metadata": {
            "userId": 21496,
            "bucketPrefix": "ffn",
            "offeringId": 302,
            "loanId": 456
        }
    }

def load_test_payload(payload_file: str) -> Dict[str, Any]:
    """Load test payload from JSON file"""
    
    payload_path = Path(payload_file)
    if not payload_path.exists():
        logger.error(f"Payload file not found: {payload_file}")
        return create_test_payload()
    
    try:
        with open(payload_path, 'r') as f:
            payload = json.load(f)
        logger.info(f"Loaded test payload from {payload_file}")
        return payload
    except Exception as e:
        logger.error(f"Failed to load payload file {payload_file}: {e}")
        return create_test_payload()

def mock_s3_operations():
    """Mock S3 operations for local testing but create real PDFs"""
    from reportlab.pdfgen import canvas
    from reportlab.lib.pagesizes import letter
    import io
    import os
    
    def mock_download_pdf_from_s3(document_id):
        logger.info(f"MOCK: Generating realistic 15-page PDF for document {document_id}")
        
        # Create a realistic 15-page PDF using reportlab
        buffer = io.BytesIO()
        c = canvas.Canvas(buffer, pagesize=letter)
        width, height = letter
        
        # Generate 15 pages with different content
        for page_num in range(1, 16):
            c.drawString(100, height - 100, f"Document ID: {document_id}")
            c.drawString(100, height - 130, f"Page {page_num} of 15")
            c.drawString(100, height - 160, f"Generated for PDF splitting test")
            
            # Add some content based on page ranges that will be split
            if page_num <= 5:
                c.drawString(100, height - 200, "AGREEMENT SECTION")
                c.drawString(100, height - 230, "This content belongs to the Agreement document type.")
            elif page_num <= 10:
                c.drawString(100, height - 200, "AMENDMENT SECTION") 
                c.drawString(100, height - 230, "This content belongs to the Amendment document type.")
            else:
                c.drawString(100, height - 200, "SCHEDULE SECTION")
                c.drawString(100, height - 230, "This content belongs to the Schedule document type.")
            
            # Add page footer
            c.drawString(100, 50, f"Test PDF - Page {page_num}")
            c.showPage()
        
        c.save()
        pdf_data = buffer.getvalue()
        
        # Save source PDF to disk for inspection
        source_file = f"source_document_{document_id}.pdf"
        with open(source_file, 'wb') as f:
            f.write(pdf_data)
        logger.info(f"MOCK: Source PDF saved to {source_file} ({len(pdf_data)} bytes)")
        
        return pdf_data
    
    def mock_upload_split_to_s3(pdf_data, filename):
        logger.info(f"MOCK: Saving split PDF {filename} to disk ({len(pdf_data)} bytes)")
        
        # Save split PDF to disk
        split_file = f"split_{filename}"
        with open(split_file, 'wb') as f:
            f.write(pdf_data)
        
        mock_s3_key = f"IProcessing/Images/{filename}"
        logger.info(f"MOCK: Split PDF saved to {split_file}, S3 key: {mock_s3_key}")
        return mock_s3_key
    
    return mock_download_pdf_from_s3, mock_upload_split_to_s3

def mock_database_operations():
    """Mock database operations for local testing"""
    
    # Counter for unique image IDs
    _image_id_counter = [1000]
    
    def mock_create_image_record(source_document_id, split_range, s3_key, filename, page_count, metadata):
        # Generate unique image ID for each split
        _image_id_counter[0] += 1
        mock_image_id = _image_id_counter[0]
        logger.info(f"MOCK: Created Image record {mock_image_id} for split document")
        logger.debug(f"MOCK: Split range: {split_range}")
        logger.debug(f"MOCK: S3 key: {s3_key}, filename: {filename}, pages: {page_count}")
        return mock_image_id
    
    def mock_update_image_record_s3_info(image_id, s3_key, filename):
        logger.info(f"MOCK: Updated Image record {image_id} with S3 info: {s3_key}")
        logger.debug(f"MOCK: Filename: {filename}")
        return True
    
    def mock_update_database_records(source_document_id, bookmarks, results):
        logger.info(f"MOCK: Updated database records for document {source_document_id}")
        logger.debug(f"MOCK: Updated {len(results)} bookmark results")
        return True
    
    def mock_mark_original_document_obsolete(document_id):
        logger.info(f"MOCK: Marked original document {document_id} as obsolete")
        return True
    
    return mock_create_image_record, mock_update_image_record_s3_info, mock_update_database_records, mock_mark_original_document_obsolete

def mock_api_callbacks():
    """Mock API callbacks for local testing"""
    
    def mock_update_processing_status(session_id, status, progress=None, message=None, error=None):
        logger.info(f"MOCK API: Processing status update - Session: {session_id}, Status: {status}")
        if progress is not None:
            logger.info(f"MOCK API: Progress: {progress}%")
        if message:
            logger.info(f"MOCK API: Message: {message}")
        if error:
            logger.error(f"MOCK API: Error: {error}")
        return True
    
    def mock_link_processing_results(document_id, results):
        logger.info(f"MOCK API: Linking {len(results)} processing results for document {document_id}")
        for i, result in enumerate(results):
            logger.debug(f"MOCK API: Result {i+1}: {result.get('resultImageId')} ({result.get('documentTypeName')})")
        return True
    
    return mock_update_processing_status, mock_link_processing_results

def run_lambda_locally(payload: Dict[str, Any], use_mocks: bool = True):
    """Run the Lambda function locally with optional mocking"""
    
    logger.info("="*60)
    logger.info("STARTING LOCAL LAMBDA EXECUTION")
    logger.info("="*60)
    
    # Setup environment
    setup_local_environment()
    
    # Apply mocks if requested
    patches = []
    
    if use_mocks:
        logger.info("Setting up mocks for local testing...")
        
        # Mock S3 operations
        mock_download, mock_upload = mock_s3_operations()
        patches.extend([
            patch('s3_operations.download_pdf_from_s3', side_effect=mock_download),
            patch('s3_operations.upload_split_to_s3', side_effect=mock_upload)
        ])
        
        # Mock database operations
        mock_create_image, mock_update_image_s3, mock_update_db, mock_mark_obsolete = mock_database_operations()
        patches.extend([
            patch('database_operations.create_image_record', side_effect=mock_create_image),
            patch('database_operations.update_image_record_s3_info', side_effect=mock_update_image_s3),
            patch('database_operations.update_database_records', side_effect=mock_update_db),
            patch('database_operations.mark_original_document_obsolete', side_effect=mock_mark_obsolete)
        ])
        
        # Mock API callbacks
        mock_status_update, mock_link_results = mock_api_callbacks()
        patches.extend([
            patch('api_callbacks.update_processing_status', side_effect=mock_status_update),
            patch('api_callbacks.link_processing_results', side_effect=mock_link_results)
        ])
    
    # Start all patches
    for p in patches:
        p.start()
    
    try:
        # Import and run the Lambda function
        from lambda_function import lambda_handler
        
        # Create mock context
        context = LocalLambdaContext()
        
        logger.info(f"Invoking Lambda with payload: {json.dumps(payload, indent=2)}")
        
        # Execute the Lambda function
        result = lambda_handler(payload, context)
        
        logger.info("="*60)
        logger.info("LAMBDA EXECUTION COMPLETED")
        logger.info("="*60)
        logger.info(f"Result: {json.dumps(result, indent=2)}")
        
        return result
        
    except Exception as e:
        logger.error("="*60)
        logger.error("LAMBDA EXECUTION FAILED")
        logger.error("="*60)
        logger.error(f"Error: {e}")
        logger.error("Stack trace:", exc_info=True)
        return {
            'statusCode': 500,
            'body': json.dumps({
                'status': 'error',
                'error': str(e)
            })
        }
    
    finally:
        # Stop all patches
        for p in patches:
            p.stop()

def main():
    """Main entry point for local debugging"""
    
    parser = argparse.ArgumentParser(description='Local Lambda Debug Runner')
    parser.add_argument('--test-payload', '-p', help='Path to test payload JSON file')
    parser.add_argument('--mock-s3', action='store_true', help='Mock S3 operations')
    parser.add_argument('--mock-db', action='store_true', help='Mock database operations')
    parser.add_argument('--mock-api', action='store_true', help='Mock API callbacks')
    parser.add_argument('--no-mocks', action='store_true', help='Disable all mocks (use real services)')
    parser.add_argument('--debug', '-d', action='store_true', help='Enable debug logging')
    
    args = parser.parse_args()
    
    if args.debug:
        logging.getLogger().setLevel(logging.DEBUG)
    
    # Load test payload
    if args.test_payload:
        payload = load_test_payload(args.test_payload)
    else:
        payload = create_test_payload()
    
    # Determine whether to use mocks
    use_mocks = not args.no_mocks
    
    logger.info(f"Starting local Lambda debugging...")
    logger.info(f"Mocks enabled: {use_mocks}")
    logger.info(f"Current directory: {current_dir}")
    
    # Run the Lambda function
    result = run_lambda_locally(payload, use_mocks)
    
    # Save result to file
    result_file = current_dir / 'lambda_result.json'
    with open(result_file, 'w') as f:
        json.dump(result, f, indent=2)
    
    logger.info(f"Result saved to {result_file}")

if __name__ == "__main__":
    main()
