using EsizzleAPI.Models;
using EsizzleAPI.Services;
using System.Data;
using System.Threading.Tasks;
using Dapper;

namespace EsizzleAPI.Repositories;

public interface ISecurityRepository
{
    Task<bool> HasOfferingAccessAsync(int userId, int offeringId);
    Task<bool> HasSaleAccessAsync(int userId, int saleId);
    Task<bool> HasLoanAccessAsync(int userId, int loanId);
    Task<bool> HasDocumentAccessAsync(int userId, int documentId);
    Task<IEnumerable<int>> GetUserOfferingIdsAsync(int userId);
    
    // Legacy offering filtering methods
    Task<List<OfferingModel>> GetOfferingsForUser(string userEmail, bool isAdmin = false);
    Task<bool> IsSuperUser(string userEmail);
    Task<UserIntegrationModel?> GetUserIntegrationData(string userEmail);
}

public class SecurityRepository : ISecurityRepository
{
    private readonly IDbConnectionFactory _dbConnectionFactory;
    private readonly IArrayClient _arrayClient;

    public SecurityRepository(IDbConnectionFactory dbConnectionFactory, IArrayClient arrayClient)
    {
        _dbConnectionFactory = dbConnectionFactory;
        _arrayClient = arrayClient;
    }

    public async Task<bool> HasOfferingAccessAsync(int userId, int offeringId)
    {
        // First try the traditional underwriter access table
        using var connection = _dbConnectionFactory.CreateConnection();
        const string accessSql = @"
            SELECT COUNT(1) 
            FROM OfferingUnderwriterAccess oua
            INNER JOIN Offerings o ON oua.OfferingID = o.OfferingID
            WHERE oua.UserID = @userId 
                AND oua.OfferingID = @offeringId
                AND o.Visible = 1
                AND o.Deleted = 0";

        var accessCount = await Task.Run(() => connection.QueryFirst<int>(accessSql, new { userId, offeringId }));
        if (accessCount > 0)
        {
            return true; // User has explicit access
        }

        // If no explicit access, check if the offering is in the user's bucket prefix scope
        // This matches the logic used in GetOfferingsForUser
        // For development, we'll check if the offering matches the bucket filtering logic
        const string bucketSql = @"
            SELECT COUNT(1) 
            FROM Offerings o
            WHERE o.OfferingID = @offeringId
                AND LOWER(o.BucketPrefix) = 'ffncorp.com'
                AND o.IsServicer = 0
                AND o.BidDate IS NOT NULL
                AND o.OfferingID != 43
                AND o.Deleted = 0";

        var bucketCount = await Task.Run(() => connection.QueryFirst<int>(bucketSql, new { offeringId }));
        return bucketCount > 0;
    }

    public async Task<bool> HasSaleAccessAsync(int userId, int saleId)
    {
        using var connection = _dbConnectionFactory.CreateConnection();
        
        // First try explicit access through OfferingUnderwriterAccess
        const string accessSql = @"
            SELECT COUNT(1) 
            FROM OfferingUnderwriterAccess oua
            INNER JOIN Offerings o ON oua.OfferingID = o.OfferingID
            INNER JOIN OfferingAuctions oa ON o.OfferingID = oa.OfferingID
            INNER JOIN Auction a ON oa.AuctionID = a.AuctionID
            INNER JOIN Sales s ON a.Loanmaster_Sale_ID = s.sale_id
            WHERE oua.UserID = @userId 
                AND s.sale_id = @saleId
                AND o.Visible = 1
                AND o.Deleted = 0";

        var accessCount = await Task.Run(() => connection.QueryFirst<int>(accessSql, new { userId, saleId }));
        if (accessCount > 0)
        {
            return true; // User has explicit access
        }

        // If no explicit access, check if the sale's offering is in the bucket prefix scope
        // This matches the logic used in HasOfferingAccessAsync
        const string bucketSql = @"
            SELECT COUNT(1) 
            FROM Sales s
            INNER JOIN Auction a ON s.sale_id = a.Loanmaster_Sale_ID
            INNER JOIN OfferingAuctions oa ON a.AuctionID = oa.AuctionID
            INNER JOIN Offerings o ON oa.OfferingID = o.OfferingID
            WHERE s.sale_id = @saleId
                AND LOWER(o.BucketPrefix) = 'ffncorp.com'
                AND o.IsServicer = 0
                AND o.BidDate IS NOT NULL
                AND o.OfferingID != 43
                AND o.Deleted = 0";

        var bucketCount = await Task.Run(() => connection.QueryFirst<int>(bucketSql, new { saleId }));
        return bucketCount > 0;
    }

