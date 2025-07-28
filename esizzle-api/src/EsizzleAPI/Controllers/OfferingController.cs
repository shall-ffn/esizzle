using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using EsizzleAPI.Middleware;
using EsizzleAPI.Models;
using EsizzleAPI.Repositories;

namespace EsizzleAPI.Controllers;

[ApiController]
[Route("api/v1/hydra/[controller]")]
// [Authorize] // Temporarily removed for mock auth testing
public class OfferingController : ControllerBase
{
    private readonly ILogger<OfferingController> _logger;
    private readonly IOfferingRepository _offeringRepository;
    private readonly ISecurityRepository _securityRepository;

    public OfferingController(
        ILogger<OfferingController> logger,
        IOfferingRepository offeringRepository,
        ISecurityRepository securityRepository)
    {
        _logger = logger;
        _offeringRepository = offeringRepository;
        _securityRepository = securityRepository;
    }

    /// <summary>
    /// Get all offerings that the current user has access to using legacy filtering logic
    /// </summary>
    [HttpGet("user-offerings")]
    public async Task<IActionResult> GetUserOfferings()
    {
        try
        {
            var authUser = HttpContext.Items["AuthorizedUser"] as AuthorizedUser;
            string userEmail;
            
            if (authUser == null)
            {
                // Fallback to mock auth headers in development
                var mockUserEmail = HttpContext.Request.Headers["X-Mock-User-Email"].FirstOrDefault();
                if (string.IsNullOrEmpty(mockUserEmail))
                {
                    return Unauthorized("User authentication required");
                }
                
                userEmail = mockUserEmail;
                _logger.LogInformation("Using mock auth - Getting offerings for user {Email}", userEmail);
            }
            else
            {
                userEmail = authUser.Email;
                _logger.LogInformation("Getting offerings for authenticated user {Email}", userEmail);
            }
            
            // Check if user is admin/super user (legacy logic)
            var isAdmin = await _securityRepository.IsSuperUser(userEmail);
            _logger.LogInformation("User {Email} admin status: {IsAdmin}", userEmail, isAdmin);
            
            // Get offerings using legacy filtering logic
            var offerings = await _securityRepository.GetOfferingsForUser(userEmail, isAdmin);
            
            _logger.LogInformation("Retrieved {Count} offerings for user {Email} (admin: {IsAdmin})", 
                offerings.Count, userEmail, isAdmin);
            
            return Ok(offerings);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user offerings");
            return StatusCode(500, "An error occurred while retrieving offerings");
        }
    }

    /// <summary>
    /// Get a specific offering by ID (if user has access)
    /// </summary>
    [HttpGet("{offeringId:int}")]
    public async Task<IActionResult> GetOffering(int offeringId)
    {
        try
        {
            var authUser = HttpContext.Items["AuthorizedUser"] as AuthorizedUser;
            if (authUser == null)
            {
                return Unauthorized("User not authenticated");
            }

            // Check if user has access to this offering
            var hasAccess = await _securityRepository.HasOfferingAccessAsync(authUser.Id, offeringId);
            if (!hasAccess)
            {
                return Forbid("Access denied to this offering");
            }

            _logger.LogInformation("Getting offering {OfferingId} for user {UserId}", offeringId, authUser.Id);

            var offering = await _offeringRepository.GetByIdAsync(offeringId);
            if (offering == null)
            {
                return NotFound($"Offering {offeringId} not found");
            }

            return Ok(offering);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting offering {OfferingId}", offeringId);
            return StatusCode(500, "An error occurred while retrieving the offering");
        }
    }

    /// <summary>
    /// Get all visible offerings (for admin users)
    /// </summary>
    [HttpGet("all")]
    public async Task<IActionResult> GetAllOfferings()
    {
        try
        {
            var authUser = HttpContext.Items["AuthorizedUser"] as AuthorizedUser;
            if (authUser == null)
            {
                return Unauthorized("User not authenticated");
            }

            // Only allow admin users (AccessLevel 1) to see all offerings
            if (authUser.AccessLevel > 1)
            {
                return Forbid("Insufficient access level");
            }

            _logger.LogInformation("Getting all offerings for admin user {UserId}", authUser.Id);

            var offerings = await _offeringRepository.GetVisibleOfferingsAsync();

            return Ok(offerings);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all offerings");
            return StatusCode(500, "An error occurred while retrieving offerings");
        }
    }
}