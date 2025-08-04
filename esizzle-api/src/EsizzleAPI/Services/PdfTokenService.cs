using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace EsizzleAPI.Services;

/// <summary>
/// Service for generating and validating time-limited PDF access tokens
/// Provides secure, time-limited access to PDF content without requiring full authentication context
/// </summary>
public class PdfTokenService : IPdfTokenService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<PdfTokenService> _logger;
    private readonly string _secretKey;

    public PdfTokenService(IConfiguration configuration, ILogger<PdfTokenService> logger)
    {
        _configuration = configuration;
        _logger = logger;
        
        // Use the same secret key as JWT authentication for consistency
        _secretKey = Environment.GetEnvironmentVariable("TokenSecretKey") ?? 
                    _configuration["TokenSecretKey"] ?? 
                    "4R27CP2zd//HL2TXVbIhI+304UM2IMhetUXhJRcbYgg=";
    }

    public string GeneratePdfAccessToken(int documentId, int userId, TimeSpan expiry)
    {
        try
        {
            var payload = new PdfTokenPayload
            {
                DocumentId = documentId,
                UserId = userId,
                ExpiresAt = DateTimeOffset.UtcNow.Add(expiry).ToUnixTimeSeconds(),
                IssuedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            };

            var payloadJson = JsonSerializer.Serialize(payload);
            var payloadBytes = Encoding.UTF8.GetBytes(payloadJson);
            
            // Generate HMAC signature for security
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_secretKey));
            var signature = hmac.ComputeHash(payloadBytes);
            
            // Combine payload and signature
            var tokenData = new byte[payloadBytes.Length + signature.Length];
            Array.Copy(payloadBytes, 0, tokenData, 0, payloadBytes.Length);
            Array.Copy(signature, 0, tokenData, payloadBytes.Length, signature.Length);
            
            var token = Convert.ToBase64String(tokenData);
            
            _logger.LogDebug("Generated PDF access token for document {DocumentId}, user {UserId}, expires in {Expiry}",
                documentId, userId, expiry);
                
            return token;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating PDF access token for document {DocumentId}, user {UserId}",
                documentId, userId);
            throw;
        }
    }

    public bool ValidatePdfAccessToken(string token, int documentId, out int userId)
    {
        userId = 0;
        
        try
        {
            if (string.IsNullOrEmpty(token))
                return false;

            // URL-decode the token first (Base64 chars get URL-encoded in query params)
            var decodedToken = Uri.UnescapeDataString(token);
            
            _logger.LogDebug("Token validation - Original: '{OriginalToken}' (length: {OriginalLength})", 
                token, token.Length);
            _logger.LogDebug("Token validation - Decoded: '{DecodedToken}' (length: {DecodedLength})", 
                decodedToken, decodedToken.Length);
            
            // Validate Base64 format before attempting to decode
            if (!IsValidBase64String(decodedToken))
            {
                _logger.LogWarning("Token is not valid Base64 format after URL decoding for document {DocumentId}", documentId);
                return false;
            }
            
            var tokenData = Convert.FromBase64String(decodedToken);
            
            // Token should have payload + 32-byte HMAC signature
            if (tokenData.Length < 32)
                return false;

            var payloadLength = tokenData.Length - 32;
            var payloadBytes = new byte[payloadLength];
            var signature = new byte[32];
            
            Array.Copy(tokenData, 0, payloadBytes, 0, payloadLength);
            Array.Copy(tokenData, payloadLength, signature, 0, 32);
            
            // Verify HMAC signature
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_secretKey));
            var expectedSignature = hmac.ComputeHash(payloadBytes);
            
            if (!signature.SequenceEqual(expectedSignature))
            {
                _logger.LogWarning("Invalid PDF access token signature for document {DocumentId}", documentId);
                return false;
            }
            
            // Parse payload
            var payloadJson = Encoding.UTF8.GetString(payloadBytes);
            var payload = JsonSerializer.Deserialize<PdfTokenPayload>(payloadJson);
            
            if (payload == null)
                return false;
            
            // Validate document ID
            if (payload.DocumentId != documentId)
            {
                _logger.LogWarning("PDF access token document ID mismatch. Expected: {ExpectedId}, Got: {ActualId}",
                    documentId, payload.DocumentId);
                return false;
            }
            
            // Check expiration
            var currentTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if (payload.ExpiresAt <= currentTimestamp)
            {
                _logger.LogWarning("PDF access token expired for document {DocumentId}. Expired at: {ExpiresAt}, Current: {Current}",
                    documentId, payload.ExpiresAt, currentTimestamp);
                return false;
            }
            
            userId = payload.UserId;
            
            _logger.LogDebug("Successfully validated PDF access token for document {DocumentId}, user {UserId}",
                documentId, userId);
                
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating PDF access token for document {DocumentId}", documentId);
            return false;
        }
    }
    
    /// <summary>
    /// Validates if a string is in valid Base64 format
    /// </summary>
    private static bool IsValidBase64String(string s)
    {
        if (string.IsNullOrEmpty(s))
            return false;
            
        // Base64 length must be multiple of 4
        if (s.Length % 4 != 0)
            return false;
            
        // Check for valid Base64 characters only
        return System.Text.RegularExpressions.Regex.IsMatch(s, @"^[A-Za-z0-9+/]*={0,2}$");
    }

    /// <summary>
    /// Internal structure for PDF token payload
    /// </summary>
    private class PdfTokenPayload
    {
        public int DocumentId { get; set; }
        public int UserId { get; set; }
        public long ExpiresAt { get; set; }
        public long IssuedAt { get; set; }
    }
}
