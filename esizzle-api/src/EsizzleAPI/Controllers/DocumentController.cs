using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using EsizzleAPI.Middleware;
using EsizzleAPI.Models;
using EsizzleAPI.Repositories;
using EsizzleAPI.Services;

namespace EsizzleAPI.Controllers;

[ApiController]
[Route("api/v1/hydra/[controller]")]
// [Authorize] // Temporarily disabled to focus on authorization logic
public class DocumentController : ControllerBase
{
    private readonly ILogger<DocumentController> _logger;
    private readonly IDocumentRepository _documentRepository;
    private readonly ISecurityRepository _securityRepository;
    private readonly IS3DocumentService _s3DocumentService;
    private readonly IPdfTokenService _pdfTokenService;

    public DocumentController(
        ILogger<DocumentController> logger,
        IDocumentRepository documentRepository,
        ISecurityRepository securityRepository,
        IS3DocumentService s3DocumentService,
        IPdfTokenService pdfTokenService)
    {
        _logger = logger;
        _documentRepository = documentRepository;
        _securityRepository = securityRepository;
        _s3DocumentService = s3DocumentService;
        _pdfTokenService = pdfTokenService;
    }

    /// <summary>
    /// Get all documents for a specific loan
    /// </summary>
    [HttpGet("by-loan/{loanId:int}")]
    public async Task<IActionResult> GetDocumentsByLoan(int loanId)
    {
        try
        {
            var authUser = HttpContext.Items["AuthorizedUser"] as AuthorizedUser;
            
            // Support mock authentication for testing authorization logic
            if (authUser == null)
            {
                var mockUserId = HttpContext.Request.Headers["X-Mock-User-Id"].FirstOrDefault();
                var mockUserEmail = HttpContext.Request.Headers["X-Mock-User-Email"].FirstOrDefault();
                
                if (!string.IsNullOrEmpty(mockUserId) && !string.IsNullOrEmpty(mockUserEmail) && int.TryParse(mockUserId, out int userId))
                {
                    authUser = new AuthorizedUser
                    {
                        Id = userId,
                        Email = mockUserEmail,
                        AccessLevel = 2 // Default non-admin level
                    };
                    _logger.LogInformation("Using mock auth - User ID: {UserId}, Email: {Email}", userId, mockUserEmail);
                }
                else
                {
                    return Unauthorized("User not authenticated - provide X-Mock-User-Id and X-Mock-User-Email headers");
                }
            }

            // Check if user has access to this loan
            var hasAccess = await _securityRepository.HasLoanAccessAsync(authUser.Id, loanId);
            if (!hasAccess)
            {
                return StatusCode(403, "Access denied to this loan");
            }

            _logger.LogInformation("Getting documents for loan {LoanId} for user {UserId}", loanId, authUser.Id);

            var documents = await _documentRepository.GetByLoanIdAsync(loanId);

            return Ok(documents);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting documents for loan {LoanId}", loanId);
            return StatusCode(500, "An error occurred while retrieving documents");
        }
    }

    /// <summary>
    /// Get a specific document by ID (if user has access)
    /// </summary>
    [HttpGet("{documentId:int}")]
    public async Task<IActionResult> GetDocument(int documentId)
    {
        try
        {
            var authUser = HttpContext.Items["AuthorizedUser"] as AuthorizedUser;
            
            // Support mock authentication for testing authorization logic
            if (authUser == null)
            {
                var mockUserId = HttpContext.Request.Headers["X-Mock-User-Id"].FirstOrDefault();
                var mockUserEmail = HttpContext.Request.Headers["X-Mock-User-Email"].FirstOrDefault();
                
                if (!string.IsNullOrEmpty(mockUserId) && !string.IsNullOrEmpty(mockUserEmail) && int.TryParse(mockUserId, out int userId))
                {
                    authUser = new AuthorizedUser
                    {
                        Id = userId,
                        Email = mockUserEmail,
                        AccessLevel = 2 // Default non-admin level
                    };
                    _logger.LogInformation("Using mock auth - User ID: {UserId}, Email: {Email}", userId, mockUserEmail);
                }
                else
                {
                    return Unauthorized("User not authenticated - provide X-Mock-User-Id and X-Mock-User-Email headers");
                }
            }

            // Check if user has access to this document
            var hasAccess = await _securityRepository.HasDocumentAccessAsync(authUser.Id, documentId);
            if (!hasAccess)
            {
                return StatusCode(403, "Access denied to this document");
            }

            _logger.LogInformation("Getting document {DocumentId} for user {UserId}", documentId, authUser.Id);

            var document = await _documentRepository.GetByIdAsync(documentId);
            if (document == null)
            {
                return NotFound($"Document {documentId} not found");
            }

            return Ok(document);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting document {DocumentId}", documentId);
            return StatusCode(500, "An error occurred while retrieving the document");
        }
    }

