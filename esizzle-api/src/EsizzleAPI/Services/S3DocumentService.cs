using Amazon.S3;
using Amazon.S3.Model;
using EsizzleAPI.Models;

namespace EsizzleAPI.Services;

public class S3DocumentService : IS3DocumentService
{
    private readonly IAmazonS3 _s3Client;
    private readonly ILogger<S3DocumentService> _logger;
    private readonly string _defaultBucketName;
    private readonly string _tempDirectory;
    private readonly bool _enableLocalCaching;

    public S3DocumentService(IAmazonS3 s3Client, ILogger<S3DocumentService> logger, IConfiguration configuration)
    {
        _s3Client = s3Client;
        _logger = logger;
        _defaultBucketName = configuration["AWS:S3:DocumentBucket"] ?? throw new ArgumentException("AWS:S3:DocumentBucket configuration is required");
        _tempDirectory = configuration["AWS:S3:TempDirectory"] ?? Path.Combine(Path.GetTempPath(), "esizzle-documents");
        _enableLocalCaching = configuration.GetValue<bool>("AWS:S3:EnableLocalCaching", true);

        // Ensure temp directory exists
        if (_enableLocalCaching && !Directory.Exists(_tempDirectory))
        {
            Directory.CreateDirectory(_tempDirectory);
            _logger.LogInformation("Created temp directory for document caching: {TempDirectory}", _tempDirectory);
        }
    }

    /// <summary>
    /// Legacy-compatible: Resolve document to local file path (with caching)
    /// Mimics legacy SetCurrentImage() functionality
    /// </summary>
    public async Task<string?> ResolveDocumentPathAsync(DocumentModel document, bool useCache = true)
    {
        try
        {
            // Validate document
            if (string.IsNullOrEmpty(document.Path))
            {
                _logger.LogWarning("Document {DocumentId} has no file path", document.Id);
                return null;
            }

            // Get local cache path
            var localPath = GetLocalCachePath(document.Id, document.OriginalExt);

            // Check cache first (if enabled and requested)
            if (useCache && _enableLocalCaching && File.Exists(localPath))
            {
                _logger.LogDebug("Cache hit for document {DocumentId}: {LocalPath}", document.Id, localPath);
                return localPath;
            }

            // Download from S3 to local cache
            if (useCache && _enableLocalCaching)
            {
                var downloaded = await DownloadToLocalAsync(document, localPath);
                if (downloaded)
                {
                    _logger.LogDebug("Downloaded document {DocumentId} to cache: {LocalPath}", document.Id, localPath);
                    return localPath;
                }
            }

            // If caching disabled or failed, return null (caller should use streaming)
            _logger.LogWarning("Could not resolve document {DocumentId} to local path", document.Id);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resolving document {DocumentId} to local path", document.Id);
            return null;
        }
    }

    /// <summary>
    /// Download document to local cache directory
    /// </summary>
    public async Task<bool> DownloadToLocalAsync(DocumentModel document, string localPath)
    {
        try
        {
            // Determine bucket and S3 key
            var bucketName = GetBucketName(document.BucketPrefix);
            var s3Key = GetS3Key(document.Path);

            _logger.LogDebug("Downloading document {DocumentId} from s3://{Bucket}/{Key} to {LocalPath}", 
                document.Id, bucketName, s3Key, localPath);

            // Ensure local directory exists
            var localDir = Path.GetDirectoryName(localPath);
            if (!string.IsNullOrEmpty(localDir) && !Directory.Exists(localDir))
            {
                Directory.CreateDirectory(localDir);
            }

            // Download from S3
            var request = new GetObjectRequest
            {
                BucketName = bucketName,
                Key = s3Key
            };

            using var response = await _s3Client.GetObjectAsync(request);
            using var fileStream = new FileStream(localPath, FileMode.Create, FileAccess.Write);
            await response.ResponseStream.CopyToAsync(fileStream);

            _logger.LogInformation("Successfully downloaded document {DocumentId} to local cache", document.Id);
            return true;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            _logger.LogWarning("Document {DocumentId} not found in S3: {Message}", document.Id, ex.Message);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download document {DocumentId} to local cache", document.Id);
            return false;
        }
    }

