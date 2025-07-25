using EsizzleAPI.Models;
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
}

public class SecurityRepository : ISecurityRepository
{
    private readonly IDbConnectionFactory _dbConnectionFactory;

    public SecurityRepository(IDbConnectionFactory dbConnectionFactory)
    {
        _dbConnectionFactory = dbConnectionFactory;
    }

    public async Task<bool> HasOfferingAccessAsync(int userId, int offeringId)
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

        var count = await Task.Run(() => connection.QueryFirst<int>(sql, new { userId, offeringId }));
        return count > 0;
    }

    public async Task<bool> HasSaleAccessAsync(int userId, int saleId)
    {
        using var connection = _dbConnectionFactory.CreateConnection();
        const string sql = @"
            SELECT COUNT(1) 
            FROM OfferingUnderwriterAccess oua
            INNER JOIN Offerings o ON oua.OfferingID = o.OfferingID
            INNER JOIN Sales s ON o.ClientID = s.ClientID
            WHERE oua.UserID = @userId 
                AND s.sale_id = @saleId
                AND o.Visible = 1
                AND o.Deleted = 0";

        var count = await Task.Run(() => connection.QueryFirst<int>(sql, new { userId, saleId }));
        return count > 0;
    }

    public async Task<bool> HasLoanAccessAsync(int userId, int loanId)
    {
        using var connection = _dbConnectionFactory.CreateConnection();
        const string sql = @"
            SELECT COUNT(1) 
            FROM OfferingUnderwriterAccess oua
            INNER JOIN Offerings o ON oua.OfferingID = o.OfferingID
            INNER JOIN Sales s ON o.ClientID = s.ClientID
            INNER JOIN Loan l ON s.sale_id = l.SALE_ID
            WHERE oua.UserID = @userId 
                AND l.loan_id = @loanId
                AND o.Visible = 1
                AND o.Deleted = 0
                AND l.LOAN_STATUS_ID != 0";

        var count = await Task.Run(() => connection.QueryFirst<int>(sql, new { userId, loanId }));
        return count > 0;
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
}