"""
S3 Manager

Handles all S3 operations for PDF manipulation processing.
Maintains compatibility with existing Hydra S3 bucket structure.
"""

import logging
import os
from typing import Dict, Any, Optional, List
import boto3
from botocore.exceptions import ClientError, NoCredentialsError
from io import BytesIO

logger = logging.getLogger(__name__)

class S3Manager:
    """Manages S3 operations for PDF manipulation"""
    
    def __init__(self):
        self.bucket_name = os.environ.get('S3_BUCKET', 'esizzle-documents')
        self.region = os.environ.get('AWS_DEFAULT_REGION', 'us-east-1')
        
        try:
            self.s3_client = boto3.client('s3', region_name=self.region)
        except (ClientError, NoCredentialsError) as e:
            logger.error(f"Failed to initialize S3 client: {e}")
            raise
    
    def test_connection(self) -> bool:
        """Test S3 connectivity"""
        try:
            # Try to list objects in bucket (limit to 1)
            self.s3_client.list_objects_v2(
                Bucket=self.bucket_name,
                MaxKeys=1
            )
            return True
        except Exception as e:
            logger.error(f"S3 connection test failed: {e}")
            return False
    
    def download_file(self, s3_key: str) -> Optional[bytes]:
        """
        Download a file from S3
        
        Args:
            s3_key: S3 object key (path to file)
            
        Returns:
            File content as bytes or None if failed
        """
        
        # Use s3_key directly
        full_key = s3_key.strip('/')  # Remove leading/trailing slashes
        
        try:
            logger.info(f"Downloading from S3: s3://{self.bucket_name}/{full_key}")
            
            response = self.s3_client.get_object(
                Bucket=self.bucket_name,
                Key=full_key
            )
            
            content = response['Body'].read()
            logger.info(f"Successfully downloaded {len(content)} bytes from {full_key}")
            
            return content
            
        except ClientError as e:
            error_code = e.response['Error']['Code']
            if error_code == 'NoSuchKey':
                logger.warning(f"File not found in S3: {full_key}")
            else:
                logger.error(f"S3 download failed for {full_key}: {e}")
            return None
            
        except Exception as e:
            logger.error(f"Unexpected error downloading {full_key}: {e}")
            return None
    
    def upload_file(self, file_content: bytes, s3_key: str, 
                   content_type: str = 'application/pdf') -> bool:
        """
        Upload a file to S3
        
        Args:
            file_content: File content as bytes
            s3_key: S3 object key (path to file)
            content_type: MIME type of the file
            
        Returns:
            True if successful, False otherwise
        """
        
        # Use s3_key directly
        full_key = s3_key
        full_key = full_key.strip('/')  # Remove leading/trailing slashes
        
        try:
            logger.info(f"Uploading to S3: s3://{self.bucket_name}/{full_key} ({len(file_content)} bytes)")
            
            # Upload with metadata
            self.s3_client.put_object(
                Bucket=self.bucket_name,
                Key=full_key,
                Body=file_content,
                ContentType=content_type,
                Metadata={
                    'source': 'esizzle-lambda-processor',
                    'processed-at': str(int(time.time()) if 'time' in globals() else 0)
                }
            )
            
            logger.info(f"Successfully uploaded to {full_key}")
            return True
            
        except ClientError as e:
            logger.error(f"S3 upload failed for {full_key}: {e}")
            return False
            
        except Exception as e:
            logger.error(f"Unexpected error uploading {full_key}: {e}")
            return False
    
    def file_exists(self, s3_key: str) -> bool:
        """
        Check if a file exists in S3
        
        Args:
            s3_key: S3 object key (path to file)
            
        Returns:
            True if file exists, False otherwise
        """
        
        # Use s3_key directly
        full_key = s3_key.strip('/')  # Remove leading/trailing slashes
        
        try:
            self.s3_client.head_object(
                Bucket=self.bucket_name,
                Key=full_key
            )
            return True
            
        except ClientError as e:
            error_code = e.response['Error']['Code']
            if error_code in ['NoSuchKey', '404']:
                return False
            else:
                logger.error(f"Error checking file existence {full_key}: {e}")
                return False
                
        except Exception as e:
            logger.error(f"Unexpected error checking {full_key}: {e}")
            return False
    
    def copy_file(self, source_key: str, dest_key: str) -> bool:
        """
        Copy a file within S3
        
        Args:
            source_key: Source S3 object key
            dest_key: Destination S3 object key
            
        Returns:
            True if successful, False otherwise
        """
        
        # Use keys directly
        full_source_key = source_key
        full_dest_key = dest_key
        
        full_source_key = full_source_key.strip('/')
        full_dest_key = full_dest_key.strip('/')
        
        try:
            copy_source = {
                'Bucket': self.bucket_name,
                'Key': full_source_key
            }
            
            self.s3_client.copy_object(
                CopySource=copy_source,
                Bucket=self.bucket_name,
                Key=full_dest_key,
                MetadataDirective='COPY'
            )
            
            logger.info(f"Successfully copied {full_source_key} to {full_dest_key}")
            return True
            
        except ClientError as e:
            logger.error(f"S3 copy failed from {full_source_key} to {full_dest_key}: {e}")
            return False
            
        except Exception as e:
            logger.error(f"Unexpected error copying file: {e}")
            return False
    
    def delete_file(self, s3_key: str) -> bool:
        """
        Delete a file from S3
        
        Args:
            s3_key: S3 object key (path to file)
            
        Returns:
            True if successful, False otherwise
        """
        
        # Use s3_key directly
        full_key = s3_key.strip('/')  # Remove leading/trailing slashes
        
        try:
            self.s3_client.delete_object(
                Bucket=self.bucket_name,
                Key=full_key
            )
            
            logger.info(f"Successfully deleted {full_key}")
            return True
            
        except ClientError as e:
            logger.error(f"S3 delete failed for {full_key}: {e}")
            return False
            
        except Exception as e:
            logger.error(f"Unexpected error deleting {full_key}: {e}")
            return False
    
    def list_files(self, prefix: str, max_keys: int = 1000) -> List[Dict[str, Any]]:
        """
        List files in S3 with a given prefix
        
        Args:
            prefix: S3 prefix to filter by
            max_keys: Maximum number of keys to return
            
        Returns:
            List of file information dictionaries
        """
        
        # Use prefix directly
        full_prefix = prefix.strip('/')
        
        try:
            response = self.s3_client.list_objects_v2(
                Bucket=self.bucket_name,
                Prefix=full_prefix,
                MaxKeys=max_keys
            )
            
            files = []
            if 'Contents' in response:
                for obj in response['Contents']:
                    files.append({
                        'key': obj['Key'],
                        'size': obj['Size'],
                        'lastModified': obj['LastModified'],
                        'etag': obj['ETag'].strip('"')
                    })
            
            logger.info(f"Listed {len(files)} files with prefix {full_prefix}")
            return files
            
        except ClientError as e:
            logger.error(f"S3 list failed for prefix {full_prefix}: {e}")
            return []
            
        except Exception as e:
            logger.error(f"Unexpected error listing files: {e}")
            return []
    
    def get_file_info(self, s3_key: str) -> Optional[Dict[str, Any]]:
        """
        Get metadata information about a file
        
        Args:
            s3_key: S3 object key (path to file)
            
        Returns:
            File information dictionary or None if failed
        """
        
        # Use s3_key directly
        full_key = s3_key.strip('/')  # Remove leading/trailing slashes
        
        try:
            response = self.s3_client.head_object(
                Bucket=self.bucket_name,
                Key=full_key
            )
            
            return {
                'key': full_key,
                'size': response.get('ContentLength', 0),
                'lastModified': response.get('LastModified'),
                'contentType': response.get('ContentType', ''),
                'etag': response.get('ETag', '').strip('"'),
                'metadata': response.get('Metadata', {})
            }
            
        except ClientError as e:
            error_code = e.response['Error']['Code']
            if error_code == 'NoSuchKey':
                logger.warning(f"File not found for info: {full_key}")
            else:
                logger.error(f"Failed to get file info for {full_key}: {e}")
            return None
            
        except Exception as e:
            logger.error(f"Unexpected error getting file info: {e}")
            return None
    
    def create_backup(self, source_key: str, backup_prefix: str = 'RedactOriginal', 
                     source_prefix: str = '') -> bool:
        """
        Create a backup copy of a file (used before manipulations)
        
        Args:
            source_key: Source file key
            backup_prefix: Backup folder prefix (default: RedactOriginal)
            source_prefix: Optional source bucket prefix
            
        Returns:
            True if successful, False otherwise
        """
        
        # Extract the path components from source key
        # Assuming source_key format: "Processing/path/imageId/imageId.pdf"
        path_parts = source_key.split('/')
        if len(path_parts) >= 3:
            # Reconstruct backup path
            relative_path = '/'.join(path_parts[1:])  # Remove "Processing" prefix
            backup_key = f"{backup_prefix}/{relative_path}"
        else:
            # Fallback - just change prefix
            backup_key = source_key.replace('Processing/', f'{backup_prefix}/')
        
        return self.copy_file(source_key, backup_key, source_prefix, source_prefix)
    
    def cleanup_temp_files(self, temp_prefix: str, max_age_hours: int = 24) -> int:
        """
        Clean up temporary files older than specified age
        
        Args:
            temp_prefix: Prefix for temporary files
            max_age_hours: Maximum age in hours before cleanup
            
        Returns:
            Number of files cleaned up
        """
        
        import time
        from datetime import datetime, timezone
        
        try:
            files = self.list_files(temp_prefix)
            cutoff_time = datetime.now(timezone.utc).timestamp() - (max_age_hours * 3600)
            
            cleaned_count = 0
            for file_info in files:
                file_time = file_info['lastModified'].timestamp()
                if file_time < cutoff_time:
                    if self.delete_file(file_info['key']):
                        cleaned_count += 1
            
            if cleaned_count > 0:
                logger.info(f"Cleaned up {cleaned_count} temporary files")
            
            return cleaned_count
            
        except Exception as e:
            logger.error(f"Error during temp file cleanup: {e}")
            return 0

