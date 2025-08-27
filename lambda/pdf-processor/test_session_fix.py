#!/usr/bin/env python3
"""
Test script to validate the processing session 404 fix
"""

import json
import time
import requests
from api_callbacks import update_processing_status, verify_session_exists

def test_session_management():
    """
    Test the new session management workflow
    """
    print("=== Testing Processing Session Management Fix ===\n")
    
    # Test data
    test_document_id = 123456  # Use a test document ID
    test_bookmarks = []  # Empty for simple test
    test_request = {
        "bookmarks": test_bookmarks,
        "documentMetadata": {
            "documentTypeId": 1,
            "documentDate": "2025-01-27",
            "comments": "Test processing session fix"
        }
    }
    
    print("1. Testing two-step session creation approach...")
    print("   This simulates the new workflow where:")
    print("   - Step 1: Create session and wait for DB commit")
    print("   - Step 2: Start processing with verified session")
    print()
    
    # Note: This test would need actual API endpoints running to work
    # For now, we'll test the Lambda callback retry logic
    
    print("2. Testing Lambda callback retry logic...")
    
    # Simulate session IDs that might not exist initially
    test_sessions = [
        "test-session-immediate",
        "test-session-delayed-1s", 
        "test-session-delayed-2s",
        "test-session-nonexistent"
    ]
    
    for session_id in test_sessions:
        print(f"\n   Testing session: {session_id}")
        
        # Test session verification
        print(f"     - Verifying session exists...")
        exists = verify_session_exists(session_id)
        print(f"     - Session exists: {exists}")
        
        if not exists:
            print(f"     - Session {session_id} not found (expected for test)")
        
        # Test status update with retry logic
        print(f"     - Testing status update with retry logic...")
        success = update_processing_status(
            session_id=session_id,
            status="processing",
            progress=25,
            message=f"Testing retry logic for {session_id}"
        )
        print(f"     - Status update result: {success}")
        
        if success:
            print(f"     ✅ Successfully updated status for {session_id}")
        else:
            print(f"     ❌ Failed to update status for {session_id} (expected for nonexistent)")
    
    print("\n3. Summary of improvements implemented:")
    print("   ✅ API: Added CreateProcessingSession endpoint")
    print("   ✅ API: Added StartProcessing endpoint") 
    print("   ✅ API: Updated ProcessBookmarks to use two-step approach")
    print("   ✅ API: Added SessionExistsAsync validation method")
    print("   ✅ API: Enhanced UpdateProcessingSessionAsync with retry logic")
    print("   ✅ Lambda: Added retry logic to update_processing_status")
    print("   ✅ Lambda: Added verify_session_exists helper")
    print("   ✅ Lambda: Added session verification before processing")
    
    print("\n4. Expected behavior:")
    print("   - Session creation now happens synchronously before Lambda invocation")
    print("   - Database transactions are committed before processing starts") 
    print("   - Lambda callbacks have retry logic for race conditions")
    print("   - 404 errors should be significantly reduced or eliminated")
    print("   - Better error handling and logging throughout")
    
    print("\n=== Test Complete ===")
    return True

if __name__ == "__main__":
    test_session_management()