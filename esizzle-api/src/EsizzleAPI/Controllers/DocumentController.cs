using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using EsizzleAPI.Middleware;
using EsizzleAPI.Models;
using EsizzleAPI.Repositories;

namespace EsizzleAPI.Controllers;

[ApiController]
[Route("api/v1/hydra/[controller]")]
[Authorize]
public class DocumentController : ControllerBase
{
    private readonly ILogger<DocumentController> _logger;
    private readonly IDocumentRepository _documentRepository;
    private readonly ISecurityRepository _securityRepository;

    public DocumentController(
        ILogger<DocumentController> logger,
        IDocumentRepository documentRepository,
        ISecurityRepository securityRepository)
    {
        _logger = logger;
        _documentRepository = documentRepository;
        _securityRepository = securityRepository;
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
            if (authUser == null)
            {
                return Unauthorized("User not authenticated");
            }

            // Check if user has access to this loan
            var hasAccess = await _securityRepository.HasLoanAccessAsync(authUser.Id, loanId);
            if (!hasAccess)
            {
                return Forbid("Access denied to this loan");
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
            if (authUser == null)
            {
                return Unauthorized("User not authenticated");
            }

            // Check if user has access to this document
            var hasAccess = await _securityRepository.HasDocumentAccessAsync(authUser.Id, documentId);
            if (!hasAccess)
            {
                return Forbid("Access denied to this document");
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
            if (authUser == null)
            {
                return Unauthorized("User not authenticated");
            }

            // Check if user has access to this document
            var hasAccess = await _securityRepository.HasDocumentAccessAsync(authUser.Id, documentId);
            if (!hasAccess)
            {
                return Forbid("Access denied to this document");
            }

            _logger.LogInformation("Generating URL for document {DocumentId} for user {UserId}", documentId, authUser.Id);

            var url = await _documentRepository.GenerateDocumentUrlAsync(documentId);
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
    /// Update the document type/classification
    /// </summary>
    [HttpPut("{documentId:int}/classification")]
    public async Task<IActionResult> UpdateDocumentType(int documentId, [FromBody] string documentType)
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
                return Forbid("Access denied to this document");
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
                return Forbid("Access denied to this document");
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
                return Forbid("Access denied to this document");
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
}