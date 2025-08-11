"""
API Callbacks Helper for PDF Processor Lambda

Handles all API callbacks to the Esizzle backend for status updates,
progress tracking, and result linking after PDF processing completion.

Author: Esizzle Development Team
Date: January 2025
"""

import requests
import logging
import os
import json
from typing import Dict, List, Any, Optional
from datetime import datetime

logger = logging.getLogger(__name__)

class APICallbackError(Exception):
    """Custom exception for API callback failures"""
    pass

def get_api_config() -> Dict[str, str]:
    """
    Get API configuration from environment variables
    
    Returns:
        dict: API configuration parameters
        
    Raises:
        ValueError: If required configuration is missing
    """
    api_base_url = os.environ.get('API_BASE_URL')
    if not api_base_url:
        raise ValueError("API_BASE_URL environment variable not set")
    
    # Remove trailing slash if present
    api_base_url = api_base_url.rstrip('/')
    
    return {
        'base_url': api_base_url,
        'timeout': int(os.environ.get('API_TIMEOUT', '30')),
        'retry_attempts': int(os.environ.get('API_RETRY_ATTEMPTS', '3'))
    }

def get_auth_headers() -> Dict[str, str]:
    """
    Get authentication headers for API requests
    
    Returns:
        dict: HTTP headers for authentication
    """
    # For Lambda-to-API communication, we can use a service token
    service_token = os.environ.get('API_SERVICE_TOKEN')
    
    headers = {
        'Content-Type': 'application/json',
        'User-Agent': 'esizzle-pdf-processor-lambda/1.0'
    }
    
    if service_token:
        headers['Authorization'] = f'Bearer {service_token}'
    
    return headers

def make_api_request(method: str, endpoint: str, data: Optional[Dict] = None, 
                    retry_count: int = 0) -> Dict[str, Any]:
    """
    Make API request with retry logic and error handling
    
    Args:
        method: HTTP method (GET, POST, PUT, etc.)
        endpoint: API endpoint (without base URL)
        data: Request payload data
        retry_count: Current retry attempt
        
    Returns:
        dict: API response data
        
    Raises:
        APICallbackError: If request fails after retries
    """
    try:
        config = get_api_config()
        url = f"{config['base_url']}{endpoint}"
        headers = get_auth_headers()
        
        logger.info(f"Making API {method} request to {url}")
        
        # Make the request
        response = requests.request(
            method=method,
            url=url,
            json=data,
            headers=headers,
            timeout=config['timeout']
        )
        
        # Handle response
        if response.status_code in [200, 201, 204]:
            try:
                return response.json() if response.content else {}
            except json.JSONDecodeError:
                return {}
        else:
            error_msg = f"API request failed with status {response.status_code}: {response.text}"
            logger.error(error_msg)
            
            # Retry on certain status codes
            if response.status_code in [500, 502, 503, 504] and retry_count < config['retry_attempts']:
                logger.info(f"Retrying API request (attempt {retry_count + 1})")
                return make_api_request(method, endpoint, data, retry_count + 1)
            
            raise APICallbackError(error_msg)
            
    except requests.exceptions.Timeout:
        error_msg = f"API request timeout for {endpoint}"
        logger.error(error_msg)
        
        if retry_count < config['retry_attempts']:
            logger.info(f"Retrying API request after timeout (attempt {retry_count + 1})")
            return make_api_request(method, endpoint, data, retry_count + 1)
        
        raise APICallbackError(error_msg)
        
    except requests.exceptions.RequestException as e:
        error_msg = f"API request failed for {endpoint}: {e}"
        logger.error(error_msg)
        
        if retry_count < config['retry_attempts']:
            logger.info(f"Retrying API request after error (attempt {retry_count + 1})")
            return make_api_request(method, endpoint, data, retry_count + 1)
        
        raise APICallbackError(error_msg)

def update_processing_status(session_id: str, status: str, progress: Optional[int] = None,
                           message: Optional[str] = None, error: Optional[str] = None) -> bool:
    """
    Update processing session status via API callback
    
    Args:
        session_id: Processing session ID
        status: Status ('processing', 'completed', 'error')
        progress: Optional progress percentage (0-100)
        message: Optional status message
        error: Optional error message if status is 'error'
        
    Returns:
        bool: True if update successful
    """
    try:
        endpoint = f"/api/documents/processing/{session_id}/status"
        
        payload = {
            'status': status,
            'timestamp': datetime.utcnow().isoformat() + 'Z'
        }
        
        if progress is not None:
            payload['progress'] = progress
            
        if message:
            payload['message'] = message
            
        if error:
            payload['error'] = error
        
        logger.info(f"Updating processing status for session {session_id}: {status}")
        
        response = make_api_request('PUT', endpoint, payload)
        
        logger.info(f"Successfully updated processing status for session {session_id}")
        return True
        
    except Exception as e:
        logger.error(f"Failed to update processing status for session {session_id}: {e}")
        return False

