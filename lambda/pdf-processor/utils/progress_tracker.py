"""
Progress Tracker

Handles progress updates and callbacks for PDF manipulation processing.
Provides real-time status updates to the ESizzle API during Lambda execution.
"""

import logging
import os
import asyncio
import aiohttp
import json
from typing import Dict, Any, Optional
from datetime import datetime

logger = logging.getLogger(__name__)

class ProgressTracker:
    """Manages progress tracking and callback updates during processing"""
    
    def __init__(self, session_id: Optional[str], image_id: int):
        self.session_id = session_id
        self.image_id = image_id
        self.callback_url = os.environ.get('PROGRESS_CALLBACK_URL', '')
        self.enabled = os.environ.get('ENABLE_PROGRESS_CALLBACKS', 'true').lower() == 'true'
        
        if not self.enabled:
            logger.info("Progress callbacks disabled via environment variable")
        
        if not self.callback_url and self.enabled:
            logger.warning("No progress callback URL configured")
    
    async def update_progress(self, status: str, progress: int, message: str, 
                            data: Optional[Dict[str, Any]] = None) -> bool:
        """
        Send progress update to the ESizzle API
        
        Args:
            status: Current status (processing, completed, error)
            progress: Progress percentage (0-100)
            message: Human-readable progress message
            data: Optional additional data
            
        Returns:
            True if update was sent successfully, False otherwise
        """
        
        if not self.enabled or not self.session_id or not self.callback_url:
            return False
        
        try:
            payload = {
                'sessionId': self.session_id,
                'imageId': self.image_id,
                'status': status,
                'progress': min(max(progress, 0), 100),  # Clamp to 0-100
                'message': message,
                'timestamp': datetime.utcnow().isoformat() + 'Z'
            }
            
            if data:
                payload['data'] = data
            
            # Use the callback URL with session ID
            full_url = f"{self.callback_url}/{self.session_id}"
            
            # Create async HTTP session
            timeout = aiohttp.ClientTimeout(total=5.0)  # 5 second timeout
            
            async with aiohttp.ClientSession(timeout=timeout) as session:
                async with session.post(
                    full_url,
                    json=payload,
                    headers={
                        'Content-Type': 'application/json',
                        'User-Agent': 'ESizzle-Lambda-Processor/1.0'
                    }
                ) as response:
                    
                    if response.status == 200:
                        logger.debug(f"Progress update sent: {status} {progress}% - {message}")
                        return True
                    else:
                        logger.warning(f"Progress update failed with status {response.status}")
                        return False
                        
        except asyncio.TimeoutError:
            logger.warning("Progress update timed out")
            return False
            
        except Exception as e:
            logger.warning(f"Failed to send progress update: {e}")
            return False
    
    def update_progress_sync(self, status: str, progress: int, message: str, 
                           data: Optional[Dict[str, Any]] = None) -> bool:
        """
        Synchronous version of update_progress for non-async contexts
        
        Args:
            status: Current status (processing, completed, error)
            progress: Progress percentage (0-100)
            message: Human-readable progress message
            data: Optional additional data
            
        Returns:
            True if update was sent successfully, False otherwise
        """
        
        if not self.enabled or not self.session_id or not self.callback_url:
            return False
        
        try:
            import requests
            
            payload = {
                'sessionId': self.session_id,
                'imageId': self.image_id,
                'status': status,
                'progress': min(max(progress, 0), 100),  # Clamp to 0-100
                'message': message,
                'timestamp': datetime.utcnow().isoformat() + 'Z'
            }
            
            if data:
                payload['data'] = data
            
            # Use the callback URL with session ID
            full_url = f"{self.callback_url}/{self.session_id}"
            
            response = requests.post(
                full_url,
                json=payload,
                headers={
                    'Content-Type': 'application/json',
                    'User-Agent': 'ESizzle-Lambda-Processor/1.0'
                },
                timeout=5.0
            )
            
            if response.status_code == 200:
                logger.debug(f"Progress update sent: {status} {progress}% - {message}")
                return True
            else:
                logger.warning(f"Progress update failed with status {response.status_code}")
                return False
                
        except Exception as e:
            logger.warning(f"Failed to send progress update: {e}")
            return False
    
    async def send_completion_update(self, success: bool, result: Optional[Dict[str, Any]] = None, 
                                   error: Optional[str] = None) -> bool:
        """
        Send completion update with final results
        
        Args:
            success: Whether processing completed successfully
            result: Processing results if successful
            error: Error message if failed
            
        Returns:
            True if update was sent successfully, False otherwise
        """
        
        status = 'completed' if success else 'error'
        progress = 100 if success else 0
        message = 'Processing completed successfully' if success else f'Processing failed: {error}'
        
        data = {}
        if success and result:
            data['result'] = result
        if error:
            data['error'] = error
        
        return await self.update_progress(status, progress, message, data)
    
    async def send_error_update(self, error_message: str, error_details: Optional[Dict[str, Any]] = None) -> bool:
        """
        Send error update
        
        Args:
            error_message: Error message
            error_details: Optional error details
            
        Returns:
            True if update was sent successfully, False otherwise
        """
        
        data = {'error': error_message}
        if error_details:
            data['errorDetails'] = error_details
        
        return await self.update_progress('error', 0, f'Error: {error_message}', data)

