namespace EsizzleAPI.Models;

public class OfferingModel
{
    public int OfferingId { get; set; }
    public string? OfferingNumber { get; set; }
    public string? OfferingName { get; set; }
    public string? OfferingHeader { get; set; }
    public string? OfferingDescription { get; set; }
    public DateTime? BidDate { get; set; }
    public int? ClientId { get; set; }
    public int OrderBy { get; set; }
    public string? OfferingLogoUrl { get; set; }
    public string? OfferingFolder { get; set; }
    public string? AnnouncementHeader1 { get; set; }
    public string? AnnouncementHeader2 { get; set; }
    public bool Visible { get; set; } = true;
    public DateTime? DueDiligenceStart { get; set; }
    public DateTime? DueDiligenceEnd { get; set; }
    public string? CurrentOfferingsHeader { get; set; }
    public bool? CurrentOfferingsVisible { get; set; }
    public bool? RequiresPreQualified { get; set; }
    public int ConfidentialityAgreementId { get; set; }
    public int SecondConfidentialityAgreementId { get; set; }
    public bool CutOffPackageAvailable { get; set; }
    public int ActiveForAccessLevel { get; set; } = 5;
    public int? OfferingStatusId { get; set; }
    public int? OfferingTypeId { get; set; }
    public bool? ShowOnHomepage { get; set; }
    public bool IsServicer { get; set; }
    public string? BidDateTimeZone { get; set; }
    public bool AskAssetQuestions { get; set; }
    public DateTime? ClosingDate { get; set; }
    public string? PoolPrefix { get; set; }
    public bool AutoGrantDD { get; set; }
    public bool? IsTest { get; set; }
    public string? Amount { get; set; }
    public bool IsNotable { get; set; }
    public bool ShowInPreviousOfferingsList { get; set; }
    public bool CanOrderHD { get; set; }
    public bool UseWebDD { get; set; } = true;
    public bool ShowImagesDisclaimer { get; set; } = true;
    public DateTime? AnnounceDate { get; set; }
    public int? ConstellationProjectId { get; set; }
    public decimal BidDepositAmount { get; set; }
    public bool CollectWireBidInfo { get; set; } = true;
    public bool DDUseDocFolders { get; set; } = true;
    public string DefaultLoanView { get; set; } = "Pool";
    public bool ForceAffiliateEntity { get; set; }
    public bool AllowComboBids { get; set; } = true;
    public DateTime? DateArchived { get; set; }
    public bool DynamicDataDownloadEnabled { get; set; } = true;
    public string? DefaultLoanDataSort { get; set; }
    public int BidTypeId { get; set; } = 1;
    public string IndexCode { get; set; } = "ffn";
    public bool ExpandSummaryDocFolders { get; set; } = true;
    public bool DDIsLive { get; set; }
    public string? BucketPrefix { get; set; }
    public DateTime? OfflineArchiveDate { get; set; }
    public bool ShowOfferingStatistics { get; set; } = true;
    public int? BpoProjectId { get; set; }
    public bool Deleted { get; set; }
}

public class OfferingSummaryModel
{
    public int OfferingId { get; set; }
    public string? OfferingName { get; set; }
    public string? OfferingDescription { get; set; }
    public DateTime? DueDiligenceStart { get; set; }
    public DateTime? DueDiligenceEnd { get; set; }
    public bool Visible { get; set; }
    public bool DDIsLive { get; set; }
    public int SalesCount { get; set; }
    public int LoansCount { get; set; }
}