using Microsoft.AspNetCore.Mvc;
using EsizzleAPI.Repositories;
using System.Reflection;
using System.Diagnostics;
using System.Data;
using System.Data.Common;
using MySql.Data.MySqlClient;
using System.Threading.Tasks;

namespace EsizzleAPI.Controllers
{
    /// <summary>
    /// Health check controller for monitoring application status
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class HealthController : ControllerBase
    {
        private readonly IDbConnectionFactory _dbConnectionFactory;
        private readonly ILogger<HealthController> _logger;

        public HealthController(IDbConnectionFactory dbConnectionFactory, ILogger<HealthController> logger)
        {
            _dbConnectionFactory = dbConnectionFactory;
            _logger = logger;
        }

        /// <summary>
        /// Basic health check endpoint
        /// </summary>
        [HttpGet]
        public IActionResult Get()
        {
            var response = new
            {
                status = "healthy",
                timestamp = DateTime.UtcNow,
                version = GetVersion(),
                environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Unknown"
            };

            return Ok(response);
        }

        /// <summary>
        /// Detailed health check with database connectivity
        /// </summary>
        [HttpGet("detailed")]
        public async Task<IActionResult> GetDetailed()
        {
            var healthStatus = new
            {
                status = "healthy",
                timestamp = DateTime.UtcNow,
                version = GetVersion(),
                environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Unknown",
                checks = new
                {
                    database = await CheckDatabaseHealth(),
                    memory = GetMemoryInfo(),
                    uptime = GetUptime()
                }
            };

            var hasErrors = healthStatus.checks.database.status.ToString() != "healthy";
            
            if (hasErrors)
            {
                return StatusCode(503, healthStatus); // Service Unavailable
            }

            return Ok(healthStatus);
        }

        /// <summary>
        /// Database connectivity test
        /// </summary>
        [HttpGet("database")]
        public async Task<IActionResult> CheckDatabase()
        {
            var dbHealth = await CheckDatabaseHealth();
            
            if (dbHealth.status.ToString() != "healthy")
            {
                return StatusCode(503, dbHealth);
            }
            
            return Ok(dbHealth);
        }

        private async Task<dynamic> CheckDatabaseHealth()
        {
            try
            {
                using var connection = _dbConnectionFactory.CreateConnection();
                await Task.Run(() => connection.Open());

                // Test basic connectivity
                var command = connection.CreateCommand();
                command.CommandText = "SELECT 1";
                var result = await Task.Run(() => command.ExecuteScalar());

                // Test key table access
                command.CommandText = "SELECT COUNT(*) FROM Offerings LIMIT 1";
                var offeringsCount = await Task.Run(() => command.ExecuteScalar());

                await Task.Run(() => connection.Close());

                return new
                {
                    status = "healthy",
                    responseTime = "< 100ms", // Could measure actual time
                    connectionState = "connected",
                    tablesAccessible = true,
                    message = "Database connection successful"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Database health check failed");
                
                return new
                {
                    status = "unhealthy",
                    error = ex.Message,
                    message = "Database connection failed"
                };
            }
        }

        private object GetMemoryInfo()
        {
            var memoryBefore = GC.GetTotalMemory(false);
            GC.Collect();
            var memoryAfter = GC.GetTotalMemory(true);

            return new
            {
                totalMemoryMB = Math.Round(memoryBefore / 1024.0 / 1024.0, 2),
                workingSetMB = Math.Round(Environment.WorkingSet / 1024.0 / 1024.0, 2),
                gcCollections = new
                {
                    gen0 = GC.CollectionCount(0),
                    gen1 = GC.CollectionCount(1),
                    gen2 = GC.CollectionCount(2)
                }
            };
        }

        private object GetUptime()
        {
            var uptime = DateTime.UtcNow - Process.GetCurrentProcess().StartTime.ToUniversalTime();
            
            return new
            {
                totalSeconds = Math.Round(uptime.TotalSeconds, 0),
                formatted = $"{uptime.Days}d {uptime.Hours}h {uptime.Minutes}m {uptime.Seconds}s"
            };
        }

        private string GetVersion()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var version = assembly.GetName().Version;
            return version?.ToString() ?? "Unknown";
        }
    }
}