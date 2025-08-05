namespace EsizzleAPI.Models;

/// <summary>
/// System role enumeration for user permissions
/// </summary>
public enum SystemRoleEnum
{
    /// <summary>
    /// Administrator role
    /// </summary>
    Administrator,
    
    /// <summary>
    /// Analyst role
    /// </summary>
    Analyst,
    
    /// <summary>
    /// Viewer role
    /// </summary>
    Viewer,
    
    /// <summary>
    /// Manager role
    /// </summary>
    Manager
}

/// <summary>
/// Image status types that determine S3 path resolution logic
/// Matches actual database ImageStatusType table
/// </summary>
public enum ImageStatusTypeEnum
{
    /// <summary>
    /// Production - ready for viewing, uses ProdPath
    /// </summary>
    Production = 1,
    
    /// <summary>
    /// Needs Processing - uses ProcessingPath
    /// </summary>
    NeedsProcessing = 2,
    
    /// <summary>
    /// Needs Conversion - uses OriginalPath
    /// </summary>
    NeedsConversion = 3,
    
    /// <summary>
    /// Needs Doc Type - uses ProdPath (viewable)
    /// </summary>
    NeedsDocType = 4,
    
    /// <summary>
    /// Needs Indexing - uses ProdPath (viewable)
    /// </summary>
    NeedsIndexing = 5,
    
    /// <summary>
    /// Needs Doc Split - uses ProdPath (viewable)
    /// </summary>
    NeedsDocSplit = 6,
    
    /// <summary>
    /// Obsolete - uses ProcessingPath (not viewable)
    /// </summary>
    Obsolete = 7,
    
    /// <summary>
    /// Needs Text Extraction - uses ProdPath (viewable)
    /// </summary>
    NeedsTextExtraction = 8,
    
    /// <summary>
    /// Needs Verification - uses ProdPath (viewable)
    /// </summary>
    NeedsVerification = 9,
    
    /// <summary>
    /// Needs QC - uses ProdPath (viewable)
    /// </summary>
    NeedsQC = 10,
    
    /// <summary>
    /// Needs Optimization - uses ProdPath (viewable)
    /// </summary>
    NeedsOptimization = 11,
    
    /// <summary>
    /// Needs No Processing - uses ProdPath (viewable)
    /// </summary>
    NeedsNoProcessing = 12,
    
    /// <summary>
    /// Needs Loan Assignment - uses ProdPath (viewable)
    /// </summary>
    NeedsLoanAssignment = 13,
    
    /// <summary>
    /// Needs Release - uses ProdPath (viewable)
    /// </summary>
    NeedsRelease = 14,
    
    /// <summary>
    /// Corrupted - uses ProcessingPath (not viewable)
    /// </summary>
    Corrupted = 17,
    
    /// <summary>
    /// Needs Image Manipulation - uses OriginalPath (not viewable)
    /// </summary>
    NeedsImageManipulation = 18,
    
    /// <summary>
    /// Archived - uses ProdPath (viewable)
    /// </summary>
    Archived = 26
}
