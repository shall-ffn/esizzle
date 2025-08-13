"""
S3 Operations Helper for PDF Processor Lambda

Handles all S3 interactions including downloads, uploads, and bucket management
following the legacy Hydra DD bucket structure.

Author: Esizzle Development Team
Date: January 2025
"""

import boto3
import logging
import os
from botocore.exceptions import ClientError
from typing import Optional
from datetime import datetime

logger = logging.getLogger(__name__)

# Initialize S3 client
s3_client = boto3.client('s3')

def download_pdf_from_s3(document_id: int) -> bytes:
    """
    Download PDF from S3 using simplified bucket structure
    
    Args:
        document_id: Document ID
        
    Returns:
        bytes: PDF file content
        
    Raises:
        Exception: If download fails
    """
    try:
        bucket_name = get_s3_bucket_name()
        s3_key = f"IProcessing/Images/{document_id}.pdf"
        
        logger.info(f"Downloading PDF {document_id} from s3://{bucket_name}/{s3_key}")
        
        response = s3_client.get_object(
            Bucket=bucket_name,
            Key=s3_key
        )
        
        pdf_data = response['Body'].read()
        logger.info(f"Successfully downloaded PDF {document_id}: {len(pdf_data)} bytes")
        return pdf_data
        
    except ClientError as e:
        error_code = e.response['Error']['Code']
        if error_code == 'NoSuchKey':
            logger.error(f"PDF {document_id} not found in S3: {s3_key}")
        else:
            logger.error(f"S3 download failed for PDF {document_id}: {e}")
        raise Exception(f"Failed to download PDF {document_id}: {e}")
    except Exception as e:
        logger.error(f"Unexpected error downloading PDF {document_id}: {e}")
        raise Exception(f"Failed to download PDF {document_id}: {e}")

def get_s3_bucket_name() -> str:
    """
    Get S3 bucket name from environment variables
    
    Returns:
        str: S3 bucket name
        
    Raises:
        ValueError: If bucket name not configured
    """
    bucket_name = os.environ.get('S3_BUCKET_NAME')
    if not bucket_name:
        raise ValueError("S3_BUCKET_NAME environment variable not set")
    return bucket_name

def get_s3_key_for_document(document_id: int, is_split: bool = False, 
                           split_sequence: Optional[int] = None) -> str:
    """
    Generate S3 key using simplified structure
    
    Args:
        document_id: Document ID
        is_split: Whether this is a split document
        split_sequence: Split sequence number if is_split=True
        
    Returns:
        str: S3 key path
    """
    if is_split and split_sequence is not None:
        return f"IProcessing/Images/{document_id}_split_{split_sequence}.pdf"
    else:
        return f"IProcessing/Images/{document_id}.pdf"

def upload_split_to_s3(pdf_data: bytes, filename: str) -> str:
    """
    Upload split PDF to S3 using simplified structure
    
    Args:
        pdf_data: PDF file bytes
        filename: Generated filename for the split
        
    Returns:
        str: S3 key of uploaded file
        
    Raises:
        Exception: If upload fails
    """
    try:
        bucket_name = get_s3_bucket_name()
        s3_key = f"IProcessing/Images/{filename}"
        
        logger.info(f"Uploading split PDF to s3://{bucket_name}/{s3_key}")
        
        # Upload with proper content type and metadata
        s3_client.put_object(
            Bucket=bucket_name,
            Key=s3_key,
            Body=pdf_data,
            ContentType='application/pdf',
            Metadata={
                'source': 'esizzle-pdf-processor',
                'processing_date': str(datetime.utcnow()),
                'file_type': 'split_document'
            }
        )
        
        logger.info(f"Successfully uploaded split PDF: {len(pdf_data)} bytes to {s3_key}")
        return s3_key
        
    except ClientError as e:
        error_code = e.response['Error']['Code']
        logger.error(f"S3 upload failed with error {error_code}: {e}")
        raise Exception(f"Failed to upload split PDF: {e}")
    except Exception as e:
        logger.error(f"Unexpected error during S3 upload: {e}")
        raise Exception(f"Upload failed: {e}")

def verify_s3_access() -> bool:
    """
    Verify S3 access and bucket structure
    
    Returns:
        bool: True if access is working
    """
    try:
        bucket_name = get_s3_bucket_name()
        test_key = "IProcessing/Images/"
        
        # List objects to verify access
        response = s3_client.list_objects_v2(
            Bucket=bucket_name,
            Prefix=test_key,
            MaxKeys=1
        )
        
        logger.info(f"S3 access verified for bucket {bucket_name}")
        return True
        
    except Exception as e:
        logger.error(f"S3 access verification failed: {e}")
        return False

def cleanup_partial_uploads(session_id: str):
    """
    Clean up any partial uploads in case of processing failure
    
    Args:
        session_id: Processing session ID for tracking
    """
    try:
        bucket_name = get_s3_bucket_name()
        
        # List objects with session ID in metadata (if any were uploaded)
        # This is a safety measure for cleanup
        logger.info(f"Cleaning up partial uploads for session {session_id}")
        
        # Note: In practice, we would track uploads during processing
        # and clean up specific files. For now, this is a placeholder
        # for the cleanup mechanism.
        
    except Exception as e:
        logger.error(f"Failed to cleanup partial uploads: {e}")

def get_document_metadata_from_s3(s3_key: str) -> dict:
    """
    Get document metadata from S3 object
    
    Args:
        s3_key: S3 object key
        
    Returns:
        dict: Document metadata
    """
    try:
        bucket_name = get_s3_bucket_name()
        
        response = s3_client.head_object(Bucket=bucket_name, Key=s3_key)
        
        return {
            'size': response.get('ContentLength', 0),
            'last_modified': response.get('LastModified'),
            'content_type': response.get('ContentType'),
            'metadata': response.get('Metadata', {})
        }
        
    except Exception as e:
        logger.error(f"Failed to get S3 metadata for {s3_key}: {e}")
        return {}
