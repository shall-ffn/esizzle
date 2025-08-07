"""
PDF Processing Modules

Contains specialized processors for different types of PDF manipulations:
- RedactionProcessor: Handles redactions with rasterization
- RotationProcessor: Handles page rotations
- DeletionProcessor: Handles page deletions
- SplittingProcessor: Handles document splitting based on page breaks
- ManipulationOrchestrator: Coordinates all manipulation types
"""

from .redaction_processor import RedactionProcessor
from .rotation_processor import RotationProcessor
from .deletion_processor import DeletionProcessor
from .splitting_processor import SplittingProcessor
from .manipulation_orchestrator import ManipulationOrchestrator

__all__ = [
    'RedactionProcessor',
    'RotationProcessor', 
    'DeletionProcessor',
    'SplittingProcessor',
    'ManipulationOrchestrator'
]
