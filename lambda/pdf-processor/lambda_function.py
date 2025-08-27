"""
AWS Lambda Function: PDF Processor for Esizzle Document Splitting

This Lambda function handles document processing for the Esizzle indexing system,
specifically document splitting at bookmark positions based on the legacy Hydra 
DD Index system design.

Supports three processing types:
1. SimpleIndexing - No breaks, metadata only
2. IndexOnly - Single break at page 0, rename only  
3. DocumentSplitting - Multiple breaks, split PDF into separate documents

Author: Esizzle Development Team
Date: January 2025
"""

import json
import logging
import traceback
from typing import Dict, List, Any, Optional, Tuple
import PyPDF2
import io
from datetime import datetime

# Import our helper modules
from s3_operations import (
    download_pdf_from_s3, upload_split_to_s3, get_s3_bucket_name, 
    cleanup_partial_uploads, verify_s3_access
)
from database_operations import (
    get_db_connection, 
    create_image_record, 
    update_image_record_s3_info,
    mark_original_document_obsolete,
    update_bookmark_with_result,
    update_database_records,
    create_processing_session,
    update_processing_session_status
)
from api_callbacks import (
    update_processing_status, link_processing_results, 
    notify_processing_completion, validate_api_connectivity
)

# Configure logging
logger = logging.getLogger()
logger.setLevel(logging.INFO)

class PDFProcessingError(Exception):
    """Custom exception for PDF processing errors"""
    pass

class DatabaseError(Exception):
    """Custom exception for database operations"""
    pass

def lambda_handler(event, context):
    """
    Main Lambda handler for PDF processing
    
    Args:
        event: Lambda event containing processing payload
        context: Lambda context object
        
    Returns:
        dict: Processing result with status and details
    """
    # Enhanced logging for troubleshooting
    import os
    print(f"=== LAMBDA HANDLER STARTED ===")
    print(f"Event type: {type(event)}")
    print(f"Event content: {event}")
    print(f"Context: {context}")
    print(f"Environment variables:")
    for key, value in os.environ.items():
        if key.startswith(('DEBUG', 'S3', 'DB', 'API')):
            print(f"  {key}={value}")
    print(f"=== LAMBDA HANDLER PROCESSING ===")
    
    if os.environ.get('DEBUG_MODE') == 'true':
        print("DEBUG_MODE is enabled - continuing with verbose logging...")
    else:
        print("DEBUG_MODE is not enabled or not set to 'true'")
    
    session_id = None
    
    try:
        logger.info(f"PDF Processor Lambda started. Event: {json.dumps(event)}")
        print(f"STEP 1: Starting payload parsing...")
        
        # Parse and validate input payload
        payload = parse_and_validate_payload(event)
        session_id = payload.get('sessionId')
        
        print(f"STEP 2: Payload parsed successfully - SessionId: {session_id}, DocumentId: {payload['documentId']}")
        logger.info(f"Processing session {session_id} for document {payload['documentId']}")
        
        # Verify session exists before starting processing
        print(f"STEP 3: Verifying processing session exists...")
        from api_callbacks import verify_session_exists
        if not verify_session_exists(session_id):
            print(f"WARNING: Processing session {session_id} not found, attempting to continue anyway...")
        
        # Update processing status to 'processing'
        print(f"STEP 4: Updating processing status to 'processing'...")
        update_processing_status(session_id, 'processing', progress=10, 
                                message='Starting PDF processing...')
        
        # Process based on operation type
        operation = payload.get('operation', 'split_document')
        print(f"STEP 5: Processing operation type: {operation}")
        
        if operation == 'split_document':
            print(f"STEP 6: Starting document splitting process...")
            results = process_document_splitting(payload)
            print(f"STEP 7: Document splitting completed - {len(results)} results")
        else:
            raise PDFProcessingError(f"Unsupported operation: {operation}")
        
        # Update processing status to completed
        print(f"STEP 8: Updating processing status to 'completed'...")
        update_processing_status(session_id, 'completed', progress=100,
                                message=f'Successfully processed {len(results)} documents')
        
        # Link results back to API
        print(f"STEP 9: Linking results back to API...")
        link_processing_results(payload['documentId'], results)
        
        print(f"STEP 10: Processing completed successfully!")
        logger.info(f"Processing completed successfully for session {session_id}")
        
        return {
            'statusCode': 200,
            'body': json.dumps({
                'status': 'completed',
                'sessionId': session_id,
                'processedDocuments': len(results),
                'results': results
            })
        }
        
    except Exception as e:
        error_message = f"Processing failed: {str(e)}"
        logger.error(f"Lambda processing error: {error_message}")
        logger.error(traceback.format_exc())
        
        # Update processing status to error
        if session_id:
            try:
                update_processing_status(session_id, 'error', error=error_message)
            except Exception as status_error:
                logger.error(f"Failed to update error status: {status_error}")
        
        return {
            'statusCode': 500,
            'body': json.dumps({
                'status': 'error',
                'sessionId': session_id,
                'error': error_message
            })
        }

