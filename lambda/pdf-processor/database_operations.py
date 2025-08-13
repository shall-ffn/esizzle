"""
Database Operations Helper for PDF Processor Lambda

Handles all MySQL database interactions following the legacy Hydra DD schema.
Supports creating Image records, updating ImageBookmarks, and managing processing sessions.

Author: Esizzle Development Team
Date: January 2025
"""

import pymysql
import logging
import os
import json
from typing import Dict, List, Any, Optional, Tuple
from datetime import datetime, timezone
from contextlib import contextmanager

logger = logging.getLogger(__name__)

class DatabaseError(Exception):
    """Custom exception for database operations"""
    pass

def get_db_connection_params() -> Dict[str, str]:
    """
    Get database connection parameters from environment variables
    
    Returns:
        dict: Database connection parameters
        
    Raises:
        ValueError: If required parameters are missing
    """
    required_params = ['DB_HOST', 'DB_USER', 'DB_PASSWORD', 'DB_NAME']
    params = {}
    
    for param in required_params:
        value = os.environ.get(param)
        if not value:
            raise ValueError(f"Missing required environment variable: {param}")
        params[param.lower().replace('db_', '')] = value
    
    # Optional parameters with defaults
    params['port'] = int(os.environ.get('DB_PORT', '3306'))
    params['charset'] = os.environ.get('DB_CHARSET', 'utf8mb4')
    
    return params

@contextmanager
def get_db_connection():
    """
    Context manager for database connections with proper cleanup
    
    Yields:
        pymysql.Connection: Database connection
        
    Raises:
        DatabaseError: If connection fails
    """
    connection = None
    try:
        params = get_db_connection_params()
        connection = pymysql.connect(
            host=params['host'],
            user=params['user'],
            password=params['password'],
            database=params['name'],
            port=params['port'],
            charset=params['charset'],
            autocommit=False,
            cursorclass=pymysql.cursors.DictCursor
        )
        
        logger.info("Database connection established")
        yield connection
        
    except pymysql.Error as e:
        logger.error(f"Database connection failed: {e}")
        raise DatabaseError(f"Failed to connect to database: {e}")
    except Exception as e:
        logger.error(f"Unexpected database error: {e}")
        raise DatabaseError(f"Database error: {e}")
    finally:
        if connection:
            connection.close()
            logger.debug("Database connection closed")

def create_image_record(source_document_id: int, split_range: Dict[str, Any], 
                       s3_key: Optional[str], filename: Optional[str], page_count: int, 
                       metadata: Dict[str, Any]) -> int:
    """
    Create new Image record for split document
    
    Args:
        source_document_id: Original document ID
        split_range: Split range information with document type details
        s3_key: S3 key where split PDF is stored
        filename: Generated filename for split document
        page_count: Number of pages in split document
        metadata: Additional metadata (userId, loanId, etc.)
        
    Returns:
        int: New Image record ID
        
    Raises:
        DatabaseError: If database operation fails
    """
    try:
        with get_db_connection() as conn:
            cursor = conn.cursor()
            
            # First, get loan ID and original name from source document
            cursor.execute("""
                SELECT LoanID, OriginalName 
                FROM Image 
                WHERE ID = %s
            """, (source_document_id,))
            
            source_info = cursor.fetchone()
            if not source_info:
                raise DatabaseError(f"Source document {source_document_id} not found")
            
            loan_id = source_info['LoanID']
            original_name = source_info['OriginalName']
            
            # Create new Image record
            insert_sql = """
                INSERT INTO Image (
                    LoanID,
                    DocTypeManualID,
                    DocumentDate,
                    Comments,
                    OriginalName,
                    PageCount,
                    Path,
                    CreatedBy,
                    DateCreated,
                    DateUpdated,
                    Deleted
                ) VALUES (
                    %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s
                )
            """
            
            # Parse document date if provided
            document_date = None
            if split_range.get('documentDate'):
                try:
                    document_date = datetime.fromisoformat(split_range['documentDate'].replace('Z', '+00:00'))
                except:
                    document_date = None
            
            # Prepare values
            values = (
                loan_id,                                    # LoanID
                split_range.get('documentTypeId'),          # DocTypeManualID
                document_date,                              # DocumentDate
                split_range.get('comments'),                # Comments
                original_name,                              # OriginalName (from source document)  
                page_count,                                 # PageCount
                s3_key,                                     # Path (S3 key, can be None initially)
                metadata.get('userId', 1),                  # CreatedBy
                datetime.now(timezone.utc),                 # DateCreated
                datetime.now(timezone.utc),                 # DateUpdated
                False                                       # Deleted
            )
            
            cursor.execute(insert_sql, values)
            new_image_id = cursor.lastrowid
            
            conn.commit()
            
            logger.info(f"Created new Image record {new_image_id} for split document")
            return new_image_id
            
    except Exception as e:
        logger.error(f"Failed to create Image record: {e}")
        raise DatabaseError(f"Failed to create Image record: {e}")

