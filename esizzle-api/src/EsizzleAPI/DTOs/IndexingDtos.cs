namespace EsizzleAPI.DTOs
{
    // === API REQUEST/RESPONSE DTOS ===

    /// <summary>
    /// Document type DTO for frontend consumption
    /// </summary>
    public class DocumentTypeDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public bool IsGeneric { get; set; }
        public string Code { get; set; } = string.Empty;
    }

    /// <summary>
    /// Bookmark DTO for frontend consumption
    /// </summary>
    public class BookmarkDto
    {
        public int Id { get; set; }
        public int ImageId { get; set; }
        public int PageIndex { get; set; }  // 0-based
        public string Text { get; set; } = string.Empty;  // Pipe-delimited format
        public int ImageDocumentTypeId { get; set; }
        public string DocumentTypeName { get; set; } = string.Empty;
        public bool IsGeneric { get; set; }
        public DateTime DateCreated { get; set; }
        public int? ResultImageId { get; set; }
        public bool CanEdit { get; set; } = true;
        public int CreatedBy { get; set; }
    }

    /// <summary>
    /// Processing result DTO showing created documents
    /// </summary>
    public class ProcessingResultDto
    {
        public int OriginalImageId { get; set; }
        public int ResultImageId { get; set; }
        public string DocumentName { get; set; } = string.Empty;
        public string DocumentType { get; set; } = string.Empty;
        public int PageCount { get; set; }
        public int StartPage { get; set; }
        public int EndPage { get; set; }
        public string ProcessingStatus { get; set; } = "completed";
        public string? FilePath { get; set; }
    }

    /// <summary>
    /// Processing session DTO for tracking async operations
    /// </summary>
    public class ProcessingSessionDto
    {
        public string SessionId { get; set; } = string.Empty;
        public int ImageId { get; set; }
        public string ProcessingType { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string? ErrorMessage { get; set; }
        public DateTime DateCreated { get; set; }
        public DateTime? CompletedDate { get; set; }
    }

    /// <summary>
    /// Page thumbnail DTO for UI display
    /// </summary>
    public class PageThumbnailDto
    {
        public int PageNumber { get; set; }  // 1-based
        public string ThumbnailUrl { get; set; } = string.Empty;
        public int Width { get; set; }
        public int Height { get; set; }
        public bool HasBookmark { get; set; }
        public string? BookmarkType { get; set; }  // "normal" or "generic"
        public string? DocumentTypeName { get; set; }
    }

    /// <summary>
    /// Thumbnail bookmark indicator DTO
    /// </summary>
    public class ThumbnailBookmarkDto
    {
        public int PageIndex { get; set; }  // 0-based
        public string DocumentTypeName { get; set; } = string.Empty;
        public bool IsGeneric { get; set; }
        public int DocumentTypeId { get; set; }
    }

    /// <summary>
    /// Document metadata for save operations
    /// </summary>
    public class DocumentMetadata
    {
        public DateTime? DocumentDate { get; set; }
        public string? Comments { get; set; }
        public int? DocumentTypeId { get; set; }
    }

    // === REQUEST DTOS ===

    /// <summary>
    /// Request to create a new bookmark
    /// </summary>
    public class CreateBookmarkRequest
    {
        public int ImageId { get; set; }
        public int PageIndex { get; set; }  // 0-based
        public int DocumentTypeId { get; set; }
        public string DocumentTypeName { get; set; } = string.Empty;
        public DateTime? DocumentDate { get; set; }
        public string? Comments { get; set; }
    }

    /// <summary>
    /// Request to update an existing bookmark
    /// </summary>
    public class UpdateBookmarkRequest
    {
        public int DocumentTypeId { get; set; }
        public string DocumentTypeName { get; set; } = string.Empty;
        public DateTime? DocumentDate { get; set; }
        public string? Comments { get; set; }
    }

    /// <summary>
    /// Request to process bookmarks (rename or split document)
    /// </summary>
    public class ProcessBookmarksRequest
    {
        public List<BookmarkProcessingInfo> Bookmarks { get; set; } = new();
        public DocumentMetadata? DocumentMetadata { get; set; }
    }

    /// <summary>
    /// Individual bookmark processing information
    /// </summary>
    public class BookmarkProcessingInfo
    {
        public int BookmarkId { get; set; }
        public int PageIndex { get; set; }
        public int DocumentTypeId { get; set; }
        public string DocumentTypeName { get; set; } = string.Empty;
        public DateTime? DocumentDate { get; set; }
        public string? Comments { get; set; }
    }

    // === RESPONSE WRAPPERS ===

    /// <summary>
    /// Generic API response wrapper
    /// </summary>
    public class ApiResponse<T>
    {
        public bool Success { get; set; } = true;
        public T? Data { get; set; }
        public string? ErrorMessage { get; set; }
        public List<string> ValidationErrors { get; set; } = new();
    }

    /// <summary>
    /// Validation result for bookmark operations
    /// </summary>
    public class BookmarkValidationResult
    {
        public bool IsValid { get; set; }
        public List<string> Errors { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
    }

    /// <summary>
    /// Document processing statistics
    /// </summary>
    public class ProcessingStatsDto
    {
        public int TotalBookmarks { get; set; }
        public int ProcessedBookmarks { get; set; }
        public int CreatedDocuments { get; set; }
        public int FailedOperations { get; set; }
        public TimeSpan ProcessingTime { get; set; }
    }
}
