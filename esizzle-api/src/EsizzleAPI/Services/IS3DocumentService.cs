using EsizzleAPI.Models;

namespace EsizzleAPI.Services;

public interface IS3DocumentService
{
    /// <summary>
    /// Legacy-compatible: Resolve document to local file path (with caching)
    /// Mimics legacy SetCurrentImage() functionality
    /// </summary>
    /// <param name="document">Document model with BucketPrefix and Path</param>
    /// <param name="useCache">Whether to use local caching (default: true)</param>
    /// <returns>Local file path to document, or null if not accessible</returns>
    Task<string?> ResolveDocumentPathAsync(DocumentModel document, bool useCache = true);

    /// <summary>
    /// Generate a presigned URL for direct access to a document in S3
    /// </summary>
    /// <param name="bucketName">S3 bucket name (or use BucketPrefix)</param>
    /// <param name="documentPath">S3 key/path to the document</param>
    /// <param name="expirationMinutes">URL expiration time in minutes (default: 60)</param>
    /// <returns>Presigned URL for document access</returns>
    Task<string?> GeneratePresignedUrlAsync(string bucketName, string documentPath, int expirationMinutes = 60);

    /// <summary>
    /// Stream document content directly from S3
    /// </summary>
    /// <param name="bucketName">S3 bucket name (or use BucketPrefix)</param>
    /// <param name="documentPath">S3 key/path to the document</param>
    /// <returns>Stream of document content, or null if not found</returns>
    Task<Stream?> GetDocumentStreamAsync(string bucketName, string documentPath);

    /// <summary>
    /// Check if a document exists in S3
    /// </summary>
    /// <param name="bucketName">S3 bucket name (or use BucketPrefix)</param>
    /// <param name="documentPath">S3 key/path to the document</param>
    /// <returns>True if document exists, false otherwise</returns>
    Task<bool> DocumentExistsAsync(string bucketName, string documentPath);

    /// <summary>
    /// Get document metadata from S3
    /// </summary>
    /// <param name="bucketName">S3 bucket name (or use BucketPrefix)</param>
    /// <param name="documentPath">S3 key/path to the document</param>
    /// <returns>Document metadata including content type and size</returns>
    Task<S3DocumentMetadata?> GetDocumentMetadataAsync(string bucketName, string documentPath);

    /// <summary>
    /// Download document to local cache directory
    /// </summary>
    /// <param name="document">Document model with BucketPrefix and Path</param>
    /// <param name="localPath">Local file path to save to</param>
    /// <returns>True if download successful</returns>
    Task<bool> DownloadToLocalAsync(DocumentModel document, string localPath);

    /// <summary>
    /// Clear local cache for a specific document or all documents
    /// </summary>
    /// <param name="documentId">Specific document ID to clear, or null for all</param>
    /// <returns>Number of files cleaned up</returns>
    Task<int> ClearLocalCacheAsync(int? documentId = null);

    /// <summary>
    /// Get local cache file path for a document
    /// </summary>
    /// <param name="documentId">Document ID</param>
    /// <param name="originalExtension">Original file extension</param>
    /// <returns>Local cache file path</returns>
    string GetLocalCachePath(int documentId, string originalExtension);
}

public class S3DocumentMetadata
{
    public string ContentType { get; set; } = string.Empty;
    public long ContentLength { get; set; }
    public DateTime LastModified { get; set; }
    public string ETag { get; set; } = string.Empty;
}
