using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using EsizzleAPI.Repositories;
using EsizzleAPI.DTOs;
using EsizzleAPI.Services;
using System.Security.Claims;

namespace EsizzleAPI.Controllers
{
    /// <summary>
    /// Controller for legacy indexing operations
    /// </summary>
    [ApiController]
    [Route("api/documents")]
    // [Authorize]
    public class IndexingController : ControllerBase
    {
        private readonly IIndexingRepository _indexingRepository;
        private readonly ILambdaService _lambdaService;
        private readonly ILogger<IndexingController> _logger;

        public IndexingController(
            IIndexingRepository indexingRepository,
            ILambdaService lambdaService,
            ILogger<IndexingController> logger)
        {
            _indexingRepository = indexingRepository;
            _lambdaService = lambdaService;
            _logger = logger;
        }

        private int GetUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(userIdClaim, out var userId) ? userId : 0;
        }

        // === DOCUMENT TYPES ===

        /// <summary>
        /// Get available document types filtered by offering
        /// </summary>
        [HttpGet("document-types")]
        public async Task<ActionResult<List<DocumentTypeDto>>> GetDocumentTypes(
            [FromQuery] int offeringId,
            [FromQuery] string? search = null)
        {
            try
            {
                var documentTypes = await _indexingRepository.GetDocumentTypesByOfferingAsync(offeringId, search);
                return Ok(documentTypes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get document types for offering {OfferingId}", offeringId);
                return StatusCode(500, new { message = "Failed to retrieve document types" });
            }
        }

        // === BOOKMARKS ===

        /// <summary>
        /// Get all bookmarks for a document
        /// </summary>
        [HttpGet("{documentId}/bookmarks")]
        public async Task<ActionResult<List<BookmarkDto>>> GetBookmarks(int documentId)
        {
            try
            {
                var bookmarks = await _indexingRepository.GetBookmarksByDocumentAsync(documentId);
                return Ok(bookmarks);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get bookmarks for document {DocumentId}", documentId);
                return StatusCode(500, new { message = "Failed to retrieve bookmarks" });
            }
        }

        /// <summary>
        /// Create a new bookmark
        /// </summary>
        [HttpPost("{documentId}/bookmarks")]
        public async Task<ActionResult<BookmarkDto>> CreateBookmark(
            int documentId, 
            [FromBody] CreateBookmarkRequest request)
        {
            try
            {
                // Validate request
                if (request.ImageId != documentId)
                {
                    return BadRequest(new { message = "Document ID mismatch" });
                }

                if (request.PageIndex < 0)
                {
                    return BadRequest(new { message = "Page index must be non-negative" });
                }

                // Skip document type validation for generic breaks (DocumentTypeId = -1)
                if (!request.IsGenericBreak)
                {
                    // Verify document type exists
                    var documentType = await _indexingRepository.GetDocumentTypeByIdAsync(request.DocumentTypeId);
                    if (documentType == null)
                    {
                        return BadRequest(new { message = "Invalid document type" });
                    }
                }

                var bookmark = await _indexingRepository.CreateBookmarkAsync(request, GetUserId());
                return CreatedAtAction(nameof(GetBookmarks), new { documentId }, bookmark);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create bookmark for document {DocumentId}", documentId);
                return StatusCode(500, new { message = "Failed to create bookmark" });
            }
        }

        /// <summary>
        /// Create a generic document break (no document type assigned)
        /// </summary>
        [HttpPost("{documentId}/generic-break")]
        public async Task<ActionResult<BookmarkDto>> CreateGenericBreak(
            int documentId,
            [FromBody] CreateGenericBreakRequest request)
        {
            try
            {
                // Validate request
                if (request.PageIndex < 0)
                {
                    return BadRequest(new { message = "Page index must be non-negative" });
                }

                var bookmark = await _indexingRepository.CreateGenericBreakAsync(documentId, request.PageIndex, GetUserId());
                return CreatedAtAction(nameof(GetBookmarks), new { documentId }, bookmark);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create generic break for document {DocumentId}", documentId);
                return StatusCode(500, new { message = "Failed to create generic break" });
            }
        }

        /// <summary>
        /// Update an existing bookmark
        /// </summary>
        [HttpPut("{documentId}/bookmarks/{bookmarkId}")]
        public async Task<ActionResult<BookmarkDto>> UpdateBookmark(
            int documentId,
            int bookmarkId,
            [FromBody] UpdateBookmarkRequest request)
        {
            try
            {
                // Verify document type exists
                var documentType = await _indexingRepository.GetDocumentTypeByIdAsync(request.DocumentTypeId);
                if (documentType == null)
                {
                    return BadRequest(new { message = "Invalid document type" });
                }

                var bookmark = await _indexingRepository.UpdateBookmarkAsync(documentId, bookmarkId, request);
                if (bookmark == null)
                {
                    return NotFound(new { message = "Bookmark not found or cannot be updated" });
                }

                return Ok(bookmark);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update bookmark {BookmarkId} for document {DocumentId}", bookmarkId, documentId);
                return StatusCode(500, new { message = "Failed to update bookmark" });
            }
        }

        /// <summary>
        /// Delete a bookmark (soft delete)
        /// </summary>
        [HttpDelete("{documentId}/bookmarks/{bookmarkId}")]
        public async Task<ActionResult> DeleteBookmark(int documentId, int bookmarkId)
        {
            try
            {
                var success = await _indexingRepository.DeleteBookmarkAsync(documentId, bookmarkId);
                if (!success)
                {
                    return NotFound(new { message = "Bookmark not found or cannot be deleted" });
                }

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete bookmark {BookmarkId} for document {DocumentId}", bookmarkId, documentId);
                return StatusCode(500, new { message = "Failed to delete bookmark" });
            }
        }

        // === PROCESSING ===

        /// <summary>
        /// Save image data only (no bookmarks)
        /// </summary>
        [HttpPost("{documentId}/save-image-data")]
        public async Task<ActionResult<ProcessingSessionDto>> SaveImageData(
            int documentId,
            [FromBody] DocumentMetadata metadata)
        {
            try
            {
                // Validate document type if provided
                if (metadata.DocumentTypeId.HasValue)
                {
                    var documentType = await _indexingRepository.GetDocumentTypeByIdAsync(metadata.DocumentTypeId.Value);
                    if (documentType == null)
                    {
                        return BadRequest(new { message = "Invalid document type" });
                    }
                }

                var session = await _indexingRepository.SaveImageDataAsync(documentId, metadata, GetUserId());
                return Ok(session);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save image data for document {DocumentId}", documentId);
                return StatusCode(500, new { message = "Failed to save image data" });
            }
        }

        /// <summary>
        /// Create a processing session for document processing
        /// </summary>
        [HttpPost("{documentId}/create-processing-session")]
        public async Task<ActionResult<ProcessingSessionDto>> CreateProcessingSession(
            int documentId,
            [FromBody] ProcessBookmarksRequest request)
        {
            _logger.LogInformation("CreateProcessingSession called for document {DocumentId} with {BookmarkCount} bookmarks", 
                documentId, request?.Bookmarks?.Count ?? 0);
            
            try
            {
                // Validate bookmarks exist and are valid
                var bookmarkIds = request.Bookmarks.Select(b => b.BookmarkId).ToList();
                var validation = await _indexingRepository.ValidateBookmarksAsync(documentId, bookmarkIds);

                if (!validation.IsValid)
                {
                    return BadRequest(new { 
                        message = "Bookmark validation failed",
                        errors = validation.Errors,
                        warnings = validation.Warnings
                    });
                }

                // Determine processing type based on bookmark count and positions
                string processingType;
                if (request.Bookmarks.Count == 0)
                {
                    processingType = "SimpleIndexing";
                }
                else if (request.Bookmarks.Count == 1 && request.Bookmarks[0].PageIndex == 0)
                {
                    processingType = "IndexOnly";
                }
                else
                {
                    processingType = "DocumentSplitting";
                }

                // Create processing session
                var session = await _indexingRepository.CreateProcessingSessionAsync(
                    documentId, processingType, GetUserId());

                _logger.LogInformation("Created processing session {SessionId} for document {DocumentId}", 
                    session.SessionId, documentId);

                return Ok(session);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create processing session for document {DocumentId}", documentId);
                return StatusCode(500, new { message = "Failed to create processing session" });
            }
        }

        /// <summary>
        /// Start processing for an existing session
        /// </summary>
        [HttpPost("{documentId}/start-processing/{sessionId}")]
        public async Task<ActionResult> StartProcessing(
            int documentId,
            string sessionId,
            [FromBody] ProcessBookmarksRequest request)
        {
            try
            {
                // Verify session exists (using lightweight validation with enhanced error handling)
                try
                {
                    var sessionExists = await _indexingRepository.SessionExistsAsync(sessionId);
                    if (!sessionExists)
                    {
                        _logger.LogWarning("Processing session {SessionId} not found during StartProcessing", sessionId);
                        return NotFound(new { message = "Processing session not found" });
                    }
                    
                    _logger.LogInformation("Session {SessionId} validated successfully, starting processing", sessionId);
                }
                catch (Exception dbEx)
                {
                    _logger.LogError(dbEx, "Database error during session validation for {SessionId}: {Error}", 
                        sessionId, dbEx.Message);
                    return StatusCode(500, new { 
                        message = "Database error during session validation", 
                        sessionId = sessionId,
                        detail = dbEx.Message 
                    });
                }

                // Determine processing type based on bookmark count
                string processingType;
                if (request.Bookmarks.Count == 0)
                {
                    processingType = "SimpleIndexing";
                }
                else if (request.Bookmarks.Count == 1 && request.Bookmarks[0].PageIndex == 0)
                {
                    processingType = "IndexOnly";
                }
                else
                {
                    processingType = "DocumentSplitting";
                }

                if (processingType == "DocumentSplitting")
                {
                    var lambdaPayload = new
                    {
                        documentId = documentId,
                        sessionId = sessionId,
                        operation = "split_document",
                        bookmarks = request.Bookmarks.Select(b => new
                        {
                            bookmarkId = b.BookmarkId,
                            pageIndex = b.PageIndex,
                            documentTypeId = b.DocumentTypeId,
                            documentTypeName = b.DocumentTypeName,
                            documentDate = b.DocumentDate,
                            comments = b.Comments
                        }).ToList(),
                        metadata = new 
                        {
                            userId = GetUserId(),
                            documentDate = request.DocumentMetadata?.DocumentDate,
                            comments = request.DocumentMetadata?.Comments,
                            documentTypeId = request.DocumentMetadata?.DocumentTypeId
                        }
                    };

                    await _lambdaService.InvokeAsync("pdf-processor", lambdaPayload);
                    _logger.LogInformation("Queued document splitting for session {SessionId}", sessionId);
                }
                else
                {
                    // For simple indexing, update immediately
                    var metadata = request.DocumentMetadata ?? new DocumentMetadata();
                    if (request.Bookmarks.Any())
                    {
                        var bookmark = request.Bookmarks.First();
                        metadata.DocumentTypeId = bookmark.DocumentTypeId;
                        metadata.DocumentDate = bookmark.DocumentDate;
                        metadata.Comments = bookmark.Comments;
                    }

                    await _indexingRepository.UpdateDocumentTypeAsync(
                        documentId, metadata.DocumentTypeId, metadata.DocumentDate, metadata.Comments);
                    
                    await _indexingRepository.UpdateProcessingSessionAsync(sessionId, "Completed");
                    _logger.LogInformation("Completed simple indexing for session {SessionId}", sessionId);
                }

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start processing for session {SessionId}", sessionId);
                return StatusCode(500, new { message = "Failed to start processing" });
            }
        }

        /// <summary>
        /// Process bookmarks (rename or split) - Legacy endpoint for backward compatibility
        /// </summary>
        [HttpPost("{documentId}/process-bookmarks")]
        public async Task<ActionResult<ProcessingSessionDto>> ProcessBookmarks(
            int documentId,
            [FromBody] ProcessBookmarksRequest request)
        {
            _logger.LogInformation("ProcessBookmarks called for document {DocumentId} with {BookmarkCount} bookmarks", 
                documentId, request?.Bookmarks?.Count ?? 0);
            
            try
            {
                // Use two-step approach for better session management
                // Step 1: Create processing session
                var createSessionResponse = await CreateProcessingSession(documentId, request);
                if (createSessionResponse.Result is not OkObjectResult sessionResult || 
                    sessionResult.Value is not ProcessingSessionDto session)
                {
                    return createSessionResponse;
                }

                // Step 2: Start processing
                var startProcessingResponse = await StartProcessing(documentId, session.SessionId, request);
                if (startProcessingResponse is not NoContentResult)
                {
                    // If start processing failed, mark session as failed
                    await _indexingRepository.UpdateProcessingSessionAsync(session.SessionId, "Failed", 
                        "Failed to start processing after session creation");
                    return StatusCode(500, new { message = "Failed to start processing after session creation" });
                }

                return Ok(session);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process bookmarks for document {DocumentId}", documentId);
                return StatusCode(500, new { message = "Failed to process bookmarks" });
            }
        }

        /// <summary>
        /// Get processing session status
        /// </summary>
        [HttpGet("processing/{sessionId}/status")]
        public async Task<ActionResult<ProcessingSessionDto>> GetProcessingStatus(string sessionId)
        {
            try
            {
                var session = await _indexingRepository.GetProcessingSessionAsync(sessionId);
                if (session == null)
                {
                    return NotFound(new { message = "Processing session not found" });
                }

                return Ok(session);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get processing status for session {SessionId}", sessionId);
                return StatusCode(500, new { message = "Failed to get processing status" });
            }
        }

        /// <summary>
        /// Get processing results for a document
        /// </summary>
        [HttpGet("{documentId}/processing-results")]
        public async Task<ActionResult<List<ProcessingResultDto>>> GetProcessingResults(int documentId)
        {
            try
            {
                var results = await _indexingRepository.GetProcessingResultsAsync(documentId);
                return Ok(results);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get processing results for document {DocumentId}", documentId);
                return StatusCode(500, new { message = "Failed to get processing results" });
            }
        }

        // === THUMBNAILS ===

        /// <summary>
        /// Get page thumbnails for document
        /// </summary>
        [HttpGet("{documentId}/thumbnails")]
        public async Task<ActionResult<List<PageThumbnailDto>>> GetThumbnails(int documentId)
        {
            try
            {
                var thumbnails = await _indexingRepository.GetThumbnailsAsync(documentId);
                return Ok(thumbnails);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get thumbnails for document {DocumentId}", documentId);
                return StatusCode(500, new { message = "Failed to get thumbnails" });
            }
        }

        /// <summary>
        /// Get bookmark indicators for thumbnails
        /// </summary>
        [HttpGet("{documentId}/bookmarks/thumbnails")]
        public async Task<ActionResult<List<ThumbnailBookmarkDto>>> GetThumbnailBookmarks(int documentId)
        {
            try
            {
                var bookmarks = await _indexingRepository.GetThumbnailBookmarksAsync(documentId);
                return Ok(bookmarks);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get thumbnail bookmarks for document {DocumentId}", documentId);
                return StatusCode(500, new { message = "Failed to get thumbnail bookmarks" });
            }
        }

        // === INTERNAL PROCESSING ENDPOINTS ===

        /// <summary>
        /// Internal endpoint for lambda to update processing status
        /// </summary>
        [HttpPut("processing/{sessionId}/status")]
        [AllowAnonymous] // Lambda function access
        public async Task<ActionResult> UpdateProcessingStatus(
            string sessionId,
            [FromBody] UpdateProcessingStatusRequest request)
        {
            try
            {
                var success = await _indexingRepository.UpdateProcessingSessionAsync(
                    sessionId, request.Status, request.ErrorMessage);

                if (!success)
                {
                    return NotFound(new { message = "Processing session not found" });
                }

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update processing status for session {SessionId}", sessionId);
                return StatusCode(500, new { message = "Failed to update processing status" });
            }
        }

        /// <summary>
        /// Internal endpoint for lambda to link processed documents
        /// </summary>
        [HttpPost("{documentId}/link-results")]
        [AllowAnonymous] // Lambda function access
        public async Task<ActionResult> LinkProcessingResults(
            int documentId,
            [FromBody] LinkResultsRequest request)
        {
            try
            {
                foreach (var result in request.Results)
                {
                    if (result.BookmarkId.HasValue)
                    {
                        await _indexingRepository.LinkBookmarkToResultAsync(result.BookmarkId.Value, result.ResultImageId);
                    }
                }

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to link processing results for document {DocumentId}", documentId);
                return StatusCode(500, new { message = "Failed to link processing results" });
            }
        }
    }

    // === REQUEST/RESPONSE MODELS ===

    public class UpdateProcessingStatusRequest
    {
        public string Status { get; set; } = string.Empty;
        public string? ErrorMessage { get; set; }
    }

    public class LinkResultsRequest
    {
        public List<ProcessingResult> Results { get; set; } = new();
        public DateTime Timestamp { get; set; }
        public int TotalResults { get; set; }
    }

    public class ProcessingResult
    {
        public int OriginalImageId { get; set; }
        public int ResultImageId { get; set; }
        public int StartPage { get; set; }
        public int EndPage { get; set; }
        public int PageCount { get; set; }
        public int DocumentTypeId { get; set; }
        public string DocumentTypeName { get; set; } = string.Empty;
        public string Filename { get; set; } = string.Empty;
        public string ProcessingStatus { get; set; } = string.Empty;
        public int? BookmarkId { get; set; } // Optional for generic breaks
    }

    public class CreateGenericBreakRequest
    {
        public int PageIndex { get; set; }  // 0-based page index
    }

    // === LAMBDA SERVICE INTERFACE ===

    public interface ILambdaService
    {
        Task InvokeAsync(string functionName, object payload);
    }
}
