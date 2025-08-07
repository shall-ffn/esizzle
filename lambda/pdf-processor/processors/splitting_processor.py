"""
Splitting Processor

Handles PDF document splitting based on page breaks using PyMuPDF.
Maintains compatibility with existing Hydra ImageBookmark database schema.
"""

import fitz
import logging
import uuid
from typing import List, Dict, Any, Tuple
from io import BytesIO

logger = logging.getLogger(__name__)

class SplittingProcessor:
    """Handle PDF document splitting based on page breaks"""
    
    def __init__(self, db_manager, s3_manager):
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
            'originalImageId': image_record['ID'],
            'originalPageCount': original_page_count,
            'splitStrategy': '',
            'operationType': ''
        }
        
        try:
            # Sort page breaks by page index
            sorted_breaks = sorted(page_breaks, key=lambda x: x['PageIndex'])
            
            # Determine split strategy
            split_strategy = self._determine_split_strategy(sorted_breaks, original_page_count)
            result['splitStrategy'] = split_strategy
            
            if split_strategy == 'rename_only':
                # Simple case: single break at beginning (just rename/reindex)
                result = self._handle_rename_only(image_record, sorted_breaks[0], result)
                doc.close()
                return result
            
            elif split_strategy == 'full_split':
                # Complex case: split document into multiple parts
                result = self._handle_full_split(
                    image_record, doc, pdf_bytes, sorted_breaks, result
                )
                doc.close()
                return result
            
            else:
                doc.close()
                raise ValueError(f"Unknown split strategy: {split_strategy}")
                
        except Exception as e:
            doc.close()
            raise Exception(f"Document splitting failed: {str(e)}")
    
    def _determine_split_strategy(self, sorted_breaks: List[Dict[str, Any]], page_count: int) -> str:
        """Determine the appropriate splitting strategy"""
        
        if len(sorted_breaks) == 1 and sorted_breaks[0]['PageIndex'] == 0:
            # Single break at page 0 - just rename the document
            return 'rename_only'
        else:
            # Multiple breaks or break not at page 0 - full split required
            return 'full_split'
    
    def _handle_rename_only(self, image_record: Dict[str, Any], page_break: Dict[str, Any], result: Dict[str, Any]) -> Dict[str, Any]:
        """Handle simple case where document is renamed but not split"""
        
        try:
            # Update image record with new document type
            self.db_manager.update_image_document_type(
                image_record['ID'], 
                page_break['ImageDocumentTypeID'],
                page_break.get('DocumentDate'),
                page_break.get('Comments')
            )
            
            # Mark bookmark as processed
            self.db_manager.mark_bookmark_processed(page_break['ID'], image_record['ID'])
            
            result.update({
                'operationType': 'rename_only',
                'newDocumentType': page_break['ImageDocumentTypeID'],
                'message': 'Document renamed - no splitting required'
            })
            
            logger.info(f"Document {image_record['ID']} renamed to type {page_break['ImageDocumentTypeID']}")
            
            return result
            
        except Exception as e:
            raise Exception(f"Rename operation failed: {str(e)}")
    
    def _handle_full_split(self, image_record: Dict[str, Any], doc: fitz.Document, 
                          pdf_bytes: bytes, sorted_breaks: List[Dict[str, Any]], 
                          result: Dict[str, Any]) -> Dict[str, Any]:
        """Handle complex case where document is split into multiple documents"""
        
        try:
            # Calculate split ranges
            split_ranges = self._calculate_split_ranges(sorted_breaks, len(doc))
            
            logger.info(f"Splitting document into {len(split_ranges)} parts: {split_ranges}")
            
            # Determine if we need a "front section" (pages before first break)
            has_front_section = sorted_breaks[0]['PageIndex'] > 0
            
            # Process each split range
            for i, (start_page, end_page) in enumerate(split_ranges):
                # Find the corresponding page break for this range
                page_break = self._find_break_for_range(sorted_breaks, start_page)
                
                # Determine document type for this split
                if page_break:
                    doc_type_id = page_break['ImageDocumentTypeID']
                    doc_date = page_break.get('DocumentDate')
                    comments = page_break.get('Comments', '')
                    split_type = 'page_break'
                else:
                    # Front section - keep original document type
                    doc_type_id = image_record['DocTypeManualID']
                    doc_date = image_record.get('DocumentDate')
                    comments = image_record.get('Comments', '')
                    split_type = 'front_section'
                
                # Create split document
                new_image_id = self._create_split_document(
                    image_record, doc, start_page, end_page, 
                    doc_type_id, doc_date, comments, split_type
                )
                
                # Add to results
                result['newImageIds'].append(new_image_id)
                result['splitDocuments'].append({
                    'imageId': new_image_id,
                    'pageRange': [start_page, end_page],
                    'pageCount': end_page - start_page,
                    'documentType': doc_type_id,
                    'splitType': split_type,
                    'sourceBreakId': page_break['ID'] if page_break else None
                })
                
                # Update bookmark with result image ID
                if page_break:
                    self.db_manager.mark_bookmark_processed(page_break['ID'], new_image_id)
                
                logger.info(f"Created split document {new_image_id} with pages {start_page}-{end_page-1} (type: {split_type})")
            
            # Create audit trail
            for new_image_id in result['newImageIds']:
                self.db_manager.create_split_log(image_record['ID'], new_image_id)
            
            result.update({
                'operationType': 'full_split',
                'totalSplits': len(result['newImageIds']),
                'hasFrontSection': has_front_section
            })
            
            logger.info(f"Document splitting completed - created {len(result['newImageIds'])} new documents")
            
            return result
            
        except Exception as e:
            raise Exception(f"Full split operation failed: {str(e)}")
    
    def _calculate_split_ranges(self, page_breaks: List[Dict[str, Any]], total_pages: int) -> List[Tuple[int, int]]:
        """Calculate page ranges for document splits"""
        
        ranges = []
        
        # Add front section if first break is not at page 0
        if page_breaks[0]['PageIndex'] > 0:
            ranges.append((0, page_breaks[0]['PageIndex']))
        
        # Add ranges for each break
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
    
    def _create_split_document(self, original_image: Dict[str, Any], doc: fitz.Document,
                              start_page: int, end_page: int, doc_type_id: int,
                              doc_date: Any, comments: str, split_type: str) -> int:
        """Create a new document from page range"""
        
        try:
            # Create new PDF with page range
            split_doc = fitz.open()
            split_doc.insert_pdf(doc, from_page=start_page, to_page=end_page - 1)
            
            # Convert to bytes
            split_bytes = BytesIO()
            split_doc.save(split_bytes, incremental=False)
            split_doc.close()
            split_content = split_bytes.getvalue()
            
            # Create new image record in database
            new_image_id = self.db_manager.create_split_image(
                original_image=original_image,
                doc_type_id=doc_type_id,
                page_count=end_page - start_page,
                page_range=(start_page, end_page),
                document_date=doc_date,
                comments=comments,
                split_type=split_type
            )
            
            # Save files to S3
            self._save_split_to_s3(new_image_id, split_content, original_image)
            
            return new_image_id
            
        except Exception as e:
            raise Exception(f"Failed to create split document: {str(e)}")
    
    def _save_split_to_s3(self, image_id: int, pdf_bytes: bytes, original_image: Dict[str, Any]):
        """Save split document to S3 in all required paths"""
        
        try:
            # Generate paths following original Hydra convention
            base_path = f"{original_image['Path']}/{image_id}/{image_id}.pdf"
            
            paths = [
                f"Original/{base_path}",      # Original copy
                f"Processing/{base_path}",    # Working copy
                f"Production/{base_path}"     # Production copy
            ]
            
            # Upload to all paths
            for path in paths:
                success = self.s3_manager.upload_file(pdf_bytes, path, original_image['BucketPrefix'])
                if not success:
                    raise Exception(f"Failed to upload to S3 path: {path}")
            
            logger.info(f"Split document {image_id} saved to S3 in {len(paths)} paths")
            
        except Exception as e:
            raise Exception(f"Failed to save split document to S3: {str(e)}")