class ProgressStages:
    """Standard progress stages and percentages"""
    
    INITIALIZING = (10, "Initializing...")
    LOADING_DATA = (20, "Loading document data...")
    DOWNLOADING_PDF = (30, "Downloading PDF...")
    CREATING_BACKUP = (35, "Creating backup...")
    APPLYING_REDACTIONS = (45, "Applying redactions...")
    APPLYING_ROTATIONS = (60, "Applying rotations...")
    DELETING_PAGES = (75, "Deleting pages...")
    SPLITTING_DOCUMENT = (85, "Splitting document...")
    SAVING_DOCUMENT = (95, "Saving processed document...")
    COMPLETED = (100, "Processing completed successfully")
    
    @staticmethod
    def get_stage(stage_name: str) -> tuple:
        """Get progress percentage and message for a stage"""
        return getattr(ProgressStages, stage_name.upper(), (0, "Processing..."))

class BatchProgressTracker:
    """Tracks progress for batch operations with multiple documents"""
    
    def __init__(self, session_id: Optional[str], total_documents: int):
        self.session_id = session_id
        self.total_documents = total_documents
        self.completed_documents = 0
        self.failed_documents = 0
        self.current_document_id = None
        self.callback_url = os.environ.get('PROGRESS_CALLBACK_URL', '')
        self.enabled = os.environ.get('ENABLE_PROGRESS_CALLBACKS', 'true').lower() == 'true'
        
    async def start_document(self, document_id: int) -> bool:
        """Mark start of processing for a document"""
        self.current_document_id = document_id
        
        progress = int((self.completed_documents / self.total_documents) * 100)
        message = f"Processing document {document_id} ({self.completed_documents + 1}/{self.total_documents})"
        
        return await self._send_update('processing', progress, message, {
            'currentDocument': document_id,
            'completedDocuments': self.completed_documents,
            'totalDocuments': self.total_documents
        })
    
    async def complete_document(self, document_id: int, success: bool, 
                              result: Optional[Dict[str, Any]] = None) -> bool:
        """Mark completion of processing for a document"""
        
        if success:
            self.completed_documents += 1
        else:
            self.failed_documents += 1
        
        progress = int((self.completed_documents / self.total_documents) * 100)
        
        if self.completed_documents + self.failed_documents >= self.total_documents:
            # Batch complete
            status = 'completed'
            message = f"Batch completed: {self.completed_documents} succeeded, {self.failed_documents} failed"
        else:
            status = 'processing'
            message = f"Document {document_id} {'completed' if success else 'failed'}"
        
        data = {
            'completedDocuments': self.completed_documents,
            'failedDocuments': self.failed_documents,
            'totalDocuments': self.total_documents,
            'lastDocument': document_id,
            'lastDocumentSuccess': success
        }
        
        if result:
            data['lastDocumentResult'] = result
        
        return await self._send_update(status, progress, message, data)
    
    async def _send_update(self, status: str, progress: int, message: str, 
                          data: Optional[Dict[str, Any]] = None) -> bool:
        """Send batch progress update"""
        
        if not self.enabled or not self.session_id or not self.callback_url:
            return False
        
        try:
            payload = {
                'sessionId': self.session_id,
                'status': status,
                'progress': progress,
                'message': message,
                'timestamp': datetime.utcnow().isoformat() + 'Z',
                'batchOperation': True
            }
            
            if data:
                payload['data'] = data
            
            # Use the callback URL with session ID
            full_url = f"{self.callback_url}/{self.session_id}"
            
            timeout = aiohttp.ClientTimeout(total=5.0)
            
            async with aiohttp.ClientSession(timeout=timeout) as session:
                async with session.post(
                    full_url,
                    json=payload,
                    headers={
                        'Content-Type': 'application/json',
                        'User-Agent': 'ESizzle-Lambda-Processor/1.0'
                    }
                ) as response:
                    
                    return response.status == 200
                    
        except Exception as e:
            logger.warning(f"Failed to send batch progress update: {e}")
            return False

class ProgressLogger:
    """Logs progress updates for debugging and monitoring"""
    
    def __init__(self, logger_name: str = __name__):
        self.logger = logging.getLogger(logger_name)
        self.start_time = datetime.utcnow()
        self.last_update_time = self.start_time
        
    def log_progress(self, stage: str, progress: int, message: str, 
                    details: Optional[Dict[str, Any]] = None):
        """Log progress update with timing information"""
        
        current_time = datetime.utcnow()
        elapsed_total = (current_time - self.start_time).total_seconds()
        elapsed_since_last = (current_time - self.last_update_time).total_seconds()
        
        log_message = f"[{stage}] {progress}% - {message} (elapsed: {elapsed_total:.1f}s, delta: {elapsed_since_last:.1f}s)"
        
        if details:
            log_message += f" | Details: {json.dumps(details, default=str)}"
        
        self.logger.info(log_message)
        self.last_update_time = current_time
    
    def log_completion(self, success: bool, result: Optional[Dict[str, Any]] = None, 
                      error: Optional[str] = None):
        """Log completion with total processing time"""
        
        total_time = (datetime.utcnow() - self.start_time).total_seconds()
        
        if success:
            self.logger.info(f"Processing completed successfully in {total_time:.2f}s")
            if result:
                self.logger.info(f"Result: {json.dumps(result, default=str)}")
        else:
            self.logger.error(f"Processing failed after {total_time:.2f}s: {error}")

def create_progress_tracker(session_id: Optional[str], image_id: int) -> ProgressTracker:
    """Factory function to create a progress tracker"""
    return ProgressTracker(session_id, image_id)

def create_batch_progress_tracker(session_id: Optional[str], total_documents: int) -> BatchProgressTracker:
    """Factory function to create a batch progress tracker"""
    return BatchProgressTracker(session_id, total_documents)