def update_image_record_s3_info(image_id: int, s3_key: str, filename: str) -> None:
    """
    Update Image record with S3 information after upload
    
    Args:
        image_id: Image record ID to update
        s3_key: S3 key where PDF is stored
        filename: Final filename
        
    Raises:
        DatabaseError: If database operation fails
    """
    try:
        with get_db_connection() as conn:
            cursor = conn.cursor()
            
            # Update Image record with S3 details
            update_sql = """
                UPDATE Image 
                SET Path = %s, OriginalName = %s, DateUpdated = %s
                WHERE ID = %s
            """
            
            values = (
                s3_key,                          # Path (S3 key)
                filename,                        # OriginalName
                datetime.now(timezone.utc),      # DateUpdated
                image_id                         # ID
            )
            
            cursor.execute(update_sql, values)
            
            if cursor.rowcount == 0:
                raise DatabaseError(f"No Image record found with ID {image_id}")
            
            conn.commit()
            
            logger.info(f"Updated Image record {image_id} with S3 info: {s3_key}")
            
    except Exception as e:
        logger.error(f"Failed to update Image record {image_id}: {e}")
        raise DatabaseError(f"Failed to update Image record: {e}")

def mark_original_document_obsolete(document_id: int) -> None:
    """
    Mark the original document as Obsolete after successful splitting
    
    Args:
        document_id: Original document ID to mark as obsolete
        
    Raises:
        DatabaseError: If database operation fails
    """
    try:
        with get_db_connection() as conn:
            cursor = conn.cursor()
            
            # Update original document status to Obsolete (7)
            update_sql = """
                UPDATE Image 
                SET ImageStatusTypeID = %s, DateUpdated = %s
                WHERE ID = %s
            """
            
            values = (
                7,                               # ImageStatusTypeID = 7 (Obsolete)
                datetime.now(timezone.utc),      # DateUpdated
                document_id                      # ID
            )
            
            cursor.execute(update_sql, values)
            
            if cursor.rowcount == 0:
                raise DatabaseError(f"No Image record found with ID {document_id}")
            
            conn.commit()
            
            logger.info(f"Marked original document {document_id} as Obsolete after splitting")
            
    except Exception as e:
        logger.error(f"Failed to mark document {document_id} as obsolete: {e}")
        raise DatabaseError(f"Failed to mark document as obsolete: {e}")

def update_bookmark_with_result(bookmark_id: int, result_image_id: int) -> bool:
    """
    Update ImageBookmark record with the resulting split document ID
    
    Args:
        bookmark_id: ImageBookmark ID to update
        result_image_id: New Image ID created from split
        
    Returns:
        bool: True if update successful
        
    Raises:
        DatabaseError: If update fails
    """
    try:
        with get_db_connection() as conn:
            cursor = conn.cursor()
            
            update_sql = """
                UPDATE ImageBookmark 
                SET ResultImageID = %s,
                    DateUpdated = %s
                WHERE ID = %s
            """
            
            cursor.execute(update_sql, (
                result_image_id,
                datetime.now(timezone.utc),
                bookmark_id
            ))
            
            if cursor.rowcount == 0:
                logger.warning(f"No bookmark found with ID {bookmark_id}")
                return False
            
            conn.commit()
            
            logger.info(f"Updated bookmark {bookmark_id} with result image {result_image_id}")
            return True
            
    except Exception as e:
        logger.error(f"Failed to update bookmark {bookmark_id}: {e}")
        raise DatabaseError(f"Failed to update bookmark: {e}")