class SplittingValidator:
    """Validation utilities for document splitting"""
    
    @staticmethod
    def validate_page_breaks(page_breaks: List[Dict[str, Any]], page_count: int) -> Dict[str, Any]:
        """
        Validate page break data
        
        Returns:
            Validation results with issues and recommendations
        """
        
        validation = {
            'valid': True,
            'errors': [],
            'warnings': [],
            'validBreaks': [],
            'invalidBreaks': []
        }
        
        page_indices = []
        
        for page_break in page_breaks:
            page_index = page_break.get('PageIndex')
            doc_type_id = page_break.get('ImageDocumentTypeID')
            
            # Basic validation
            if page_index is None:
                validation['errors'].append("Missing PageIndex in page break")
                validation['invalidBreaks'].append(page_break)
                continue
            
            if doc_type_id is None:
                validation['errors'].append("Missing ImageDocumentTypeID in page break")
                validation['invalidBreaks'].append(page_break)
                continue
            
            if not isinstance(page_index, int) or page_index < 0 or page_index >= page_count:
                validation['errors'].append(f"Invalid PageIndex {page_index} (document has {page_count} pages)")
                validation['invalidBreaks'].append(page_break)
                continue
            
            page_indices.append(page_index)
            validation['validBreaks'].append(page_break)
        
        # Check for duplicates
        if len(page_indices) != len(set(page_indices)):
            validation['warnings'].append("Duplicate page breaks detected")
        
        # Check for break at last page
        if page_count - 1 in page_indices:
            validation['warnings'].append("Page break at last page will create empty document")
        
        # Set overall validity
        validation['valid'] = len(validation['errors']) == 0
        
        return validation
    
    @staticmethod
    def analyze_split_impact(page_breaks: List[Dict[str, Any]], page_count: int) -> Dict[str, Any]:
        """Analyze the impact of planned document splits"""
        
        analysis = {
            'totalPages': page_count,
            'numberOfBreaks': len(page_breaks),
            'numberOfSplits': 0,
            'splitSizes': [],
            'warnings': [],
            'recommendations': []
        }
        
        if not page_breaks:
            return analysis
        
        # Sort breaks by page index
        sorted_breaks = sorted(page_breaks, key=lambda x: x.get('PageIndex', 0))
        
        # Calculate split sizes
        prev_page = 0
        
        # Check if first break creates a front section
        if sorted_breaks[0]['PageIndex'] > 0:
            front_size = sorted_breaks[0]['PageIndex']
            analysis['splitSizes'].append(front_size)
            analysis['numberOfSplits'] += 1
            
            if front_size == 1:
                analysis['warnings'].append("Front section will contain only 1 page")
        
        # Calculate sizes for each break section
        for i, page_break in enumerate(sorted_breaks):
            start_page = page_break['PageIndex']
            end_page = sorted_breaks[i + 1]['PageIndex'] if i + 1 < len(sorted_breaks) else page_count
            
            split_size = end_page - start_page
            analysis['splitSizes'].append(split_size)
            analysis['numberOfSplits'] += 1
            
            if split_size == 1:
                analysis['warnings'].append(f"Split starting at page {start_page} will contain only 1 page")
            elif split_size == 0:
                analysis['warnings'].append(f"Split starting at page {start_page} will be empty")
        
        # Generate recommendations
        if analysis['numberOfSplits'] > 10:
            analysis['recommendations'].append("Large number of splits may impact performance")
        
        if min(analysis['splitSizes']) if analysis['splitSizes'] else 0 < 2:
            analysis['recommendations'].append("Consider consolidating very small splits")
        
        return analysis

