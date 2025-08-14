using EsizzleAPI.Models;
using EsizzleAPI.DTOs;

namespace EsizzleAPI.Repositories
{
    /// <summary>
    /// Repository interface for legacy indexing operations
    /// </summary>
    public interface IIndexingRepository
    {
        // === DOCUMENT TYPES ===
        
        /// <summary>
        /// Get document types filtered by offering's IndexCode
        /// </summary>
        Task<List<DocumentTypeDto>> GetDocumentTypesByOfferingAsync(int offeringId, string? search = null);
        
        /// <summary>
        /// Get document type by ID
        /// </summary>
        Task<DocumentTypeDto?> GetDocumentTypeByIdAsync(int documentTypeId);
        
        // === BOOKMARKS ===
        
        /// <summary>
        /// Get all bookmarks for a document
        /// </summary>
        Task<List<BookmarkDto>> GetBookmarksByDocumentAsync(int documentId);
        
        /// <summary>
        /// Get bookmark by ID
        /// </summary>
        Task<BookmarkDto?> GetBookmarkByIdAsync(int bookmarkId);
        
        /// <summary>
        /// Create a new bookmark
        /// </summary>
        Task<BookmarkDto> CreateBookmarkAsync(CreateBookmarkRequest request, int userId);
        
        /// <summary>
        /// Create a generic document break (ImageDocumentTypeID = -1)
        /// </summary>
        Task<BookmarkDto> CreateGenericBreakAsync(int imageId, int pageIndex, int userId);
        
        /// <summary>
        /// Update an existing bookmark
        /// </summary>
        Task<BookmarkDto?> UpdateBookmarkAsync(int documentId, int bookmarkId, UpdateBookmarkRequest request);
        
        /// <summary>
        /// Delete a bookmark (soft delete)
        /// </summary>
        Task<bool> DeleteBookmarkAsync(int documentId, int bookmarkId);
        
        /// <summary>
        /// Validate bookmarks for processing
        /// </summary>
        Task<BookmarkValidationResult> ValidateBookmarksAsync(int documentId, List<int> bookmarkIds);
        
        // === PROCESSING ===
        
        /// <summary>
        /// Save document metadata only (no bookmarks)
        /// </summary>
        Task<ProcessingSessionDto> SaveImageDataAsync(int documentId, DocumentMetadata metadata, int userId);
        
        /// <summary>
        /// Create processing session for bookmark processing
        /// </summary>
        Task<ProcessingSessionDto> CreateProcessingSessionAsync(int documentId, string processingType, int userId);
        
        /// <summary>
        /// Update processing session status
        /// </summary>
        Task<bool> UpdateProcessingSessionAsync(string sessionId, string status, string? errorMessage = null);
        
        /// <summary>
        /// Get processing session by ID
        /// </summary>
        Task<ProcessingSessionDto?> GetProcessingSessionAsync(string sessionId);
        
        /// <summary>
        /// Get processing results for a document
        /// </summary>
        Task<List<ProcessingResultDto>> GetProcessingResultsAsync(int documentId);
        
        // === DOCUMENT OPERATIONS ===
        
        /// <summary>
        /// Get document by ID
        /// </summary>
        Task<Image?> GetDocumentAsync(int documentId);
        
        /// <summary>
        /// Update document with manual document type
        /// </summary>
        Task<bool> UpdateDocumentTypeAsync(int documentId, int? documentTypeId, DateTime? documentDate, string? comments);
        
        /// <summary>
        /// Create split document record
        /// </summary>
        Task<int> CreateSplitDocumentAsync(int originalDocumentId, string newName, int pageCount, int documentTypeId, string filePath, int userId);
        
        /// <summary>
        /// Link bookmark to result document
        /// </summary>
        Task<bool> LinkBookmarkToResultAsync(int bookmarkId, int resultDocumentId);
        
        // === THUMBNAILS ===
        
        /// <summary>
        /// Get page thumbnails for document
        /// </summary>
        Task<List<PageThumbnailDto>> GetThumbnailsAsync(int documentId);
        
        /// <summary>
        /// Get bookmark indicators for thumbnails
        /// </summary>
        Task<List<ThumbnailBookmarkDto>> GetThumbnailBookmarksAsync(int documentId);
        
        // === UTILITIES ===
        
        /// <summary>
        /// Build pipe-delimited text field for bookmark
        /// </summary>
        string BuildBookmarkText(string documentTypeName, int documentTypeId, DateTime? documentDate, string? comments);
        
        /// <summary>
        /// Parse pipe-delimited text field from bookmark
        /// </summary>
        (string DocumentTypeName, int DocumentTypeId, DateTime? DocumentDate, string? Comments) ParseBookmarkText(string text);
    }
}
