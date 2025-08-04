namespace EsizzleAPI.Services;

/// <summary>
/// Service for generating and validating time-limited PDF access tokens
/// </summary>
public interface IPdfTokenService
{
    /// <summary>
    /// Generates a time-limited access token for PDF content
    /// </summary>
    /// <param name="documentId">Document ID</param>
    /// <param name="userId">User ID</param>
    /// <param name="expiry">Token expiration time</param>
    /// <returns>Base64-encoded signed token</returns>
    string GeneratePdfAccessToken(int documentId, int userId, TimeSpan expiry);

    /// <summary>
    /// Validates a PDF access token and extracts user information
    /// </summary>
    /// <param name="token">Token to validate</param>
    /// <param name="documentId">Expected document ID</param>
    /// <param name="userId">Extracted user ID if valid</param>
    /// <returns>True if token is valid and not expired</returns>
    bool ValidatePdfAccessToken(string token, int documentId, out int userId);
}