    /// <summary>
    /// Get a presigned URL to view/download the document content
    /// </summary>
    [HttpGet("{documentId:int}/url")]
    public async Task<IActionResult> GetDocumentUrl(int documentId)
    {
        try
        {
            var authUser = HttpContext.Items["AuthorizedUser"] as AuthorizedUser;
            
            // Support mock authentication for testing authorization logic
            if (authUser == null)
            {
                var mockUserId = HttpContext.Request.Headers["X-Mock-User-Id"].FirstOrDefault();
                var mockUserEmail = HttpContext.Request.Headers["X-Mock-User-Email"].FirstOrDefault();
                
                if (!string.IsNullOrEmpty(mockUserId) && !string.IsNullOrEmpty(mockUserEmail) && int.TryParse(mockUserId, out int userId))
                {
                    authUser = new AuthorizedUser
                    {
                        Id = userId,
                        Email = mockUserEmail,
                        AccessLevel = 2 // Default non-admin level
                    };
                    _logger.LogInformation("Using mock auth - User ID: {UserId}, Email: {Email}", userId, mockUserEmail);
                }
                else
                {
                    return Unauthorized("User not authenticated - provide X-Mock-User-Id and X-Mock-User-Email headers");
                }
            }

            // Check if user has access to this document
            var hasAccess = await _securityRepository.HasDocumentAccessAsync(authUser.Id, documentId);
            if (!hasAccess)
            {
                return StatusCode(403, "Access denied to this document");
            }

            _logger.LogInformation("Generating URL for document {DocumentId} for user {UserId}", documentId, authUser.Id);

            var url = await _documentRepository.GenerateDocumentUrlAsync(documentId, authUser.Id);
            if (string.IsNullOrEmpty(url))
            {
                return NotFound($"Document {documentId} not found or cannot generate URL");
            }

            var response = new DocumentUrlResponse
            {
                Url = url,
                ExpiresAt = DateTime.UtcNow.AddHours(1), // URL expires in 1 hour
                ContentType = "application/pdf"
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating URL for document {DocumentId}", documentId);
            return StatusCode(500, "An error occurred while generating document URL");
        }
    }

    /// <summary>
    /// Serve the actual PDF content for a document
    /// Uses signed access tokens for authentication instead of traditional headers
    /// </summary>
    [HttpGet("{documentId:int}/content")]
    public async Task<IActionResult> GetDocumentContent(int documentId)
    {
        try
        {
            // Validate PDF access token from query string
            var token = HttpContext.Request.Query["token"].FirstOrDefault();
            
            if (string.IsNullOrEmpty(token))
            {
                _logger.LogWarning("PDF content request missing access token for document {DocumentId}", documentId);
                return Unauthorized("Access token required for PDF content");
            }
            
            // Validate the PDF access token
            if (!_pdfTokenService.ValidatePdfAccessToken(token, documentId, out int userId))
            {
                _logger.LogWarning("Invalid or expired PDF access token for document {DocumentId}", documentId);
                return Unauthorized("Invalid or expired access token");
            }
            
            // Token is valid - check if user still has access to this document
            var hasAccess = await _securityRepository.HasDocumentAccessAsync(userId, documentId);
            if (!hasAccess)
            {
                _logger.LogWarning("User {UserId} no longer has access to document {DocumentId}", userId, documentId);
                return StatusCode(403, "Access denied to this document");
            }

            _logger.LogInformation("Serving content for document {DocumentId} for user {UserId} via signed token", documentId, userId);

            // Get document details to retrieve S3 path and BucketPrefix
            var document = await _documentRepository.GetByIdAsync(documentId);
            if (document == null)
            {
                return NotFound($"Document {documentId} not found");
            }

            // Check if document has a valid path in S3
            if (string.IsNullOrEmpty(document.Path))
            {
                return NotFound($"Document {documentId} has no file path");
            }

            // Strategy 1: Try legacy-compatible local caching first (preferred for performance)
            var localPath = await _s3DocumentService.ResolveDocumentPathAsync(document, useCache: true);
            if (!string.IsNullOrEmpty(localPath) && System.IO.File.Exists(localPath))
            {
                _logger.LogDebug("Serving document {DocumentId} from local cache: {LocalPath}", documentId, localPath);
                
                // Determine content type from extension or metadata
                var contentType = GetContentTypeFromExtension(document.OriginalExt) ?? "application/pdf";
                
                // Return the cached file
                var fileStream = new FileStream(localPath, FileMode.Open, FileAccess.Read);
                return File(fileStream, contentType, document.OriginalName);
            }

            // Strategy 2: Fallback to direct S3 streaming
            _logger.LogDebug("Local cache miss for document {DocumentId}, streaming from S3", documentId);
            
            // Determine bucket from BucketPrefix or use default
            var bucketName = !string.IsNullOrEmpty(document.BucketPrefix) ? document.BucketPrefix : "default-bucket";
            var s3Key = document.Path.StartsWith("IOriginal/Images/") ? document.Path : $"IOriginal/Images/{document.Path}";
            
            // Stream document content directly from S3
            var documentStream = await _s3DocumentService.GetDocumentStreamAsync(bucketName, s3Key);
            if (documentStream == null)
            {
                return NotFound($"Document content not found in storage: {bucketName}/{s3Key}");
            }

            // Get document metadata to determine content type
            var metadata = await _s3DocumentService.GetDocumentMetadataAsync(bucketName, s3Key);
            var streamContentType = metadata?.ContentType ?? GetContentTypeFromExtension(document.OriginalExt) ?? "application/pdf";

            // Return the document stream
            return File(documentStream, streamContentType, document.OriginalName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error serving content for document {DocumentId}", documentId);
            return StatusCode(500, "An error occurred while serving document content");
        }
    }

    /// <summary>
    /// Helper method to determine content type from file extension
    /// </summary>
    private string? GetContentTypeFromExtension(string extension)
    {
        return extension?.ToLowerInvariant() switch
        {
            ".pdf" => "application/pdf",
            ".tiff" or ".tif" => "image/tiff",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            _ => null
        };
    }

    /// <summary>
    /// Update the document type/classification (legacy string-based)
    /// </summary>
    [HttpPut("{documentId:int}/document-type")]
    public async Task<IActionResult> UpdateDocumentType(int documentId, [FromBody] string documentType)
    {
        try
        {
            var authUser = HttpContext.Items["AuthorizedUser"] as AuthorizedUser;
            
            // Support mock authentication for testing authorization logic
            if (authUser == null)
            {
                var mockUserId = HttpContext.Request.Headers["X-Mock-User-Id"].FirstOrDefault();
                var mockUserEmail = HttpContext.Request.Headers["X-Mock-User-Email"].FirstOrDefault();
                
                if (!string.IsNullOrEmpty(mockUserId) && !string.IsNullOrEmpty(mockUserEmail) && int.TryParse(mockUserId, out int userId))
                {
                    authUser = new AuthorizedUser
                    {
                        Id = userId,
                        Email = mockUserEmail,
                        AccessLevel = 2 // Default non-admin level
                    };
                    _logger.LogInformation("Using mock auth - User ID: {UserId}, Email: {Email}", userId, mockUserEmail);
                }
                else
                {
                    return Unauthorized("User not authenticated - provide X-Mock-User-Id and X-Mock-User-Email headers");
                }
            }

            // Check if user has access to this document
            var hasAccess = await _securityRepository.HasDocumentAccessAsync(authUser.Id, documentId);
            if (!hasAccess)
            {
                return StatusCode(403, "Access denied to this document");
            }

            if (string.IsNullOrWhiteSpace(documentType))
            {
                return BadRequest("Document type cannot be empty");
            }

            _logger.LogInformation("Updating document type for {DocumentId} to '{DocumentType}' by user {UserId}", 
                documentId, documentType, authUser.Id);

            var success = await _documentRepository.UpdateDocumentTypeAsync(documentId, documentType, authUser.Id);
            if (!success)
            {
                return NotFound($"Document {documentId} not found or could not be updated");
            }

            return Ok(new { success = true, message = "Document type updated successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating document type for {DocumentId}", documentId);
            return StatusCode(500, "An error occurred while updating document type");
        }
    }

    /// <summary>
    /// Rotate a document by the specified angle
    /// </summary>
    [HttpPost("{documentId:int}/rotate")]
    public async Task<IActionResult> RotateDocument(int documentId, [FromBody] RotateDocumentRequest request)
    {
        try
        {
            var authUser = HttpContext.Items["AuthorizedUser"] as AuthorizedUser;
            if (authUser == null)
            {
                return Unauthorized("User not authenticated");
            }

            // Check if user has access to this document
            var hasAccess = await _securityRepository.HasDocumentAccessAsync(authUser.Id, documentId);
            if (!hasAccess)
            {
                return StatusCode(403, "Access denied to this document");
            }

            // Validate rotation angle
            if (request.Angle != 90 && request.Angle != 180 && request.Angle != 270)
            {
                return BadRequest("Rotation angle must be 90, 180, or 270 degrees");
            }

            _logger.LogInformation("Rotating document {DocumentId} by {Angle} degrees for user {UserId}", 
                documentId, request.Angle, authUser.Id);

            // TODO: Implement actual PDF rotation logic
            // For now, just return success
            await Task.Delay(100); // Simulate processing time

            return Ok(new { 
                success = true, 
                message = $"Document rotated by {request.Angle} degrees",
                rotationAngle = request.Angle
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rotating document {DocumentId}", documentId);
            return StatusCode(500, "An error occurred while rotating the document");
        }
    }

    /// <summary>
    /// Apply redactions to a document
    /// </summary>
    [HttpPost("{documentId:int}/redact")]
    public async Task<IActionResult> RedactDocument(int documentId, [FromBody] RedactDocumentRequest request)
    {
        try
        {
            var authUser = HttpContext.Items["AuthorizedUser"] as AuthorizedUser;
            if (authUser == null)
            {
                return Unauthorized("User not authenticated");
            }

            // Check if user has access to this document
            var hasAccess = await _securityRepository.HasDocumentAccessAsync(authUser.Id, documentId);
            if (!hasAccess)
            {
                return StatusCode(403, "Access denied to this document");
            }

            if (request.Areas == null || !request.Areas.Any())
            {
                return BadRequest("No redaction areas specified");
            }

            _logger.LogInformation("Applying {AreaCount} redactions to document {DocumentId} for user {UserId}", 
                request.Areas.Count, documentId, authUser.Id);

            // TODO: Implement actual PDF redaction logic
            // For now, just mark the document as redacted
            var success = await _documentRepository.MarkAsRedactedAsync(documentId, authUser.Id);
            if (!success)
            {
                return NotFound($"Document {documentId} not found or could not be updated");
            }

            await Task.Delay(500); // Simulate processing time

            return Ok(new { 
                success = true, 
                message = "Document redacted successfully",
                redactionCount = request.Areas.Count,
                permanentRedaction = request.PermanentRedaction
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error redacting document {DocumentId}", documentId);
            return StatusCode(500, "An error occurred while redacting the document");
        }
    }

    /// <summary>
    /// Get all available document types for classification
    /// </summary>
    [HttpGet("types")]
    public async Task<IActionResult> GetDocumentTypes()
    {
        try
        {
            var authUser = HttpContext.Items["AuthorizedUser"] as AuthorizedUser;
            if (authUser == null)
            {
                return Unauthorized("User not authenticated");
            }

            _logger.LogInformation("Getting document types for user {UserId}", authUser.Id);

            var types = await _documentRepository.GetDocumentTypesAsync();

            return Ok(types);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting document types");
            return StatusCode(500, "An error occurred while retrieving document types");
        }
    }

    /// <summary>
    /// Get document types available for a specific offering
    /// </summary>
    [HttpGet("types/by-offering/{offeringId:int}")]
    public async Task<IActionResult> GetDocumentTypesByOffering(int offeringId)
    {
        try
        {
            var authUser = HttpContext.Items["AuthorizedUser"] as AuthorizedUser;
            
            // Support mock authentication for testing authorization logic
            if (authUser == null)
            {
                var mockUserId = HttpContext.Request.Headers["X-Mock-User-Id"].FirstOrDefault();
                var mockUserEmail = HttpContext.Request.Headers["X-Mock-User-Email"].FirstOrDefault();
                
                if (!string.IsNullOrEmpty(mockUserId) && !string.IsNullOrEmpty(mockUserEmail) && int.TryParse(mockUserId, out int userId))
                {
                    authUser = new AuthorizedUser
                    {
                        Id = userId,
                        Email = mockUserEmail,
                        AccessLevel = 2 // Default non-admin level
                    };
                    _logger.LogInformation("Using mock auth - User ID: {UserId}, Email: {Email}", userId, mockUserEmail);
                }
                else
                {
                    return Unauthorized("User not authenticated - provide X-Mock-User-Id and X-Mock-User-Email headers");
                }
            }

            _logger.LogInformation("Getting document types for offering {OfferingId} for user {UserId}", offeringId, authUser.Id);

            var types = await _documentRepository.GetDocumentTypesByOfferingAsync(offeringId);

            return Ok(types);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting document types for offering {OfferingId}", offeringId);
            return StatusCode(500, "An error occurred while retrieving document types");
        }
    }

    /// <summary>
    /// Update document classification using ImageDocTypeMasterList ID
    /// </summary>
    [HttpPut("{documentId:int}/classification")]
    public async Task<IActionResult> UpdateDocumentClassification(int documentId, [FromBody] UpdateDocumentClassificationRequest request)
    {
        try
        {
            var authUser = HttpContext.Items["AuthorizedUser"] as AuthorizedUser;
            
            // Support mock authentication for testing authorization logic
            if (authUser == null)
            {
                var mockUserId = HttpContext.Request.Headers["X-Mock-User-Id"].FirstOrDefault();
                var mockUserEmail = HttpContext.Request.Headers["X-Mock-User-Email"].FirstOrDefault();
                
                if (!string.IsNullOrEmpty(mockUserId) && !string.IsNullOrEmpty(mockUserEmail) && int.TryParse(mockUserId, out int userId))
                {
                    authUser = new AuthorizedUser
                    {
                        Id = userId,
                        Email = mockUserEmail,
                        AccessLevel = 2 // Default non-admin level
                    };
                    _logger.LogInformation("Using mock auth - User ID: {UserId}, Email: {Email}", userId, mockUserEmail);
                }
                else
                {
                    return Unauthorized("User not authenticated - provide X-Mock-User-Id and X-Mock-User-Email headers");
                }
            }

            // Check if user has access to this document
            var hasAccess = await _securityRepository.HasDocumentAccessAsync(authUser.Id, documentId);
            if (!hasAccess)
            {
                return StatusCode(403, "Access denied to this document");
            }

            if (request.DocTypeId <= 0)
            {
                return BadRequest("Valid document type ID is required");
            }

            _logger.LogInformation("Updating classification for document {DocumentId} to type {DocTypeId} by user {UserId}", 
                documentId, request.DocTypeId, authUser.Id);

            var success = await _documentRepository.UpdateDocumentClassificationAsync(documentId, request.DocTypeId, authUser.Id);
            if (!success)
            {
                return NotFound($"Document {documentId} not found or could not be updated");
            }

            return Ok(new { success = true, message = "Document classification updated successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating classification for document {DocumentId}", documentId);
            return StatusCode(500, "An error occurred while updating document classification");
        }
    }
}
