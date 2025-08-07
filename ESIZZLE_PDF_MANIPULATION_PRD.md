# ESizzle PDF Manipulation System - Product Requirements Document

## Table of Contents
1. [Executive Summary](#executive-summary)
2. [System Architecture](#system-architecture)
3. [Frontend Vue.js Implementation](#frontend-vuejs-implementation)
4. [Python Lambda Processing](#python-lambda-processing)
5. [Database Integration](#database-integration)
6. [API Layer Enhancement](#api-layer-enhancement)
7. [Deployment & Infrastructure](#deployment--infrastructure)
8. [Testing Strategy](#testing-strategy)
9. [Success Criteria](#success-criteria)

## Executive Summary

This PRD defines the implementation of a comprehensive PDF manipulation system for the ESizzle platform, replacing the existing C# Watchman/Workman plugin architecture with a modern Vue.js frontend and Python Lambda backend solution. The system will provide pixel-perfect visual feedback for document manipulations while maintaining full compatibility with the existing LoanMaster database schema and S3 storage architecture.

**Key Objectives:**
- Replace C# plugins with Python Lambda functions using PyMuPDF (fitz)
- Implement comprehensive Vue.js annotation overlay system  
- Maintain exact visual compatibility with original Hydra system
- Use direct Lambda invocation for immediate processing
- Preserve existing database schema and S3 bucket structure

## System Architecture

### **Architecture Overview**
```
Vue.js Frontend → C# ESizzle API → Python Lambda → MySQL Database
                                      ↓
                                   AWS S3 Storage
```

### **Component Stack**
- **Frontend**: Vue.js 3 + TypeScript + PDF.js + Pinia + Tailwind CSS
- **Backend**: Python Lambda + PyMuPDF (fitz) + boto3
- **Database**: MySQL LoanMaster (existing schema)
- **Storage**: AWS S3 (existing bucket structure)
- **Integration**: C# ESizzle API (enhanced)

### **Processing Flow**
1. **User Interaction**: Vue.js UI captures manipulations (redactions, rotations, deletions, page breaks)
2. **Data Storage**: C# API saves manipulation metadata to existing database tables
3. **Direct Processing**: API directly invokes Python Lambda for immediate PDF processing
4. **File Manipulation**: Lambda applies changes using PyMuPDF and saves to S3
5. **Status Update**: Database status updated, frontend refreshes document view

## Frontend Vue.js Implementation

### **Component Architecture**

```typescript
// Component Hierarchy
PDFViewer.vue (Enhanced)
├── AnnotationOverlay.vue (Master orchestrator)
│   ├── RedactionVisualization.vue
│   ├── PageBreakVisualization.vue  
│   ├── PageDeletionVisualization.vue
│   ├── RotationIndicator.vue
│   └── AnnotationControls.vue
├── DocumentToolbar.vue (Enhanced with all manipulation modes)
└── ThumbnailPanel.vue (With annotation indicators)
```

### **Enhanced Type System**

```typescript
// src/types/manipulation.ts
export interface DocumentManipulationState {
  documentId: number
  pageCount: number
  redactions: RedactionAnnotation[]
  rotations: RotationAnnotation[]
  pageBreaks: PageBreakAnnotation[]
  pageDeletions: PageDeletionAnnotation[]
  hasUnsavedChanges: boolean
  processingStatus: 'idle' | 'processing' | 'completed' | 'error'
  lastModified: Date
  modifiedBy: number
}

export interface RedactionAnnotation {
  id: number
  imageId: number
  pageNumber: number // 0-based (matches DB)
  pageX: number // Exact DB field mapping
  pageY: number
  pageWidth: number
  pageHeight: number
  guid: string
  text?: string
  applied: boolean
  drawOrientation: number
  createdBy: number
  dateCreated: Date
  deleted: boolean
}

export interface RotationAnnotation {
  id: number
  imageId: number
  pageIndex: number // 0-based (matches DB)
  rotate: number // 0, 90, 180, 270 (matches DB enum)
}

export interface PageBreakAnnotation {
  id: number
  imageId: number
  pageIndex: number // 0-based (matches DB)
  text: string // Document type and metadata
  imageDocumentTypeId: number
  resultImageId?: number // Populated after split
  isGeneric: boolean
  displayText: string
  deleted: boolean
}

export interface PageDeletionAnnotation {
  id: number
  imageId: number
  pageIndex: number // 0-based (matches DB)
  createdBy: number
  dateCreated: Date
}

export type EditMode = 'view' | 'redaction' | 'pagebreak' | 'deletion' | 'rotation'

export interface ProcessingProgress {
  sessionId: string
  documentId: number
  status: 'starting' | 'processing' | 'completed' | 'error'
  progress: number // 0-100
  currentOperation?: string
  error?: string
}
```

### **Coordinate Translation System**

```typescript
// src/utils/coordinate-translator.ts
export class CoordinateTranslator {
  constructor(
    private viewport: any,
    private canvasDimensions: { width: number; height: number },
    private zoomLevel: number
  ) {}

  // Convert PDF page coordinates to canvas screen coordinates
  pageToCanvas(coord: { x: number; y: number }): { x: number; y: number } {
    const scaleX = this.canvasDimensions.width / this.viewport.width
    const scaleY = this.canvasDimensions.height / this.viewport.height
    
    return {
      x: coord.x * scaleX * (this.zoomLevel / 100),
      y: coord.y * scaleY * (this.zoomLevel / 100)
    }
  }

  // Convert canvas screen coordinates to PDF page coordinates
  canvasToPage(coord: { x: number; y: number }): { x: number; y: number } {
    const scaleX = this.viewport.width / this.canvasDimensions.width
    const scaleY = this.viewport.height / this.canvasDimensions.height
    
    return {
      x: coord.x * scaleX / (this.zoomLevel / 100),
      y: coord.y * scaleY / (this.zoomLevel / 100)
    }
  }

  // Handle rotation transformations
  applyRotation(coord: { x: number; y: number }, rotation: number): { x: number; y: number } {
    if (rotation === 0) return coord
    
    const centerX = this.viewport.width / 2
    const centerY = this.viewport.height / 2
    const rad = (rotation * Math.PI) / 180
    
    const cos = Math.cos(rad)
    const sin = Math.sin(rad)
    
    const translatedX = coord.x - centerX
    const translatedY = coord.y - centerY
    
    return {
      x: translatedX * cos - translatedY * sin + centerX,
      y: translatedX * sin + translatedY * cos + centerY
    }
  }
}
```

### **Master Annotation Overlay Component**

```vue
<!-- src/components/viewer/AnnotationOverlay.vue -->
<template>
  <div 
    v-if="manipulationState"
    class="absolute inset-0 pointer-events-none z-20"
  >
    <!-- Page-specific overlays for each page -->
    <div
      v-for="pageIndex in pageCount"
      :key="pageIndex"
      class="absolute page-overlay"
      :style="getPageStyle(pageIndex - 1)"
    >
      <!-- Redaction overlays -->
      <RedactionVisualization
        :redactions="getPageRedactions(pageIndex - 1)"
        :page-number="pageIndex - 1"
        :editable="editMode === 'redaction'"
        :coordinate-translator="coordinateTranslator"
        @redaction-updated="handleRedactionUpdate"
        @redaction-removed="handleRedactionRemove"
      />

      <!-- Page break overlays -->
      <PageBreakVisualization
        :page-breaks="getPageBreaks(pageIndex - 1)"
        :page-number="pageIndex - 1"
        :editable="editMode === 'pagebreak'"
        @break-added="handlePageBreakAdd"
        @break-removed="handlePageBreakRemove"
      />

      <!-- Page deletion overlays -->
      <PageDeletionVisualization
        v-if="isPageDeleted(pageIndex - 1)"
        :page-number="pageIndex - 1"
        :editable="editMode === 'deletion'"
        @deletion-toggled="handlePageDeletionToggle"
      />

      <!-- Rotation indicators -->
      <RotationIndicator
        :rotation="getPageRotation(pageIndex - 1)"
        :page-number="pageIndex - 1"
        :editable="editMode === 'rotation'"
        @rotation-changed="handleRotationChange"
      />
    </div>

    <!-- Mode-specific UI elements -->
    <AnnotationControls
      :edit-mode="editMode"
      :has-changes="manipulationState.hasUnsavedChanges"
      :processing-status="manipulationState.processingStatus"
      @mode-changed="handleModeChange"
      @save-changes="handleSaveChanges"
      @discard-changes="handleDiscardChanges"
    />

    <!-- Processing progress overlay -->
    <ProcessingOverlay
      v-if="manipulationState.processingStatus === 'processing'"
      :progress="processingProgress"
      @cancel="handleCancelProcessing"
    />
  </div>
</template>

<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import type { DocumentManipulationState, EditMode, ProcessingProgress } from '@/types/manipulation'
import { CoordinateTranslator } from '@/utils/coordinate-translator'
import { useMainStore } from '@/stores/main'

// ... component implementation
</script>
```

### **Visual Design Specifications**

```css
/* src/styles/annotations.css */

/* Applied redactions - black overlay (matches original) */
.redaction-applied {
  background-color: #000000;
  opacity: 1.0;
  border: 2px solid #000000;
  z-index: 20;
  pointer-events: none;
}

/* Pending redactions - yellow semi-transparent (matches original) */
.redaction-pending {
  background-color: rgba(255, 255, 0, 0.5);
  border: 2px solid #000000;
  z-index: 20;
  pointer-events: auto;
  cursor: pointer;
}

/* Drawing redactions - yellow dashed */
.redaction-drawing {
  background-color: rgba(255, 255, 0, 0.3);
  border: 2px dashed #ca8a04;
  z-index: 25;
  pointer-events: none;
}

/* Normal document breaks - green (matches original) */
.page-break-normal {
  background-color: rgba(0, 128, 0, 0.9);
  height: 20px;
  position: absolute;
  top: -22px;
  left: 0;
  right: 0;
  z-index: 10;
  display: flex;
  align-items: center;
  justify-content: center;
}

/* Generic breaks - orange (matches original) */
.page-break-generic {
  background-color: rgba(255, 165, 0, 0.9);
  height: 20px;
  position: absolute;
  top: -22px;
  left: 0;
  right: 0;
  z-index: 10;
  display: flex;
  align-items: center;
  justify-content: center;
}

.page-break-text {
  color: white;
  font-weight: bold;
  font-size: 12px;
  text-shadow: 1px 1px 2px rgba(0,0,0,0.5);
  padding: 2px 6px;
  background-color: rgba(0,0,0,0.3);
  border-radius: 3px;
}

/* Page deletion X pattern (matches original) */
.page-deletion {
  position: absolute;
  inset: 0;
  background-color: rgba(255, 0, 0, 0.2);
  z-index: 30;
  pointer-events: auto;
}

.page-deletion::before,
.page-deletion::after {
  content: '';
  position: absolute;
  top: 0;
  left: 50%;
  width: 5px;
  height: 100%;
  background-color: rgba(220, 38, 38, 0.75);
  transform-origin: center;
}

.page-deletion::before {
  transform: translateX(-50%) rotate(45deg);
}

.page-deletion::after {
  transform: translateX(-50%) rotate(-45deg);
}

/* Rotation indicator */
.rotation-indicator {
  position: absolute;
  top: 8px;
  right: 8px;
  background-color: rgba(59, 130, 246, 0.9);
  color: white;
  padding: 4px 8px;
  border-radius: 12px;
  font-size: 11px;
  font-weight: bold;
  z-index: 15;
  display: flex;
  align-items: center;
  gap: 4px;
}
```

### **Enhanced Document Toolbar**

```vue
<!-- src/components/tools/DocumentToolbar.vue -->
<template>
  <div class="document-toolbar bg-white border-t border-gray-200 p-3">
    <div class="flex items-center justify-between">
      <!-- Mode Selection -->
      <div class="flex items-center space-x-2">
        <span class="text-sm font-medium text-gray-700">Mode:</span>
        <div class="flex rounded-lg border border-gray-300 overflow-hidden">
          <ModeButton 
            mode="view" 
            :active="editMode === 'view'"
            @click="setEditMode('view')"
          >
            <CursorArrowRaysIcon class="h-4 w-4" />
            View
          </ModeButton>
          <ModeButton 
            mode="redaction" 
            :active="editMode === 'redaction'"
            @click="setEditMode('redaction')"
          >
            <PencilIcon class="h-4 w-4" />
            Redact
          </ModeButton>
          <ModeButton 
            mode="pagebreak" 
            :active="editMode === 'pagebreak'"
            @click="setEditMode('pagebreak')"
          >
            <ScissorsIcon class="h-4 w-4" />
            Break
          </ModeButton>
          <ModeButton 
            mode="deletion" 
            :active="editMode === 'deletion'"
            @click="setEditMode('deletion')"
          >
            <TrashIcon class="h-4 w-4" />
            Delete
          </ModeButton>
          <ModeButton 
            mode="rotation" 
            :active="editMode === 'rotation'"
            @click="setEditMode('rotation')"
          >
            <ArrowPathIcon class="h-4 w-4" />
            Rotate
          </ModeButton>
        </div>
      </div>

      <!-- Status and Actions -->
      <div class="flex items-center space-x-3">
        <!-- Processing Status -->
        <StatusIndicator 
          :status="processingStatus"
          :progress="processingProgress"
        />

        <!-- Change Summary -->
        <div v-if="hasUnsavedChanges" class="text-sm text-gray-600">
          <ChangeSummary :manipulation-state="manipulationState" />
        </div>

        <!-- Action Buttons -->
        <div class="flex items-center space-x-2">
          <button
            v-if="hasUnsavedChanges"
            @click="discardChanges"
            :disabled="processingStatus === 'processing'"
            class="px-3 py-1.5 text-sm border border-gray-300 rounded-md hover:bg-gray-50 disabled:opacity-50 transition-colors"
          >
            Discard Changes
          </button>
          <button
            v-if="hasUnsavedChanges"
            @click="saveAndProcess"
            :disabled="processingStatus === 'processing'"
            class="px-4 py-1.5 text-sm bg-hydra-600 text-white rounded-md hover:bg-hydra-700 disabled:opacity-50 transition-colors flex items-center space-x-2"
          >
            <span v-if="processingStatus === 'processing'">
              <Spinner class="h-4 w-4" />
              Processing...
            </span>
            <span v-else>Save & Apply</span>
          </button>
        </div>
      </div>
    </div>

    <!-- Mode-specific instructions -->
    <div v-if="editMode !== 'view'" class="mt-3 p-2 bg-blue-50 border border-blue-200 rounded-md">
      <InstructionBar :mode="editMode" />
    </div>
  </div>
</template>

<script setup lang="ts">
// Component logic for toolbar
</script>
```

## Python Lambda Processing

### **Lambda Function Structure**

```
lambda_pdf_processor/
├── main.py                    # Lambda entry point
├── processors/
│   ├── __init__.py
│   ├── redaction_processor.py # PyMuPDF redaction logic
│   ├── rotation_processor.py  # Page rotation handling
│   ├── deletion_processor.py  # Page deletion logic
│   ├── splitting_processor.py # Document splitting
│   └── base_processor.py      # Common processing utilities
├── utils/
│   ├── __init__.py
│   ├── s3_manager.py          # S3 file operations
│   ├── db_manager.py          # Database operations
│   ├── pdf_utils.py           # PDF manipulation utilities
│   └── coordinate_utils.py    # Coordinate transformations
├── models/
│   ├── __init__.py
│   └── manipulation_models.py # Data models
├── requirements.txt           # Python dependencies
└── deployment/
    ├── lambda_function.zip
    └── layer_dependencies.zip
```

### **Main Lambda Handler**

```python
# main.py
import json
import traceback
import logging
from typing import Dict, Any, Optional
from processors.redaction_processor import RedactionProcessor
from processors.rotation_processor import RotationProcessor
from processors.deletion_processor import DeletionProcessor  
from processors.splitting_processor import SplittingProcessor
from utils.s3_manager import S3Manager
from utils.db_manager import DatabaseManager

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
    
    try:
        # Validate input
        operation = event.get('operation')
        image_id = event.get('imageId')
        timeout_seconds = event.get('timeout', 840)  # 14 minutes default
        
        if not image_id:
            raise ValueError("imageId is required")
            
        logger.info(f"Processing started for image {image_id}, session {session_id}")
        
        # Initialize managers
        s3_manager = S3Manager()
        db_manager = DatabaseManager()
        
        # Update progress
        update_progress(session_id, image_id, 'processing', 10, 'Initializing...')
        
        # Update processing status in database
        db_manager.update_image_status(image_id, 'InWorkman')
        
        if operation == 'process_manipulations':
            result = process_document_manipulations(
                image_id, 
                s3_manager,
                db_manager,
                session_id,
                timeout_seconds
            )
        else:
            raise ValueError(f"Unknown operation: {operation}")
            
        # Mark as completed
        db_manager.update_image_status(image_id, 'NeedsProcessing')
        update_progress(session_id, image_id, 'completed', 100, 'Processing completed successfully', result)
        
        logger.info(f"Processing completed successfully for image {image_id}")
        
        return {
            'statusCode': 200,
            'body': json.dumps({
                'success': True,
                'imageId': image_id,
                'sessionId': session_id,
                'result': result
            })
        }
        
    except Exception as e:
        error_msg = f"Lambda processing failed for image {image_id}: {str(e)}"
        logger.error(f"ERROR: {error_msg}")
        logger.error(f"TRACEBACK: {traceback.format_exc()}")
        
        # Handle error recovery
        if image_id:
            handle_processing_error(image_id, error_msg, session_id)
            
        return {
            'statusCode': 500,
            'body': json.dumps({
                'success': False,
                'error': error_msg,
                'imageId': image_id,
                'sessionId': session_id
            })
        }

def process_document_manipulations(
    image_id: int, 
    s3_manager: S3Manager,
    db_manager: DatabaseManager,
    session_id: Optional[str] = None,
    timeout_seconds: int = 840
) -> Dict[str, Any]:
    """Process all manipulations for a document"""
    
    logger.info(f"Starting manipulation processing for image {image_id}")
    
    # Get image metadata and manipulations from database
    update_progress(session_id, image_id, 'processing', 20, 'Loading document data...')
    
    image_record = db_manager.get_image_record(image_id)
    if not image_record:
        raise ValueError(f"Image record not found: {image_id}")
    
    # Get all manipulation data
    manipulations = {
        'redactions': db_manager.get_pending_redactions(image_id),
        'rotations': db_manager.get_rotations(image_id),
        'deletions': db_manager.get_page_deletions(image_id),
        'pageBreaks': db_manager.get_page_breaks(image_id)
    }
    
    logger.info(f"Found manipulations: {len(manipulations['redactions'])} redactions, "
                f"{len(manipulations['rotations'])} rotations, "
                f"{len(manipulations['deletions'])} deletions, "
                f"{len(manipulations['pageBreaks'])} page breaks")
    
    # Check if any manipulations need processing
    if not any(manipulations.values()):
        logger.info("No manipulations found to process")
        return {'message': 'No manipulations to process'}
    
    # Download PDF from S3 Processing path
    update_progress(session_id, image_id, 'processing', 30, 'Downloading PDF...')
    
    processing_path = f"Processing/{image_record['Path']}/{image_id}/{image_id}.pdf"
    pdf_bytes = s3_manager.download_file(processing_path, image_record['BucketPrefix'])
    
    result = {
        'originalPageCount': 0,
        'finalPageCount': 0,
        'operationsApplied': [],
        'splitImages': [],
        'processingTime': 0
    }
    
    import time
    start_time = time.time()
    
    # Create backup if manipulations will be applied
    if any(manipulations.values()):
        update_progress(session_id, image_id, 'processing', 35, 'Creating backup...')
        redact_original_path = f"RedactOriginal/{image_record['Path']}/{image_id}/{image_id}.pdf"
        s3_manager.upload_file(pdf_bytes, redact_original_path, image_record['BucketPrefix'])
        logger.info("Backup created in RedactOriginal path")
    
    # Process manipulations in correct order
    modified_pdf = pdf_bytes
    
    # 1. Apply redactions first (they need to be rasterized)
    if manipulations.get('redactions'):
        update_progress(session_id, image_id, 'processing', 45, 'Applying redactions...')
        processor = RedactionProcessor(db_manager)
        modified_pdf, redaction_result = processor.process(
            image_id, modified_pdf, manipulations['redactions']
        )
        result['operationsApplied'].append('redactions')
        logger.info(f"Applied {len(manipulations['redactions'])} redactions")
        
    # 2. Apply rotations
    if manipulations.get('rotations'):
        update_progress(session_id, image_id, 'processing', 60, 'Applying rotations...')
        processor = RotationProcessor(db_manager)
        modified_pdf, rotation_result = processor.process(
            image_id, modified_pdf, manipulations['rotations']
        )
        result['operationsApplied'].append('rotations')
        logger.info(f"Applied {len(manipulations['rotations'])} rotations")
        
    # 3. Apply page deletions
    if manipulations.get('deletions'):
        update_progress(session_id, image_id, 'processing', 75, 'Deleting pages...')
        processor = DeletionProcessor(db_manager)
        modified_pdf, deletion_result = processor.process(
            image_id, modified_pdf, manipulations['deletions']
        )
        result['operationsApplied'].append('deletions')
        result['finalPageCount'] = deletion_result.get('finalPageCount', 0)
        logger.info(f"Deleted {len(manipulations['deletions'])} pages")
        
    # 4. Handle page breaks (document splitting)
    if manipulations.get('pageBreaks'):
        update_progress(session_id, image_id, 'processing', 85, 'Splitting document...')
        processor = SplittingProcessor(db_manager, s3_manager)
        split_result = processor.process(
            image_record, modified_pdf, manipulations['pageBreaks']
        )
        result['operationsApplied'].append('splitting')
        result['splitImages'] = split_result.get('newImageIds', [])
        
        # If document was split, mark original as obsolete
        if split_result.get('newImageIds'):
            db_manager.update_image_status(image_id, 'Obsolete')
            logger.info(f"Document split into {len(result['splitImages'])} new documents")
            result['processingTime'] = time.time() - start_time
            return result
    
    # Save processed PDF back to S3
    update_progress(session_id, image_id, 'processing', 95, 'Saving processed document...')
    s3_manager.upload_file(modified_pdf, processing_path, image_record['BucketPrefix'])
    
    result['processingTime'] = time.time() - start_time
    logger.info(f"Processing completed in {result['processingTime']:.2f} seconds")
    
    return result

def update_progress(session_id: Optional[str], image_id: int, status: str, progress: int, message: str, data: Optional[Dict] = None):
    """Update processing progress via API callback"""
    if not session_id:
        return
        
    import requests
    import os
    
    try:
        callback_url = os.environ.get('PROGRESS_CALLBACK_URL')
        if not callback_url:
            return
            
        payload = {
            'sessionId': session_id,
            'imageId': image_id,
            'status': status,
            'progress': progress,
            'message': message
        }
        
        if data:
            payload['data'] = data
            
        requests.post(f"{callback_url}/{session_id}", json=payload, timeout=5)
    except Exception as e:
        logger.warning(f"Failed to update progress: {e}")

def handle_processing_error(image_id: int, error_msg: str, session_id: Optional[str] = None):
    """Handle processing errors with database rollback"""
    try:
        db_manager = DatabaseManager()
        
        # Reset image status
        db_manager.update_image_status(image_id, 'NeedsImageManipulation')
        
        # Update progress with error
        update_progress(session_id, image_id, 'error', 0, error_msg)
        
        logger.info(f"Error recovery completed for image {image_id}")
        
    except Exception as e:
        logger.error(f"Error in error handler: {e}")
```

### **Redaction Processor**

```python
# processors/redaction_processor.py
import fitz  # PyMuPDF
import logging
from typing import List, Tuple, Dict, Any
from io import BytesIO
from utils.db_manager import DatabaseManager

logger = logging.getLogger(__name__)

class RedactionProcessor:
    """Handle PDF redaction operations using PyMuPDF"""
    
    def __init__(self, db_manager: DatabaseManager):
        self.db_manager = db_manager
    
    def process(self, image_id: int, pdf_bytes: bytes, redactions: List[Dict[str, Any]]) -> Tuple[bytes, Dict[str, Any]]:
        """
        Apply redactions to PDF document
        
        Args:
            image_id: Database image ID
            pdf_bytes: Original PDF content
            redactions: List of redaction areas from ImageRedaction table
            
        Returns:
            Tuple of (modified_pdf_bytes, processing_result)
        """
        
        if not redactions:
            return pdf_bytes, {'message': 'No redactions to apply'}
        
        logger.info(f"Processing {len(redactions)} redactions for image {image_id}")
        
        # Load PDF document
        doc = fitz.open(stream=pdf_bytes, filetype="pdf")
        
        result = {
            'totalRedactions': len(redactions),
            'pagesModified': set(),
            'rasterizedPages': []
        }
        
        try:
            # Group redactions by page for efficient processing
            redactions_by_page = {}
            for redaction in redactions:
                page_num = redaction['PageNumber']  # 0-based from DB
                if page_num not in redactions_by_page:
                    redactions_by_page[page_num] = []
                redactions_by_page[page_num].append(redaction)
            
            logger.info(f"Redactions grouped across {len(redactions_by_page)} pages")
            
            # Apply redactions page by page
            for page_num, page_redactions in redactions_by_page.items():
                if page_num >= len(doc):
                    continue  # Skip invalid page numbers
                    
                page = doc[page_num]
                logger.info(f"Processing {len(page_redactions)} redactions on page {page_num}")
                
                # Apply each redaction on this page
                for redaction in page_redactions:
                    rect = fitz.Rect(
                        redaction['PageX'],
                        redaction['PageY'], 
                        redaction['PageX'] + redaction['PageWidth'],
                        redaction['PageY'] + redaction['PageHeight']
                    )
                    
                    # Handle rotation if specified
                    if redaction.get('DrawOrientation', 0) != 0:
                        rect = self._apply_rotation(rect, redaction['DrawOrientation'], page.rect)
                    
                    # Create redaction annotation
                    redact_annot = page.add_redact_annot(rect)
                    
                    # Set redaction properties
                    redact_annot.set_info(content=redaction.get('Text', ''))
                    redact_annot.set_fill(color=(0, 0, 0))  # Black fill
                    redact_annot.update()
                
                # Apply all redactions on this page
                page.apply_redactions()
                result['pagesModified'].add(page_num)
                
                # Rasterize page to prevent text extraction
                # This converts the page to an image to ensure redacted content cannot be recovered
                pix = page.get_pixmap(matrix=fitz.Matrix(2.0, 2.0))  # 2x resolution
                img_data = pix.tobytes("png")
                
                # Replace page with rasterized version
                img_rect = page.rect
                page.clean_contents()
                page.insert_image(img_rect, stream=img_data)
                result['rasterizedPages'].append(page_num)
            
            # Mark redactions as applied in database
            for redaction in redactions:
                self.db_manager.mark_redaction_applied(redaction['ID'])
            
            # Save modified document
            output_buffer = BytesIO()
            doc.save(output_buffer)
            doc.close()
            
            return output_buffer.getvalue(), result
            
        except Exception as e:
            doc.close()
            raise Exception(f"Redaction processing failed: {str(e)}")
    
    def _apply_rotation(self, rect: fitz.Rect, orientation: int, page_rect: fitz.Rect) -> fitz.Rect:
        """Apply rotation transformation to redaction rectangle"""
        if orientation == 0:
            return rect
            
        # Convert orientation to radians
        import math
        
        # Get rectangle center
        center_x = page_rect.width / 2
        center_y = page_rect.height / 2
        
        # Apply rotation matrix (simplified for common angles)
        if orientation == 90:
            return fitz.Rect(
                center_x - (rect.y1 - center_y),
                center_y + (rect.x0 - center_x),
                center_x - (rect.y0 - center_y), 
                center_y + (rect.x1 - center_x)
            )
        elif orientation == 180:
            return fitz.Rect(
                center_x - (rect.x1 - center_x),
                center_y - (rect.y1 - center_y),
                center_x - (rect.x0 - center_x),
                center_y - (rect.y0 - center_y)
            )
        elif orientation == 270:
            return fitz.Rect(
                center_x + (rect.y0 - center_y),
                center_y - (rect.x1 - center_x),
                center_x + (rect.y1 - center_y),
                center_y - (rect.x0 - center_x)
            )
        
        return rect
```

### **Rotation Processor**

```python
# processors/rotation_processor.py
import fitz
import logging
from typing import List, Dict, Any, Tuple
from io import BytesIO
from utils.db_manager import DatabaseManager

logger = logging.getLogger(__name__)

class RotationProcessor:
    """Handle PDF page rotation operations"""
    
    def __init__(self, db_manager: DatabaseManager):
        self.db_manager = db_manager
    
    def process(self, image_id: int, pdf_bytes: bytes, rotations: List[Dict[str, Any]]) -> Tuple[bytes, Dict[str, Any]]:
        """
        Apply page rotations to PDF document
        
        Args:
            image_id: Database image ID  
            pdf_bytes: PDF content to modify
            rotations: List of rotation specifications from ImageRotation table
            
        Returns:
            Tuple of (modified_pdf_bytes, processing_result)
        """
        
        if not rotations:
            return pdf_bytes, {'message': 'No rotations to apply'}
        
        logger.info(f"Processing {len(rotations)} rotations for image {image_id}")
        
        doc = fitz.open(stream=pdf_bytes, filetype="pdf")
        
        result = {
            'totalRotations': len(rotations),
            'pagesRotated': []
        }
        
        try:
            # Apply rotations
            for rotation in rotations:
                page_index = rotation['PageIndex']  # 0-based from DB
                rotate_angle = rotation['Rotate']   # 0, 90, 180, 270 from DB
                
                if page_index >= len(doc):
                    continue  # Skip invalid page indices
                    
                if rotate_angle not in [0, 90, 180, 270]:
                    continue  # Skip invalid rotation angles
                
                page = doc[page_index]
                
                # Apply rotation (PyMuPDF uses 90-degree increments)
                if rotate_angle != 0:
                    page.set_rotation(rotate_angle)
                    result['pagesRotated'].append({
                        'pageIndex': page_index,
                        'rotation': rotate_angle
                    })
                    logger.info(f"Rotated page {page_index} by {rotate_angle} degrees")
            
            # Save modified document
            output_buffer = BytesIO()
            doc.save(output_buffer)
            doc.close()
            
            return output_buffer.getvalue(), result
            
        except Exception as e:
            doc.close()
            raise Exception(f"Rotation processing failed: {str(e)}")
```

### **Page Deletion Processor**

```python
# processors/deletion_processor.py
import fitz
import logging
from typing import List, Dict, Any, Tuple
from io import BytesIO
from utils.db_manager import DatabaseManager

logger = logging.getLogger(__name__)

class DeletionProcessor:
    """Handle PDF page deletion operations"""
    
    def __init__(self, db_manager: DatabaseManager):
        self.db_manager = db_manager
    
    def process(self, image_id: int, pdf_bytes: bytes, deletions: List[Dict[str, Any]]) -> Tuple[bytes, Dict[str, Any]]:
        """
        Remove specified pages from PDF document
        
        Args:
            image_id: Database image ID
            pdf_bytes: PDF content to modify  
            deletions: List of page deletion records from ImagePageDeletion table
            
        Returns:
            Tuple of (modified_pdf_bytes, processing_result)
        """
        
        if not deletions:
            return pdf_bytes, {'message': 'No pages to delete'}
        
        logger.info(f"Processing {len(deletions)} page deletions for image {image_id}")
        
        doc = fitz.open(stream=pdf_bytes, filetype="pdf")
        original_page_count = len(doc)
        
        result = {
            'originalPageCount': original_page_count,
            'deletedPages': [],
            'finalPageCount': 0
        }
        
        try:
            # Extract page indices to delete (0-based from DB)
            pages_to_delete = [deletion['PageIndex'] for deletion in deletions]
            pages_to_delete = [p for p in pages_to_delete if 0 <= p < original_page_count]
            
            # Check if all pages are being deleted
            if len(pages_to_delete) >= original_page_count:
                # Mark entire document as deleted in database
                self.db_manager.mark_image_deleted(image_id)
                doc.close()
                logger.info(f"All pages deleted - marking image {image_id} as deleted")
                return pdf_bytes, {
                    **result,
                    'finalPageCount': 0,
                    'documentDeleted': True
                }
            
            # Sort in reverse order to maintain indices while deleting
            pages_to_delete.sort(reverse=True)
            
            # Delete pages
            for page_index in pages_to_delete:
                doc.delete_page(page_index)
                result['deletedPages'].append(page_index)
                logger.info(f"Deleted page {page_index}")
            
            result['finalPageCount'] = len(doc)
            
            # Update page count in database
            self.db_manager.update_page_count(image_id, result['finalPageCount'])
            
            # Save modified document
            output_buffer = BytesIO()
            doc.save(output_buffer)
            doc.close()
            
            return output_buffer.getvalue(), result
            
        except Exception as e:
            doc.close()
            raise Exception(f"Page deletion processing failed: {str(e)}")
```

### **Document Splitting Processor**

```python
# processors/splitting_processor.py
import fitz
import logging
from typing import List, Dict, Any, Tuple
from io import BytesIO
from utils.db_manager import DatabaseManager
from utils.s3_manager import S3Manager
import uuid

logger = logging.getLogger(__name__)

class SplittingProcessor:
    """Handle PDF document splitting based on page breaks"""
    
    def __init__(self, db_manager: DatabaseManager, s3_manager: S3Manager):
        self.db_manager = db_manager
        self.s3_manager = s3_manager
    
    def process(self, image_record: Dict[str, Any], pdf_bytes: bytes, page_breaks: List[Dict[str, Any]]) -> Dict[str, Any]:
        """
        Split PDF document based on page break locations
        
        Args:
            image_record: Original image database record
            pdf_bytes: PDF content to split
            page_breaks: List of page break records from ImageBookmark table
            
        Returns:
            Processing result with new image IDs
        """
        
        if not page_breaks:
            return {'message': 'No page breaks to process'}
        
        logger.info(f"Processing {len(page_breaks)} page breaks for image {image_record['ID']}")
        
        doc = fitz.open(stream=pdf_bytes, filetype="pdf")
        original_page_count = len(doc)
        
        result = {
            'newImageIds': [],
            'splitDocuments': [],
            'originalImageId': image_record['ID']
        }
        
        try:
            # Sort page breaks by page index
            sorted_breaks = sorted(page_breaks, key=lambda x: x['PageIndex'])
            
            # Determine split ranges
            split_ranges = self._calculate_split_ranges(sorted_breaks, original_page_count)
            
            # Handle case where first break is not at page 0
            has_root_break = any(br['PageIndex'] == 0 for br in page_breaks)
            
            if len(split_ranges) == 1 and has_root_break:
                # Simple case: single break at beginning (just rename/reindex)
                page_break = sorted_breaks[0]
                self.db_manager.update_image_document_type(
                    image_record['ID'], 
                    page_break['ImageDocumentTypeID'],
                    page_break.get('DocumentDate'),
                    page_break.get('Comments')
                )
                
                # Mark bookmark as processed
                self.db_manager.mark_bookmark_processed(page_break['ID'], image_record['ID'])
                
                result['operationType'] = 'rename_only'
                logger.info("Single break at page 0 - renamed document only")
                return result
            
            # Create split documents
            for i, (start_page, end_page) in enumerate(split_ranges):
                # Determine document type for this split
                page_break = self._find_break_for_range(page_breaks, start_page)
                doc_type_id = page_break['ImageDocumentTypeID'] if page_break else image_record['DocTypeManualID']
                
                # Create new PDF with page range
                split_doc = fitz.open()
                split_doc.insert_pdf(doc, from_page=start_page, to_page=end_page - 1)
                
                # Save split document
                split_bytes = BytesIO()
                split_doc.save(split_bytes)
                split_doc.close()
                
                # Create new image record in database
                new_image_id = self.db_manager.create_split_image(
                    original_image=image_record,
                    doc_type_id=doc_type_id,
                    page_count=end_page - start_page,
                    page_range=(start_page, end_page)
                )
                
                # Save files to S3
                self._save_split_to_s3(new_image_id, split_bytes.getvalue(), image_record)
                
                # Update bookmark with result image ID
                if page_break:
                    self.db_manager.mark_bookmark_processed(page_break['ID'], new_image_id)
                
                result['newImageIds'].append(new_image_id)
                result['splitDocuments'].append({
                    'imageId': new_image_id,
                    'pageRange': [start_page, end_page],
                    'pageCount': end_page - start_page,
                    'documentType': doc_type_id
                })
                
                logger.info(f"Created split document {new_image_id} with pages {start_page}-{end_page-1}")
            
            # Create audit trail
            for new_image_id in result['newImageIds']:
                self.db_manager.create_split_log(image_record['ID'], new_image_id)
            
            doc.close()
            logger.info(f"Document splitting completed - created {len(result['newImageIds'])} new documents")
            
            return result
            
        except Exception as e:
            doc.close()
            raise Exception(f"Document splitting failed: {str(e)}")
    
    def _calculate_split_ranges(self, page_breaks: List[Dict[str, Any]], total_pages: int) -> List[Tuple[int, int]]:
        """Calculate page ranges for document splits"""
        ranges = []
        
        for i, page_break in enumerate(page_breaks):
            start_page = page_break['PageIndex']
            end_page = page_breaks[i + 1]['PageIndex'] if i + 1 < len(page_breaks) else total_pages
            ranges.append((start_page, end_page))
        
        return ranges
    
    def _find_break_for_range(self, page_breaks: List[Dict[str, Any]], start_page: int) -> Dict[str, Any] or None:
        """Find the page break that corresponds to a given start page"""
        for page_break in page_breaks:
            if page_break['PageIndex'] == start_page:
                return page_break
        return None
    
    def _save_split_to_s3(self, image_id: int, pdf_bytes: bytes, original_image: Dict[str, Any]):
        """Save split document to S3 in all required paths"""
        
        # Generate paths
        base_path = f"{original_image['Path']}/{image_id}/{image_id}.pdf"
        
        paths = [
            f"Original/{base_path}",
            f"Processing/{base_path}",
            f"Production/{base_path}"
        ]
        
        # Upload to all paths
        for path in paths:
            self.s3_manager.upload_file(pdf_bytes, path, original_image['BucketPrefix'])
            
        logger.info(f"Split document {image_id} saved to S3 in {len(paths)} paths")
```

## Database Integration

### **Database Schema Compatibility**

The system maintains 100% compatibility with existing LoanMaster database tables:

```sql
-- Existing tables used as-is
ImageRedaction (
    ID int PRIMARY KEY IDENTITY,
    ImageID int,
    PageNumber int,           -- 0-based page index
    PageX float,              -- X coordinate
    PageY float,              -- Y coordinate  
    PageWidth float,          -- Width of redaction box
    PageHeight float,         -- Height of redaction box
    Guid nvarchar(50),        -- Unique identifier
    Text nvarchar(max),       -- Optional text
    CreatedBy int,
    DateCreated datetime,
    Deleted bit DEFAULT 0,
    Applied bit,              -- Whether redaction has been processed
    DrawOrientation int       -- Orientation when drawn
)

ImageRotation (
    ID int PRIMARY KEY IDENTITY,
    ImageID int,
    PageIndex int,            -- 0-based page index
    Rotate int               -- 0, 90, 180, 270 degrees
)

ImagePageDeletion (
    ID int PRIMARY KEY IDENTITY,
    ImageID int,
    PageIndex int,            -- 0-based page index
    CreatedBy int,
    DateCreated datetime
)

ImageBookmark (
    ID int PRIMARY KEY IDENTITY,
    ImageID int,
    PageIndex int,            -- 0-based page index
    Text nvarchar(max),       -- Document type and metadata
    ImageDocumentTypeID int,
    ResultImageID int,        -- Populated after split
    Deleted bit DEFAULT 0
)
```

### **Enhanced C# API Layer**

```csharp
// DocumentController.cs - Enhanced with Lambda integration
[ApiController]
[Route("api/documents")]
public class DocumentController : ControllerBase
{
    private readonly IDocumentRepository _documentRepository;
    private readonly IAmazonLambda _lambdaClient;
    private readonly IMemoryCache _cache;
    private readonly ILogger<DocumentController> _logger;

    [HttpPost("{documentId}/process-manipulations")]
    public async Task<IActionResult> ProcessManipulations(int documentId)
    {
        var lockKey = Guid.NewGuid().ToString();
        
        try
        {
            // Attempt to lock document
            if (!await TryLockDocument(documentId, lockKey, TimeSpan.FromMinutes(15)))
            {
                return Conflict(new { error = "Document is currently being processed" });
            }

            // Create processing session
            var sessionId = Guid.NewGuid().ToString();
            await _cache.SetAsync($"processing:{sessionId}", new ProcessingStatus
            {
                DocumentId = documentId,
                Status = "starting",
                Progress = 0,
                StartTime = DateTime.UtcNow
            }, TimeSpan.FromMinutes(20));

            // Update database status
            await _documentRepository.UpdateImageStatus(documentId, ImageStatusTypeEnum.InWorkman);

            // Invoke Lambda function
            var lambdaResponse = await InvokeLambdaWithRetry(new
            {
                operation = "process_manipulations",
                imageId = documentId,
                sessionId = sessionId,
                progressCallbackUrl = $"{Request.Scheme}://{Request.Host}/api/processing/progress"
            });

            return Ok(new
            {
                sessionId,
                status = "processing",
                documentId
            });
        }
        catch (Exception ex)
        {
            await ReleaseLock(documentId, lockKey);
            await _documentRepository.UpdateImageStatus(documentId, ImageStatusTypeEnum.NeedsImageManipulation);
            _logger.LogError(ex, "Failed to process manipulations for document {DocumentId}", documentId);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpGet("processing/{sessionId}/status")]
    public async Task<IActionResult> GetProcessingStatus(string sessionId)
    {
        var status = await _cache.GetAsync<ProcessingStatus>($"processing:{sessionId}");
        if (status == null)
        {
            return NotFound(new { error = "Processing session not found" });
        }

        return Ok(status);
    }

    [HttpPost("processing/progress/{sessionId}")]
    public async Task<IActionResult> UpdateProcessingProgress(string sessionId, [FromBody] ProcessingProgressUpdate update)
    {
        try
        {
            var status = await _cache.GetAsync<ProcessingStatus>($"processing:{sessionId}");
            if (status != null)
            {
                status.Status = update.Status;
                status.Progress = update.Progress;
                status.CurrentOperation = update.Message;
                status.LastUpdate = DateTime.UtcNow;
                
                if (update.Data != null)
                {
                    status.Result = update.Data;
                }

                await _cache.SetAsync($"processing:{sessionId}", status, TimeSpan.FromMinutes(20));
            }

            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update processing progress for session {SessionId}", sessionId);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpGet("{documentId}/manipulations")]
    public async Task<IActionResult> GetDocumentManipulations(int documentId)
    {
        try
        {
            var manipulations = new
            {
                documentId,
                redactions = await _documentRepository.GetRedactions(documentId),
                rotations = await _documentRepository.GetRotations(documentId),
                pageBreaks = await _documentRepository.GetPageBreaks(documentId),
                pageDeletions = await _documentRepository.GetPageDeletions(documentId)
            };

            return Ok(manipulations);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get manipulations for document {DocumentId}", documentId);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpPost("{documentId}/redactions")]
    public async Task<IActionResult> SaveRedactions(int documentId, [FromBody] List<RedactionAnnotation> redactions)
    {
        try
        {
            foreach (var redaction in redactions)
            {
                await _documentRepository.SaveRedaction(documentId, redaction);
            }

            await _documentRepository.MarkImageChanged(documentId);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save redactions for document {DocumentId}", documentId);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    // Similar endpoints for rotations, page deletions, page breaks...

    private async Task<bool> TryLockDocument(int documentId, string lockKey, TimeSpan lockDuration)
    {
        try
        {
            var lockRecord = new DocumentLock
            {
                DocumentId = documentId,
                LockKey = lockKey,
                LockedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.Add(lockDuration),
                LockedBy = GetCurrentUserId()
            };

            await _context.DocumentLocks.AddAsync(lockRecord);
            await _context.SaveChangesAsync();
            return true;
        }
        catch (DbUpdateException)
        {
            return false; // Lock already exists
        }
    }

    private async Task<ProcessingResult> InvokeLambdaWithRetry(object payload, int maxRetries = 3)
    {
        var retryPolicy = Policy
            .Handle<Exception>()
            .WaitAndRetryAsync(
                maxRetries,
                retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                onRetry: (outcome, timespan, retryCount, context) =>
                {
                    _logger.LogWarning("Lambda invocation failed, retry {RetryCount} in {Timespan}", retryCount, timespan);
                });

        return await retryPolicy.ExecuteAsync(async () =>
        {
            var response = await _lambdaClient.InvokeAsync(new InvokeRequest
            {
                FunctionName = "esizzle-pdf-processor",
                InvocationType = InvocationType.Event, // Async invocation
                Payload = JsonSerializer.Serialize(payload)
            });

            if (response.StatusCode != 202) // Accepted for async
            {
                throw new Exception($"Lambda invocation failed with status {response.StatusCode}");
            }

            return new ProcessingResult { Success = true };
        });
    }
}
```

## Deployment & Infrastructure

### **AWS Lambda Deployment**

```yaml
# cloudformation/lambda-deployment.yaml
Resources:
  ESizzlePDFProcessorLambda:
    Type: AWS::Lambda::Function
    Properties:
      FunctionName: esizzle-pdf-processor
      Runtime: python3.9
      Handler: main.lambda_handler
      Code:
        S3Bucket: !Ref DeploymentBucket
        S3Key: lambda-deployment/pdf-processor.zip
      Timeout: 900  # 15 minutes
      MemorySize: 3008  # Maximum for PDF processing
      Environment:
        Variables:
          DB_HOST: !Ref DatabaseHost
          DB_NAME: !Ref DatabaseName
          DB_USER: !Ref DatabaseUser
          DB_PASSWORD: !Ref DatabasePassword
          S3_BUCKET: !Ref DocumentsBucket
          PROGRESS_CALLBACK_URL: !Ref APIGatewayURL
      Layers:
        - !Ref PyMuPDFLayer
        - !Ref CommonUtilsLayer
      VpcConfig:
        SecurityGroupIds:
          - !Ref LambdaSecurityGroup
        SubnetIds:
          - !Ref PrivateSubnet1
          - !Ref PrivateSubnet2

  PyMuPDFLayer:
    Type: AWS::Lambda::LayerVersion
    Properties:
      LayerName: pymupdf-layer
      Content:
        S3Bucket: !Ref DeploymentBucket
        S3Key: layers/pymupdf-layer.zip
      CompatibleRuntimes:
        - python3.9
```

### **Lambda Dependencies**

```
# requirements.txt for Lambda layer
PyMuPDF==1.23.8
boto3==1.34.0
pymysql==1.1.0
requests==2.31.0
pillow==10.0.1
```

### **Deployment Scripts**

```bash
#!/bin/bash
# deploy.sh - Lambda deployment script

set -e

echo "Building Lambda deployment package..."

# Create deployment directory
mkdir -p deployment/lambda
mkdir -p deployment/layers

# Install dependencies for layer
pip install -r requirements.txt -t deployment/layers/python/

# Create layer zip
cd deployment/layers
zip -r ../pymupdf-layer.zip python/
cd ../..

# Create Lambda package
cp -r lambda_pdf_processor/* deployment/lambda/
cd deployment/lambda
zip -r ../lambda-deployment.zip .
cd ../..

# Upload to S3
aws s3 cp deployment/pymupdf-layer.zip s3://esizzle-deployment-bucket/layers/
aws s3 cp deployment/lambda-deployment.zip s3://esizzle-deployment-bucket/lambda-deployment/

# Deploy CloudFormation stack
aws cloudformation deploy \
  --template-file cloudformation/lambda-deployment.yaml \
  --stack-name esizzle-pdf-processor \
  --capabilities CAPABILITY_IAM \
  --parameter-overrides \
    DeploymentBucket=esizzle-deployment-bucket \
    DocumentsBucket=esizzle-documents \
    DatabaseHost=$DB_HOST \
    DatabaseName=$DB_NAME \
    DatabaseUser=$DB_USER \
    DatabasePassword=$DB_PASSWORD \
    APIGatewayURL=$API_GATEWAY_URL

echo "Deployment completed successfully!"
```

## Testing Strategy

### **Unit Testing**

```python
# tests/test_redaction_processor.py
import unittest
from unittest.mock import Mock, patch
import fitz
from processors.redaction_processor import RedactionProcessor

class TestRedactionProcessor(unittest.TestCase):
    
    def setUp(self):
        self.db_manager = Mock()
        self.processor = RedactionProcessor(self.db_manager)
    
    def test_process_redactions_success(self):
        """Test successful redaction processing"""
        # Create test PDF
        test_pdf = fitz.open()
        test_pdf.new_page()
        pdf_bytes = test_pdf.tobytes()
        test_pdf.close()
        
        # Test redaction data
        redactions = [
            {
                'ID': 1,
                'PageNumber': 0,
                'PageX': 100,
                'PageY': 100,
                'PageWidth': 200,
                'PageHeight': 50,
                'DrawOrientation': 0
            }
        ]
        
        # Process redactions
        result_pdf, result_data = self.processor.process(1, pdf_bytes, redactions)
        
        # Verify results
        self.assertIsNotNone(result_pdf)
        self.assertEqual(result_data['totalRedactions'], 1)
        self.db_manager.mark_redaction_applied.assert_called_once_with(1)
    
    def test_process_no_redactions(self):
        """Test processing when no redactions exist"""
        test_pdf = fitz.open()
        test_pdf.new_page()
        pdf_bytes = test_pdf.tobytes()
        test_pdf.close()
        
        result_pdf, result_data = self.processor.process(1, pdf_bytes, [])
        
        self.assertEqual(result_pdf, pdf_bytes)
        self.assertIn('No redactions to apply', result_data['message'])
```

### **Integration Testing**

```python
# tests/integration/test_lambda_integration.py
import json
import boto3
from moto import mock_lambda, mock_s3

@mock_lambda
@mock_s3
class TestLambdaIntegration(unittest.TestCase):
    
    def setUp(self):
        self.lambda_client = boto3.client('lambda', region_name='us-east-1')
        self.s3_client = boto3.client('s3', region_name='us-east-1')
        
        # Create test bucket
        self.s3_client.create_bucket(Bucket='test-bucket')
        
    def test_full_processing_workflow(self):
        """Test complete processing workflow"""
        
        # Upload test PDF to S3
        test_pdf_content = self._create_test_pdf()
        self.s3_client.put_object(
            Bucket='test-bucket',
            Key='Processing/test/123/123.pdf',
            Body=test_pdf_content
        )
        
        # Create Lambda event
        event = {
            'operation': 'process_manipulations',
            'imageId': 123,
            'sessionId': 'test-session'
        }
        
        # Invoke Lambda function (mocked)
        response = self.lambda_client.invoke(
            FunctionName='esizzle-pdf-processor',
            Payload=json.dumps(event)
        )
        
        # Verify response
        self.assertEqual(response['StatusCode'], 200)
        
        # Verify processed file was uploaded back
        processed_objects = self.s3_client.list_objects_v2(
            Bucket='test-bucket',
            Prefix='Processing/test/123/'
        )
        self.assertTrue(processed_objects.get('Contents'))
    
    def _create_test_pdf(self):
        """Create a simple test PDF"""
        import fitz
        doc = fitz.open()
        page = doc.new_page()
        page.insert_text((72, 72), "Test content for manipulation")
        pdf_bytes = doc.tobytes()
        doc.close()
        return pdf_bytes
```

### **Frontend Testing**

```typescript
// tests/unit/AnnotationOverlay.test.ts
import { describe, it, expect, vi } from 'vitest'
import { mount } from '@vue/test-utils'
import AnnotationOverlay from '@/components/viewer/AnnotationOverlay.vue'
import type { DocumentManipulationState } from '@/types/manipulation'

describe('AnnotationOverlay', () => {
  const mockManipulationState: DocumentManipulationState = {
    documentId: 123,
    pageCount: 3,
    redactions: [
      {
        id: 1,
        imageId: 123,
        pageNumber: 0,
        pageX: 100,
        pageY: 100,
        pageWidth: 200,
        pageHeight: 50,
        guid: 'test-guid-1',
        applied: false,
        drawOrientation: 0,
        createdBy: 1,
        dateCreated: new Date(),
        deleted: false
      }
    ],
    rotations: [],
    pageBreaks: [],
    pageDeletions: [],
    hasUnsavedChanges: true,
    processingStatus: 'idle',
    lastModified: new Date(),
    modifiedBy: 1
  }

  it('renders redaction overlays correctly', () => {
    const wrapper = mount(AnnotationOverlay, {
      props: {
        manipulationState: mockManipulationState,
        pageCount: 3,
        editMode: 'redaction',
        zoomLevel: 100
      }
    })

    // Should render redaction visualization component
    expect(wrapper.findComponent({ name: 'RedactionVisualization' })).toBeTruthy()
    
    // Should show unsaved changes indicator
    expect(wrapper.find('[data-testid="unsaved-changes"]')).toBeTruthy()
  })

  it('switches edit modes correctly', async () => {
    const wrapper = mount(AnnotationOverlay, {
      props: {
        manipulationState: mockManipulationState,
        pageCount: 3,
        editMode: 'view',
        zoomLevel: 100
      }
    })

    // Switch to redaction mode
    await wrapper.setProps({ editMode: 'redaction' })
    
    // Should emit mode change event
    expect(wrapper.emitted('mode-changed')).toBeTruthy()
  })

  it('handles coordinate translation correctly', () => {
    const wrapper = mount(AnnotationOverlay, {
      props: {
        manipulationState: mockManipulationState,
        pageCount: 3,
        editMode: 'redaction',
        zoomLevel: 150
      }
    })

    // Test coordinate translation at 150% zoom
    const redactionCoords = wrapper.vm.translatePageToCanvas({ x: 100, y: 100 })
    expect(redactionCoords.x).toBeGreaterThan(100) // Should be scaled up
    expect(redactionCoords.y).toBeGreaterThan(100)
  })
})
```

```typescript
// tests/unit/CoordinateTranslator.test.ts
import { describe, it, expect } from 'vitest'
import { CoordinateTranslator } from '@/utils/coordinate-translator'

describe('CoordinateTranslator', () => {
  const mockViewport = {
    width: 800,
    height: 1000
  }
  
  const canvasDimensions = {
    width: 800,
    height: 1000
  }

  it('converts page coordinates to canvas coordinates correctly', () => {
    const translator = new CoordinateTranslator(mockViewport, canvasDimensions, 100)
    
    const pageCoord = { x: 100, y: 100 }
    const canvasCoord = translator.pageToCanvas(pageCoord)
    
    expect(canvasCoord.x).toBe(100)
    expect(canvasCoord.y).toBe(100)
  })

  it('handles zoom level scaling', () => {
    const translator = new CoordinateTranslator(mockViewport, canvasDimensions, 200)
    
    const pageCoord = { x: 100, y: 100 }
    const canvasCoord = translator.pageToCanvas(pageCoord)
    
    expect(canvasCoord.x).toBe(200) // 100 * 2.0 zoom
    expect(canvasCoord.y).toBe(200)
  })

  it('applies rotation transformations', () => {
    const translator = new CoordinateTranslator(mockViewport, canvasDimensions, 100)
    
    const coord = { x: 100, y: 100 }
    const rotated = translator.applyRotation(coord, 90)
    
    // Should rotate around center point
    expect(rotated.x).not.toBe(coord.x)
    expect(rotated.y).not.toBe(coord.y)
  })
})
```

### **End-to-End Testing**

```typescript
// tests/e2e/pdf-manipulation.e2e.ts
import { test, expect } from '@playwright/test'

test.describe('PDF Manipulation Workflow', () => {
  test.beforeEach(async ({ page }) => {
    // Login and navigate to document
    await page.goto('/login')
    await page.fill('[data-testid="username"]', 'testuser')
    await page.fill('[data-testid="password"]', 'testpass')
    await page.click('[data-testid="login-button"]')
    
    // Navigate to a test document
    await page.goto('/documents/123')
    await page.waitForSelector('[data-testid="pdf-viewer"]')
  })

  test('should create and apply redaction', async ({ page }) => {
    // Switch to redaction mode
    await page.click('[data-testid="redaction-mode-button"]')
    
    // Draw redaction box
    const viewer = page.locator('[data-testid="pdf-viewer"]')
    await viewer.click({ position: { x: 200, y: 200 } })
    await page.mouse.down()
    await page.mouse.move(400, 300)
    await page.mouse.up()
    
    // Verify redaction overlay appears
    await expect(page.locator('.redaction-pending')).toBeVisible()
    
    // Apply redaction
    await page.click('[data-testid="apply-redaction"]')
    
    // Verify processing starts
    await expect(page.locator('[data-testid="processing-indicator"]')).toBeVisible()
    
    // Wait for processing to complete
    await page.waitForSelector('[data-testid="processing-completed"]', { timeout: 30000 })
    
    // Verify redaction is now applied (black overlay)
    await expect(page.locator('.redaction-applied')).toBeVisible()
  })

  test('should rotate page and save', async ({ page }) => {
    // Switch to rotation mode
    await page.click('[data-testid="rotation-mode-button"]')
    
    // Click rotate 90 degrees button
    await page.click('[data-testid="rotate-90-button"]')
    
    // Verify rotation indicator appears
    await expect(page.locator('.rotation-indicator')).toHaveText('90°')
    
    // Save changes
    await page.click('[data-testid="save-changes-button"]')
    
    // Wait for processing
    await page.waitForSelector('[data-testid="processing-completed"]', { timeout: 30000 })
    
    // Verify page is visually rotated
    const canvas = page.locator('[data-testid="pdf-canvas"]')
    await expect(canvas).toHaveCSS('transform', /rotate\(90deg\)/)
  })

  test('should create page break and split document', async ({ page }) => {
    // Switch to page break mode
    await page.click('[data-testid="pagebreak-mode-button"]')
    
    // Click at top of page 2 to create break
    await page.click('[data-testid="page-2"]', { position: { x: 400, y: 0 } })
    
    // Select document type for split
    await page.selectOption('[data-testid="document-type-select"]', 'appraisal')
    
    // Verify page break bar appears
    await expect(page.locator('.page-break-normal')).toBeVisible()
    
    // Save and split
    await page.click('[data-testid="save-changes-button"]')
    
    // Wait for splitting to complete
    await page.waitForSelector('[data-testid="split-completed"]', { timeout: 45000 })
    
    // Verify notification of split documents created
    await expect(page.locator('[data-testid="split-result"]')).toContainText('2 documents created')
  })

  test('should delete pages', async ({ page }) => {
    // Switch to deletion mode
    await page.click('[data-testid="deletion-mode-button"]')
    
    // Click on page to mark for deletion
    await page.click('[data-testid="page-2"]')
    
    // Verify deletion overlay (red X)
    await expect(page.locator('.page-deletion')).toBeVisible()
    
    // Save changes
    await page.click('[data-testid="save-changes-button"]')
    
    // Wait for processing
    await page.waitForSelector('[data-testid="processing-completed"]', { timeout: 30000 })
    
    // Verify page count decreased
    const pageCount = await page.locator('[data-testid="page-count"]').textContent()
    expect(parseInt(pageCount!)).toBeLessThan(3) // Original had 3 pages
  })

  test('should handle processing errors gracefully', async ({ page }) => {
    // Mock network failure during processing
    await page.route('**/api/documents/*/process-manipulations', route => {
      route.abort()
    })
    
    // Try to create redaction
    await page.click('[data-testid="redaction-mode-button"]')
    const viewer = page.locator('[data-testid="pdf-viewer"]')
    await viewer.click({ position: { x: 200, y: 200 } })
    await page.mouse.down()
    await page.mouse.move(400, 300)
    await page.mouse.up()
    
    // Try to save
    await page.click('[data-testid="save-changes-button"]')
    
    // Verify error message appears
    await expect(page.locator('[data-testid="error-message"]')).toBeVisible()
    await expect(page.locator('[data-testid="error-message"]')).toContainText('Processing failed')
    
    // Verify changes are preserved for retry
    await expect(page.locator('.redaction-pending')).toBeVisible()
  })
})
```

### **Performance Testing**

```python
# tests/performance/lambda_performance_test.py
import time
import concurrent.futures
from lambda_pdf_processor.main import lambda_handler

def test_concurrent_processing():
    """Test Lambda performance under concurrent load"""
    
    def process_document(image_id):
        event = {
            'operation': 'process_manipulations',
            'imageId': image_id,
            'sessionId': f'test-session-{image_id}'
        }
        
        start_time = time.time()
        result = lambda_handler(event, None)
        processing_time = time.time() - start_time
        
        return {
            'imageId': image_id,
            'success': result['statusCode'] == 200,
            'processingTime': processing_time
        }
    
    # Test with 10 concurrent documents
    with concurrent.futures.ThreadPoolExecutor(max_workers=10) as executor:
        futures = [executor.submit(process_document, i) for i in range(1, 11)]
        results = [future.result() for future in futures]
    
    # Analyze results
    success_rate = sum(1 for r in results if r['success']) / len(results)
    avg_processing_time = sum(r['processingTime'] for r in results) / len(results)
    
    assert success_rate >= 0.9  # 90% success rate minimum
    assert avg_processing_time < 60  # Under 1 minute average
    
    print(f"Success rate: {success_rate:.2%}")
    print(f"Average processing time: {avg_processing_time:.2f}s")
```

## Success Criteria

### **Functional Requirements**

✅ **Visual Compatibility**
- Redaction overlays match original system (yellow 50% opacity, black borders)
- Page break bars match original system (green/orange horizontal bars)
- Page deletion X pattern matches original system (red diagonal lines)
- Rotation indicators show current page orientation

✅ **PDF Manipulation Accuracy**
- Redactions create permanent black overlays that prevent text extraction
- Page rotations apply correctly (0°, 90°, 180°, 270°)
- Page deletions remove specified pages from PDF
- Document splitting creates separate PDFs with correct page ranges

✅ **Database Integration**
- All existing database tables used without modification
- Manipulation metadata stored correctly in existing schema
- Image status workflow maintained (NeedsImageManipulation → InWorkman → NeedsProcessing)
- Audit trails preserved (ImageSplitLog, user tracking)

✅ **Error Recovery**
- Processing failures rollback database status
- S3 backup files (RedactOriginal) created before manipulation
- User can retry failed operations without data loss
- Comprehensive error logging and monitoring

### **Performance Requirements**

✅ **Processing Speed**
- Simple manipulations (1-2 redactions) complete in < 10 seconds
- Complex manipulations (multiple operations) complete in < 60 seconds
- Document splitting completes in < 90 seconds
- Lambda function stays within 15-minute timeout limit

✅ **User Experience**
- Real-time progress updates during processing
- Immediate visual feedback for all manipulation types
- Responsive UI at all zoom levels (25% - 500%)
- No blocking operations - users can view other documents while processing

✅ **System Scalability**
- Support for concurrent document processing (up to 50 simultaneous users)
- Lambda auto-scaling handles peak load
- Database connection pooling prevents connection exhaustion
- S3 operations handle large file uploads/downloads efficiently

### **Quality Assurance**

✅ **Testing Coverage**
- Unit tests: 85%+ code coverage for all processors
- Integration tests: Complete workflow validation
- End-to-end tests: Full user journey automation
- Performance tests: Load testing with realistic document sizes

✅ **Browser Compatibility**
- Chrome 90+ (primary target)
- Firefox 88+ (secondary)
- Safari 14+ (secondary)
- Edge 90+ (tertiary)

✅ **Document Compatibility**
- PDF versions 1.4 through 2.0
- Documents up to 100MB in size
- Up to 500 pages per document
- Password-protected PDFs (with user-provided password)

### **Security & Compliance**

✅ **Data Protection**
- All redacted content permanently removed (rasterized pages)
- Original documents preserved in RedactOriginal S3 path
- User access controls maintained from existing system
- Audit logging for all manipulation operations

✅ **Infrastructure Security**
- Lambda functions run in VPC with private subnets
- Database connections encrypted in transit
- S3 buckets with proper IAM policies
- API endpoints protected by existing authentication

### **Deployment & Operations**

✅ **Deployment Process**
- Automated CloudFormation deployment
- Blue-green deployment strategy for zero downtime
- Rollback capability within 5 minutes
- Environment-specific configurations (dev, staging, production)

✅ **Monitoring & Alerting**
- CloudWatch metrics for Lambda execution time and errors
- Database performance monitoring
- S3 storage utilization tracking
- User activity dashboard

✅ **Documentation**
- Complete API documentation with examples
- Deployment runbook for operations team
- Troubleshooting guide for common issues
- User training materials for new features

### **Migration Success**

✅ **Seamless Transition**
- Zero downtime deployment from C# plugins to Lambda
- All existing manipulation data preserved
- Users experience no workflow changes
- Performance equal or better than original system

✅ **Feature Parity**
- All original Hydra manipulation features implemented
- Visual design pixel-perfect match to original
- Database operations identical to original system
- Error handling and recovery equivalent or improved

This PRD provides a comprehensive roadmap for implementing a modern, scalable PDF manipulation system that maintains full compatibility with the existing Hydra architecture while providing improved performance, reliability, and user experience through Vue.js and Python Lambda technologies.

---

**Project Timeline**: 12-16 weeks
**Team Size**: 3-4 developers (2 frontend, 1-2 backend)
**Budget Estimate**: $150K - $200K (development + AWS infrastructure)
**Go-Live Date**: Q2 2025
