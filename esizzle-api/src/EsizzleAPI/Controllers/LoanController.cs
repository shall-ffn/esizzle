using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using EsizzleAPI.Middleware;
using EsizzleAPI.Models;
using EsizzleAPI.Repositories;

namespace EsizzleAPI.Controllers;

[ApiController]
[Route("api/v1/hydra/[controller]")]
// [Authorize] // Temporarily disabled to focus on authorization logic
public class LoanController : ControllerBase
{
    private readonly ILogger<LoanController> _logger;
    private readonly ILoanRepository _loanRepository;
    private readonly ISecurityRepository _securityRepository;

    public LoanController(
        ILogger<LoanController> logger,
        ILoanRepository loanRepository,
        ISecurityRepository securityRepository)
    {
        _logger = logger;
        _loanRepository = loanRepository;
        _securityRepository = securityRepository;
    }

    /// <summary>
    /// Get all loans for a specific sale
    /// </summary>
    [HttpGet("by-sale/{saleId:int}")]
    public async Task<IActionResult> GetLoansBySale(int saleId)
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

            // Check if user has access to this sale
            var hasAccess = await _securityRepository.HasSaleAccessAsync(authUser.Id, saleId);
            if (!hasAccess)
            {
                return StatusCode(403, "Access denied to this sale");
            }

            _logger.LogInformation("Getting loans for sale {SaleId} for user {UserId}", saleId, authUser.Id);

            var loans = await _loanRepository.GetBySaleIdAsync(saleId);

            return Ok(loans);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting loans for sale {SaleId}", saleId);
            return StatusCode(500, "An error occurred while retrieving loans");
        }
    }

    /// <summary>
    /// Search loans within a sale by asset name or number
    /// </summary>
    [HttpGet("by-sale/{saleId:int}/search")]
    public async Task<IActionResult> SearchLoansBySale(int saleId, [FromQuery] string searchTerm)
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

            // Check if user has access to this sale
            var hasAccess = await _securityRepository.HasSaleAccessAsync(authUser.Id, saleId);
            if (!hasAccess)
            {
                return StatusCode(403, "Access denied to this sale");
            }

            if (string.IsNullOrWhiteSpace(searchTerm) || searchTerm.Length < 2)
            {
                return BadRequest("Search term must be at least 2 characters");
            }

            _logger.LogInformation("Searching loans for sale {SaleId} with term '{SearchTerm}' for user {UserId}", 
                saleId, searchTerm, authUser.Id);

            var loans = await _loanRepository.SearchLoansAsync(saleId, searchTerm);

            return Ok(loans);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching loans for sale {SaleId}", saleId);
            return StatusCode(500, "An error occurred while searching loans");
        }
    }

    /// <summary>
    /// Get a specific loan by ID (if user has access)
    /// </summary>
    [HttpGet("{loanId:int}")]
    public async Task<IActionResult> GetLoan(int loanId)
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

            _logger.LogInformation("Getting loan {LoanId} for user {UserId}", loanId, authUser.Id);

            var loan = await _loanRepository.GetByIdAsync(loanId);
            if (loan == null)
            {
                return NotFound($"Loan {loanId} not found");
            }

            return Ok(loan);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting loan {LoanId}", loanId);
            return StatusCode(500, "An error occurred while retrieving the loan");
        }
    }
}