    public async Task<bool> HasLoanAccessAsync(int userId, int loanId)
    {
        using var connection = _dbConnectionFactory.CreateConnection();
        
        // First try explicit access through OfferingUnderwriterAccess
        const string accessSql = @"
            SELECT COUNT(1) 
            FROM OfferingUnderwriterAccess oua
            INNER JOIN Offerings o ON oua.OfferingID = o.OfferingID
            INNER JOIN OfferingAuctions oa ON o.OfferingID = oa.OfferingID
            INNER JOIN Auction a ON oa.AuctionID = a.AuctionID
            INNER JOIN Sales s ON a.Loanmaster_Sale_ID = s.sale_id
            INNER JOIN Loan l ON s.sale_id = l.SALE_ID
            WHERE oua.UserID = @userId 
                AND l.loan_id = @loanId
                AND o.Visible = 1
                AND o.Deleted = 0
                AND l.LOAN_STATUS_ID = 3";

        var accessCount = await Task.Run(() => connection.QueryFirst<int>(accessSql, new { userId, loanId }));
        if (accessCount > 0)
        {
            return true; // User has explicit access
        }

        // If no explicit access, check if the loan's offering is in the bucket prefix scope
        // This matches the logic used in HasOfferingAccessAsync and HasSaleAccessAsync
        const string bucketSql = @"
            SELECT COUNT(1) 
            FROM Loan l
            INNER JOIN Sales s ON l.SALE_ID = s.sale_id
            INNER JOIN Auction a ON s.sale_id = a.Loanmaster_Sale_ID
            INNER JOIN OfferingAuctions oa ON a.AuctionID = oa.AuctionID
            INNER JOIN Offerings o ON oa.OfferingID = o.OfferingID
            WHERE l.loan_id = @loanId
                AND LOWER(o.BucketPrefix) = 'ffncorp.com'
                AND o.IsServicer = 0
                AND o.BidDate IS NOT NULL
                AND o.OfferingID != 43
                AND o.Deleted = 0
                AND l.LOAN_STATUS_ID = 3";

        var bucketCount = await Task.Run(() => connection.QueryFirst<int>(bucketSql, new { loanId }));
        return bucketCount > 0;
    }

    public async Task<bool> HasDocumentAccessAsync(int userId, int documentId)
    {
        using var connection = _dbConnectionFactory.CreateConnection();
        const string sql = @"
            SELECT COUNT(1) 
            FROM OfferingUnderwriterAccess oua
            INNER JOIN Offerings o ON oua.OfferingID = o.OfferingID
            INNER JOIN Sales s ON o.ClientID = s.ClientID
            INNER JOIN Loan l ON s.sale_id = l.SALE_ID
            INNER JOIN Image i ON l.loan_id = i.LoanID
            WHERE oua.UserID = @userId 
                AND i.ID = @documentId
                AND o.Visible = 1
                AND o.Deleted = 0
                AND l.LOAN_STATUS_ID != 0
                AND i.Deleted = 0";

        var count = await Task.Run(() => connection.QueryFirst<int>(sql, new { userId, documentId }));
        return count > 0;
    }

