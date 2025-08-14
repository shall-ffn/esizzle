/**
 * Comprehensive type system for PDF manipulation operations
 * Maps directly to LoanMaster database schema for full compatibility
 */

// Core manipulation state
export interface DocumentManipulationState {
  documentId: number
  pageCount: number
  redactions: RedactionAnnotation[]
  rotations: RotationAnnotation[]
  pageBreaks: PageBreakAnnotation[]
  pageDeletions: PageDeletionAnnotation[]
  hasUnsavedChanges: boolean
  processingStatus: ProcessingStatus
  lastModified: Date
  modifiedBy: number
}

// Redaction annotations (maps to ImageRedaction table)
export interface RedactionAnnotation {
  id: number
  imageId: number
  pageNumber: number              // 0-based (matches DB PageNumber field)
  pageX: number                  // Exact DB field mapping
  pageY: number
  pageWidth: number
  pageHeight: number
  guid: string
  text?: string
  applied: boolean
  drawOrientation: number        // 0, 90, 180, 270
  createdBy: number
  dateCreated: Date
  deleted: boolean
}

// Page rotation annotations (maps to ImageRotation table)
export interface RotationAnnotation {
  id: number
  imageId: number
  pageIndex: number              // 0-based (matches DB PageIndex field)
  rotate: number                 // 0, 90, 180, 270 (matches DB enum)
}

// Page break annotations (maps to ImageBookmark table)
export interface PageBreakAnnotation {
  id: number
  imageId: number
  pageIndex: number              // 0-based (matches DB PageIndex field)
  text: string                   // Document type and metadata
  imageDocumentTypeId: number
  resultImageId?: number         // Populated after split
  displayText: string            // Human-readable display text
  deleted: boolean
  documentDate?: Date
  comments?: string
}

// Page deletion annotations (maps to ImagePageDeletion table)
export interface PageDeletionAnnotation {
  id: number
  imageId: number
  pageIndex: number              // 0-based (matches DB PageIndex field)
  createdBy: number
  dateCreated: Date
}

// Edit modes
export type EditMode = 'view' | 'redaction' | 'pagebreak' | 'deletion' | 'rotation'

// Processing status
export type ProcessingStatus = 'idle' | 'processing' | 'completed' | 'error'

// Progress tracking
export interface ProcessingProgress {
  sessionId: string
  documentId: number
  status: 'starting' | 'processing' | 'completed' | 'error'
  progress: number              // 0-100
  currentOperation?: string
  message: string
  error?: string
  result?: any
}

// API request/response types
export interface SaveRedactionRequest {
  documentId: number
  redactions: Omit<RedactionAnnotation, 'id' | 'dateCreated'>[]
}

export interface SaveRotationRequest {
  documentId: number
  rotations: Omit<RotationAnnotation, 'id'>[]
}

export interface SavePageBreakRequest {
  documentId: number
  pageBreaks: Omit<PageBreakAnnotation, 'id' | 'resultImageId'>[]
}

export interface SavePageDeletionRequest {
  documentId: number
  pageDeletions: Omit<PageDeletionAnnotation, 'id' | 'dateCreated'>[]
}

export interface ProcessManipulationsRequest {
  documentId: number
  sessionId?: string
}

export interface ProcessManipulationsResponse {
  sessionId: string
  status: string
  documentId: number
}

// Coordinate system types
export interface Point {
  x: number
  y: number
}

export interface Rectangle {
  x: number
  y: number
  width: number
  height: number
}

export interface ViewportInfo {
  width: number
  height: number
  scale: number
  rotation: number
}

// Document types for page breaks
export interface DocumentType {
  id: number
  name: string
  description?: string
  category?: string
}

// Processing result types
export interface RedactionProcessingResult {
  totalRedactions: number
  pagesModified: number[]
  rasterizedPages: number[]
}

export interface RotationProcessingResult {
  totalRotations: number
  pagesRotated: Array<{
    pageIndex: number
    rotation: number
  }>
}

export interface DeletionProcessingResult {
  originalPageCount: number
  deletedPages: number[]
  finalPageCount: number
  documentDeleted?: boolean
}

export interface SplittingProcessingResult {
  newImageIds: number[]
  splitDocuments: Array<{
    imageId: number
    pageRange: [number, number]
    pageCount: number
    documentType: number
    splitType: string
  }>
  operationType: 'rename_only' | 'full_split'
}

// Error types
export interface ManipulationError {
  type: 'validation' | 'processing' | 'network' | 'permission'
  message: string
  details?: any
}

// Validation results
export interface ValidationResult {
  valid: boolean
  errors: string[]
  warnings: string[]
}

// Change summary for UI display
export interface ChangeSummary {
  pendingRedactions: number
  pendingRotations: number
  pendingPageBreaks: number
  pendingDeletions: number
  totalChanges: number
}

// Coordinate translation utilities
export interface CoordinateTranslationOptions {
  viewport: ViewportInfo
  canvasDimensions: { width: number; height: number }
  zoomLevel: number
  pageRotation?: number
}

// === PAGE BREAK UTILITY FUNCTIONS ===

/**
 * Check if a page break is a generic break
 */
export function isGenericPageBreak(pageBreak: PageBreakAnnotation): boolean {
  return pageBreak.imageDocumentTypeId === -1
}

/**
 * Get display text for a page break (generic or normal)
 */
export function getPageBreakDisplayText(pageBreak: PageBreakAnnotation): string {
  return isGenericPageBreak(pageBreak) ? '---GENERIC BREAK---' : pageBreak.displayText
}

/**
 * Get CSS class for page break styling
 */
export function getPageBreakClass(pageBreak: PageBreakAnnotation): string {
  const baseClasses = ['absolute', 'h-5', 'flex', 'items-center', 'justify-center', 'text-white', 'font-bold', 'text-xs', 'cursor-pointer', 'transition-all']
  
  if (isGenericPageBreak(pageBreak)) {
    return [...baseClasses, 'page-break-generic', 'bg-orange-500'].join(' ')
  } else {
    return [...baseClasses, 'page-break-normal', 'bg-green-600'].join(' ')
  }
}
