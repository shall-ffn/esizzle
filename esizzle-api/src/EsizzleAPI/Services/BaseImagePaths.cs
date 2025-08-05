using EsizzleAPI.Models;

namespace EsizzleAPI.Services;

/// <summary>
/// Static class for constructing S3 image paths based on legacy Hydra.DueDiligence.App logic
/// Matches BaseImagePaths from global.DAL.LN.cs
/// </summary>
public static class BaseImagePaths
{
    /// <summary>
    /// Root path for original images - maps to IOriginal/Images/
    /// </summary>
    public static string OriginalRoot => "IOriginal/Images";
    
    /// <summary>
    /// Root path for production PDFs - maps to IProd/Images/
    /// </summary>
    public static string PDFRoot => "IProd/Images";
    
    /// <summary>
    /// Root path for processing images - maps to IProcessing/Images/
    /// </summary>
    public static string ProcessingRoot => "IProcessing/Images";
    
    /// <summary>
    /// Root path for redacted originals - maps to IRedact/Images/
    /// </summary>
    public static string RedactRoot => "IRedact/Images";

    /// <summary>
    /// Gets the appropriate S3 path based on image status
    /// Mimics the GetImagePath logic from legacy Image entity
    /// </summary>
    /// <param name="imageId">The image ID</param>
    /// <param name="statusType">The image status type</param>
    /// <param name="originalExtension">Original file extension (with or without dot)</param>
    /// <returns>Full S3 path for the image</returns>
    public static string GetImagePath(int imageId, ImageStatusTypeEnum statusType, string? originalExtension = null)
    {
        return statusType switch
        {
            // Original file statuses (need conversion or manipulation)
            ImageStatusTypeEnum.NeedsConversion or 
            ImageStatusTypeEnum.NeedsImageManipulation => GetOriginalPath(imageId, originalExtension),
            
            // Production/viewable statuses (already processed to PDF)
            ImageStatusTypeEnum.Production or
            ImageStatusTypeEnum.NeedsDocType or
            ImageStatusTypeEnum.NeedsIndexing or
            ImageStatusTypeEnum.NeedsDocSplit or
            ImageStatusTypeEnum.NeedsTextExtraction or
            ImageStatusTypeEnum.NeedsVerification or
            ImageStatusTypeEnum.NeedsQC or
            ImageStatusTypeEnum.NeedsOptimization or
            ImageStatusTypeEnum.NeedsNoProcessing or
            ImageStatusTypeEnum.NeedsLoanAssignment or
            ImageStatusTypeEnum.NeedsRelease or
            ImageStatusTypeEnum.Archived => GetProdPath(imageId),
            
            // Processing/non-viewable statuses
            _ => GetProcessingPath(imageId)
        };
    }

    /// <summary>
    /// Gets the original image path: IOriginal/Images/{imageId}{extension}
    /// </summary>
    /// <param name="imageId">The image ID</param>
    /// <param name="originalExtension">Original file extension</param>
    /// <returns>Original image S3 path</returns>
    public static string GetOriginalPath(int imageId, string? originalExtension = null)
    {
        var extension = NormalizeExtension(originalExtension);
        return $"{OriginalRoot}/{imageId}{extension}";
    }

    /// <summary>
    /// Gets the processing image path: IProcessing/Images/{imageId}.pdf
    /// </summary>
    /// <param name="imageId">The image ID</param>
    /// <returns>Processing image S3 path</returns>
    public static string GetProcessingPath(int imageId)
    {
        return $"{ProcessingRoot}/{imageId}.pdf";
    }

    /// <summary>
    /// Gets the production PDF path: IProd/Images/{imageId}.pdf
    /// </summary>
    /// <param name="imageId">The image ID</param>
    /// <returns>Production PDF S3 path</returns>
    public static string GetProdPath(int imageId)
    {
        return $"{PDFRoot}/{imageId}.pdf";
    }

    /// <summary>
    /// Gets the redacted original path: IRedact/Images/{imageId}{extension}
    /// </summary>
    /// <param name="imageId">The image ID</param>
    /// <param name="originalExtension">Original file extension</param>
    /// <returns>Redacted original S3 path</returns>
    public static string GetRedactPath(int imageId, string? originalExtension = null)
    {
        var extension = NormalizeExtension(originalExtension);
        return $"{RedactRoot}/{imageId}{extension}";
    }

    /// <summary>
    /// Determines the best unredacted path to use for viewing
    /// Mimics the GetUnredactedPath logic from legacy extensions
    /// </summary>
    /// <param name="imageId">The image ID</param>
    /// <param name="statusType">The image status type</param>
    /// <param name="originalExtension">Original file extension</param>
    /// <param name="isRedacted">Whether the image is redacted</param>
    /// <returns>Best available S3 path for viewing</returns>
    public static string GetUnredactedPath(int imageId, ImageStatusTypeEnum statusType, string? originalExtension = null, bool? isRedacted = null)
    {
        // If explicitly redacted, use redact path
        if (isRedacted == true)
        {
            return GetRedactPath(imageId, originalExtension);
        }

        // For production status, prefer production path
        if (statusType == ImageStatusTypeEnum.Production)
        {
            return GetProdPath(imageId);
        }

        // For original-based statuses, use original path
        if (statusType == ImageStatusTypeEnum.NeedsConversion || 
            statusType == ImageStatusTypeEnum.NeedsImageManipulation)
        {
            return GetOriginalPath(imageId, originalExtension);
        }

        // Default to processing path
        return GetProcessingPath(imageId);
    }

    /// <summary>
    /// Normalizes file extension to include leading dot and handle null/empty values
    /// </summary>
    /// <param name="extension">File extension with or without leading dot</param>
    /// <returns>Normalized extension with leading dot, or .pdf if null/empty</returns>
    private static string NormalizeExtension(string? extension)
    {
        if (string.IsNullOrEmpty(extension))
        {
            return ".pdf";
        }

        return extension.StartsWith(".") ? extension : "." + extension;
    }
}