def link_processing_results(document_id: int, results: List[Dict[str, Any]]) -> bool:
    """
    Link processed document results back to the API
    
    Args:
        document_id: Original document ID
        results: List of processing results
        
    Returns:
        bool: True if linking successful
    """
    try:
        endpoint = f"/api/documents/{document_id}/link-results"
        
        # Transform results to match API expected format
        api_results = []
        for result in results:
            api_result = {
                'originalImageId': result['originalImageId'],
                'resultImageId': result['resultImageId'],
                'startPage': result['startPage'],
                'endPage': result['endPage'],
                'pageCount': result['pageCount'],
                'documentTypeId': result['documentTypeId'],
                'documentTypeName': result['documentTypeName'],
                'filename': result['filename'],
                'processingStatus': result['processingStatus']
            }
            
            # Include optional fields if present
            if 'bookmarkId' in result:
                api_result['bookmarkId'] = result['bookmarkId']
                
            api_results.append(api_result)
        
        payload = {
            'results': api_results,
            'timestamp': datetime.utcnow().isoformat() + 'Z',
            'totalResults': len(api_results)
        }
        
        logger.info(f"Linking {len(api_results)} processing results for document {document_id}")
        
        response = make_api_request('POST', endpoint, payload)
        
        logger.info(f"Successfully linked processing results for document {document_id}")
        return True
        
    except Exception as e:
        logger.error(f"Failed to link processing results for document {document_id}: {e}")
        return False

def notify_processing_completion(session_id: str, document_id: int, 
                               results: List[Dict[str, Any]], success: bool = True,
                               error_message: Optional[str] = None) -> bool:
    """
    Send comprehensive processing completion notification
    
    Args:
        session_id: Processing session ID
        document_id: Original document ID
        results: Processing results
        success: Whether processing was successful
        error_message: Error message if not successful
        
    Returns:
        bool: True if notification sent successfully
    """
    try:
        if success:
            # Update status to completed
            status_updated = update_processing_status(
                session_id=session_id,
                status='completed',
                progress=100,
                message=f'Successfully processed {len(results)} documents'
            )
            
            # Link results
            results_linked = link_processing_results(document_id, results)
            
            return status_updated and results_linked
        else:
            # Update status to error
            return update_processing_status(
                session_id=session_id,
                status='error',
                error=error_message or 'Processing failed'
            )
            
    except Exception as e:
        logger.error(f"Failed to notify processing completion: {e}")
        return False

def validate_api_connectivity() -> bool:
    """
    Validate API connectivity and authentication
    
    Returns:
        bool: True if API is accessible
    """
    try:
        # Test API connectivity with health check endpoint
        endpoint = "/api/health"
        
        response = make_api_request('GET', endpoint)
        
        logger.info("API connectivity validation successful")
        return True
        
    except Exception as e:
        logger.error(f"API connectivity validation failed: {e}")
        return False

def get_document_processing_session(session_id: str) -> Optional[Dict[str, Any]]:
    """
    Get processing session information from API
    
    Args:
        session_id: Processing session ID
        
    Returns:
        dict: Session information or None if not found
    """
    try:
        endpoint = f"/api/documents/processing/{session_id}/status"
        
        response = make_api_request('GET', endpoint)
        
        logger.info(f"Retrieved processing session info for {session_id}")
        return response
        
    except Exception as e:
        logger.error(f"Failed to get processing session {session_id}: {e}")
        return None

# Test function for local development
def test_api_callbacks():
    """
    Test API callbacks functionality
    """
    try:
        logger.info("Testing API callbacks...")
        
        # Test connectivity
        if not validate_api_connectivity():
            logger.error("API connectivity test failed")
            return False
        
        # Test status update
        test_session = "test-session-" + datetime.utcnow().strftime("%Y%m%d%H%M%S")
        
        if not update_processing_status(test_session, 'processing', progress=50, 
                                       message='Test processing'):
            logger.error("Status update test failed")
            return False
        
        logger.info("API callbacks test completed successfully")
        return True
        
    except Exception as e:
        logger.error(f"API callbacks test failed: {e}")
        return False

if __name__ == "__main__":
    # Local testing
    test_api_callbacks()
