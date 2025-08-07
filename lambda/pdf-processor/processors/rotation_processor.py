"""
Rotation Processor

Handles PDF page rotation operations using PyMuPDF.
Maintains compatibility with existing Hydra ImageRotation database schema.
"""

import fitz
import logging
from typing import List, Dict, Any, Tuple
from io import BytesIO

logger = logging.getLogger(__name__)

class RotationProcessor:
    """Handle PDF page rotation operations"""
    
    def __init__(self, db_manager):
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
        original_page_count = len(doc)
        
        result = {
            'totalRotations': len(rotations),
            'originalPageCount': original_page_count,
            'pagesRotated': [],
            'rotationAngles': {},
            'skippedRotations': []
        }
        
        try:
            # Validate and group rotations by page
            valid_rotations = []
            for rotation in rotations:
                if self._validate_rotation(rotation, original_page_count):
                    valid_rotations.append(rotation)
                else:
                    result['skippedRotations'].append({
                        'rotation': rotation,
                        'reason': 'Invalid rotation data or page index'
                    })
            
            # Apply rotations
            for rotation in valid_rotations:
                page_index = rotation['PageIndex']  # 0-based from DB
                rotate_angle = rotation['Rotate']   # 0, 90, 180, 270 from DB
                
                page = doc[page_index]
                
                # Apply rotation (PyMuPDF uses degrees)
                if rotate_angle != 0:
                    # Set the page rotation
                    page.set_rotation(rotate_angle)
                    
                    result['pagesRotated'].append({
                        'pageIndex': page_index,
                        'rotation': rotate_angle,
                        'previousRotation': page.rotation - rotate_angle
                    })
                    result['rotationAngles'][str(page_index)] = rotate_angle
                    
                    logger.info(f"Rotated page {page_index} by {rotate_angle} degrees")
                else:
                    # Rotation to 0 degrees (reset to original)
                    page.set_rotation(0)
                    result['pagesRotated'].append({
                        'pageIndex': page_index,
                        'rotation': 0,
                        'previousRotation': page.rotation,
                        'resetToOriginal': True
                    })
                    result['rotationAngles'][str(page_index)] = 0
                    logger.info(f"Reset page {page_index} rotation to 0 degrees")
            
            # Update database records to mark rotations as applied
            for rotation in valid_rotations:
                try:
                    # In the original Hydra system, rotations are permanent once applied
                    # We keep the rotation record for audit purposes but could add an Applied flag
                    pass
                except Exception as e:
                    logger.error(f"Failed to update rotation record {rotation.get('ID')}: {e}")
            
            # Save modified document
            output_buffer = BytesIO()
            doc.save(output_buffer, incremental=False)
            doc.close()
            
            result['finalPageCount'] = original_page_count
            logger.info(f"Rotation processing completed: {len(result['pagesRotated'])} pages rotated")
            
            return output_buffer.getvalue(), result
            
        except Exception as e:
            doc.close()
            raise Exception(f"Rotation processing failed: {str(e)}")
    
    def _validate_rotation(self, rotation: Dict[str, Any], page_count: int) -> bool:
        """Validate rotation data"""
        
        # Check required fields
        if 'PageIndex' not in rotation or 'Rotate' not in rotation:
            logger.error(f"Missing required fields in rotation data: {rotation}")
            return False
        
        page_index = rotation['PageIndex']
        rotate_angle = rotation['Rotate']
        
        # Validate page index
        if not isinstance(page_index, int) or page_index < 0 or page_index >= page_count:
            logger.error(f"Invalid page index {page_index} (document has {page_count} pages)")
            return False
        
        # Validate rotation angle
        if rotate_angle not in [0, 90, 180, 270]:
            logger.error(f"Invalid rotation angle {rotate_angle}. Must be 0, 90, 180, or 270")
            return False
        
        return True
    
    def get_effective_rotation(self, base_rotation: int, additional_rotation: int) -> int:
        """Calculate the effective rotation angle"""
        
        total_rotation = (base_rotation + additional_rotation) % 360
        
        # Normalize to standard angles
        if total_rotation == 0:
            return 0
        elif total_rotation == 90:
            return 90
        elif total_rotation == 180:
            return 180
        elif total_rotation == 270:
            return 270
        else:
            # Round to nearest 90-degree increment
            if total_rotation <= 45:
                return 0
            elif total_rotation <= 135:
                return 90
            elif total_rotation <= 225:
                return 180
            else:
                return 270

