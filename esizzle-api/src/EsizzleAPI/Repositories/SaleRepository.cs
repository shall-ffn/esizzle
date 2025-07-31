using Dapper;
using EsizzleAPI.Models;

namespace EsizzleAPI.Repositories;

public class SaleRepository : ISaleRepository
{
    private readonly IDbConnectionFactory _dbConnectionFactory;

    public SaleRepository(IDbConnectionFactory dbConnectionFactory)
    {
        _dbConnectionFactory = dbConnectionFactory;
    }

    public async Task<IEnumerable<SaleSummaryModel>> GetByOfferingIdAsync(int offeringId)
    {
        using var connection = _dbConnectionFactory.CreateConnection();
        const string sql = @"
            SELECT 
                s.sale_id as SaleId,
                s.sale_desc as SaleDesc,
                s.sale_num as SaleNum,
                s.display_order as DisplayOrder,
                s.adds_start_on as AddsStartOn,
                s.bid_date_on as BidDateOn,
                s.imaging_folder as ImagingFolder,
                COUNT(DISTINCT l.loan_id) as LoansCount
            FROM Sales s
            INNER JOIN Auction a ON s.sale_id = a.Loanmaster_Sale_ID
            INNER JOIN OfferingAuctions oa ON a.AuctionID = oa.AuctionID
            INNER JOIN Offerings o ON oa.OfferingID = o.OfferingID
            LEFT JOIN Loan l ON s.sale_id = l.SALE_ID AND l.LOAN_STATUS_ID != 0
            WHERE o.OfferingID = @offeringId
            GROUP BY s.sale_id, s.sale_desc, s.sale_num, s.display_order, 
                     s.adds_start_on, s.bid_date_on, s.imaging_folder
            ORDER BY COALESCE(s.display_order, 999), s.sale_desc";

        return await connection.QueryAsync<SaleSummaryModel>(sql, new { offeringId });
    }

    public async Task<SaleModel?> GetByIdAsync(int saleId)
    {
        using var connection = _dbConnectionFactory.CreateConnection();
        const string sql = @"
            SELECT 
                sale_id as SaleId,
                sale_desc as SaleDesc,
                sale_num as SaleNum,
                foxpro_data_dir as FoxproDataDir,
                display_order as DisplayOrder,
                report_header_1 as ReportHeader1,
                report_header_2 as ReportHeader2,
                adds_start_on as AddsStartOn,
                generate_post_closing_reports as GeneratePostClosingReports,
                seller_id as SellerId,
                sale_type_id as SaleTypeId,
                initial_strat_on as InitialStratOn,
                interim_strat_on as InterimStratOn,
                cutoff_on as CutoffOn,
                allows_allocated_combo_bids as AllowsAllocatedComboBids,
                bid_date_on as BidDateOn,
                scrape_directory as ScrapeDirectory,
                initial_qc_start_date as InitialQcStartDate,
                initial_qc_end_date as InitialQcEndDate,
                cutoff_qc_start_date as CutoffQcStartDate,
                cutoff_qc_end_date as CutoffQcEndDate,
                bidroomactive as BidroomActive,
                usepecbidderinfo as UsePecBidderInfo,
                ClientID as ClientId,
                funding1_date as Funding1Date,
                funding2_date as Funding2Date,
                allow_data_upload as AllowDataUpload,
                bank_sign as BankSign,
                bank_title as BankTitle,
                bank_name as BankName,
                bank_name2 as BankName2,
                bank_addr1 as BankAddr1,
                bank_addr2 as BankAddr2,
                bank_city as BankCity,
                bank_state as BankState,
                bank_zip as BankZip,
                image_upload_cutoff_date as ImageUploadCutoffDate,
                userules as UseRules,
                CurrentBidRoundID as CurrentBidRoundId,
                imaging_folder as ImagingFolder,
                sale_folder as SaleFolder,
                max_bid_percentage as MaxBidPercentage,
                ValuationSummary,
                OfflineArchiveDate,
                GlacierArchiveID as GlacierArchiveId,
                AllowDollarInput
            FROM Sales 
            WHERE sale_id = @saleId";

        return await connection.QueryFirstOrDefaultAsync<SaleModel>(sql, new { saleId });
    }

    public async Task<bool> BelongsToOfferingAsync(int saleId, int offeringId)
    {
        using var connection = _dbConnectionFactory.CreateConnection();
        const string sql = @"
            SELECT COUNT(1) 
            FROM Sales s
            INNER JOIN Auction a ON s.sale_id = a.Loanmaster_Sale_ID
            INNER JOIN OfferingAuctions oa ON a.AuctionID = oa.AuctionID
            INNER JOIN Offerings o ON oa.OfferingID = o.OfferingID
            WHERE s.sale_id = @saleId AND o.OfferingID = @offeringId";

        var count = await connection.QueryFirstAsync<int>(sql, new { saleId, offeringId });
        return count > 0;
    }
}
