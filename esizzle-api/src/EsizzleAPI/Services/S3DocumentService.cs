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
    private readonly string _environment;
    private readonly string _environmentPath;
    private readonly bool _useProductionPaths;

    public S3DocumentService(IAmazonS3 s3Client, ILogger<S3DocumentService> logger, IConfiguration configuration)
    {
        _s3Client = s3Client;
        _logger = logger;
        _defaultBucketName = configuration["AWS:S3:DocumentBucket"] ?? throw new ArgumentException("AWS:S3:DocumentBucket configuration is required");
        _tempDirectory = configuration["AWS:S3:TempDirectory"] ?? Path.Combine(Path.GetTempPath(), "esizzle-documents");
        _enableLocalCaching = configuration.GetValue<bool>("AWS:S3:EnableLocalCaching", true);
        _useProductionPaths = configuration.GetValue<bool>("AWS:S3:UseProductionPaths", false);
        _environment = configuration["Environment"] ?? "Development";
        _environmentPath = GetEnvironmentPath(_environment);

        // Ensure temp directory exists
        if (_enableLocalCaching && !Directory.Exists(_tempDirectory))
        {
            Directory.CreateDirectory(_tempDirectory);
            _logger.LogInformation("Created temp directory for document caching: {TempDirectory}", _tempDirectory);
        }

        _logger.LogInformation("S3DocumentService initialized for environment: {Environment}, path: {EnvironmentPath}, useProductionPaths: {UseProductionPaths}", 
            _environment, _environmentPath, _useProductionPaths);
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

            using var response = await _s3Client.GetObjectAsync(request);
            _logger.LogDebug("Retrieved document stream for {BucketName}/{DocumentPath}", bucketName, documentPath);
            
            // Copy S3 response stream to MemoryStream to enable seeking for PDF.js
            var memoryStream = new MemoryStream();
            await response.ResponseStream.CopyToAsync(memoryStream);
            memoryStream.Position = 0; // Reset position to beginning
            
            _logger.LogDebug("Copied S3 stream to seekable MemoryStream, size: {Size} bytes", memoryStream.Length);
            return memoryStream;
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
    /// Legacy-compatible: Resolve document using status-based path logic
    /// Mimics legacy RefreshBytes() functionality
    /// For web viewing, prioritizes PDF versions over original office documents
    /// </summary>
    public async Task<string?> ResolveDocumentWithStatusAsync(DocumentModel document, bool useCache = true, ImageStatusTypeEnum? preferredStatus = null)
    {
        try
        {
            var bucketName = GetEnvironmentBucketName(document.BucketPrefix);
            
            // For web viewing, prefer PDF versions regardless of current status
            // This handles cases where documents have NeedsConversion status but PDFs exist
            var pdfOptimizedStatuses = GetPdfOptimizedStatusOrder(document);
            
            foreach (var statusToTry in pdfOptimizedStatuses)
            {
                var s3Path = document.GetImagePath(statusToTry);
                
                // Get appropriate local cache path (always use .pdf for processed versions)
                var cacheExtension = IsProcessedStatus(statusToTry) ? ".pdf" : document.OriginalExt;
                var localPath = GetLocalCachePath(document.Id, cacheExtension);

                // Check cache first (if enabled and requested)
                if (useCache && _enableLocalCaching && File.Exists(localPath))
                {
                    _logger.LogDebug("Cache hit for document {DocumentId} with status {Status}: {LocalPath}", 
                        document.Id, statusToTry, localPath);
                    return localPath;
                }

                // Try to download from S3 using this status path
                if (useCache && _enableLocalCaching)
                {
                    var downloaded = await DownloadFromS3Async(bucketName, s3Path, localPath, document.Id);
                    if (downloaded)
                    {
                        _logger.LogInformation("Downloaded document {DocumentId} using status {Status} to cache: {LocalPath}", 
                            document.Id, statusToTry, localPath);
                        return localPath;
                    }
                }
            }

            _logger.LogWarning("Could not resolve document {DocumentId} using any PDF-optimized status paths", document.Id);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resolving document {DocumentId} with PDF-optimized logic", document.Id);
            return null;
        }
    }

    /// <summary>
    /// Generate presigned URL using PDF-optimized status-based path resolution
    /// Prioritizes processed PDF versions for web viewing
    /// </summary>
    public async Task<string?> GenerateDocumentPresignedUrlAsync(DocumentModel document, int expirationMinutes = 60, ImageStatusTypeEnum? preferredStatus = null)
    {
        try
        {
            var bucketName = GetEnvironmentBucketName(document.BucketPrefix);
            
            // Use PDF-optimized status order for web viewing
            var pdfOptimizedStatuses = GetPdfOptimizedStatusOrder(document);
            
            foreach (var statusToTry in pdfOptimizedStatuses)
            {
                var s3Path = document.GetImagePath(statusToTry);
                
                // Check if document exists before generating URL
                if (await DocumentExistsAsync(bucketName, s3Path))
                {
                    var presignedUrl = await GeneratePresignedUrlAsync(bucketName, s3Path, expirationMinutes);
                    if (!string.IsNullOrEmpty(presignedUrl))
                    {
                        _logger.LogInformation("Generated presigned URL for document {DocumentId} using status {Status} at path {S3Path}", 
                            document.Id, statusToTry, s3Path);
                        return presignedUrl;
                    }
                }
                else
                {
                    _logger.LogDebug("Document {DocumentId} not found at status {Status} path {S3Path} for presigned URL", 
                        document.Id, statusToTry, s3Path);
                }
            }

            _logger.LogWarning("Could not generate presigned URL for document {DocumentId} - no accessible versions found using PDF-optimized paths", document.Id);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating presigned URL for document {DocumentId}", document.Id);
            return null;
        }
    }

    /// <summary>
    /// Stream document using PDF-optimized status-based path resolution
    /// Prioritizes processed PDF versions for web viewing
    /// </summary>
    public async Task<Stream?> GetDocumentStreamWithStatusAsync(DocumentModel document, ImageStatusTypeEnum? preferredStatus = null)
    {
        try
        {
            var bucketName = GetEnvironmentBucketName(document.BucketPrefix);
            
            // Use PDF-optimized status order for web viewing
            var pdfOptimizedStatuses = GetPdfOptimizedStatusOrder(document);
            
            foreach (var statusToTry in pdfOptimizedStatuses)
            {
                var s3Path = document.GetImagePath(statusToTry);
                var stream = await GetDocumentStreamAsync(bucketName, s3Path);
                if (stream != null)
                {
                    _logger.LogInformation("Document {DocumentId} stream found using status {Status} at path {S3Path}", 
                        document.Id, statusToTry, s3Path);
                    return stream;
                }
                else
                {
                    _logger.LogDebug("Document {DocumentId} not found at status {Status} path {S3Path}", 
                        document.Id, statusToTry, s3Path);
                }
            }

            _logger.LogWarning("Could not get stream for document {DocumentId} - no accessible versions found using PDF-optimized paths", document.Id);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting stream for document {DocumentId}", document.Id);
            return null;
        }
    }

    /// <summary>
    /// Check document existence across multiple status-based paths
    /// </summary>
    public async Task<Dictionary<ImageStatusTypeEnum, bool>> CheckDocumentExistenceAsync(DocumentModel document)
    {
        var results = new Dictionary<ImageStatusTypeEnum, bool>();
        var bucketName = GetEnvironmentBucketName(document.BucketPrefix);

        var statusesToCheck = new[]
        {
            ImageStatusTypeEnum.Production,
            ImageStatusTypeEnum.NeedsProcessing,
            ImageStatusTypeEnum.NeedsConversion,
            ImageStatusTypeEnum.NeedsImageManipulation,
            ImageStatusTypeEnum.Archived
        };

        foreach (var status in statusesToCheck)
        {
            try
            {
                var s3Path = document.GetImagePath(status);
                results[status] = await DocumentExistsAsync(bucketName, s3Path);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error checking existence for document {DocumentId} with status {Status}", 
                    document.Id, status);
                results[status] = false;
            }
        }

        return results;
    }

    /// <summary>
    /// Get environment-aware bucket name from BucketPrefix
    /// Supports UseProductionPaths override for development
    /// </summary>
    public string GetEnvironmentBucketName(string? bucketPrefix)
    {
        var bucketName = !string.IsNullOrEmpty(bucketPrefix) ? bucketPrefix : _defaultBucketName;
        
        // If UseProductionPaths is enabled, always use production bucket structure
        if (_useProductionPaths)
        {
            _logger.LogDebug("Using production paths override - bucket: {BucketName}", bucketName);
            return bucketName;
        }
        
        // For production environment, use bucket name as-is
        if (_environment.Equals("Production", StringComparison.OrdinalIgnoreCase))
        {
            return bucketName;
        }

        // For non-production, append environment path
        var environmentBucket = $"{bucketName}{_environmentPath}";
        _logger.LogDebug("Using environment-specific bucket: {EnvironmentBucket}", environmentBucket);
        return environmentBucket;
    }

    /// <summary>
    /// Try fallback resolution for document download
    /// </summary>
    private async Task<string?> TryFallbackResolution(DocumentModel document, bool useCache, string localPath, string bucketName)
    {
        var currentStatus = (ImageStatusTypeEnum)document.ImageStatusTypeId;
        var fallbackStatuses = GetFallbackStatuses(currentStatus);

        foreach (var fallbackStatus in fallbackStatuses)
        {
            try
            {
                var fallbackPath = document.GetImagePath(fallbackStatus);
                var downloaded = await DownloadFromS3Async(bucketName, fallbackPath, localPath, document.Id);
                if (downloaded)
                {
                    _logger.LogInformation("Document {DocumentId} resolved using fallback status {FallbackStatus}", 
                        document.Id, fallbackStatus);
                    return localPath;
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Fallback status {Status} failed for document {DocumentId}", 
                    fallbackStatus, document.Id);
            }
        }

        return null;
    }

    /// <summary>
    /// Get fallback statuses to try when primary status fails
    /// </summary>
    private List<ImageStatusTypeEnum> GetFallbackStatuses(ImageStatusTypeEnum primaryStatus)
    {
        return primaryStatus switch
        {
            ImageStatusTypeEnum.Production => 
                new List<ImageStatusTypeEnum> { ImageStatusTypeEnum.NeedsProcessing, ImageStatusTypeEnum.NeedsConversion },
            
            ImageStatusTypeEnum.NeedsProcessing => 
                new List<ImageStatusTypeEnum> { ImageStatusTypeEnum.Production, ImageStatusTypeEnum.NeedsConversion },
            
            ImageStatusTypeEnum.NeedsConversion or ImageStatusTypeEnum.NeedsImageManipulation => 
                new List<ImageStatusTypeEnum> { ImageStatusTypeEnum.Production, ImageStatusTypeEnum.NeedsProcessing },
            
            _ => new List<ImageStatusTypeEnum> { ImageStatusTypeEnum.Production, ImageStatusTypeEnum.NeedsProcessing, ImageStatusTypeEnum.NeedsConversion }
        };
    }

    /// <summary>
    /// Download file from S3 to local path
    /// </summary>
    private async Task<bool> DownloadFromS3Async(string bucketName, string s3Key, string localPath, int documentId)
    {
        try
        {
            // Ensure local directory exists
            var localDir = Path.GetDirectoryName(localPath);
            if (!string.IsNullOrEmpty(localDir) && !Directory.Exists(localDir))
            {
                Directory.CreateDirectory(localDir);
            }

            var request = new GetObjectRequest
            {
                BucketName = bucketName,
                Key = s3Key
            };

            using var response = await _s3Client.GetObjectAsync(request);
            using var fileStream = new FileStream(localPath, FileMode.Create, FileAccess.Write);
            await response.ResponseStream.CopyToAsync(fileStream);

            _logger.LogDebug("Successfully downloaded document {DocumentId} from s3://{Bucket}/{Key}", 
                documentId, bucketName, s3Key);
            return true;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            _logger.LogDebug("Document {DocumentId} not found in S3: s3://{Bucket}/{Key}", 
                documentId, bucketName, s3Key);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download document {DocumentId} from s3://{Bucket}/{Key}", 
                documentId, bucketName, s3Key);
            return false;
        }
    }

    /// <summary>
    /// Get PDF-optimized status order for web document viewing
    /// Prioritizes processed PDF versions over original office documents
    /// </summary>
    private List<ImageStatusTypeEnum> GetPdfOptimizedStatusOrder(DocumentModel document)
    {
        var currentStatus = (ImageStatusTypeEnum)document.ImageStatusTypeId;
        
        // For web viewing, always try PDF versions first regardless of current status
        var optimizedOrder = new List<ImageStatusTypeEnum>();
        
        // Priority 1: Production PDFs (best quality)
        optimizedOrder.Add(ImageStatusTypeEnum.Production);
        optimizedOrder.Add(ImageStatusTypeEnum.Archived);
        
        // Priority 2: Viewable PDFs (processed and viewable but may need additional work)
        optimizedOrder.Add(ImageStatusTypeEnum.NeedsDocType);
        optimizedOrder.Add(ImageStatusTypeEnum.NeedsIndexing);
        optimizedOrder.Add(ImageStatusTypeEnum.NeedsVerification);
        optimizedOrder.Add(ImageStatusTypeEnum.NeedsQC);
        optimizedOrder.Add(ImageStatusTypeEnum.NeedsOptimization);
        optimizedOrder.Add(ImageStatusTypeEnum.NeedsRelease);
        
        // Priority 3: Processing PDFs (converted but may need final processing)
        optimizedOrder.Add(ImageStatusTypeEnum.NeedsProcessing);
        
        // Priority 3: Current status (if not already included and if it's not an original file status)
        if (!optimizedOrder.Contains(currentStatus) && IsProcessedStatus(currentStatus))
        {
            optimizedOrder.Add(currentStatus);
        }
        
        // Priority 4: Original files (last resort - only for images or when no PDF exists)
        if (IsImageFile(document.OriginalExt) || !HasPdfFallbacks(optimizedOrder))
        {
            optimizedOrder.Add(ImageStatusTypeEnum.NeedsConversion);
            optimizedOrder.Add(ImageStatusTypeEnum.NeedsImageManipulation);
            
            // Add current status if it's an original file status
            if (!optimizedOrder.Contains(currentStatus))
            {
                optimizedOrder.Add(currentStatus);
            }
        }
        
        return optimizedOrder.Distinct().ToList();
    }

    /// <summary>
    /// Check if a status represents a processed (PDF) version
    /// </summary>
    private bool IsProcessedStatus(ImageStatusTypeEnum status)
    {
        return status switch
        {
            ImageStatusTypeEnum.Production or
            ImageStatusTypeEnum.NeedsProcessing or
            ImageStatusTypeEnum.NeedsDocType or
            ImageStatusTypeEnum.NeedsIndexing or
            ImageStatusTypeEnum.NeedsDocSplit or
            ImageStatusTypeEnum.NeedsTextExtraction or
            ImageStatusTypeEnum.NeedsVerification or
            ImageStatusTypeEnum.NeedsQC or
            ImageStatusTypeEnum.NeedsOptimization or
            ImageStatusTypeEnum.NeedsNoProcessing or
            ImageStatusTypeEnum.NeedsLoanAssignment or
            ImageStatusTypeEnum.NeedsRelease or
            ImageStatusTypeEnum.Archived => true,
            _ => false
        };
    }

    /// <summary>
    /// Check if the file extension represents an image file
    /// </summary>
    private bool IsImageFile(string? extension)
    {
        if (string.IsNullOrEmpty(extension)) return false;
        
        var ext = extension.ToLowerInvariant().TrimStart('.');
        return ext switch
        {
            "jpg" or "jpeg" or "png" or "gif" or "tiff" or "tif" or "bmp" => true,
            _ => false
        };
    }

    /// <summary>
    /// Check if the list contains any PDF-generating statuses
    /// </summary>
    private bool HasPdfFallbacks(List<ImageStatusTypeEnum> statuses)
    {
        return statuses.Any(IsProcessedStatus);
    }

    /// <summary>
    /// Get environment path suffix for bucket names
    /// </summary>
    private string GetEnvironmentPath(string environment)
    {
        return environment.ToLowerInvariant() switch
        {
            "production" => "",
            "prod" => "",
            _ => $"/Branches/{environment}"
        };
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
        // If path already includes known prefixes, use as-is
        if (documentPath.StartsWith("IOriginal/", StringComparison.OrdinalIgnoreCase) ||
            documentPath.StartsWith("IProd/", StringComparison.OrdinalIgnoreCase) ||
            documentPath.StartsWith("IProcessing/", StringComparison.OrdinalIgnoreCase) ||
            documentPath.StartsWith("IRedact/", StringComparison.OrdinalIgnoreCase))
        {
            return documentPath;
        }

        // Legacy fallback - prepend IOriginal/Images/
        return $"IOriginal/Images/{documentPath}";
    }
}
