"""
Database Manager

Handles all database operations for PDF manipulation processing.
Maintains compatibility with existing LoanMaster database schema.
"""

import logging
import os
from typing import Dict, Any, List, Optional, Tuple
from datetime import datetime
import pymysql.cursors

logger = logging.getLogger(__name__)

class DatabaseManager:
    """Manages database connections and operations for PDF manipulation"""
    
    def __init__(self):
        self.db_config = {
            'host': os.environ.get('DB_HOST', 'localhost'),
            'database': os.environ.get('DB_NAME', 'LoanMaster'),
            'user': os.environ.get('DB_USER', 'esizzle_api'),
            'password': self._get_db_password(),
            'charset': 'utf8mb4',
            'cursorclass': pymysql.cursors.DictCursor,
            'autocommit': False,
            'connect_timeout': 30,
            'read_timeout': 30,
            'write_timeout': 30
        }
        
    def _get_db_password(self) -> str:
        """Get database password from environment or AWS Secrets Manager"""
        
        # First try direct environment variable
        password = os.environ.get('DB_PASSWORD')
        if password:
            return password
            
        # Try AWS Secrets Manager
        secret_name = os.environ.get('DB_PASSWORD_SECRET_NAME')
        if secret_name:
            try:
                import boto3
                import json
                
                session = boto3.session.Session()
                client = session.client(
                    service_name='secretsmanager',
                    region_name=os.environ.get('AWS_DEFAULT_REGION', 'us-east-1')
                )
                
                response = client.get_secret_value(SecretId=secret_name)
                secret = json.loads(response['SecretString'])
                return secret.get('password', '')
                
            except Exception as e:
                logger.error(f"Failed to get password from Secrets Manager: {e}")
                return ''
        
        return ''
    
    def get_connection(self):
        """Get a database connection"""
        try:
            connection = pymysql.connect(**self.db_config)
            return connection
        except Exception as e:
            logger.error(f"Database connection failed: {e}")
            raise
    
    def test_connection(self) -> bool:
        """Test database connectivity"""
        try:
            with self.get_connection() as connection:
                with connection.cursor() as cursor:
                    cursor.execute("SELECT 1 as test")
                    result = cursor.fetchone()
                    return result['test'] == 1
        except Exception as e:
            logger.error(f"Database connection test failed: {e}")
            return False
    
    def get_image_record(self, image_id: int) -> Optional[Dict[str, Any]]:
        """Get image record from database"""
        try:
            with self.get_connection() as connection:
                with connection.cursor() as cursor:
                    cursor.execute("""
                        SELECT 
                            ID, OfferingID, LoanID, ImageStatusTypeID, DocTypeManualID,
                            PageCount, IsRedacted, Deleted, BucketPrefix, Path,
                            DocumentDate, Comments, DateCreated, DateUpdated
                        FROM Image 
                        WHERE ID = %s AND Deleted = 0
                    """, (image_id,))
                    
                    result = cursor.fetchone()
                    if result:
                        logger.info(f"Found image record {image_id}")
                        return result
                    else:
                        logger.warning(f"Image record {image_id} not found")
                        return None
                        
        except Exception as e:
            logger.error(f"Failed to get image record {image_id}: {e}")
            return None
    
    def get_pending_redactions(self, image_id: int) -> List[Dict[str, Any]]:
        """Get pending redactions for an image"""
        try:
            with self.get_connection() as connection:
                with connection.cursor() as cursor:
                    cursor.execute("""
                        SELECT 
                            ID, ImageID, PageNumber, PageX, PageY, PageWidth, PageHeight,
                            Guid, Text, CreatedBy, DateCreated, Applied, DrawOrientation
                        FROM ImageRedaction 
                        WHERE ImageID = %s 
                          AND Deleted = 0 
                          AND (Applied IS NULL OR Applied = 0)
                        ORDER BY PageNumber, PageY, PageX
                    """, (image_id,))
                    
                    results = cursor.fetchall()
                    logger.info(f"Found {len(results)} pending redactions for image {image_id}")
                    return results
                    
        except Exception as e:
            logger.error(f"Failed to get redactions for image {image_id}: {e}")
            return []
    
    def get_rotations(self, image_id: int) -> List[Dict[str, Any]]:
        """Get page rotations for an image"""
        try:
            with self.get_connection() as connection:
                with connection.cursor() as cursor:
                    cursor.execute("""
                        SELECT ID, ImageID, PageIndex, Rotate
                        FROM ImageRotation 
                        WHERE ImageID = %s
                        ORDER BY PageIndex
                    """, (image_id,))
                    
                    results = cursor.fetchall()
                    logger.info(f"Found {len(results)} rotations for image {image_id}")
                    return results
                    
        except Exception as e:
            logger.error(f"Failed to get rotations for image {image_id}: {e}")
            return []
    
    def get_page_deletions(self, image_id: int) -> List[Dict[str, Any]]:
        """Get page deletions for an image"""
        try:
            with self.get_connection() as connection:
                with connection.cursor() as cursor:
                    cursor.execute("""
                        SELECT ID, ImageID, PageIndex, CreatedBy, DateCreated
                        FROM ImagePageDeletion 
                        WHERE ImageID = %s
                        ORDER BY PageIndex
                    """, (image_id,))
                    
                    results = cursor.fetchall()
                    logger.info(f"Found {len(results)} page deletions for image {image_id}")
                    return results
                    
        except Exception as e:
            logger.error(f"Failed to get page deletions for image {image_id}: {e}")
            return []
    
    def get_page_breaks(self, image_id: int) -> List[Dict[str, Any]]:
        """Get page breaks/bookmarks for an image"""
        try:
            with self.get_connection() as connection:
                with connection.cursor() as cursor:
                    cursor.execute("""
                        SELECT 
                            ID, ImageID, PageIndex, Text, ImageDocumentTypeID,
                            ResultImageID, Deleted, DocumentDate, Comments
                        FROM ImageBookmark 
                        WHERE ImageID = %s 
                          AND Deleted = 0
                        ORDER BY PageIndex
                    """, (image_id,))
                    
                    results = cursor.fetchall()
                    logger.info(f"Found {len(results)} page breaks for image {image_id}")
                    return results
                    
        except Exception as e:
            logger.error(f"Failed to get page breaks for image {image_id}: {e}")
            return []
    
    def mark_redaction_applied(self, redaction_id: int) -> bool:
        """Mark a redaction as applied"""
        try:
            with self.get_connection() as connection:
                with connection.cursor() as cursor:
                    cursor.execute("""
                        UPDATE ImageRedaction 
                        SET Applied = 1, DateApplied = %s 
                        WHERE ID = %s
                    """, (datetime.utcnow(), redaction_id))
                    
                    connection.commit()
                    logger.info(f"Marked redaction {redaction_id} as applied")
                    return True
                    
        except Exception as e:
            logger.error(f"Failed to mark redaction {redaction_id} as applied: {e}")
            return False
    
    def update_image_status(self, image_id: int, status: str) -> bool:
        """Update image status"""
        
        # Map status strings to enum values
        status_mapping = {
            'Sync': 1,
            'NeedsProcessing': 3,
            'NeedsImageManipulation': 7,
            'PendingWorkman': 8,
            'InWorkman': 9,
            'Obsolete': 15
        }
        
        status_id = status_mapping.get(status)
        if not status_id:
            logger.error(f"Invalid status: {status}")
            return False
        
        try:
            with self.get_connection() as connection:
                with connection.cursor() as cursor:
                    cursor.execute("""
                        UPDATE Image 
                        SET ImageStatusTypeID = %s, DateUpdated = %s 
                        WHERE ID = %s
                    """, (status_id, datetime.utcnow(), image_id))
                    
                    connection.commit()
                    logger.info(f"Updated image {image_id} status to {status}")
                    return True
                    
        except Exception as e:
            logger.error(f"Failed to update image {image_id} status: {e}")
            return False
    
    def update_page_count(self, image_id: int, page_count: int) -> bool:
        """Update image page count"""
        try:
            with self.get_connection() as connection:
                with connection.cursor() as cursor:
                    cursor.execute("""
                        UPDATE Image 
                        SET PageCount = %s, DateUpdated = %s 
                        WHERE ID = %s
                    """, (page_count, datetime.utcnow(), image_id))
                    
                    connection.commit()
                    logger.info(f"Updated image {image_id} page count to {page_count}")
                    return True
                    
        except Exception as e:
            logger.error(f"Failed to update page count for image {image_id}: {e}")
            return False
    
    def mark_image_deleted(self, image_id: int) -> bool:
        """Mark entire image as deleted"""
        try:
            with self.get_connection() as connection:
                with connection.cursor() as cursor:
                    cursor.execute("""
                        UPDATE Image 
                        SET Deleted = 1, DateUpdated = %s 
                        WHERE ID = %s
                    """, (datetime.utcnow(), image_id))
                    
                    connection.commit()
                    logger.info(f"Marked image {image_id} as deleted")
                    return True
                    
        except Exception as e:
            logger.error(f"Failed to mark image {image_id} as deleted: {e}")
            return False
    
    def mark_page_deletion_processed(self, deletion_id: int) -> bool:
        """Mark page deletion as processed"""
        try:
            with self.get_connection() as connection:
                with connection.cursor() as cursor:
                    # Add a processed timestamp or flag if needed
                    cursor.execute("""
                        UPDATE ImagePageDeletion 
                        SET DateProcessed = %s 
                        WHERE ID = %s
                    """, (datetime.utcnow(), deletion_id))
                    
                    connection.commit()
                    logger.info(f"Marked page deletion {deletion_id} as processed")
                    return True
                    
        except Exception as e:
            logger.error(f"Failed to mark page deletion {deletion_id} as processed: {e}")
            return False
    
    def update_image_document_type(self, image_id: int, doc_type_id: int, 
                                 document_date: Optional[datetime] = None, 
                                 comments: Optional[str] = None) -> bool:
        """Update image document type and metadata"""
        try:
            with self.get_connection() as connection:
                with connection.cursor() as cursor:
                    update_fields = ['DocTypeManualID = %s', 'DateUpdated = %s']
                    params = [doc_type_id, datetime.utcnow()]
                    
                    if document_date:
                        update_fields.append('DocumentDate = %s')
                        params.append(document_date)
                    
                    if comments:
                        update_fields.append('Comments = %s')
                        params.append(comments)
                    
                    params.append(image_id)
                    
                    cursor.execute(f"""
                        UPDATE Image 
                        SET {', '.join(update_fields)}
                        WHERE ID = %s
                    """, params)
                    
                    connection.commit()
                    logger.info(f"Updated image {image_id} document type to {doc_type_id}")
                    return True
                    
        except Exception as e:
            logger.error(f"Failed to update image {image_id} document type: {e}")
            return False
    
    def mark_bookmark_processed(self, bookmark_id: int, result_image_id: int) -> bool:
        """Mark bookmark as processed with result image ID"""
        try:
            with self.get_connection() as connection:
                with connection.cursor() as cursor:
                    cursor.execute("""
                        UPDATE ImageBookmark 
                        SET ResultImageID = %s, Deleted = 1, DateProcessed = %s 
                        WHERE ID = %s
                    """, (result_image_id, datetime.utcnow(), bookmark_id))
                    
                    connection.commit()
                    logger.info(f"Marked bookmark {bookmark_id} as processed with result {result_image_id}")
                    return True
                    
        except Exception as e:
            logger.error(f"Failed to mark bookmark {bookmark_id} as processed: {e}")
            return False
    
    def create_split_image(self, original_image: Dict[str, Any], doc_type_id: int, 
                          page_count: int, page_range: Tuple[int, int],
                          document_date: Optional[datetime] = None,
                          comments: Optional[str] = None,
                          split_type: str = 'page_break') -> int:
        """Create a new image record for a split document"""
        try:
            with self.get_connection() as connection:
                with connection.cursor() as cursor:
                    # Create new image record
                    insert_data = {
                        'OfferingID': original_image.get('OfferingID'),
                        'LoanID': original_image.get('LoanID'),
                        'DocTypeManualID': doc_type_id,
                        'PageCount': page_count,
                        'IsRedacted': 0,  # New split starts without redactions
                        'Deleted': 0,
                        'BucketPrefix': original_image.get('BucketPrefix'),
                        'Path': original_image.get('Path'),  # Will be updated with new ID
                        'ImageStatusTypeID': 1,  # Sync status
                        'DocumentDate': document_date,
                        'Comments': comments or f"Split from image {original_image['ID']} (pages {page_range[0]}-{page_range[1]-1})",
                        'DateCreated': datetime.utcnow(),
                        'DateUpdated': datetime.utcnow(),
                        'CreatedBy': 1,  # System user
                        'SplitFromImageID': original_image['ID'],  # Track source (if column exists)
                        'SplitType': split_type
                    }
                    
                    # Build dynamic INSERT query
                    columns = []
                    values = []
                    placeholders = []
                    
                    for key, value in insert_data.items():
                        if value is not None:
                            columns.append(key)
                            values.append(value)
                            placeholders.append('%s')
                    
                    cursor.execute(f"""
                        INSERT INTO Image ({', '.join(columns)})
                        VALUES ({', '.join(placeholders)})
                    """, values)
                    
                    new_image_id = cursor.lastrowid
                    connection.commit()
                    
                    logger.info(f"Created split image {new_image_id} from {original_image['ID']}")
                    return new_image_id
                    
        except Exception as e:
            logger.error(f"Failed to create split image: {e}")
            raise
    
    def create_split_log(self, original_image_id: int, split_image_id: int) -> bool:
        """Create audit log entry for document split"""
        try:
            with self.get_connection() as connection:
                with connection.cursor() as cursor:
                    cursor.execute("""
                        INSERT INTO ImageSplitLog 
                        (OriginalImageID, SplitImageID, SplitBy, DateCreated)
                        VALUES (%s, %s, %s, %s)
                    """, (original_image_id, split_image_id, 1, datetime.utcnow()))  # System user ID = 1
                    
                    connection.commit()
                    logger.info(f"Created split log entry: {original_image_id} -> {split_image_id}")
                    return True
                    
        except Exception as e:
            logger.error(f"Failed to create split log: {e}")
            return False
    
    def clear_image_manipulations(self, image_id: int) -> bool:
        """Clear all manipulation records for an image (for error recovery)"""
        try:
            with self.get_connection() as connection:
                with connection.cursor() as cursor:
                    
                    # Clear pending changes
                    cursor.execute("DELETE FROM ImageChangesPending WHERE ImageID = %s", (image_id,))
                    
                    # Reset redactions
                    cursor.execute("""
                        UPDATE ImageRedaction 
                        SET Applied = 0, DateApplied = NULL 
                        WHERE ImageID = %s
                    """, (image_id,))
                    
                    # Note: We don't delete rotations/deletions/bookmarks as they may be intentional
                    
                    connection.commit()
                    logger.info(f"Cleared manipulations for image {image_id}")
                    return True
                    
        except Exception as e:
            logger.error(f"Failed to clear manipulations for image {image_id}: {e}")
            return False
    
    def get_processing_queue_stats(self) -> Dict[str, Any]:
        """Get statistics about the processing queue"""
        try:
            with self.get_connection() as connection:
                with connection.cursor() as cursor:
                    
                    # Count images by status
                    cursor.execute("""
                        SELECT ist.Name, COUNT(*) as Count
                        FROM Image i
                        JOIN ImageStatusType ist ON i.ImageStatusTypeID = ist.ID
                        WHERE i.Deleted = 0
                        GROUP BY ist.Name
                    """)
                    
                    status_counts = {row['Name']: row['Count'] for row in cursor.fetchall()}
                    
                    # Count pending manipulations
                    cursor.execute("""
                        SELECT COUNT(*) as Count
                        FROM Image i
                        WHERE i.ImageStatusTypeID = 7  -- NeedsImageManipulation
                          AND i.Deleted = 0
                    """)
                    
                    pending_manipulations = cursor.fetchone()['Count']
                    
                    return {
                        'statusCounts': status_counts,
                        'pendingManipulations': pending_manipulations,
                        'timestamp': datetime.utcnow()
                    }
                    
        except Exception as e:
            logger.error(f"Failed to get processing queue stats: {e}")
            return {'error': str(e)}
