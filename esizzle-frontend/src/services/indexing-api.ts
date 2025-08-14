import { apiClient } from '@/services/api'
import type {
  DocumentTypeDto,
  BookmarkDto,
  ProcessingResultDto,
  ProcessingSessionDto,
  CreateBookmarkRequest,
  CreateGenericBreakRequest,
  UpdateBookmarkRequest,
  ProcessBookmarksRequest,
  DocumentMetadata,
  PageThumbnailDto,
  ThumbnailBookmarkDto
} from '@/types/indexing'

/**
 * API service for indexing operations - matches legacy database schema
 */
export const indexingApi = {
  
  // === MAPPERS ===
  
  _normalizeStatus(status: string): ProcessingSessionDto['status'] {
    const s = (status || '').toLowerCase()
    if (s === 'failed' || s === 'error') return 'error'
    if (s === 'completed' || s === 'complete' || s === 'done') return 'completed'
    if (s === 'processing' || s === 'inprogress' || s === 'in_progress') return 'processing'
    return 'queued'
  },
  
  _mapBackendSession(b: any): ProcessingSessionDto {
    return {
      sessionId: b.sessionId || b.SessionId,
      status: this._normalizeStatus(b.status || b.Status),
      documentId: b.documentId ?? b.imageId ?? b.ImageId,
      progress: b.progress ?? undefined,
      message: b.message ?? undefined,
      error: b.error ?? b.ErrorMessage ?? undefined
    }
  },
  
  _mapBackendResult(r: any): ProcessingResultDto {
    const statusRaw = r.processingStatus || r.ProcessingStatus
    const normalized = this._normalizeStatus(statusRaw)
    const processingStatus: ProcessingResultDto['processingStatus'] = normalized === 'error'
      ? 'error'
      : normalized === 'completed'
        ? 'completed'
        : 'pending'
    return {
      originalImageId: r.originalImageId ?? r.OriginalImageId,
      resultImageId: r.resultImageId ?? r.ResultImageId,
      documentName: r.documentName ?? r.DocumentName,
      documentType: r.documentType ?? r.DocumentType,
      pageCount: r.pageCount ?? r.PageCount,
      pageRange: [
        r.startPage ?? r.StartPage ?? 0,
        r.endPage ?? r.EndPage ?? 0
      ],
      processingStatus,
      filePath: r.filePath ?? r.FilePath ?? undefined
    }
  },
  
  // === DOCUMENT TYPES ===
  
  /**
   * Get available document types filtered by offering
   */
  async getDocumentTypes(offeringId: number, search?: string): Promise<DocumentTypeDto[]> {
    const params = new URLSearchParams({
      offeringId: offeringId.toString()
    })
    if (search) {
      params.append('search', search)
    }
    
    return await apiClient.get(`/api/documents/document-types?${params}`)
  },
  
  // === BOOKMARKS ===
  
  /**
   * Get all bookmarks for a document
   */
  async getBookmarks(documentId: number): Promise<BookmarkDto[]> {
    return await apiClient.get(`/api/documents/${documentId}/bookmarks`)
  },
  
  /**
   * Create a new bookmark
   */
  async createBookmark(documentId: number, request: CreateBookmarkRequest): Promise<BookmarkDto> {
    return await apiClient.post(`/api/documents/${documentId}/bookmarks`, request)
  },

  /**
   * Create a generic document break (no document type assigned)
   */
  async createGenericBreak(documentId: number, request: CreateGenericBreakRequest): Promise<BookmarkDto> {
    return await apiClient.post(`/api/documents/${documentId}/generic-break`, request)
  },
  
  /**
   * Update an existing bookmark
   */
  async updateBookmark(documentId: number, bookmarkId: number, updates: UpdateBookmarkRequest): Promise<BookmarkDto> {
    return await apiClient.put(`/api/documents/${documentId}/bookmarks/${bookmarkId}`, updates)
  },
  
  /**
   * Delete a bookmark (soft delete)
   */
  async deleteBookmark(documentId: number, bookmarkId: number): Promise<void> {
    await apiClient.delete(`/api/documents/${documentId}/bookmarks/${bookmarkId}`)
  },
  
  // === PROCESSING ===
  
  /**
   * Save image data only (no bookmarks)
   */
  async saveImageData(documentId: number, metadata: DocumentMetadata): Promise<ProcessingSessionDto> {
    const backend = await apiClient.post(`/api/documents/${documentId}/save-image-data`, metadata)
    return this._mapBackendSession(backend)
  },
  
  /**
   * Process bookmarks (rename or split)
   */
  async processBookmarks(documentId: number, request: ProcessBookmarksRequest): Promise<ProcessingSessionDto> {
    const backend = await apiClient.post(`/api/documents/${documentId}/process-bookmarks`, request)
    return this._mapBackendSession(backend)
  },
  
  /**
   * Get processing session status
   */
  async getProcessingStatus(sessionId: string): Promise<ProcessingSessionDto> {
    const backend = await apiClient.get(`/api/documents/processing/${sessionId}/status`)
    return this._mapBackendSession(backend)
  },
  
  /**
   * Get processing results for a document
   */
  async getProcessingResults(documentId: number): Promise<ProcessingResultDto[]> {
    const backend = await apiClient.get(`/api/documents/${documentId}/processing-results`)
    return Array.isArray(backend) ? backend.map((r: any) => this._mapBackendResult(r)) : []
  },
  
  // === THUMBNAILS ===
  
  /**
   * Get page thumbnails for document
   */
  async getThumbnails(documentId: number): Promise<PageThumbnailDto[]> {
    return await apiClient.get(`/api/documents/${documentId}/thumbnails`)
  },
  
  /**
   * Get bookmark indicators for thumbnails
   */
  async getThumbnailBookmarks(documentId: number): Promise<ThumbnailBookmarkDto[]> {
    return await apiClient.get(`/api/documents/${documentId}/bookmarks/thumbnails`)
  }
}
