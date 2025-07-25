using Dapper;
using EsizzleAPI.Models;

namespace EsizzleAPI.Repositories;

public class LoanRepository : ILoanRepository
{
    private readonly IDbConnectionFactory _dbConnectionFactory;

    public LoanRepository(IDbConnectionFactory dbConnectionFactory)
    {
        _dbConnectionFactory = dbConnectionFactory;
    }

    public async Task<IEnumerable<LoanSummaryModel>> GetBySaleIdAsync(int saleId)
    {
        using var connection = _dbConnectionFactory.CreateConnection();
        const string sql = @"
            SELECT 
                l.loan_id as LoanId,
                l.ASSET_NO as AssetNo,
                l.ASSET_NAME as AssetName,
                l.ASSET_NAME2 as AssetName2,
                l.BOOK_BALANCE as BookBalance,
                l.LOADED_ON as LoadedOn,
                l.SALE_ID as SaleId,
                l.LOAN_STATUS_ID as LoanStatusId,
                COUNT(DISTINCT i.ID) as DocumentCount,
                MAX(i.DateCreated) as LastDocumentDate
            FROM Loan l
            LEFT JOIN Image i ON l.loan_id = i.LoanID AND i.Deleted = 0
            WHERE l.SALE_ID = @saleId 
                AND l.LOAN_STATUS_ID != 0
            GROUP BY l.loan_id, l.ASSET_NO, l.ASSET_NAME, l.ASSET_NAME2, 
                     l.BOOK_BALANCE, l.LOADED_ON, l.SALE_ID, l.LOAN_STATUS_ID
            ORDER BY l.ASSET_NAME, l.ASSET_NO";

        return await connection.QueryAsync<LoanSummaryModel>(sql, new { saleId });
    }

    public async Task<LoanModel?> GetByIdAsync(int loanId)
    {
        using var connection = _dbConnectionFactory.CreateConnection();
        const string sql = @"
            SELECT 
                loan_id as LoanId,
                ASSET_NO as AssetNo,
                ASSET_NAME as AssetName,
                ASSET_NAME2 as AssetName2,
                ACCRUAL_CODE as AccrualCode,
                ACCRUAL_COMMENT as AccrualComment,
                ACCRUAL_STATUS as AccrualStatus,
                ADD_CODE as AddCode,
                ADD_COMMENT as AddComment,
                ADDED_ON as AddedOn,
                INVESTOR_ADDED_ON as InvestorAddedOn,
                ADD_SHIPPED_ON as AddShippedOn,
                ALLOCATED_PCT as AllocatedPct,
                BANK,
                BOOK_BALANCE as BookBalance,
                BOX,
                CHARGED_OFF_ON as ChargedOffOn,
                IS_CHARGED_OFF as IsChargedOff,
                COST_CODE as CostCode,
                COST_CENTER as CostCenter,
                FICO_SCORE as FicoScore,
                FICO_SCORE_ON as FicoScoreOn,
                GENERAL_LEDGER_BANK as GeneralLedgerBank,
                HIERARCHY,
                APPLIED_INTEREST as AppliedInterest,
                LITIGATION_REPORT_ON as LitigationReportOn,
                LOADED_ON as LoadedOn,
                NET_CHARGE_OFF_AMT as NetChargeOffAmt,
                NET_PCT as NetPct,
                NET_RETURN as NetReturn,
                NOTE,
                OBLIGOR_ID as ObligorId,
                OFFICER_NO as OfficerNo,
                IS_ON_STRAT as IsOnStrat,
                POOL_ID as PoolId,
                POOL_ID_PREVIOUS as PoolIdPrevious,
                POOL_CHANGED_ON as PoolChangedOn,
                PULL_CODE as PullCode,
                PULL_COMMENT as PullComment,
                PULLED_ON as PulledOn,
                INVESTOR_PULLED_ON as InvestorPulledOn,
                PULL_SHIPPED_ON as PullShippedOn,
                REGION,
                REGION_LEAD as RegionLead,
                RESERVE_PCT as ReservePct,
                SALE_ID as SaleId,
                SYSTEM_CODE as SystemCode,
                UNDERWRITTEN_BY as UnderwrittenBy,
                VALUE_LOCK as ValueLock,
                LEGAL_BALANCE as LegalBalance,
                LOAN_STATUS_ID as LoanStatusId,
                OFFICER_NAME as OfficerName,
                INTEREST_RATE as InterestRate,
                ORIGINAL_NOTE_AMT as OriginalNoteAmt,
                ORIGINATED_ON as OriginatedOn,
                MATURES_ON as MaturesOn,
                CURRENT_NOTE_AMT as CurrentNoteAmt,
                CURRENT_BANK_NAME as CurrentBankName,
                PAYOFF_AMT as PayoffAmt,
                PAYOFF_AS_OF as PayoffAsOf,
                PRINCIPLE_BALANCE as PrincipleBalance,
                PAST_DUE_AMT as PastDueAmt,
                PAST_DUE_ON as PastDueOn,
                DAYS_PAST_DUE as DaysPastDue,
                LOAN_TYPE as LoanType,
                IS_PERFORMING as IsPerforming,
                IS_SECURED as IsSecured,
                IS_UNDERWRITTEN as IsUnderwritten,
                UNDERWRITTEN_ON as UnderwrittenOn,
                FFN_NO as FfnNo,
                COLLATERAL_DESCRIPTION as CollateralDescription,
                NoteComments
            FROM Loan 
            WHERE loan_id = @loanId";

        return await connection.QueryFirstOrDefaultAsync<LoanModel>(sql, new { loanId });
    }

    public async Task<bool> BelongsToSaleAsync(int loanId, int saleId)
    {
        using var connection = _dbConnectionFactory.CreateConnection();
        const string sql = "SELECT COUNT(1) FROM Loan WHERE loan_id = @loanId AND SALE_ID = @saleId";

        var count = await connection.QueryFirstAsync<int>(sql, new { loanId, saleId });
        return count > 0;
    }

    public async Task<IEnumerable<LoanSummaryModel>> SearchLoansAsync(int saleId, string searchTerm)
    {
        using var connection = _dbConnectionFactory.CreateConnection();
        const string sql = @"
            SELECT 
                l.loan_id as LoanId,
                l.ASSET_NO as AssetNo,
                l.ASSET_NAME as AssetName,
                l.ASSET_NAME2 as AssetName2,
                l.BOOK_BALANCE as BookBalance,
                l.LOADED_ON as LoadedOn,
                l.SALE_ID as SaleId,
                l.LOAN_STATUS_ID as LoanStatusId,
                COUNT(DISTINCT i.ID) as DocumentCount,
                MAX(i.DateCreated) as LastDocumentDate
            FROM Loan l
            LEFT JOIN Image i ON l.loan_id = i.LoanID AND i.Deleted = 0
            WHERE l.SALE_ID = @saleId 
                AND l.LOAN_STATUS_ID != 0
                AND (l.ASSET_NAME LIKE @searchPattern 
                     OR l.ASSET_NO LIKE @searchPattern
                     OR l.ASSET_NAME2 LIKE @searchPattern
                     OR l.FFN_NO LIKE @searchPattern)
            GROUP BY l.loan_id, l.ASSET_NO, l.ASSET_NAME, l.ASSET_NAME2, 
                     l.BOOK_BALANCE, l.LOADED_ON, l.SALE_ID, l.LOAN_STATUS_ID
            ORDER BY l.ASSET_NAME, l.ASSET_NO";

        var searchPattern = $"%{searchTerm}%";
        return await connection.QueryAsync<LoanSummaryModel>(sql, new { saleId, searchPattern });
    }
}