def parse_and_validate_payload(event) -> Dict[str, Any]:
    """
    Parse and validate the Lambda event payload
    
    Args:
        event: Raw Lambda event
        
    Returns:
        dict: Validated payload
        
    Raises:
        PDFProcessingError: If payload is invalid
    """
    try:
        # Handle different event formats
        if isinstance(event, str):
            payload = json.loads(event)
        elif 'body' in event:
            payload = json.loads(event['body']) if isinstance(event['body'], str) else event['body']
        else:
            payload = event
            
        # Validate required fields
        required_fields = ['documentId', 'sessionId', 'operation', 'bookmarks']
        for field in required_fields:
            if field not in payload:
                raise PDFProcessingError(f"Missing required field: {field}")
        
        # Validate document ID
        if not isinstance(payload['documentId'], int) or payload['documentId'] <= 0:
            raise PDFProcessingError("Invalid documentId")
            
        # Validate session ID
        if not payload['sessionId'] or not isinstance(payload['sessionId'], str):
            raise PDFProcessingError("Invalid sessionId")
            
        # Validate bookmarks
        if not isinstance(payload['bookmarks'], list):
            raise PDFProcessingError("Bookmarks must be a list")
            
        # Validate each bookmark
        for i, bookmark in enumerate(payload['bookmarks']):
            required_bookmark_fields = ['bookmarkId', 'pageIndex', 'documentTypeId', 'documentTypeName']
            for field in required_bookmark_fields:
                if field not in bookmark:
                    raise PDFProcessingError(f"Bookmark {i}: missing field {field}")
        
        logger.info(f"Payload validation successful for document {payload['documentId']}")
        return payload
        
    except json.JSONDecodeError as e:
        raise PDFProcessingError(f"Invalid JSON payload: {e}")
    except Exception as e:
        raise PDFProcessingError(f"Payload validation failed: {e}")