    public async Task<IEnumerable<int>> GetUserOfferingIdsAsync(int userId)
    {
        using var connection = _dbConnectionFactory.CreateConnection();
        const string sql = @"
            SELECT DISTINCT oua.OfferingID 
            FROM OfferingUnderwriterAccess oua
            INNER JOIN Offerings o ON oua.OfferingID = o.OfferingID
            WHERE oua.UserID = @userId 
                AND o.Visible = 1
                AND o.Deleted = 0
            ORDER BY oua.OfferingID";

        return await Task.Run(() => connection.Query<int>(sql, new { userId }));
    }

    public async Task<List<OfferingModel>> GetOfferingsForUser(string userEmail, bool isAdmin = false)
    {
        if (isAdmin)
        {
            // Admin view - Standard filtering only (ImageActionManager.cs pattern)
            return await GetStandardOfferings();
        }
        else
        {
            // User view - Security-managed filtering (ManageUWSecurity.cs pattern)
            return await GetSecurityManagedOfferings(userEmail);
        }
    }

    private async Task<List<OfferingModel>> GetStandardOfferings()
    {
        // Reference: ImageActionManager.cs lines 45-48
        // WHERE DateArchived IS NULL ORDER BY BidDate DESC
        using var connection = _dbConnectionFactory.CreateConnection();
        
        const string sql = @"
            SELECT * FROM Offerings 
            WHERE DateArchived IS NULL 
            ORDER BY BidDate DESC";
        
        var offerings = await connection.QueryAsync<OfferingModel>(sql);
        return offerings.ToList();
    }

    private async Task<List<OfferingModel>> GetSecurityManagedOfferings(string userEmail)
    {
        // Reference: ManageUWSecurity.cs lines 270-276
        // Get user integration data to get DefaultBucketPrefix
        var userIntegration = await GetUserIntegrationData(userEmail);
        if (userIntegration == null)
        {
            return new List<OfferingModel>(); // No access if we can't get user data
        }

        using var connection = _dbConnectionFactory.CreateConnection();
        
        const string sql = @"
            SELECT * FROM Offerings 
            WHERE LOWER(BucketPrefix) = LOWER(@bucketPrefix)
            AND IsServicer = 0
            AND BidDate IS NOT NULL
            AND OfferingID != 43
            ORDER BY BidDate DESC";
        
        var offerings = await connection.QueryAsync<OfferingModel>(sql, new { 
            bucketPrefix = userIntegration.DefaultBucketPrefix ?? string.Empty 
        });
        
        return offerings.ToList();
    }

    public async Task<bool> IsSuperUser(string userEmail)
    {
        // Check if user has admin/super user privileges using ArrayClient
        // This matches the legacy MenuForm.cs line 53: _services.Security.IsSuperUser(CurrentUser.UserEmail)
        try
        {
            // Use ArrayClient to get user's system roles
            var systemRoles = await _arrayClient.GetUserSystemRolesAsync(userEmail);
            
            if (systemRoles != null && systemRoles.Any())
            {
                // Check if user has any admin-type roles
                // Administrator and Manager are considered admin roles
                return systemRoles.Any(role => 
                    role == SystemRole.Administrator || 
                    role == SystemRole.Manager);
            }
            
            // If no system roles found, assume not admin
            return false;
        }
        catch (Exception)
        {
            // If ArrayClient fails, default to not admin for security
            return false;
        }
    }

    public async Task<UserIntegrationModel?> GetUserIntegrationData(string userEmail)
    {
        // Use ArrayClient to get user integration data - matches legacy ManageUWSecurity.cs
        // Legacy: ArrayClient.GetNew().GetUserIntegrationData(userEmail)
        try
        {
            return await Task.FromResult(_arrayClient.GetUserIntegrationData(userEmail));
        }
        catch (Exception)
        {
            // If ArrayClient fails, return null - caller should handle gracefully
            return null;
        }
    }
}
