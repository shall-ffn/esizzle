using Dapper;
using EsizzleAPI.Models;
using EsizzleAPI.Services;

namespace EsizzleAPI.Repositories;

public class OfferingRepository : IOfferingRepository
{
    private readonly IDbConnectionFactory _dbConnectionFactory;
    private readonly IArrayClient _arrayClient;
    private readonly ILogger<OfferingRepository> _logger;

    public OfferingRepository(
        IDbConnectionFactory dbConnectionFactory, 
        IArrayClient arrayClient,
        ILogger<OfferingRepository> logger)
    {
        _dbConnectionFactory = dbConnectionFactory;
        _arrayClient = arrayClient;
        _logger = logger;
    }

    public async Task<IEnumerable<OfferingSummaryModel>> GetUserOfferingsAsync(int userId)
    {
        using var connection = _dbConnectionFactory.CreateConnection();
        
        // First get the user's email for ArrayClient lookup
        const string userEmailSql = "SELECT UserEmail FROM Users WHERE UserID = @userId AND Active = 1 AND Deleted = 0";
        var userEmail = await connection.QueryFirstOrDefaultAsync<string>(userEmailSql, new { userId });
        
        if (string.IsNullOrEmpty(userEmail))
        {
            _logger.LogWarning("User not found or inactive: {UserId}", userId);
            return Enumerable.Empty<OfferingSummaryModel>();
        }

        // Check if user is administrator using ArrayClient (matches legacy IsSuperUser logic)
        bool isAdministrator = await _arrayClient.IsUserAdministratorAsync(userEmail);
        _logger.LogInformation("User {Email} (ID: {UserId}) administrator status: {IsAdmin}", userEmail, userId, isAdministrator);
        
        string sql;
        
        if (isAdministrator)
        {
            // Administrator - return all visible offerings (bypass access control like legacy)
            _logger.LogInformation("Administrator access: returning all offerings for user {Email}", userEmail);
            sql = @"
                SELECT 
                    o.OfferingID as OfferingId,
                    o.OfferingName,
                    o.OfferingDescription,
                    o.DueDiligenceStart,
                    o.DueDiligenceEnd,
                    o.Visible,
                    o.DDIsLive,
                    COUNT(DISTINCT s.sale_id) as SalesCount,
                    COUNT(DISTINCT l.loan_id) as LoansCount
                FROM Offerings o
                LEFT JOIN Sales s ON o.ClientID = s.ClientID
                LEFT JOIN Loan l ON s.sale_id = l.SALE_ID AND l.LOAN_STATUS_ID != 0
                WHERE o.Visible = 1 AND o.Deleted = 0
                GROUP BY o.OfferingID, o.OfferingName, o.OfferingDescription, 
                         o.DueDiligenceStart, o.DueDiligenceEnd, o.Visible, o.DDIsLive
                ORDER BY o.OfferingName";
                
            return await connection.QueryAsync<OfferingSummaryModel>(sql);
        }
        else
        {
            // Regular user - check OfferingUnderwriterAccess (like legacy)
            _logger.LogInformation("Regular user access: checking OfferingUnderwriterAccess for user {Email}", userEmail);
            sql = @"
                SELECT 
                    o.OfferingID as OfferingId,
                    o.OfferingName,
                    o.OfferingDescription,
                    o.DueDiligenceStart,
                    o.DueDiligenceEnd,
                    o.Visible,
                    o.DDIsLive,
                    COUNT(DISTINCT s.sale_id) as SalesCount,
                    COUNT(DISTINCT l.loan_id) as LoansCount
                FROM Offerings o
                INNER JOIN OfferingUnderwriterAccess oua ON o.OfferingID = oua.OfferingID
                LEFT JOIN Sales s ON o.ClientID = s.ClientID
                LEFT JOIN Loan l ON s.sale_id = l.SALE_ID AND l.LOAN_STATUS_ID != 0
                WHERE oua.UserID = @userId 
                    AND o.Visible = 1
                    AND o.Deleted = 0
                GROUP BY o.OfferingID, o.OfferingName, o.OfferingDescription, 
                         o.DueDiligenceStart, o.DueDiligenceEnd, o.Visible, o.DDIsLive
                ORDER BY o.OfferingName";
                
            return await connection.QueryAsync<OfferingSummaryModel>(sql, new { userId });
        }
    }

