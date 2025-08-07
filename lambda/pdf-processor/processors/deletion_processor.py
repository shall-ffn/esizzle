"""
Deletion Processor

Handles PDF page deletion operations using PyMuPDF.
Maintains compatibility with existing Hydra ImagePageDeletion database schema.
"""

import fitz
import logging
from typing import List, Dict, Any, Tuple
from io import BytesIO

logger = logging.getLogger(__name__)

class DeletionProcessor:
    """Handle PDF page deletion operations"""
    
    def __init__(self, db_manager):
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
            'totalDeletions': len(deletions),
            'originalPageCount': original_page_count,
            'deletedPages': [],
            'finalPageCount': 0,
            'skippedDeletions': [],
            'documentDeleted': False
        }
        
        try:
            # Extract and validate page indices to delete (0-based from DB)
            pages_to_delete = []
            for deletion in deletions:
                page_index = deletion.get('PageIndex')
                if self._validate_page_deletion(deletion, original_page_count):
                    pages_to_delete.append(page_index)
                    result['deletedPages'].append({
                        'pageIndex': page_index,
                        'deletionId': deletion.get('ID'),
                        'deletedBy': deletion.get('CreatedBy'),
                        'deletedAt': deletion.get('DateCreated')
                    })
                else:
                    result['skippedDeletions'].append({
                        'deletion': deletion,
                        'reason': 'Invalid page index or deletion data'
                    })
            
            # Remove duplicates and sort
            pages_to_delete = sorted(list(set(pages_to_delete)), reverse=True)
            
            logger.info(f"Valid pages to delete: {pages_to_delete}")
            
            # Check if all pages are being deleted
            if len(pages_to_delete) >= original_page_count:
                # Mark entire document as deleted in database
                self.db_manager.mark_image_deleted(image_id)
                doc.close()
                
                logger.info(f"All pages deleted - marking image {image_id} as deleted")
                result.update({
                    'finalPageCount': 0,
                    'documentDeleted': True,
                    'message': 'Entire document marked as deleted'
                })
                
                return pdf_bytes, result
            
            # Check for edge case: no valid pages to delete
            if not pages_to_delete:
                logger.info("No valid pages to delete after validation")
                result.update({
                    'finalPageCount': original_page_count,
                    'message': 'No valid pages to delete'
                })
                doc.close()
                return pdf_bytes, result
            
            # Delete pages in reverse order to maintain indices
            deleted_count = 0
            for page_index in pages_to_delete:
                if page_index < len(doc):  # Double-check bounds after previous deletions
                    doc.delete_page(page_index)
                    deleted_count += 1
                    logger.info(f"Deleted page {page_index}")
                else:
                    logger.warning(f"Page {page_index} no longer exists (already deleted or invalid)")
            
            result['finalPageCount'] = len(doc)
            result['actualDeletions'] = deleted_count
            
            logger.info(f"Deleted {deleted_count} pages, final page count: {result['finalPageCount']}")
            
            # Update page count in database
            if result['finalPageCount'] != original_page_count:
                self.db_manager.update_page_count(image_id, result['finalPageCount'])
            
            # Mark deletion records as processed
            for deletion in deletions:
                if deletion.get('PageIndex') in [d['pageIndex'] for d in result['deletedPages']]:
                    try:
                        # In the original system, page deletions are permanent
                        # We keep the record for audit purposes
                        self.db_manager.mark_page_deletion_processed(deletion.get('ID'))
                    except Exception as e:
                        logger.error(f"Failed to mark deletion {deletion.get('ID')} as processed: {e}")
            
            # Save modified document
            output_buffer = BytesIO()
            doc.save(output_buffer, incremental=False)
            doc.close()
            
            logger.info(f"Page deletion processing completed: {deleted_count} pages removed")
            
            return output_buffer.getvalue(), result
            
        except Exception as e:
            doc.close()
            raise Exception(f"Page deletion processing failed: {str(e)}")
    
    def _validate_page_deletion(self, deletion: Dict[str, Any], page_count: int) -> bool:
        """Validate page deletion data"""
        
        # Check required fields
        required_fields = ['PageIndex']
        for field in required_fields:
            if field not in deletion:
                logger.error(f"Missing required field {field} in deletion data")
                return False
        
        page_index = deletion['PageIndex']
        
        # Validate page index
        if not isinstance(page_index, int):
            logger.error(f"Page index must be integer, got {type(page_index)}")
            return False
        
        if page_index < 0 or page_index >= page_count:
            logger.error(f"Invalid page index {page_index} (document has {page_count} pages)")
            return False
        
        return True
    
    def get_deletion_impact_analysis(self, deletions: List[Dict[str, Any]], page_count: int) -> Dict[str, Any]:
        """
        Analyze the impact of planned deletions
        
        Returns:
            Analysis of deletion impact including warnings
        """
        
        analysis = {
            'totalPages': page_count,
            'pagesToDelete': len(deletions),
            'pagesRemaining': 0,
            'warnings': [],
            'errors': [],
            'recommendations': []
        }
        
        # Extract page indices
        page_indices = []
        for deletion in deletions:
            page_index = deletion.get('PageIndex')
            if page_index is not None and 0 <= page_index < page_count:
                page_indices.append(page_index)
            else:
                analysis['errors'].append(f"Invalid page index: {page_index}")
        
        # Remove duplicates
        unique_pages = list(set(page_indices))
        analysis['uniquePagesToDelete'] = len(unique_pages)
        analysis['pagesRemaining'] = page_count - len(unique_pages)
        
        # Generate warnings and recommendations
        if len(unique_pages) == page_count:
            analysis['warnings'].append("All pages will be deleted - document will be marked as deleted")
        elif len(unique_pages) > page_count * 0.8:
            analysis['warnings'].append("More than 80% of pages will be deleted")
            analysis['recommendations'].append("Consider if document splitting would be more appropriate")
        
        if len(page_indices) != len(unique_pages):
            analysis['warnings'].append(f"Duplicate page deletions found: {len(page_indices) - len(unique_pages)} duplicates")
        
        # Check for consecutive page deletions
        unique_pages.sort()
        consecutive_ranges = self._find_consecutive_ranges(unique_pages)
        if any(end - start >= 5 for start, end in consecutive_ranges):
            analysis['recommendations'].append("Large consecutive page ranges detected - consider document splitting")
        
        return analysis
    
    def _find_consecutive_ranges(self, page_indices: List[int]) -> List[Tuple[int, int]]:
        """Find consecutive ranges in page indices"""
        
        if not page_indices:
            return []
        
        ranges = []
        start = page_indices[0]
        end = start
        
        for i in range(1, len(page_indices)):
            if page_indices[i] == end + 1:
                end = page_indices[i]
            else:
                ranges.append((start, end))
                start = page_indices[i]
                end = start
        
        ranges.append((start, end))
        return ranges