    public async Task<string?> GeneratePresignedUrlAsync(string bucketName, string documentPath, int expirationMinutes = 60)
    {
        try
        {
            var request = new GetPreSignedUrlRequest
            {
                BucketName = bucketName,
                Key = documentPath,
                Verb = HttpVerb.GET,
                Expires = DateTime.UtcNow.AddMinutes(expirationMinutes)
            };

            var url = _s3Client.GetPreSignedURL(request);
            _logger.LogDebug("Generated presigned URL for {BucketName}/{DocumentPath}, expires in {ExpirationMinutes} minutes", 
                bucketName, documentPath, expirationMinutes);
            
            return url ?? string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate presigned URL for {BucketName}/{DocumentPath}", bucketName, documentPath);
            return null;
        }
    }

    public async Task<Stream?> GetDocumentStreamAsync(string bucketName, string documentPath)
    {
        try
        {
            var request = new GetObjectRequest
            {
                BucketName = bucketName,
                Key = documentPath
            };

            var response = await _s3Client.GetObjectAsync(request);
            _logger.LogDebug("Retrieved document stream for {BucketName}/{DocumentPath}", bucketName, documentPath);
            
            return response.ResponseStream;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            _logger.LogWarning("Document not found in S3: {BucketName}/{DocumentPath}", bucketName, documentPath);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve document stream for {BucketName}/{DocumentPath}", bucketName, documentPath);
            return null;
        }
    }

    public async Task<bool> DocumentExistsAsync(string bucketName, string documentPath)
    {
        try
        {
            var request = new GetObjectMetadataRequest
            {
                BucketName = bucketName,
                Key = documentPath
            };

            await _s3Client.GetObjectMetadataAsync(request);
            return true;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check if document exists: {BucketName}/{DocumentPath}", bucketName, documentPath);
            return false;
        }
    }

    public async Task<S3DocumentMetadata?> GetDocumentMetadataAsync(string bucketName, string documentPath)
    {
        try
        {
            var request = new GetObjectMetadataRequest
            {
                BucketName = bucketName,
                Key = documentPath
            };

            var response = await _s3Client.GetObjectMetadataAsync(request);
            
            return new S3DocumentMetadata
            {
                ContentType = response.Headers.ContentType,
                ContentLength = response.Headers.ContentLength,
                LastModified = response.LastModified ?? DateTime.UtcNow,
                ETag = response.ETag
            };
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            _logger.LogWarning("Document not found in S3: {BucketName}/{DocumentPath}", bucketName, documentPath);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get document metadata for {BucketName}/{DocumentPath}", bucketName, documentPath);
            return null;
        }
    }

    public async Task<int> ClearLocalCacheAsync(int? documentId = null)
    {
        try
        {
            if (!_enableLocalCaching || !Directory.Exists(_tempDirectory))
                return 0;

            var deletedCount = 0;

            if (documentId.HasValue)
            {
                // Clear specific document
                var pattern = $"document_{documentId.Value}.*";
                var files = Directory.GetFiles(_tempDirectory, pattern);
                
                foreach (var file in files)
                {
                    File.Delete(file);
                    deletedCount++;
                    _logger.LogDebug("Deleted cached file: {File}", file);
                }
            }
            else
            {
                // Clear all cached documents
                var files = Directory.GetFiles(_tempDirectory, "document_*.*");
                
                foreach (var file in files)
                {
                    File.Delete(file);
                    deletedCount++;
                }
                
                _logger.LogInformation("Cleared all document cache files: {Count} files deleted", deletedCount);
            }

            return deletedCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing local cache for document {DocumentId}", documentId);
            return 0;
        }
    }

    public string GetLocalCachePath(int documentId, string originalExtension)
    {
        // Ensure extension starts with dot
        if (!string.IsNullOrEmpty(originalExtension) && !originalExtension.StartsWith("."))
        {
            originalExtension = "." + originalExtension;
        }
        
        // Default to .pdf if no extension
        if (string.IsNullOrEmpty(originalExtension))
        {
            originalExtension = ".pdf";
        }

        return Path.Combine(_tempDirectory, $"document_{documentId}{originalExtension}");
    }

    /// <summary>
    /// Get bucket name from BucketPrefix or use default
    /// </summary>
    private string GetBucketName(string? bucketPrefix)
    {
        return !string.IsNullOrEmpty(bucketPrefix) ? bucketPrefix : _defaultBucketName;
    }

    /// <summary>
    /// Get S3 key with legacy path structure
    /// </summary>
    private string GetS3Key(string documentPath)
    {
        // If path already includes IOriginal/Images/, use as-is
        if (documentPath.StartsWith("IOriginal/Images/", StringComparison.OrdinalIgnoreCase))
        {
            return documentPath;
        }

        // Otherwise, prepend legacy path structure
        return $"IOriginal/Images/{documentPath}";
    }
}
