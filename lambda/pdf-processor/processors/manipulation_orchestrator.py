"""
Manipulation Orchestrator

Coordinates all PDF manipulation operations including redactions, rotations, 
page deletions, and document splitting while maintaining compatibility with 
the existing Hydra system architecture.
"""

import logging
import time
from typing import Dict, Any, List, Optional
from io import BytesIO

logger = logging.getLogger(__name__)

class ManipulationOrchestrator:
    """
    Orchestrates PDF manipulation operations in the correct order:
    1. Apply redactions (rasterize for security)
    2. Apply rotations 
    3. Apply page deletions
    4. Handle document splitting (page breaks)
    """
    
    def __init__(self, s3_manager, db_manager, progress_tracker, timeout_seconds: int = 840):
        self.s3_manager = s3_manager
        self.db_manager = db_manager
        self.progress_tracker = progress_tracker
        self.timeout_seconds = timeout_seconds
        
    def process_document_manipulations(self, image_id: int) -> Dict[str, Any]:
        """
        Process all manipulations for a document
        
        Args:
            image_id: Database image ID
            
        Returns:
            Processing result dictionary
        """
        
        logger.info(f"Starting manipulation processing for image {image_id}")
        start_time = time.time()
        
        # Get image metadata and manipulations from database
        self.progress_tracker.update_progress('processing', 20, 'Loading document data...')
        
        image_record = self.db_manager.get_image_record(image_id)
        if not image_record:
            raise ValueError(f"Image record not found: {image_id}")
        
        # Get all manipulation data
        manipulations = {
            'redactions': self.db_manager.get_pending_redactions(image_id),
            'rotations': self.db_manager.get_rotations(image_id),
            'deletions': self.db_manager.get_page_deletions(image_id),
            'pageBreaks': self.db_manager.get_page_breaks(image_id)
        }
        
        logger.info(f"Found manipulations: {len(manipulations['redactions'])} redactions, "
                    f"{len(manipulations['rotations'])} rotations, "
                    f"{len(manipulations['deletions'])} deletions, "
                    f"{len(manipulations['pageBreaks'])} page breaks")
        
        # Check if any manipulations need processing
        total_manipulations = sum(len(m) if m else 0 for m in manipulations.values())
        if total_manipulations == 0:
            logger.info("No manipulations found to process")
            return {'message': 'No manipulations to process', 'operationsApplied': []}
        
        # Download PDF from S3 Processing path
        self.progress_tracker.update_progress('processing', 30, 'Downloading PDF...')
        
        processing_path = f"Processing/{image_record['Path']}/{image_id}/{image_id}.pdf"
        pdf_bytes = self.s3_manager.download_file(processing_path, image_record['BucketPrefix'])
        
        if not pdf_bytes:
            raise ValueError(f"Could not download PDF from S3: {processing_path}")
        
        result = {
            'originalPageCount': 0,
            'finalPageCount': 0,
            'operationsApplied': [],
            'splitImages': [],
            'processingTime': 0
        }
        
        try:
            # Create backup if manipulations will be applied
            has_file_manipulations = (manipulations.get('redactions') or 
                                    manipulations.get('rotations') or 
                                    manipulations.get('deletions'))
            
            if has_file_manipulations:
                self.progress_tracker.update_progress('processing', 35, 'Creating backup...')
                redact_original_path = f"RedactOriginal/{image_record['Path']}/{image_id}/{image_id}.pdf"
                self.s3_manager.upload_file(pdf_bytes, redact_original_path, image_record['BucketPrefix'])
                logger.info("Backup created in RedactOriginal path")
            
            # Process manipulations in correct order
            modified_pdf = pdf_bytes
            
            # 1. Apply redactions first (they need to be rasterized for security)
            if manipulations.get('redactions'):
                self.progress_tracker.update_progress('processing', 45, 'Applying redactions...')
                
                from processors.redaction_processor import RedactionProcessor
                processor = RedactionProcessor(self.db_manager)
                modified_pdf, redaction_result = processor.process(
                    image_id, modified_pdf, manipulations['redactions']
                )
                result['operationsApplied'].append('redactions')
                result['redactionResult'] = redaction_result
                logger.info(f"Applied {len(manipulations['redactions'])} redactions")
                
            # Check timeout
            if time.time() - start_time > self.timeout_seconds - 60:  # Leave 1 minute buffer
                raise TimeoutError("Processing timeout approaching")
                
            # 2. Apply rotations
            if manipulations.get('rotations'):
                self.progress_tracker.update_progress('processing', 60, 'Applying rotations...')
                
                from processors.rotation_processor import RotationProcessor
                processor = RotationProcessor(self.db_manager)
                modified_pdf, rotation_result = processor.process(
                    image_id, modified_pdf, manipulations['rotations']
                )
                result['operationsApplied'].append('rotations')
                result['rotationResult'] = rotation_result
                logger.info(f"Applied {len(manipulations['rotations'])} rotations")
                
            # 3. Apply page deletions
            if manipulations.get('deletions'):
                self.progress_tracker.update_progress('processing', 75, 'Deleting pages...')
                
                from processors.deletion_processor import DeletionProcessor
                processor = DeletionProcessor(self.db_manager)
                modified_pdf, deletion_result = processor.process(
                    image_id, modified_pdf, manipulations['deletions']
                )
                result['operationsApplied'].append('deletions')
                result['deletionResult'] = deletion_result
                result['finalPageCount'] = deletion_result.get('finalPageCount', 0)
                logger.info(f"Deleted {len(manipulations['deletions'])} pages")
                
            # 4. Handle page breaks (document splitting)
            if manipulations.get('pageBreaks'):
                self.progress_tracker.update_progress('processing', 85, 'Splitting document...')
                
                from processors.splitting_processor import SplittingProcessor
                processor = SplittingProcessor(self.db_manager, self.s3_manager)
                split_result = processor.process(
                    image_record, modified_pdf, manipulations['pageBreaks']
                )
                result['operationsApplied'].append('splitting')
                result['splitResult'] = split_result
                result['splitImages'] = split_result.get('newImageIds', [])
                
                # If document was split, mark original as obsolete
                if split_result.get('newImageIds'):
                    self.db_manager.update_image_status(image_id, 'Obsolete')
                    logger.info(f"Document split into {len(result['splitImages'])} new documents")
                    
                    # Don't save the processed PDF back - it's been split
                    result['processingTime'] = time.time() - start_time
                    return result
            
            # Save processed PDF back to S3 (only if not split)
            self.progress_tracker.update_progress('processing', 95, 'Saving processed document...')
            self.s3_manager.upload_file(modified_pdf, processing_path, image_record['BucketPrefix'])
            
            # Update page count if it changed
            if result.get('finalPageCount') and result['finalPageCount'] != image_record.get('PageCount'):
                self.db_manager.update_page_count(image_id, result['finalPageCount'])
            
            result['processingTime'] = time.time() - start_time
            logger.info(f"Processing completed in {result['processingTime']:.2f} seconds")
            
            return result
            
        except Exception as e:
            logger.error(f"Error during manipulation processing: {e}")
            raise
    
    def perform_health_check(self, image_id: int) -> Dict[str, Any]:
        """
        Perform health check operations
        
        Args:
            image_id: Image ID to test with
            
        Returns:
            Health check results
        """
        
        health_result = {
            'status': 'healthy',
            'checks': {},
            'timestamp': time.time()
        }
        
        try:
            # Test database connectivity
            image_record = self.db_manager.get_image_record(image_id)
            health_result['checks']['database'] = {
                'status': 'healthy' if image_record else 'warning',
                'message': f"Image record {'found' if image_record else 'not found'}"
            }
            
            # Test S3 connectivity
            if image_record:
                processing_path = f"Processing/{image_record['Path']}/{image_id}/{image_id}.pdf"
                file_exists = self.s3_manager.file_exists(processing_path, image_record['BucketPrefix'])
                health_result['checks']['s3'] = {
                    'status': 'healthy' if file_exists else 'warning',
                    'message': f"Processing file {'exists' if file_exists else 'not found'}"
                }
            else:
                health_result['checks']['s3'] = {
                    'status': 'skipped',
                    'message': 'No image record to test'
                }
            
            # Test PyMuPDF functionality
            try:
                import fitz
                test_doc = fitz.open()
                test_page = test_doc.new_page()
                test_doc.close()
                
                health_result['checks']['pymupdf'] = {
                    'status': 'healthy',
                    'message': 'PyMuPDF functioning correctly'
                }
            except Exception as pdf_error:
                health_result['checks']['pymupdf'] = {
                    'status': 'unhealthy',
                    'message': f'PyMuPDF error: {str(pdf_error)}'
                }
                health_result['status'] = 'unhealthy'
            
            # Overall status
            unhealthy_checks = [check for check in health_result['checks'].values() 
                              if check['status'] == 'unhealthy']
            if unhealthy_checks:
                health_result['status'] = 'unhealthy'
            elif any(check['status'] == 'warning' for check in health_result['checks'].values()):
                health_result['status'] = 'warning'
                
        except Exception as e:
            health_result['status'] = 'unhealthy'
            health_result['error'] = str(e)
            logger.error(f"Health check failed: {e}")
        
        return health_result

    def _get_original_page_count(self, pdf_bytes: bytes) -> int:
        """Get the original page count from PDF"""
        try:
            import fitz
            doc = fitz.open(stream=pdf_bytes, filetype="pdf")
            page_count = len(doc)
            doc.close()
            return page_count
        except Exception as e:
            logger.warning(f"Could not determine page count: {e}")
            return 0