class DeletionValidator:
    """Validation utilities for page deletion processing"""
    
    @staticmethod
    def validate_deletion_batch(deletions: List[Dict[str, Any]], page_count: int) -> Dict[str, Any]:
        """
        Validate a batch of page deletions
        
        Returns:
            Validation results with issues and recommendations
        """
        
        validation = {
            'valid': True,
            'errors': [],
            'warnings': [],
            'validDeletions': [],
            'invalidDeletions': []
        }
        
        page_indices = []
        
        for deletion in deletions:
            page_index = deletion.get('PageIndex')
            
            # Basic validation
            if page_index is None:
                validation['errors'].append("Missing PageIndex in deletion record")
                validation['invalidDeletions'].append(deletion)
                continue
            
            if not isinstance(page_index, int):
                validation['errors'].append(f"PageIndex must be integer, got {type(page_index)}")
                validation['invalidDeletions'].append(deletion)
                continue
            
            if page_index < 0 or page_index >= page_count:
                validation['errors'].append(f"PageIndex {page_index} out of range (0-{page_count-1})")
                validation['invalidDeletions'].append(deletion)
                continue
            
            page_indices.append(page_index)
            validation['validDeletions'].append(deletion)
        
        # Check for duplicates
        if len(page_indices) != len(set(page_indices)):
            validation['warnings'].append("Duplicate page deletions detected")
        
        # Check if all pages being deleted
        if len(set(page_indices)) >= page_count:
            validation['warnings'].append("All pages will be deleted - document will be marked as deleted")
        
        # Set overall validity
        validation['valid'] = len(validation['errors']) == 0
        
        return validation

class DeletionOptimizer:
    """Optimization utilities for page deletion operations"""
    
    @staticmethod
    def optimize_deletion_order(deletions: List[Dict[str, Any]]) -> List[Dict[str, Any]]:
        """
        Optimize deletion order to maintain page indices
        
        Returns:
            Deletions sorted in reverse page order
        """
        
        # Sort by page index in descending order
        # This ensures that deleting pages doesn't affect indices of remaining deletions
        return sorted(deletions, key=lambda d: d.get('PageIndex', 0), reverse=True)
    
    @staticmethod
    def merge_consecutive_deletions(deletions: List[Dict[str, Any]]) -> List[Dict[str, Any]]:
        """
        Merge consecutive page deletions into ranges for efficiency
        
        Note: PyMuPDF doesn't have range deletion, but this could be useful
        for reporting and validation purposes
        """
        
        if not deletions:
            return deletions
        
        # Sort by page index
        sorted_deletions = sorted(deletions, key=lambda d: d.get('PageIndex', 0))
        
        # Group consecutive pages
        merged = []
        current_group = [sorted_deletions[0]]
        
        for i in range(1, len(sorted_deletions)):
            current_page = sorted_deletions[i].get('PageIndex')
            last_page = current_group[-1].get('PageIndex')
            
            if current_page == last_page + 1:
                current_group.append(sorted_deletions[i])
            else:
                # Process current group
                if len(current_group) > 1:
                    # Create range deletion record
                    range_deletion = {
                        'PageIndexStart': current_group[0].get('PageIndex'),
                        'PageIndexEnd': current_group[-1].get('PageIndex'),
                        'Count': len(current_group),
                        'Type': 'range',
                        'OriginalDeletions': current_group
                    }
                    merged.append(range_deletion)
                else:
                    merged.extend(current_group)
                
                current_group = [sorted_deletions[i]]
        
        # Process final group
        if len(current_group) > 1:
            range_deletion = {
                'PageIndexStart': current_group[0].get('PageIndex'),
                'PageIndexEnd': current_group[-1].get('PageIndex'),
                'Count': len(current_group),
                'Type': 'range',
                'OriginalDeletions': current_group
            }
            merged.append(range_deletion)
        else:
            merged.extend(current_group)
        
        return merged
