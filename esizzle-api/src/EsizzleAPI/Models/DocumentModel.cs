using EsizzleAPI.Services;

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

    /// <summary>
    /// Gets the S3 path for this document based on its ImageStatusTypeId
    /// Mimics legacy GetImagePath functionality
    /// </summary>
    /// <returns>S3 path for the current document status</returns>
    public string GetImagePath()
    {
        var statusType = (ImageStatusTypeEnum)ImageStatusTypeId;
        return BaseImagePaths.GetImagePath(Id, statusType, OriginalExt);
    }

    /// <summary>
    /// Gets the S3 path for a specific image status
    /// </summary>
    /// <param name="statusType">The image status to get path for</param>
    /// <returns>S3 path for the specified status</returns>
    public string GetImagePath(ImageStatusTypeEnum statusType)
    {
        return BaseImagePaths.GetImagePath(Id, statusType, OriginalExt);
    }

    /// <summary>
    /// Gets the original image S3 path
    /// </summary>
    /// <returns>Original image S3 path</returns>
    public string GetOriginalPath()
    {
        return BaseImagePaths.GetOriginalPath(Id, OriginalExt);
    }

    /// <summary>
    /// Gets the processing image S3 path
    /// </summary>
    /// <returns>Processing image S3 path</returns>
    public string GetProcessingPath()
    {
        return BaseImagePaths.GetProcessingPath(Id);
    }

    /// <summary>
    /// Gets the production PDF S3 path
    /// </summary>
    /// <returns>Production PDF S3 path</returns>
    public string GetProdPath()
    {
        return BaseImagePaths.GetProdPath(Id);
    }

    /// <summary>
    /// Gets the redacted original S3 path
    /// </summary>
    /// <returns>Redacted original S3 path</returns>
    public string GetRedactPath()
    {
        return BaseImagePaths.GetRedactPath(Id, OriginalExt);
    }

    /// <summary>
    /// Gets the best unredacted path for viewing this document
    /// Mimics legacy GetUnredactedPath functionality
    /// </summary>
    /// <returns>Best available S3 path for viewing</returns>
    public string GetUnredactedPath()
    {
        var statusType = (ImageStatusTypeEnum)ImageStatusTypeId;
        return BaseImagePaths.GetUnredactedPath(Id, statusType, OriginalExt, IsRedacted);
    }
}

public class DocumentSummaryModel
{
    public int Id { get; set; }
    public string OriginalName { get; set; } = string.Empty;
    public string? DocumentType { get; set; }
    public int? ImageDocumentTypeID { get; set; }
    public string? ClassifiedDocumentType { get; set; }
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

public class DocumentTypeModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Code { get; set; }
    public DateTime DateCreated { get; set; }
}

public class ImageOfferingActionModel
{
    public int Id { get; set; }
    public int OfferingId { get; set; }
    public int? DocTypeId { get; set; }
    public string? DocTypeName { get; set; }
    public int ImageActionTypeId { get; set; }
    public string ImageActionType { get; set; } = string.Empty;
    public string ActionName { get; set; } = string.Empty;
    public string? ActionNote { get; set; }
}

public class UpdateDocumentClassificationRequest
{
    public int DocTypeId { get; set; }
}
