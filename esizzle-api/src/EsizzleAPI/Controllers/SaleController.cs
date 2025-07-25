using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using EsizzleAPI.Middleware;
using EsizzleAPI.Models;
using EsizzleAPI.Repositories;

namespace EsizzleAPI.Controllers;

[ApiController]
[Route("api/v1/hydra/[controller]")]
[Authorize]
public class SaleController : ControllerBase
{
    private readonly ILogger<SaleController> _logger;
    private readonly ISaleRepository _saleRepository;
    private readonly ISecurityRepository _securityRepository;

    public SaleController(
        ILogger<SaleController> logger,
        ISaleRepository saleRepository,
        ISecurityRepository securityRepository)
    {
        _logger = logger;
        _saleRepository = saleRepository;
        _securityRepository = securityRepository;
    }

    /// <summary>
    /// Get all sales for a specific offering
    /// </summary>
    [HttpGet("by-offering/{offeringId:int}")]
    public async Task<IActionResult> GetSalesByOffering(int offeringId)
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

            _logger.LogInformation("Getting sales for offering {OfferingId} for user {UserId}", offeringId, authUser.Id);

            var sales = await _saleRepository.GetByOfferingIdAsync(offeringId);

            return Ok(sales);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting sales for offering {OfferingId}", offeringId);
            return StatusCode(500, "An error occurred while retrieving sales");
        }
    }

    /// <summary>
    /// Get a specific sale by ID (if user has access)
    /// </summary>
    [HttpGet("{saleId:int}")]
    public async Task<IActionResult> GetSale(int saleId)
    {
        try
        {
            var authUser = HttpContext.Items["AuthorizedUser"] as AuthorizedUser;
            if (authUser == null)
            {
                return Unauthorized("User not authenticated");
            }

            // Check if user has access to this sale
            var hasAccess = await _securityRepository.HasSaleAccessAsync(authUser.Id, saleId);
            if (!hasAccess)
            {
                return Forbid("Access denied to this sale");
            }

            _logger.LogInformation("Getting sale {SaleId} for user {UserId}", saleId, authUser.Id);

            var sale = await _saleRepository.GetByIdAsync(saleId);
            if (sale == null)
            {
                return NotFound($"Sale {saleId} not found");
            }

            return Ok(sale);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting sale {SaleId}", saleId);
            return StatusCode(500, "An error occurred while retrieving the sale");
        }
    }
}