class RotationValidator:
    """Validation utilities for rotation processing"""
    
    @staticmethod
    def validate_rotation_consistency(rotations: List[Dict[str, Any]]) -> List[str]:
        """
        Validate that rotations are consistent and don't conflict
        
        Returns:
            List of validation warnings/errors
        """
        
        issues = []
        page_rotations = {}
        
        for rotation in rotations:
            page_index = rotation.get('PageIndex')
            rotate_angle = rotation.get('Rotate')
            
            if page_index in page_rotations:
                previous_angle = page_rotations[page_index]
                if previous_angle != rotate_angle:
                    issues.append(f"Conflicting rotations for page {page_index}: {previous_angle}° vs {rotate_angle}°")
            else:
                page_rotations[page_index] = rotate_angle
        
        return issues
    
    @staticmethod
    def optimize_rotations(rotations: List[Dict[str, Any]]) -> List[Dict[str, Any]]:
        """
        Optimize rotation operations by combining multiple rotations on same page
        
        Returns:
            Optimized list of rotations
        """
        
        page_rotations = {}
        
        # Combine rotations for each page
        for rotation in rotations:
            page_index = rotation['PageIndex']
            rotate_angle = rotation['Rotate']
            
            if page_index in page_rotations:
                # Combine with existing rotation
                current_angle = page_rotations[page_index]['Rotate']
                combined_angle = (current_angle + rotate_angle) % 360
                page_rotations[page_index]['Rotate'] = combined_angle
                
                # Keep the latest ID for database updates
                page_rotations[page_index]['ID'] = rotation.get('ID', page_rotations[page_index].get('ID'))
            else:
                page_rotations[page_index] = rotation.copy()
        
        # Convert back to list
        optimized = list(page_rotations.values())
        
        # Remove no-op rotations (0 degrees)
        optimized = [rot for rot in optimized if rot['Rotate'] != 0]
        
        logger.info(f"Optimized {len(rotations)} rotations to {len(optimized)} operations")
        
        return optimized

class RotationUtils:
    """Utility functions for rotation operations"""
    
    @staticmethod
    def get_rotation_matrix(angle: int) -> List[List[float]]:
        """
        Get rotation matrix for given angle
        
        Returns:
            2D rotation matrix
        """
        import math
        
        radians = math.radians(angle)
        cos_a = math.cos(radians)
        sin_a = math.sin(radians)
        
        return [
            [cos_a, -sin_a],
            [sin_a, cos_a]
        ]
    
    @staticmethod
    def rotate_point(x: float, y: float, angle: int, center_x: float = 0, center_y: float = 0) -> Tuple[float, float]:
        """
        Rotate a point around a center point
        
        Args:
            x, y: Point coordinates
            angle: Rotation angle in degrees
            center_x, center_y: Center of rotation
            
        Returns:
            Tuple of rotated coordinates
        """
        import math
        
        # Translate to origin
        x -= center_x
        y -= center_y
        
        # Rotate
        radians = math.radians(angle)
        cos_a = math.cos(radians)
        sin_a = math.sin(radians)
        
        new_x = x * cos_a - y * sin_a
        new_y = x * sin_a + y * cos_a
        
        # Translate back
        new_x += center_x
        new_y += center_y
        
        return new_x, new_y
    
    @staticmethod
    def get_rotated_page_dimensions(width: float, height: float, rotation: int) -> Tuple[float, float]:
        """
        Calculate page dimensions after rotation
        
        Args:
            width, height: Original page dimensions
            rotation: Rotation angle in degrees
            
        Returns:
            Tuple of (new_width, new_height)
        """
        
        if rotation == 0 or rotation == 180:
            return width, height
        elif rotation == 90 or rotation == 270:
            return height, width
        else:
            # For arbitrary angles, calculate bounding box
            import math
            
            radians = math.radians(rotation)
            cos_a = abs(math.cos(radians))
            sin_a = abs(math.sin(radians))
            
            new_width = width * cos_a + height * sin_a
            new_height = width * sin_a + height * cos_a
            
            return new_width, new_height
