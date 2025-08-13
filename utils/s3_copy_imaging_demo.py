#!/usr/bin/env python3
"""
S3 Copy Utility for ImagingDemo Offering

Copies IOriginal and IProcessing files from production bucket (ffncorp.com) 
to development bucket (ffncorp.com-dev-db-cluster) for the ImagingDemo offering/sale combo.

Usage:
    python s3_copy_imaging_demo.py [--dry-run] [--offering-name ImagingDemo]

Environment Variables:
    - AWS_ACCESS_KEY_ID: AWS access key
    - AWS_SECRET_ACCESS_KEY: AWS secret key  
    - AWS_DEFAULT_REGION: AWS region (default: us-east-1)
    - DB_HOST: Database host
    - DB_USER: Database user
    - DB_PASSWORD: Database password
    - DB_NAME: Database name
"""

import os
import sys
import logging
import argparse
from typing import List, Dict, Any, Optional
import boto3
from botocore.exceptions import ClientError, NoCredentialsError
import pymysql
from dotenv import load_dotenv

# Load environment variables from .env file
script_dir = os.path.dirname(os.path.abspath(__file__))
env_file = os.path.join(script_dir, '.env')
if os.path.exists(env_file):
    load_dotenv(env_file)
    print(f"Loaded environment variables from: {env_file}")

# Add lambda utils directory to path for DatabaseManager import
lambda_utils_path = os.path.join(os.path.dirname(os.path.abspath(__file__)), '../lambda/pdf-processor')
sys.path.append(lambda_utils_path)
from utils.db_manager import DatabaseManager

# Configure logging
logging.basicConfig(
    level=logging.INFO,
    format='%(asctime)s - %(levelname)s - %(message)s',
    handlers=[
        logging.FileHandler('s3_copy_imaging_demo.log'),
        logging.StreamHandler()
    ]
)
logger = logging.getLogger(__name__)

