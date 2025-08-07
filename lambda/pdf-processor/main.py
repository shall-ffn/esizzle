"""
ESizzle PDF Manipulation Lambda Handler

This Lambda function handles PDF document manipulations including:
- Redactions (with rasterization for security)
- Page rotations
- Page deletions  
- Document splitting based on page breaks

Compatible with existing LoanMaster database schema and S3 structure.
"""

import json
import traceback
import logging
import time
import os
from typing import Dict, Any, Optional

# Configure logging
logging.basicConfig(level=logging.INFO)
logger = logging.getLogger(__name__)

def lambda_handler(event: Dict[str, Any], context: Any) -> Dict[str, Any]:
    """
    Main Lambda handler for PDF manipulation operations
    
    Event structure:
    {
        "operation": "process_manipulations",
        "imageId": 12345,
        "sessionId": "uuid-string", 
        "timeout": 840,
        "progressCallbackUrl": "https://api.esizzle.com/api/processing/progress"
    }
    """
    
    image_id = None
    session_id = event.get('sessionId')
    start_time = time.time()
    
    try:
        # Validate input
        operation = event.get('operation')
        image_id = event.get('imageId')
        timeout_seconds = event.get('timeout', 840)  # 14 minutes default
        
        logger.info(f"Lambda invoked with event: {json.dumps(event)}")
        
        if not image_id:
            raise ValueError("imageId is required")
            
        if not operation:
            raise ValueError("operation is required")
            
        logger.info(f"Processing started for image {image_id}, session {session_id}, operation {operation}")
        
        # Import dependencies (lazy loading for faster cold starts)
        from processors.manipulation_orchestrator import ManipulationOrchestrator
        from utils.s3_manager import S3Manager
        from utils.db_manager import DatabaseManager
        from utils.progress_tracker import ProgressTracker
        
        # Initialize managers
        s3_manager = S3Manager()
        db_manager = DatabaseManager()
        progress_tracker = ProgressTracker(session_id, image_id)
        
        # Update initial progress
        await_safe(progress_tracker.update_progress('processing', 10, 'Initializing...'))
        
        # Update processing status in database
        db_manager.update_image_status(image_id, 'InWorkman')
        
        # Create orchestrator for coordinated processing
        orchestrator = ManipulationOrchestrator(
            s3_manager=s3_manager,
            db_manager=db_manager,
            progress_tracker=progress_tracker,
            timeout_seconds=timeout_seconds
        )
        
        # Route to appropriate handler
        if operation == 'process_manipulations':
            result = orchestrator.process_document_manipulations(image_id)
        elif operation == 'health_check':
            result = orchestrator.perform_health_check(image_id)
        else:
            raise ValueError(f"Unknown operation: {operation}")
            
        # Calculate processing time
        processing_time = time.time() - start_time
        result['processingTime'] = processing_time
        
        # Mark as completed successfully
        db_manager.update_image_status(image_id, 'NeedsProcessing')
        await_safe(progress_tracker.update_progress('completed', 100, 'Processing completed successfully', result))
        
        logger.info(f"Processing completed successfully for image {image_id} in {processing_time:.2f}s")
        
        return {
            'statusCode': 200,
            'body': json.dumps({
                'success': True,
                'imageId': image_id,
                'sessionId': session_id,
                'result': result,
                'processingTime': processing_time
            })
        }
        
    except Exception as e:
        processing_time = time.time() - start_time
        error_msg = f"Lambda processing failed for image {image_id}: {str(e)}"
        logger.error(f"ERROR: {error_msg}")
        logger.error(f"TRACEBACK: {traceback.format_exc()}")
        
        # Handle error recovery
        if image_id:
            try:
                handle_processing_error(image_id, error_msg, session_id)
            except Exception as recovery_error:
                logger.error(f"Error in error handler: {recovery_error}")
            
        return {
            'statusCode': 500,
            'body': json.dumps({
                'success': False,
                'error': error_msg,
                'imageId': image_id,
                'sessionId': session_id,
                'processingTime': processing_time,
                'traceback': traceback.format_exc() if os.environ.get('LOG_LEVEL') == 'DEBUG' else None
            })
        }

def handle_processing_error(image_id: int, error_msg: str, session_id: Optional[str] = None):
    """Handle processing errors with database rollback"""
    try:
        from utils.db_manager import DatabaseManager
        from utils.progress_tracker import ProgressTracker
        
        db_manager = DatabaseManager()
        
        # Reset image status to allow retry
        db_manager.update_image_status(image_id, 'NeedsImageManipulation')
        
        # Update progress with error
        if session_id:
            progress_tracker = ProgressTracker(session_id, image_id)
            await_safe(progress_tracker.update_progress('error', 0, error_msg))
        
        logger.info(f"Error recovery completed for image {image_id}")
        
    except Exception as e:
        logger.error(f"Error in error handler: {e}")

def await_safe(coro):
    """
    Safely execute async operations in Lambda context
    Since Lambda doesn't have an event loop, we handle this gracefully
    """
    try:
        import asyncio
        try:
            loop = asyncio.get_event_loop()
        except RuntimeError:
            loop = asyncio.new_event_loop()
            asyncio.set_event_loop(loop)
        return loop.run_until_complete(coro)
    except Exception as e:
        logger.warning(f"Async operation failed: {e}")
        return None

# Health check handler for monitoring
def health_handler(event: Dict[str, Any], context: Any) -> Dict[str, Any]:
    """Simple health check handler"""
    try:
        from utils.db_manager import DatabaseManager
        from utils.s3_manager import S3Manager
        
        # Test database connection
        db_manager = DatabaseManager()
        db_healthy = db_manager.test_connection()
        
        # Test S3 connection
        s3_manager = S3Manager()
        s3_healthy = s3_manager.test_connection()
        
        return {
            'statusCode': 200,
            'body': json.dumps({
                'status': 'healthy' if db_healthy and s3_healthy else 'unhealthy',
                'database': 'healthy' if db_healthy else 'unhealthy', 
                's3': 'healthy' if s3_healthy else 'unhealthy',
                'timestamp': time.time(),
                'environment': os.environ.get('ENVIRONMENT', 'unknown')
            })
        }
        
    except Exception as e:
        logger.error(f"Health check failed: {e}")
        return {
            'statusCode': 500,
            'body': json.dumps({
                'status': 'unhealthy',
                'error': str(e),
                'timestamp': time.time()
            })
        }

if __name__ == '__main__':
    # For local testing
    test_event = {
        'operation': 'process_manipulations',
        'imageId': 123,
        'sessionId': 'test-session-123'
    }
    
    result = lambda_handler(test_event, None)
    print(json.dumps(result, indent=2))