class SplittingOptimizer:
    """Optimization utilities for document splitting"""
    
    @staticmethod
    def optimize_split_ranges(page_breaks: List[Dict[str, Any]], page_count: int) -> List[Dict[str, Any]]:
        """
        Optimize split ranges to minimize small documents
        
        Returns:
            Optimized list of page breaks
        """
        
        if len(page_breaks) <= 1:
            return page_breaks
        
        # Sort by page index
        sorted_breaks = sorted(page_breaks, key=lambda x: x['PageIndex'])
        optimized = []
        
        # Look for opportunities to merge small splits
        for i, page_break in enumerate(sorted_breaks):
            start_page = page_break['PageIndex']
            end_page = sorted_breaks[i + 1]['PageIndex'] if i + 1 < len(sorted_breaks) else page_count
            
            split_size = end_page - start_page
            
            # If split is very small (1-2 pages), consider merging
            if split_size <= 2 and i + 1 < len(sorted_breaks):
                # Check if next split is also small
                next_break = sorted_breaks[i + 1]
                next_end = sorted_breaks[i + 2]['PageIndex'] if i + 2 < len(sorted_breaks) else page_count
                next_size = next_end - next_break['PageIndex']
                
                if next_size <= 2:
                    # Skip current break to merge with next
                    logger.info(f"Suggesting merge of small splits at pages {start_page} and {next_break['PageIndex']}")
                    continue
            
            optimized.append(page_break)
        
        return optimized
    
    @staticmethod
    def suggest_alternative_breaks(page_breaks: List[Dict[str, Any]], page_count: int) -> List[Dict[str, Any]]:
        """
        Suggest alternative break points for better document organization
        
        Returns:
            List of suggested optimizations
        """
        
        suggestions = []
        
        # Sort breaks by page index
        sorted_breaks = sorted(page_breaks, key=lambda x: x['PageIndex'])
        
        for i, page_break in enumerate(sorted_breaks):
            start_page = page_break['PageIndex']
            end_page = sorted_breaks[i + 1]['PageIndex'] if i + 1 < len(sorted_breaks) else page_count
            
            split_size = end_page - start_page
            
            if split_size == 1:
                suggestions.append({
                    'type': 'single_page_split',
                    'pageIndex': start_page,
                    'suggestion': f"Consider removing break at page {start_page} (creates 1-page document)",
                    'breakId': page_break.get('ID')
                })
            
            elif split_size > 50:
                mid_point = start_page + split_size // 2
                suggestions.append({
                    'type': 'large_split',
                    'pageIndex': start_page,
                    'suggestion': f"Consider additional break around page {mid_point} (current split has {split_size} pages)",
                    'suggestedBreakPage': mid_point
                })
        
        return suggestions
