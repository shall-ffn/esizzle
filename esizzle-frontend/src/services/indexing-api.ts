import { apiClient } from '@/services/api'
import type {
  DocumentTypeDto,
  BookmarkDto,
  ProcessingResultDto,
  ProcessingSessionDto,
  CreateBookmarkRequest,
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
    return await apiClient.post(`/api/documents/${documentId}/save-image-data`, metadata)
  },
  
  /**
   * Process bookmarks (rename or split)
   */
  async processBookmarks(documentId: number, request: ProcessBookmarksRequest): Promise<ProcessingSessionDto> {
    return await apiClient.post(`/api/documents/${documentId}/process-bookmarks`, request)
  },
  
  /**
   * Get processing session status
   */
  async getProcessingStatus(sessionId: string): Promise<ProcessingSessionDto> {
    return await apiClient.get(`/api/processing/${sessionId}/status`)
  },
  
  /**
   * Get processing results for a document
   */
  async getProcessingResults(documentId: number): Promise<ProcessingResultDto[]> {
    return await apiClient.get(`/api/documents/${documentId}/processing-results`)
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
