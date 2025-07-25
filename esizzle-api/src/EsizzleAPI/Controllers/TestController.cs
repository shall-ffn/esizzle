using Microsoft.AspNetCore.Mvc;
using EsizzleAPI.Services;
using EsizzleAPI.Models;

namespace EsizzleAPI.Controllers
{
    /// <summary>
    /// Test controller for verifying ArrayClient integration
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class TestController : ControllerBase
    {
        private readonly IArrayClient _arrayClient;
        private readonly ILogger<TestController> _logger;

        public TestController(
            IArrayClient arrayClient,
            ILogger<TestController> logger)
        {
            _arrayClient = arrayClient;
            _logger = logger;
        }

        /// <summary>
        /// Test endpoint for ArrayClient user system roles
        /// </summary>
        [HttpGet("user-roles/{email}")]
        public IActionResult GetUserRoles(string email)
        {
            try
            {
                var roles = _arrayClient.GetUserSystemRoles(email);
                return Ok(new { Email = email, Roles = roles });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving user roles for {Email}", email);
                return StatusCode(500, "Error retrieving user roles");
            }
        }

        /// <summary>
        /// Test endpoint for ArrayClient projects
        /// </summary>
        [HttpGet("projects")]
        public IActionResult GetProjects()
        {
            try
            {
                var projects = _arrayClient.GetProjects();
                return Ok(new { ProjectCount = projects.Count, Projects = projects });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving projects");
                return StatusCode(500, "Error retrieving projects");
            }
        }

        /// <summary>
        /// Test endpoint for user integration data
        /// </summary>
        [HttpGet("user-integration/{email}")]
        public IActionResult GetUserIntegration(string email)
        {
            try
            {
                var userData = _arrayClient.GetUserIntegrationData(email);
                return Ok(userData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving user integration data for {Email}", email);
                return StatusCode(500, "Error retrieving user integration data");
            }
        }
    }
}