class S3CopyUtility:
    """Utility for copying S3 files between buckets for specific offerings"""
    
    def __init__(self, source_bucket: str, dest_bucket: str, dry_run: bool = False):
        self.source_bucket = source_bucket
        self.dest_bucket = dest_bucket
        self.dry_run = dry_run
        
        # Initialize S3 client
        try:
            self.s3_client = boto3.client('s3')
            logger.info(f"S3 client initialized for region: {self.s3_client.meta.region_name}")
        except (ClientError, NoCredentialsError) as e:
            logger.error(f"Failed to initialize S3 client: {e}")
            raise
        
        # Initialize database manager
        self.db_manager = DatabaseManager()
        
        # Stats tracking
        self.stats = {
            'found_documents': 0,
            'copied_files': 0,
            'skipped_files': 0,
            'failed_files': 0,
            'total_bytes': 0
        }
    
    def get_imaging_demo_documents(self, offering_id: int = 302, asset_no: Optional[int] = None) -> List[Dict[str, Any]]:
        """
        Get all documents for the specified offering from the database
        
        Args:
            offering_id: ID of the offering to filter by (default: 302 for ImagingDemo)
            asset_no: Optional asset number to filter by for targeted copying
            
        Returns:
            List of document records with Image and Path information
        """
        try:
            with self.db_manager.get_connection() as connection:
                with connection.cursor() as cursor:
                    # Query to get documents for specific offering using the canonical join pattern from ISecurityRepository
                    query = """
                        SELECT 
                            i.ID as ImageId,
                            i.LoanID,
                            i.Path,
                            i.OriginalName,
                            i.OriginalExt,
                            o.OfferingName,
                            s.sale_desc as SaleName
                        FROM Image i
                        INNER JOIN Loan l ON i.LoanID = l.loan_id
                        INNER JOIN Sales s ON l.SALE_ID = s.sale_id
                        INNER JOIN Auction a ON s.sale_id = a.Loanmaster_Sale_ID
                        INNER JOIN OfferingAuctions oa ON a.AuctionID = oa.AuctionID
                        INNER JOIN Offerings o ON oa.OfferingID = o.OfferingID
                        WHERE o.OfferingID = %s 
                        AND i.Deleted = 0
                    """
                    
                    # Build dynamic query based on parameters
                    params = [offering_id]
                    
                    if asset_no is not None:
                        query += " AND l.ASSET_NO = %s"
                        params.append(asset_no)
                    
                    query += """
                        ORDER BY i.ID
                        LIMIT 100
                    """
                    
                    cursor.execute(query, tuple(params))
                    documents = cursor.fetchall()
                    
                    if asset_no:
                        logger.info(f"Found {len(documents)} documents for offering ID '{offering_id}', asset '{asset_no}'")
                    else:
                        logger.info(f"Found {len(documents)} documents for offering ID '{offering_id}'")
                    self.stats['found_documents'] = len(documents)
                    
                    return documents
                    
        except Exception as e:
            if asset_no:
                logger.error(f"Error querying documents for offering ID '{offering_id}', asset '{asset_no}': {e}")
            else:
                logger.error(f"Error querying documents for offering ID '{offering_id}': {e}")
            return []
    
    def construct_s3_paths(self, document: Dict[str, Any]) -> Dict[str, str]:
        """
        Construct S3 paths for IOriginal and IProcessing files based on document info
        
        Args:
            document: Document record from database with ImageId and other metadata
            
        Returns:
            Dictionary with 'original' and 'processing' S3 key paths
        """
        image_id = document['ImageId']
        original_ext = document.get('OriginalExt', '.pdf')  # Default to .pdf if not found
        
        # Construct S3 paths using environment-only bucket approach
        # IOriginal: Uses the actual OriginalExt from database
        # IProcessing: Always uses .pdf (processed files are converted to PDF)
        s3_paths = {
            'original': f"IOriginal/Images/{image_id}{original_ext}",
            'processing': f"IProcessing/Images/{image_id}.pdf"
        }
        
        return s3_paths
    
    def file_exists_in_s3(self, bucket: str, key: str) -> bool:
        """Check if a file exists in S3"""
        try:
            self.s3_client.head_object(Bucket=bucket, Key=key)
            return True
        except ClientError as e:
            if e.response['Error']['Code'] == '404':
                return False
            else:
                logger.warning(f"Error checking file existence {bucket}/{key}: {e}")
                return False
    
    def copy_s3_file(self, source_key: str, dest_key: str) -> bool:
        """
        Copy a file from source bucket to destination bucket
        
        Args:
            source_key: S3 key in source bucket
            dest_key: S3 key in destination bucket
            
        Returns:
            True if successful, False otherwise
        """
        try:
            # Check if source file exists
            if not self.file_exists_in_s3(self.source_bucket, source_key):
                logger.warning(f"Source file not found: s3://{self.source_bucket}/{source_key}")
                self.stats['skipped_files'] += 1
                return False
            
            # Check if destination file already exists
            if self.file_exists_in_s3(self.dest_bucket, dest_key):
                logger.info(f"Destination file already exists, skipping: s3://{self.dest_bucket}/{dest_key}")
                self.stats['skipped_files'] += 1
                return False
            
            if self.dry_run:
                logger.info(f"DRY RUN: Would copy s3://{self.source_bucket}/{source_key} -> s3://{self.dest_bucket}/{dest_key}")
                return True
            
            # Get file size for stats
            try:
                response = self.s3_client.head_object(Bucket=self.source_bucket, Key=source_key)
                file_size = response['ContentLength']
                self.stats['total_bytes'] += file_size
            except:
                file_size = 0
            
            # Perform the copy
            copy_source = {'Bucket': self.source_bucket, 'Key': source_key}
            self.s3_client.copy_object(
                CopySource=copy_source,
                Bucket=self.dest_bucket,
                Key=dest_key
            )
            
            logger.info(f"Successfully copied: s3://{self.source_bucket}/{source_key} -> s3://{self.dest_bucket}/{dest_key} ({file_size} bytes)")
            self.stats['copied_files'] += 1
            return True
            
        except Exception as e:
            logger.error(f"Failed to copy s3://{self.source_bucket}/{source_key}: {e}")
            self.stats['failed_files'] += 1
            return False
    
    def copy_imaging_demo_files(self, offering_id: int = 302, asset_no: Optional[int] = None) -> None:
        """
        Main method to copy all files from source to destination bucket for specified offering
        
        Args:
            offering_id: ID of the offering to copy files for (default: 302 for ImagingDemo)
            asset_no: Optional asset number to filter by for targeted copying
        """
        if asset_no:
            logger.info(f"Starting S3 copy operation for offering ID: {offering_id}, asset: {asset_no}")
        else:
            logger.info(f"Starting S3 copy operation for offering ID: {offering_id}")
        logger.info(f"Source bucket: {self.source_bucket}")
        logger.info(f"Destination bucket: {self.dest_bucket}")
        logger.info(f"Dry run mode: {self.dry_run}")
        
        # Get documents for the offering
        documents = self.get_imaging_demo_documents(offering_id, asset_no)
        
        if not documents:
            if asset_no:
                logger.warning(f"No documents found for offering ID '{offering_id}', asset '{asset_no}'")
            else:
                logger.warning(f"No documents found for offering ID '{offering_id}'")
            return
        
        # Process each document
        for doc in documents:
            image_id = doc['ImageId']
            offering = doc['OfferingName']
            sale = doc['SaleName']
            
            logger.info(f"Processing document {image_id} (Offering: {offering}, Sale: {sale})")
            
            # Get S3 paths for this document
            s3_paths = self.construct_s3_paths(doc)
            
            # Copy IOriginal file
            original_success = self.copy_s3_file(s3_paths['original'], s3_paths['original'])
            
            # Copy IProcessing file  
            processing_success = self.copy_s3_file(s3_paths['processing'], s3_paths['processing'])
            
            if original_success or processing_success:
                logger.info(f"Document {image_id}: Original={'✓' if original_success else '✗'}, Processing={'✓' if processing_success else '✗'}")
        
        self.print_summary()
    
    def print_summary(self) -> None:
        """Print operation summary"""
        logger.info("\n" + "="*60)
        logger.info("COPY OPERATION SUMMARY")
        logger.info("="*60)
        logger.info(f"Documents found:    {self.stats['found_documents']}")
        logger.info(f"Files copied:       {self.stats['copied_files']}")
        logger.info(f"Files skipped:      {self.stats['skipped_files']}")
        logger.info(f"Files failed:       {self.stats['failed_files']}")
        logger.info(f"Total bytes copied: {self.stats['total_bytes']:,}")
        logger.info(f"Dry run mode:       {self.dry_run}")
        logger.info("="*60)

def main():
    """Main entry point"""
    parser = argparse.ArgumentParser(description="Copy S3 files for ImagingDemo offering")
    parser.add_argument('--dry-run', action='store_true', help='Show what would be copied without actually copying')
    parser.add_argument('--offering-id', type=int, default=302, help='ID of the offering to copy files for (default: 302 for ImagingDemo)')
    parser.add_argument('--asset-no', type=int, help='Optional asset number to filter by for targeted copying')
    parser.add_argument('--source-bucket', default='ffncorp.com', help='Source S3 bucket')
    parser.add_argument('--dest-bucket', default='ffncorp.com-dev-db-cluster', help='Destination S3 bucket')
    
    args = parser.parse_args()
    
    # Initialize the copy utility
    copy_utility = S3CopyUtility(
        source_bucket=args.source_bucket,
        dest_bucket=args.dest_bucket,
        dry_run=args.dry_run
    )
    
    # Perform the copy operation
    try:
        copy_utility.copy_imaging_demo_files(args.offering_id, args.asset_no)
    except KeyboardInterrupt:
        logger.info("Operation cancelled by user")
    except Exception as e:
        logger.error(f"Operation failed: {e}")
        sys.exit(1)

if __name__ == "__main__":
    main()