class S3PathManager:
    """Utility class for managing S3 path conventions"""
    
    @staticmethod
    def get_processing_path(image_record: Dict[str, Any]) -> str:
        """Get the Processing path for an image"""
        return f"Processing/{image_record['Path']}/{image_record['ID']}/{image_record['ID']}.pdf"
    
    @staticmethod
    def get_original_path(image_record: Dict[str, Any]) -> str:
        """Get the Original path for an image"""
        return f"Original/{image_record['Path']}/{image_record['ID']}/{image_record['ID']}.pdf"
    
    @staticmethod
    def get_production_path(image_record: Dict[str, Any]) -> str:
        """Get the Production path for an image"""
        return f"Production/{image_record['Path']}/{image_record['ID']}/{image_record['ID']}.pdf"
    
    @staticmethod
    def get_redact_original_path(image_record: Dict[str, Any]) -> str:
        """Get the RedactOriginal backup path for an image"""
        return f"RedactOriginal/{image_record['Path']}/{image_record['ID']}/{image_record['ID']}.pdf"
    
    @staticmethod
    def get_all_paths(image_record: Dict[str, Any]) -> Dict[str, str]:
        """Get all standard paths for an image"""
        return {
            'processing': S3PathManager.get_processing_path(image_record),
            'original': S3PathManager.get_original_path(image_record),
            'production': S3PathManager.get_production_path(image_record),
            'redactOriginal': S3PathManager.get_redact_original_path(image_record)
        }
    
    @staticmethod
    def parse_image_path(s3_key: str) -> Optional[Dict[str, Any]]:
        """
        Parse an S3 key to extract image information
        
        Args:
            s3_key: S3 object key to parse
            
        Returns:
            Dictionary with parsed information or None if invalid format
        """
        
        try:
            # Expected format: "Stage/path/imageId/imageId.pdf"
            parts = s3_key.split('/')
            if len(parts) < 4:
                return None
            
            stage = parts[0]  # Original, Processing, Production, RedactOriginal
            image_id_str = parts[-2]  # Second to last part should be image ID
            filename = parts[-1]  # Last part should be filename
            
            # Validate image ID
            try:
                image_id = int(image_id_str)
            except ValueError:
                return None
            
            # Construct path (everything between stage and imageId)
            path_parts = parts[1:-2]
            path = '/'.join(path_parts) if path_parts else ''
            
            return {
                'stage': stage,
                'imageId': image_id,
                'path': path,
                'filename': filename,
                'isValidFormat': filename == f"{image_id}.pdf"
            }
            
        except Exception as e:
            logger.error(f"Error parsing S3 path {s3_key}: {e}")
            return None

# Import time module for metadata
import time
