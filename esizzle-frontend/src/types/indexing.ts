/**
 * Legacy-compatible indexing types for Hydra Due Diligence workflow
 * Maps directly to LoanMaster database schema
 */

// Document type from ImageDocTypeMasterLists table
export interface DocumentTypeDto {
  id: number
  name: string
  isGeneric: boolean
  code: string
  dateCreated: Date
  isUsed: boolean
}

// Bookmark from ImageBookmarks table
export interface BookmarkDto {
  id: number
  imageId: number
  pageIndex: number
  text: string
  imageDocumentTypeId: number
  documentTypeName: string
  isGeneric: boolean
  dateCreated: Date
  resultImageId?: number
  canEdit: boolean
  createdBy: number
}

// Page thumbnail data
export interface PageThumbnailDto {
  pageNumber: number
  thumbnailUrl: string
  width: number
  height: number
  hasBookmark: boolean
  bookmarkType?: 'normal' | 'generic'
  documentTypeName?: string
}

// Thumbnail-specific bookmark data
export interface ThumbnailBookmarkDto {
  pageIndex: number
  documentTypeName: string
  isGeneric: boolean
  documentTypeId: number
}

// Processing results after document splitting
export interface ProcessingResultDto {
  originalImageId: number
  resultImageId: number
  documentName: string
  documentType: string
  pageCount: number
  pageRange: [number, number]
  processingStatus: 'pending' | 'completed' | 'error'
  filePath?: string
}

// Processing session tracking
export interface ProcessingSessionDto {
  sessionId: string
  status: 'queued' | 'processing' | 'completed' | 'error'
  documentId: number
  progress?: number
  message?: string
  error?: string
}

// API request types
export interface CreateBookmarkRequest {
  imageId: number
  pageIndex: number
  documentTypeId: number
  documentTypeName: string
  documentDate?: Date
  comments?: string
}

export interface UpdateBookmarkRequest {
  documentTypeId?: number
  documentTypeName?: string
  documentDate?: Date
  comments?: string
}

export interface ProcessBookmarksRequest {
  bookmarks: BookmarkDto[]
  processingMode: 'split' | 'rename'
}

// Document metadata for save operations
export interface DocumentMetadata {
  documentDate?: Date
  comments?: string
  documentTypeId?: number
}

// Validation results
export interface ValidationResult {
  valid: boolean
  errors: string[]
  warnings: string[]
}

// Save processing types
export type SaveProcessingType = 'simple' | 'index_only' | 'document_splitting'

export interface SaveProgress {
  current: number
  total: number
  message: string
}

// Error handling
export interface SaveError {
  type: 'validation' | 'processing' | 'database' | 'file_system'
  code: string
  message: string
  recoverable: boolean
  recovery_action?: string
}

// Change summary for UI
export interface IndexingChangeSummary {
  pendingBookmarks: number
  selectedDocumentType: string | null
  hasUnsavedChanges: boolean
  canSave: boolean
}

// Indexing state for store
export interface IndexingState {
  // Current workflow state
  indexingMode: boolean
  selectedDocumentType: DocumentTypeDto | null
  
  // Document types (filtered by offering)
  availableDocumentTypes: DocumentTypeDto[]
  documentTypesLoading: boolean
  
  // Bookmarks management
  pendingBookmarks: BookmarkDto[]
  bookmarksLoading: boolean
  
  // Processing results
  processingResults: ProcessingResultDto[]
  processingSession: ProcessingSessionDto | null
  
  // UI state
  currentBookmarkPage: number | null
  showBookmarksList: boolean
  showProcessingResults: boolean
  saving: boolean
  saveProgress: SaveProgress | null
}
