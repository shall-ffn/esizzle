using Microsoft.AspNetCore.Mvc;
using EsizzleAPI.Models;
using EsizzleAPI.Repositories;
using EsizzleAPI.Middleware;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace EsizzleAPI.Controllers
{
    /// <summary>
    /// Authentication controller for user login and token management
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IUserRepository _userRepository;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AuthController> _logger;

        public AuthController(
            IUserRepository userRepository, 
            IConfiguration configuration,
            ILogger<AuthController> logger)
        {
            _userRepository = userRepository;
            _configuration = configuration;
            _logger = logger;
        }

        /// <summary>
        /// Authenticate user by email and password
        /// </summary>
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.Email) || string.IsNullOrEmpty(request.Password))
                {
                    return BadRequest("Email and password are required");
                }

                _logger.LogInformation("Login attempt for email: {Email}", request.Email);

                // Look up user by email in SecureUser table
                var user = await _userRepository.GetUserByEmailAsync(request.Email);
                
                if (user == null)
                {
                    _logger.LogWarning("User not found for email: {Email}", request.Email);
                    return Unauthorized("Invalid email or password");
                }

                _logger.LogInformation("Found user: {UserName} (ID: {UserId})", user.Name, user.UserID);

                // For development - accept any password for now
                // In production, you'd verify the password here
                var isDevelopment = _configuration.GetValue<bool>("Development:AcceptAnyPassword", true);
                if (!isDevelopment)
                {
                    // TODO: Implement proper password verification
                    // var passwordValid = await _userRepository.VerifyPasswordAsync(user.UserID, request.Password);
                    // if (!passwordValid) return Unauthorized("Invalid email or password");
                }

                // Generate JWT token
                var token = GenerateJwtToken(user);

                // Return authentication response
                var response = new AuthenticationResponse
                {
                    Success = true,
                    Token = token,
                    User = new UserInfo
                    {
                        UserID = user.UserID,
                        Name = user.Name,
                        Email = user.Email,
                        UserName = user.UserName,
                        IsSuperUser = user.IsSuperUser
                    },
                    ExpiresAt = DateTime.UtcNow.AddHours(8)
                };

                _logger.LogInformation("Login successful for user: {UserName}", user.Name);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during login for email: {Email}", request.Email);
                return StatusCode(500, "An error occurred during login");
            }
        }

        /// <summary>
        /// Get current authenticated user information
        /// </summary>
        [HttpGet("me")]
        public async Task<IActionResult> GetCurrentUser()
        {
            try
            {
                var authUser = HttpContext.Items["AuthorizedUser"] as AuthorizedUser;
                if (authUser == null)
                {
                    return Unauthorized("User not authenticated");
                }

                var user = await _userRepository.GetUserByIdAsync(authUser.Id);
                if (user == null)
                {
                    return NotFound("User not found");
                }

                var userInfo = new UserInfo
                {
                    UserID = user.UserID,
                    Name = user.Name,
                    Email = user.Email,
                    UserName = user.UserName,
                    IsSuperUser = user.IsSuperUser
                };

                return Ok(userInfo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting current user");
                return StatusCode(500, "An error occurred while retrieving user information");
            }
        }

        private string GenerateJwtToken(SecureUser user)
        {
            var jwtSettings = _configuration.GetSection("JwtSettings");
            var secretKey = jwtSettings["SecretKey"] ?? "your-super-secret-jwt-key-for-development";
            var issuer = jwtSettings["Issuer"] ?? "EsizzleAPI";
            var audience = jwtSettings["Audience"] ?? "EsizzleApp";

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.UserID.ToString()),
                new Claim(ClaimTypes.Name, user.Name ?? ""),
                new Claim(ClaimTypes.Email, user.Email ?? ""),
                new Claim("username", user.UserName ?? ""),
                new Claim("is_super_user", user.IsSuperUser.ToString())
            };

            var token = new JwtSecurityToken(
                issuer: issuer,
                audience: audience,
                claims: claims,
                expires: DateTime.UtcNow.AddHours(8),
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }

    public class LoginRequest
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public class AuthenticationResponse
    {
        public bool Success { get; set; }
        public string Token { get; set; } = string.Empty;
        public UserInfo User { get; set; } = new UserInfo();
        public DateTime ExpiresAt { get; set; }
    }

    public class UserInfo
    {
        public int UserID { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public bool IsSuperUser { get; set; }
    }
}