    public async Task<OfferingModel?> GetByIdAsync(int offeringId)
    {
        using var connection = _dbConnectionFactory.CreateConnection();
        const string sql = @"
            SELECT 
                OfferingID as OfferingId,
                OfferingNumber,
                OfferingName,
                OfferingHeader,
                OfferingDescription,
                BidDate,
                ClientID as ClientId,
                OrderBy,
                OfferingLogoURL as OfferingLogoUrl,
                OfferingFolder,
                AnnouncementHeader1,
                AnnouncementHeader2,
                Visible,
                DueDiligenceStart,
                DueDiligenceEnd,
                CurrentOfferingsHeader,
                CurrentOfferingsVisible,
                RequiresPreQualified,
                ConfidentialityAgreementID as ConfidentialityAgreementId,
                SecondConfidentialityAgreementID as SecondConfidentialityAgreementId,
                CutOffPackageAvailable,
                ActiveForAccessLevel,
                OfferingStatus_ID as OfferingStatusId,
                OfferingType_ID as OfferingTypeId,
                ShowOnHomepage,
                IsServicer,
                BidDateTimeZone,
                AskAssetQuestions,
                ClosingDate,
                PoolPrefix,
                AutoGrantDD,
                IsTest,
                Amount,
                IsNotable,
                ShowInPreviousOfferingsList,
                CanOrderHD,
                UseWebDD,
                ShowImagesDisclaimer,
                AnnounceDate,
                ConstellationProjectID as ConstellationProjectId,
                BidDepositAmount,
                CollectWireBidInfo,
                DDUseDocFolders,
                DefaultLoanView,
                ForceAffiliateEntity,
                AllowComboBids,
                DateArchived,
                DynamicDataDownloadEnabled,
                DefaultLoanDataSort,
                BidTypeID as BidTypeId,
                IndexCode,
                ExpandSummaryDocFolders,
                DDIsLive,
                BucketPrefix,
                OfflineArchiveDate,
                ShowOfferingStatistics,
                BpoProjectID as BpoProjectId,
                Deleted
            FROM Offerings 
            WHERE OfferingID = @offeringId AND Deleted = 0";

        return await connection.QueryFirstOrDefaultAsync<OfferingModel>(sql, new { offeringId });
    }

    public async Task<bool> HasUserAccessAsync(int userId, int offeringId)
    {
        using var connection = _dbConnectionFactory.CreateConnection();
        const string sql = @"
            SELECT COUNT(1) 
            FROM OfferingUnderwriterAccess oua
            INNER JOIN Offerings o ON oua.OfferingID = o.OfferingID
            WHERE oua.UserID = @userId 
                AND oua.OfferingID = @offeringId
                AND o.Visible = 1
                AND o.Deleted = 0";

        var count = await connection.QueryFirstAsync<int>(sql, new { userId, offeringId });
        return count > 0;
    }

    public async Task<IEnumerable<OfferingSummaryModel>> GetVisibleOfferingsAsync()
    {
        using var connection = _dbConnectionFactory.CreateConnection();
        const string sql = @"
            SELECT 
                o.OfferingID as OfferingId,
                o.OfferingName,
                o.OfferingDescription,
                o.DueDiligenceStart,
                o.DueDiligenceEnd,
                o.Visible,
                o.DDIsLive,
                COUNT(DISTINCT s.sale_id) as SalesCount,
                COUNT(DISTINCT l.loan_id) as LoansCount
            FROM Offerings o
            LEFT JOIN Sales s ON o.ClientID = s.ClientID
            LEFT JOIN Loan l ON s.sale_id = l.SALE_ID AND l.LOAN_STATUS_ID != 0
            WHERE o.Visible = 1 AND o.Deleted = 0
            GROUP BY o.OfferingID, o.OfferingName, o.OfferingDescription, 
                     o.DueDiligenceStart, o.DueDiligenceEnd, o.Visible, o.DDIsLive
            ORDER BY o.OfferingName";

        return await connection.QueryAsync<OfferingSummaryModel>(sql);
    }
}