def process_document_splitting(payload: Dict[str, Any]) -> List[Dict[str, Any]]:
    """
    Process document splitting based on bookmarks
    
    Args:
        payload: Processing payload with document and bookmark information
        
    Returns:
        list: Processing results for each split document
    """
    document_id = payload['documentId']
    bookmarks = payload['bookmarks']
    metadata = payload.get('metadata', {})
    
    logger.info(f"Starting document splitting for document {document_id} with {len(bookmarks)} bookmarks")
    
    # Step 1: Download source PDF from S3
    update_processing_status(payload['sessionId'], 'processing', progress=20,
                            message='Downloading source PDF...')
    
    source_pdf_data = download_pdf_from_s3(document_id)
    
    # Step 2: Load and validate PDF
    update_processing_status(payload['sessionId'], 'processing', progress=30,
                            message='Loading and validating PDF...')
    
    pdf_reader = load_and_validate_pdf(source_pdf_data)
    total_pages = len(pdf_reader.pages)
    
    logger.info(f"Source PDF loaded: {total_pages} pages")
    
    # Step 3: Generate split ranges from bookmarks
    update_processing_status(payload['sessionId'], 'processing', progress=40,
                            message='Calculating split ranges...')
    
    split_ranges = calculate_split_ranges(bookmarks, total_pages)
    
    logger.info(f"Generated {len(split_ranges)} split ranges: {split_ranges}")
    
    # Handle special case: Index Only (single bookmark at page 0)
    if not split_ranges:
        logger.info("Index Only case detected - no splitting required")
        update_processing_status(payload['sessionId'], 'completed', progress=100,
                                message='Index Only operation - no splitting required')
        return []
    
    # Step 4: Split PDF and process each section
    results = []
    
    for i, split_range in enumerate(split_ranges):
        progress = 50 + (i * 30 // len(split_ranges))
        update_processing_status(payload['sessionId'], 'processing', progress=progress,
                                message=f'Processing split {i+1} of {len(split_ranges)}...')
        
        result = process_single_split(pdf_reader, split_range, document_id, metadata)
        results.append(result)
        
        logger.info(f"Processed split {i+1}: {result}")
    
    update_processing_status(payload['sessionId'], 'processing', progress=80,
                            message='Updating database records...')
    
    # Step 5: Update database with results
    update_database_records(document_id, bookmarks, results)
    
    # Step 6: Mark original document as Obsolete (per Hydra DD design)
    update_processing_status(payload['sessionId'], 'processing', progress=90,
                            message='Marking original document as obsolete...')
    
    mark_original_document_obsolete(document_id)
    
    logger.info(f"Document splitting completed for document {document_id}")
    return results

# download_pdf_from_s3 function is now imported from s3_operations.py

def load_and_validate_pdf(pdf_data: bytes) -> PyPDF2.PdfReader:
    """
    Load PDF data and validate structure
    
    Args:
        pdf_data: PDF file bytes
        
    Returns:
        PyPDF2.PdfReader: PDF reader object
        
    Raises:
        PDFProcessingError: If PDF is invalid or corrupted
    """
    try:
        pdf_stream = io.BytesIO(pdf_data)
        pdf_reader = PyPDF2.PdfReader(pdf_stream)
        
        # Validate PDF structure
        if len(pdf_reader.pages) == 0:
            raise PDFProcessingError("PDF contains no pages")
            
        # Test reading first page to ensure PDF is not corrupted
        first_page = pdf_reader.pages[0]
        _ = first_page.extract_text()  # This will fail if PDF is corrupted
        
        logger.info(f"PDF validation successful: {len(pdf_reader.pages)} pages")
        return pdf_reader
        
    except PyPDF2.errors.PdfReadError as e:
        raise PDFProcessingError(f"PDF is corrupted or invalid: {e}")
    except Exception as e:
        raise PDFProcessingError(f"Failed to load PDF: {e}")

def calculate_split_ranges(bookmarks: List[Dict], total_pages: int) -> List[Dict[str, Any]]:
    """
    Calculate page ranges for PDF splitting based on bookmarks
    
    Args:
        bookmarks: List of bookmark dictionaries with pageIndex
        total_pages: Total number of pages in source PDF
        
    Returns:
        list: Split ranges with start/end pages and document type info
        
    Note: Bookmarks are SPLIT POINTS, not document starts. Each bookmark creates 
          a break, resulting in documents BEFORE and AFTER the break position.
          Single bookmark at page 0 should be handled by API (IndexOnly case).
    """
    if not bookmarks:
        # No bookmarks - single document covering all pages
        return [{
            'startPage': 0,
            'endPage': total_pages - 1,
            'documentTypeId': None,
            'documentTypeName': 'Unknown',
            'bookmarkId': None
        }]
    
    # Sort bookmarks by page index
    sorted_bookmarks = sorted(bookmarks, key=lambda b: b['pageIndex'])
    
    # Special case: Single bookmark at page 0 (Index Only)
    # This should be handled by API locally, not processed here
    if len(sorted_bookmarks) == 1 and sorted_bookmarks[0]['pageIndex'] == 0:
        logger.warning("Single bookmark at page 0 received in Lambda - this should be handled by API as IndexOnly")
        # Return empty ranges to indicate no splitting should occur
        return []
    
    ranges = []
    
    # Process bookmarks to create ranges
    # Each bookmark creates a range that starts FROM that bookmark's position
    current_start = 0
    
    for i, bookmark in enumerate(sorted_bookmarks):
        split_point = bookmark['pageIndex']
        
        # Create a range from current_start up to (but not including) this split point
        if current_start < split_point:
            if i == 0:
                # First range: pages before first bookmark are unlabeled
                ranges.append({
                    'startPage': current_start,
                    'endPage': split_point - 1,
                    'documentTypeId': None,
                    'documentTypeName': 'Unlabeled Pages',
                    'documentDate': None,
                    'comments': 'Pages before first bookmark',
                    'bookmarkId': None
                })
            else:
                # Subsequent ranges get the PREVIOUS bookmark's document type
                prev_bookmark = sorted_bookmarks[i-1]
                ranges.append({
                    'startPage': current_start,
                    'endPage': split_point - 1,
                    'documentTypeId': prev_bookmark['documentTypeId'],
                    'documentTypeName': prev_bookmark['documentTypeName'],
                    'documentDate': prev_bookmark.get('documentDate'),
                    'comments': prev_bookmark.get('comments'),
                    'bookmarkId': prev_bookmark['bookmarkId']
                })
        
        # Update current_start for next range
        current_start = split_point
    
    # Handle remaining pages after the last bookmark
    if current_start < total_pages:
        last_bookmark = sorted_bookmarks[-1]
        ranges.append({
            'startPage': current_start,
            'endPage': total_pages - 1,
            'documentTypeId': last_bookmark['documentTypeId'],
            'documentTypeName': last_bookmark['documentTypeName'],
            'documentDate': last_bookmark.get('documentDate'),
            'comments': last_bookmark.get('comments'),
            'bookmarkId': last_bookmark['bookmarkId']
        })
    
    # Sort ranges by startPage to ensure correct processing order
    ranges.sort(key=lambda x: x['startPage'])
    
    logger.info(f"Calculated {len(ranges)} split ranges from {len(bookmarks)} bookmarks:")
    for i, range_info in enumerate(ranges):
        logger.info(f"  Range {i+1}: Pages {range_info['startPage']}-{range_info['endPage']} "
                   f"({range_info['documentTypeName']}, BookmarkId: {range_info['bookmarkId']})")
    
    return ranges

def process_single_split(pdf_reader: PyPDF2.PdfReader, split_range: Dict[str, Any], 
                        source_document_id: int, metadata: Dict[str, Any]) -> Dict[str, Any]:
    """
    Process a single PDF split range
    
    Args:
        pdf_reader: Source PDF reader
        split_range: Range information with start/end pages
        source_document_id: Original document ID
        metadata: Processing metadata
        
    Returns:
        dict: Processing result for this split
    """
    try:
        start_page = split_range['startPage']
        end_page = split_range['endPage']
        
        logger.info(f"Processing split: pages {start_page}-{end_page}")
        
        # Create new PDF with selected pages
        pdf_writer = PyPDF2.PdfWriter()
        
        for page_num in range(start_page, end_page + 1):
            if page_num < len(pdf_reader.pages):
                pdf_writer.add_page(pdf_reader.pages[page_num])
        
        # Generate PDF bytes
        output_stream = io.BytesIO()
        pdf_writer.write(output_stream)
        split_pdf_data = output_stream.getvalue()
        output_stream.close()
        
        # Create new database record FIRST to get the new image ID
        new_image_id = create_image_record(
            source_document_id=source_document_id,
            split_range=split_range,
            s3_key=None,  # Will update after upload
            filename=None,  # Will update after upload
            page_count=end_page - start_page + 1,
            metadata=metadata
        )
        
        # Generate filename using the NEW image ID
        split_filename = f"{new_image_id}.pdf"
        
        # Upload to S3 with proper filename
        s3_key = upload_split_to_s3(split_pdf_data, split_filename)
        
        # Update database record with S3 details
        update_image_record_s3_info(new_image_id, s3_key, split_filename)
        
        return {
            'originalImageId': source_document_id,
            'resultImageId': new_image_id,
            'startPage': start_page,
            'endPage': end_page,
            'pageCount': end_page - start_page + 1,
            'documentTypeId': split_range['documentTypeId'],
            'documentTypeName': split_range['documentTypeName'],
            'filename': split_filename,
            's3Key': s3_key,
            'processingStatus': 'completed',
            'bookmarkId': split_range.get('bookmarkId')
        }
        
    except Exception as e:
        logger.error(f"Failed to process split {start_page}-{end_page}: {e}")
        raise PDFProcessingError(f"Split processing failed: {e}")

# Additional helper functions will be implemented in subsequent files...
# This includes S3 operations, database operations, and utility functions

if __name__ == "__main__":
    # Local testing
    test_event = {
        "documentId": 123,
        "sessionId": "test-session-123",
        "operation": "split_document",
        "bookmarks": [
            {
                "bookmarkId": 1,
                "pageIndex": 5,
                "documentTypeId": 42,
                "documentTypeName": "Agreement"
            }
        ],
        "metadata": {
            "userId": 21496,
            "bucketPrefix": "ffn",
            "offeringId": 302
        }
    }
    
    print("Testing PDF processor locally...")
    result = lambda_handler(test_event, None)
    print(f"Result: {json.dumps(result, indent=2)}")
