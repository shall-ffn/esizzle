using Dapper;
using EsizzleAPI.Models;
using System.Data;

namespace EsizzleAPI.Repositories
{
    public class UserRepository : IUserRepository
    {
        private readonly IDbConnectionFactory _connectionFactory;
        private readonly ILogger<UserRepository> _logger;

        public UserRepository(IDbConnectionFactory connectionFactory, ILogger<UserRepository> logger)
        {
            _connectionFactory = connectionFactory;
            _logger = logger;
        }

        public async Task<SecureUser?> GetUserByEmailAsync(string email)
        {
            try
            {
                using var connection = _connectionFactory.CreateConnection();
                
                // Query the Users table by email
                const string sql = @"
                    SELECT 
                        UserID,
                        FirstName,
                        LastName,
                        UserEmail as Email,
                        UserName,
                        AccessLevel,
                        AuthLevel,
                        Active,
                        ClientID,
                        DateCreated,
                        LastLogin
                    FROM Users 
                    WHERE UserEmail = @Email 
                    AND Active = 1 
                    AND Deleted = 0";

                var user = await connection.QuerySingleOrDefaultAsync<SecureUser>(sql, new { Email = email });
                
                if (user != null)
                {
                    _logger.LogInformation("Found user by email {Email}: {UserName} (ID: {UserId})", 
                        email, user.Name, user.UserID);
                }
                else
                {
                    _logger.LogWarning("No user found for email: {Email}", email);
                }

                return user;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user by email: {Email}", email);
                throw;
            }
        }

        public async Task<SecureUser?> GetUserByIdAsync(int userId)
        {
            try
            {
                using var connection = _connectionFactory.CreateConnection();
                
                const string sql = @"
                    SELECT 
                        UserID,
                        FirstName,
                        LastName,
                        UserEmail as Email,
                        UserName,
                        AccessLevel,
                        AuthLevel,
                        Active,
                        ClientID,
                        DateCreated,
                        LastLogin
                    FROM Users 
                    WHERE UserID = @UserId 
                    AND Active = 1 
                    AND Deleted = 0";

                var user = await connection.QuerySingleOrDefaultAsync<SecureUser>(sql, new { UserId = userId });
                
                return user;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user by ID: {UserId}", userId);
                throw;
            }
        }

        public async Task<List<int>> GetUserOfferingAccessAsync(int userId)
        {
            try
            {
                using var connection = _connectionFactory.CreateConnection();
                
                // Get all offering IDs that this user has access to
                const string sql = @"
                    SELECT DISTINCT OfferingID
                    FROM OfferingUnderwriterAccess 
                    WHERE UserID = @UserId";

                var offeringIds = await connection.QueryAsync<int>(sql, new { UserId = userId });
                
                var result = offeringIds.ToList();
                _logger.LogInformation("User {UserId} has access to {Count} offerings", userId, result.Count);
                
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user offering access for user: {UserId}", userId);
                throw;
            }
        }

        public async Task<bool> VerifyPasswordAsync(int userId, string password)
        {
            // TODO: Implement password verification
            // This would typically involve checking a hashed password
            // For now, return true for development purposes
            await Task.CompletedTask;
            return true;
        }
    }
}