def update_database_records(source_document_id: int, bookmarks: List[Dict[str, Any]], 
                          results: List[Dict[str, Any]]) -> bool:
    """
    Update all database records after successful PDF processing
    
    Args:
        source_document_id: Original document ID
        bookmarks: Original bookmark list
        results: Processing results with new Image IDs
        
    Returns:
        bool: True if all updates successful
        
    Raises:
        DatabaseError: If any update fails
    """
    try:
        with get_db_connection() as conn:
            cursor = conn.cursor()
            
            # Start transaction
            conn.begin()
            
            try:
                # Update each bookmark with its corresponding result
                for result in results:
                    bookmark_id = result.get('bookmarkId')
                    result_image_id = result.get('resultImageId')
                    
                    if bookmark_id and result_image_id:
                        cursor.execute("""
                            UPDATE ImageBookmark 
                            SET ResultImageID = %s,
                                DateUpdated = %s
                            WHERE ID = %s
                        """, (
                            result_image_id,
                            datetime.now(timezone.utc),
                            bookmark_id
                        ))
                
                # Mark source document with processing completion if needed
                cursor.execute("""
                    UPDATE Image 
                    SET DateUpdated = %s
                    WHERE ID = %s
                """, (
                    datetime.now(timezone.utc),
                    source_document_id
                ))
                
                # Commit transaction
                conn.commit()
                
                logger.info(f"Successfully updated all database records for document {source_document_id}")
                return True
                
            except Exception as e:
                # Rollback on any error
                conn.rollback()
                raise e
                
    except Exception as e:
        logger.error(f"Failed to update database records: {e}")
        raise DatabaseError(f"Database update failed: {e}")

def get_document_info(document_id: int) -> Optional[Dict[str, Any]]:
    """
    Get document information from database
    
    Args:
        document_id: Document ID to lookup
        
    Returns:
        dict: Document information or None if not found
    """
    try:
        with get_db_connection() as conn:
            cursor = conn.cursor()
            
            cursor.execute("""
                SELECT i.ID, i.LoanID, i.OriginalName, i.PageCount, 
                       i.Path, i.BucketPrefix, i.DocTypeManualID,
                       i.DocumentDate, i.Comments, i.CreatedBy,
                       l.LoanNumber
                FROM Image i
                LEFT JOIN Loan l ON i.LoanID = l.ID
                WHERE i.ID = %s AND i.Deleted = 0
            """, (document_id,))
            
            result = cursor.fetchone()
            
            if result:
                logger.info(f"Retrieved document info for ID {document_id}")
                return dict(result)
            else:
                logger.warning(f"Document {document_id} not found")
                return None
                
    except Exception as e:
        logger.error(f"Failed to get document info for {document_id}: {e}")
        return None

def get_bookmark_info(bookmark_ids: List[int]) -> List[Dict[str, Any]]:
    """
    Get bookmark information from database
    
    Args:
        bookmark_ids: List of bookmark IDs to lookup
        
    Returns:
        list: Bookmark information records
    """
    try:
        with get_db_connection() as conn:
            cursor = conn.cursor()
            
            if not bookmark_ids:
                return []
            
            placeholders = ','.join(['%s'] * len(bookmark_ids))
            
            cursor.execute(f"""
                SELECT ib.ID, ib.ImageID, ib.PageIndex, ib.Text,
                       ib.ImageDocumentTypeID, ib.ResultImageID,
                       idtml.Name as DocumentTypeName
                FROM ImageBookmark ib
                LEFT JOIN ImageDocTypeMasterList idtml ON ib.ImageDocumentTypeID = idtml.ID
                WHERE ib.ID IN ({placeholders}) AND ib.Deleted = 0
                ORDER BY ib.PageIndex
            """, bookmark_ids)
            
            results = cursor.fetchall()
            
            logger.info(f"Retrieved {len(results)} bookmark records")
            return [dict(result) for result in results]
            
    except Exception as e:
        logger.error(f"Failed to get bookmark info: {e}")
        return []

def validate_database_connection() -> bool:
    """
    Validate database connectivity and basic table access
    
    Returns:
        bool: True if database is accessible
    """
    try:
        with get_db_connection() as conn:
            cursor = conn.cursor()
            
            # Test basic table access
            cursor.execute("SELECT COUNT(*) as count FROM Image LIMIT 1")
            result = cursor.fetchone()
            
            logger.info("Database connection validation successful")
            return True
            
    except Exception as e:
        logger.error(f"Database validation failed: {e}")
        return False
