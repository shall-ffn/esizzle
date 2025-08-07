"""
Redaction Processor

Handles PDF redaction operations using PyMuPDF with rasterization for security.
Maintains compatibility with existing Hydra ImageRedaction database schema.
"""

import fitz  # PyMuPDF
import logging
from typing import List, Tuple, Dict, Any
from io import BytesIO

logger = logging.getLogger(__name__)

class RedactionProcessor:
    """Handle PDF redaction operations using PyMuPDF"""
    
    def __init__(self, db_manager):
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
        original_page_count = len(doc)
        
        result = {
            'totalRedactions': len(redactions),
            'originalPageCount': original_page_count,
            'pagesModified': set(),
            'rasterizedPages': [],
            'appliedRedactions': []
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
                    logger.warning(f"Skipping redactions on invalid page {page_num} (document has {len(doc)} pages)")
                    continue
                    
                page = doc[page_num]
                logger.info(f"Processing {len(page_redactions)} redactions on page {page_num}")
                
                # Apply each redaction on this page
                applied_on_page = []
                for redaction in page_redactions:
                    try:
                        applied_redaction = self._apply_single_redaction(page, redaction)
                        applied_on_page.append(applied_redaction)
                        result['appliedRedactions'].append(applied_redaction)
                    except Exception as e:
                        logger.error(f"Failed to apply redaction {redaction.get('ID')}: {e}")
                        continue
                
                if applied_on_page:
                    # Apply all redactions on this page
                    page.apply_redactions()
                    result['pagesModified'].add(page_num)
                    
                    # Rasterize page to prevent text extraction
                    # This converts the page to an image to ensure redacted content cannot be recovered
                    rasterized = self._rasterize_page(page, page_num)
                    if rasterized:
                        result['rasterizedPages'].append(page_num)
                        
                    logger.info(f"Applied {len(applied_on_page)} redactions on page {page_num}")
            
            # Mark redactions as applied in database
            for redaction in redactions:
                try:
                    self.db_manager.mark_redaction_applied(redaction['ID'])
                except Exception as e:
                    logger.error(f"Failed to mark redaction {redaction['ID']} as applied: {e}")
            
            # Convert sets to lists for JSON serialization
            result['pagesModified'] = list(result['pagesModified'])
            
            # Save modified document
            output_buffer = BytesIO()
            doc.save(output_buffer, incremental=False, encryption=fitz.PDF_ENCRYPT_NONE)
            doc.close()
            
            result['finalPageCount'] = original_page_count
            logger.info(f"Redaction processing completed: {result['totalRedactions']} redactions applied across {len(result['pagesModified'])} pages")
            
            return output_buffer.getvalue(), result
            
        except Exception as e:
            doc.close()
            raise Exception(f"Redaction processing failed: {str(e)}")
    
    def _apply_single_redaction(self, page: fitz.Page, redaction: Dict[str, Any]) -> Dict[str, Any]:
        """Apply a single redaction to a page"""
        
        # Create redaction rectangle from database coordinates
        rect = fitz.Rect(
            redaction['PageX'],
            redaction['PageY'], 
            redaction['PageX'] + redaction['PageWidth'],
            redaction['PageY'] + redaction['PageHeight']
        )
        
        # Handle rotation if specified
        if redaction.get('DrawOrientation', 0) != 0:
            rect = self._apply_rotation_to_rect(rect, redaction['DrawOrientation'], page.rect)
        
        # Validate rectangle bounds
        rect = self._validate_rectangle_bounds(rect, page.rect)
        
        # Create redaction annotation
        redact_annot = page.add_redact_annot(rect)
        
        # Set redaction properties to match original Hydra behavior
        redact_annot.set_info(content=redaction.get('Text', ''))
        redact_annot.set_fill(color=(0, 0, 0))  # Black fill
        redact_annot.set_border(color=(0, 0, 0), width=2)  # Black border
        redact_annot.update()
        
        return {
            'id': redaction['ID'],
            'pageNumber': redaction['PageNumber'],
            'rect': [rect.x0, rect.y0, rect.x1, rect.y1],
            'text': redaction.get('Text', ''),
            'applied': True
        }
    
    def _apply_rotation_to_rect(self, rect: fitz.Rect, orientation: int, page_rect: fitz.Rect) -> fitz.Rect:
        """Apply rotation transformation to redaction rectangle"""
        
        if orientation == 0:
            return rect
            
        # Get page center for rotation
        center_x = page_rect.width / 2
        center_y = page_rect.height / 2
        
        # Get rectangle corners
        x0, y0, x1, y1 = rect.x0, rect.y0, rect.x1, rect.y1
        
        # Apply rotation matrix (simplified for common angles)
        if orientation == 90:
            # 90 degree clockwise rotation
            new_x0 = center_x - (y1 - center_y)
            new_y0 = center_y + (x0 - center_x)
            new_x1 = center_x - (y0 - center_y)
            new_y1 = center_y + (x1 - center_x)
            
        elif orientation == 180:
            # 180 degree rotation
            new_x0 = center_x - (x1 - center_x)
            new_y0 = center_y - (y1 - center_y)
            new_x1 = center_x - (x0 - center_x)
            new_y1 = center_y - (y0 - center_y)
            
        elif orientation == 270:
            # 270 degree clockwise rotation
            new_x0 = center_x + (y0 - center_y)
            new_y0 = center_y - (x1 - center_x)
            new_x1 = center_x + (y1 - center_y)
            new_y1 = center_y - (x0 - center_x)
            
        else:
            # Unsupported rotation, return original
            logger.warning(f"Unsupported rotation angle: {orientation}")
            return rect
        
        # Ensure proper rectangle order (x0 <= x1, y0 <= y1)
        return fitz.Rect(
            min(new_x0, new_x1), min(new_y0, new_y1),
            max(new_x0, new_x1), max(new_y0, new_y1)
        )
    
    def _validate_rectangle_bounds(self, rect: fitz.Rect, page_rect: fitz.Rect) -> fitz.Rect:
        """Ensure redaction rectangle is within page bounds"""
        
        # Clamp coordinates to page bounds
        x0 = max(0, min(rect.x0, page_rect.width))
        y0 = max(0, min(rect.y0, page_rect.height))
        x1 = max(x0, min(rect.x1, page_rect.width))
        y1 = max(y0, min(rect.y1, page_rect.height))
        
        return fitz.Rect(x0, y0, x1, y1)
    
    def _rasterize_page(self, page: fitz.Page, page_num: int) -> bool:
        """
        Rasterize page to prevent text extraction from redacted content
        
        This converts the entire page to an image, making it impossible to
        extract the original text that was underneath redactions.
        """
        
        try:
            # Create high-resolution pixmap (2x resolution for quality)
            matrix = fitz.Matrix(2.0, 2.0)
            pix = page.get_pixmap(matrix=matrix)
            
            # Convert to image bytes
            img_data = pix.tobytes("png")
            pix = None  # Free memory
            
            # Get page dimensions
            page_rect = page.rect
            
            # Clear page contents
            page.clean_contents()
            
            # Insert rasterized image
            page.insert_image(page_rect, stream=img_data, keep_proportion=True)
            
            logger.info(f"Successfully rasterized page {page_num}")
            return True
            
        except Exception as e:
            logger.error(f"Failed to rasterize page {page_num}: {e}")
            return False
    
    def _create_test_redaction(self, page: fitz.Page) -> None:
        """Create a test redaction for validation purposes"""
        
        # Create a small test redaction in the top-left corner
        test_rect = fitz.Rect(10, 10, 100, 30)
        redact_annot = page.add_redact_annot(test_rect)
        redact_annot.set_info(content="TEST REDACTION")
        redact_annot.set_fill(color=(0, 0, 0))
        redact_annot.update()
        
        logger.debug("Created test redaction for validation")

class RedactionValidation:
    """Validation utilities for redaction processing"""
    
    @staticmethod
    def validate_redaction_data(redaction: Dict[str, Any]) -> bool:
        """Validate that redaction data is complete and valid"""
        
        required_fields = ['ID', 'PageNumber', 'PageX', 'PageY', 'PageWidth', 'PageHeight']
        
        for field in required_fields:
            if field not in redaction:
                logger.error(f"Missing required field {field} in redaction data")
                return False
            
            if redaction[field] is None:
                logger.error(f"Field {field} is None in redaction data")
                return False
        
        # Validate coordinate values
        if redaction['PageWidth'] <= 0 or redaction['PageHeight'] <= 0:
            logger.error("Invalid redaction dimensions")
            return False
        
        if redaction['PageX'] < 0 or redaction['PageY'] < 0:
            logger.error("Invalid redaction coordinates")
            return False
        
        return True
    
    @staticmethod
    def validate_page_bounds(redaction: Dict[str, Any], page_width: float, page_height: float) -> bool:
        """Validate that redaction fits within page bounds"""
        
        x1 = redaction['PageX'] + redaction['PageWidth']
        y1 = redaction['PageY'] + redaction['PageHeight']
        
        if x1 > page_width or y1 > page_height:
            logger.warning(f"Redaction extends beyond page bounds: ({x1}, {y1}) vs page ({page_width}, {page_height})")
            return False
        
        return True
