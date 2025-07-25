namespace EsizzleAPI.Models;

public class SaleModel
{
    public int SaleId { get; set; }
    public string SaleDesc { get; set; } = string.Empty;
    public string? SaleNum { get; set; }
    public string? FoxproDataDir { get; set; }
    public short? DisplayOrder { get; set; }
    public string? ReportHeader1 { get; set; }
    public string? ReportHeader2 { get; set; }
    public DateTime AddsStartOn { get; set; }
    public bool GeneratePostClosingReports { get; set; } = true;
    public int SellerId { get; set; }
    public int SaleTypeId { get; set; }
    public DateTime? InitialStratOn { get; set; }
    public DateTime? InterimStratOn { get; set; }
    public DateTime? CutoffOn { get; set; }
    public bool AllowsAllocatedComboBids { get; set; } = true;
    public DateTime? BidDateOn { get; set; }
    public string? ScrapeDirectory { get; set; }
    public DateTime? InitialQcStartDate { get; set; }
    public DateTime? InitialQcEndDate { get; set; }
    public DateTime? CutoffQcStartDate { get; set; }
    public DateTime? CutoffQcEndDate { get; set; }
    public bool? BidroomActive { get; set; }
    public bool? UsePecBidderInfo { get; set; }
    public int? ClientId { get; set; }
    public DateTime? Funding1Date { get; set; }
    public DateTime? Funding2Date { get; set; }
    public bool AllowDataUpload { get; set; }
    public string? BankSign { get; set; }
    public string? BankTitle { get; set; }
    public string? BankName { get; set; }
    public string? BankName2 { get; set; }
    public string? BankAddr1 { get; set; }
    public string? BankAddr2 { get; set; }
    public string? BankCity { get; set; }
    public string? BankState { get; set; }
    public string? BankZip { get; set; }
    public DateTime? ImageUploadCutoffDate { get; set; }
    public bool UseRules { get; set; } = true;
    public int? CurrentBidRoundId { get; set; }
    public string? ImagingFolder { get; set; }
    public string? SaleFolder { get; set; }
    public int? MaxBidPercentage { get; set; } = 115;
    public string? ValuationSummary { get; set; }
    public DateTime? OfflineArchiveDate { get; set; }
    public string? GlacierArchiveId { get; set; }
    public bool AllowDollarInput { get; set; }
}

public class SaleSummaryModel
{
    public int SaleId { get; set; }
    public string SaleDesc { get; set; } = string.Empty;
    public string? SaleNum { get; set; }
    public short? DisplayOrder { get; set; }
    public DateTime AddsStartOn { get; set; }
    public DateTime? BidDateOn { get; set; }
    public int LoansCount { get; set; }
    public string? ImagingFolder { get; set; }
}