namespace EsizzleAPI.Models;

public class DocumentModel
{
    public int Id { get; set; }
    public int? OfferingId { get; set; }
    public string OriginalName { get; set; } = string.Empty;
    public DateTime? DocumentDate { get; set; }
    public DateTime? SupplementalDate { get; set; }
    public DateTime? DocumentFileDate { get; set; }
    public string Path { get; set; } = string.Empty;
    public string? LockedByKey { get; set; }
    public string? AssetNumber { get; set; }
    public string? SellerUniqueId { get; set; }
    public int? LoanId { get; set; }
    public bool HasHardCopy { get; set; }
    public bool Corrupted { get; set; }
    public bool? Optimized { get; set; }
    public bool Deleted { get; set; }
    public long Length { get; set; }
    public int? PageCount { get; set; }
    public int ProcessOrder { get; set; }
    public string? DocumentType { get; set; }
    public DateTime DateCreated { get; set; }
    public int ImageStatusTypeId { get; set; } = 2;
    public int? ImageDocumentTypeId { get; set; }
    public DateTime? DateUpdated { get; set; }
    public string OriginalExt { get; set; } = string.Empty;
    public int? NextDocumentStatusTypeId { get; set; }
    public string? Comments { get; set; }
    public DateTime? QcDate { get; set; }
    public int? QcUserId { get; set; }
    public DateTime? OcrDate { get; set; }
    public string? BoxNumber { get; set; }
    public DateTime? TextExtractDate { get; set; }
    public DateTime? TextMetricDate { get; set; }
    public bool? IsRedacted { get; set; }
    public string? DocTypeClient { get; set; }
    public int? DocTypeManualId { get; set; }
    public int? DocTypeAutoId { get; set; }
    public string? BatchId { get; set; }
    public bool? IsWorkingFile { get; set; }
    public bool? IsSoftCopy { get; set; }
    public DateTime? ReleaseDate { get; set; }
    public string? FileHash { get; set; }
    public string ReleaseImageType { get; set; } = ".jpg";
    public bool ClosingDoc { get; set; }
    public bool HasDuplicate { get; set; }
    public int? ReleaseUserId { get; set; }
    public bool OverrideActions { get; set; }
    public string? BucketPrefix { get; set; }
    public int? ExternalId { get; set; }
}

public class DocumentSummaryModel
{
    public int Id { get; set; }
    public string OriginalName { get; set; } = string.Empty;
    public string? DocumentType { get; set; }
    public int? PageCount { get; set; }
    public long Length { get; set; }
    public DateTime DateCreated { get; set; }
    public DateTime? DateUpdated { get; set; }
    public int ImageStatusTypeId { get; set; }
    public bool Corrupted { get; set; }
    public bool? IsRedacted { get; set; }
    public string? Comments { get; set; }
    public int? LoanId { get; set; }
    public string? AssetNumber { get; set; }
}

public class DocumentUrlResponse
{
    public string Url { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public string ContentType { get; set; } = string.Empty;
}

public class RotateDocumentRequest
{
    public int Angle { get; set; } // 90, 180, 270
    public int? PageNumber { get; set; } // null for all pages
}

public class RedactionArea
{
    public int PageNumber { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
}

public class RedactDocumentRequest
{
    public List<RedactionArea> Areas { get; set; } = new();
    public bool PermanentRedaction { get; set; } = true;
}