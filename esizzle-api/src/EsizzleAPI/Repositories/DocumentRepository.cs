using Dapper;
using EsizzleAPI.Models;
using EsizzleAPI.Services;

namespace EsizzleAPI.Repositories;

public class DocumentRepository : IDocumentRepository
{
    private readonly IDbConnectionFactory _dbConnectionFactory;
    private readonly IPdfTokenService _pdfTokenService;

    public DocumentRepository(IDbConnectionFactory dbConnectionFactory, IPdfTokenService pdfTokenService)
    {
        _dbConnectionFactory = dbConnectionFactory;
        _pdfTokenService = pdfTokenService;
    }

    public async Task<IEnumerable<DocumentSummaryModel>> GetByLoanIdAsync(int loanId)
    {
        using var connection = _dbConnectionFactory.CreateConnection();
        const string sql = @"
            SELECT 
                i.ID as Id,
                i.OriginalName,
                COALESCE(dt.Name, i.DocumentType, 'Unclassified') as DocumentType,
                i.DocTypeManualID as ImageDocumentTypeID,
                dt.Name as ClassifiedDocumentType,
                i.PageCount,
                i.Length,
                i.DateCreated,
                i.DateUpdated,
                i.ImageStatusTypeID as ImageStatusTypeId,
                i.Corrupted,
                i.IsRedacted,
                i.Comments,
                i.LoanID as LoanId,
                i.AssetNumber
            FROM Image i
            LEFT JOIN ImageDocTypeMasterList dt ON i.DocTypeManualID = dt.ID
            WHERE i.LoanID = @loanId AND i.Deleted = 0
            ORDER BY 
                CASE WHEN dt.Name IS NULL THEN 1 ELSE 0 END,
                dt.Name,
                i.OriginalName,
                i.DateCreated";

        return await connection.QueryAsync<DocumentSummaryModel>(sql, new { loanId });
    }

    public async Task<DocumentModel?> GetByIdAsync(int documentId)
    {
        using var connection = _dbConnectionFactory.CreateConnection();
        const string sql = @"
            SELECT 
                ID as Id,
                OfferingID as OfferingId,
                OriginalName,
                DocumentDate,
                SupplementalDate,
                DocumentFileDate,
                Path,
                LockedByKey,
                AssetNumber,
                SellerUniqueID as SellerUniqueId,
                LoanID as LoanId,
                HasHardCopy,
                Corrupted,
                Optimized,
                Deleted,
                Length,
                PageCount,
                ProcessOrder,
                DocumentType,
                DateCreated,
                ImageStatusTypeID as ImageStatusTypeId,
                ImageDocumentTypeID as ImageDocumentTypeId,
                DateUpdated,
                OriginalExt,
                NextDocumentStatusTypeID as NextDocumentStatusTypeId,
                Comments,
                QCDate as QcDate,
                QCUserID as QcUserId,
                OCRDate as OcrDate,
                BoxNumber,
                TextExtractDate,
                TextMetricDate,
                IsRedacted,
                DocTypeClient,
                DocTypeManualID as DocTypeManualId,
                DocTypeAutoID as DocTypeAutoId,
                BatchID as BatchId,
                IsWorkingFile,
                IsSoftCopy,
                ReleaseDate,
                FileHash,
                ReleaseImageType,
                ClosingDoc,
                HasDuplicate,
                ReleaseUserID as ReleaseUserId,
                OverrideActions,
                BucketPrefix,
                ExternalID as ExternalId
            FROM Image 
            WHERE ID = @documentId AND Deleted = 0";

        return await connection.QueryFirstOrDefaultAsync<DocumentModel>(sql, new { documentId });
    }

    public async Task<bool> BelongsToLoanAsync(int documentId, int loanId)
    {
        using var connection = _dbConnectionFactory.CreateConnection();
        const string sql = "SELECT COUNT(1) FROM Image WHERE ID = @documentId AND LoanID = @loanId AND Deleted = 0";

        var count = await connection.QueryFirstAsync<int>(sql, new { documentId, loanId });
        return count > 0;
    }

    public async Task<string> GenerateDocumentUrlAsync(int documentId, int userId)
    {
        // Get document path from database
        var document = await GetByIdAsync(documentId);
        if (document == null)
            return string.Empty;

        // Generate a time-limited access token for PDF content (15 minutes)
        var accessToken = _pdfTokenService.GeneratePdfAccessToken(documentId, userId, TimeSpan.FromMinutes(15));
        
        // Return signed URL with embedded access token
        return $"http://localhost:5000/api/v1/hydra/document/{documentId}/content?token={accessToken}";
    }

    public async Task<bool> UpdateDocumentTypeAsync(int documentId, string documentType, int userId)
    {
        using var connection = _dbConnectionFactory.CreateConnection();
        const string sql = @"
            UPDATE Image 
            SET DocumentType = @documentType,
                DateUpdated = UTC_TIMESTAMP()
            WHERE ID = @documentId AND Deleted = 0";

        var rowsAffected = await connection.ExecuteAsync(sql, new { documentId, documentType });
        return rowsAffected > 0;
    }

    public async Task<bool> MarkAsRedactedAsync(int documentId, int userId)
    {
        using var connection = _dbConnectionFactory.CreateConnection();
        const string sql = @"
            UPDATE Image 
            SET IsRedacted = 1,
                DateUpdated = UTC_TIMESTAMP()
            WHERE ID = @documentId AND Deleted = 0";

        var rowsAffected = await connection.ExecuteAsync(sql, new { documentId });
        return rowsAffected > 0;
    }

    public async Task<bool> UpdateProcessingStatusAsync(int documentId, int statusId)
    {
        using var connection = _dbConnectionFactory.CreateConnection();
        const string sql = @"
            UPDATE Image 
            SET ImageStatusTypeID = @statusId,
                DateUpdated = UTC_TIMESTAMP()
            WHERE ID = @documentId AND Deleted = 0";

        var rowsAffected = await connection.ExecuteAsync(sql, new { documentId, statusId });
        return rowsAffected > 0;
    }

    public async Task<IEnumerable<string>> GetDocumentTypesAsync()
    {
        using var connection = _dbConnectionFactory.CreateConnection();
        const string sql = @"
            SELECT DISTINCT Name
            FROM ImageDocTypeMasterList 
            WHERE Name IS NOT NULL 
                AND Name != ''
            ORDER BY Name";

        return await connection.QueryAsync<string>(sql);
    }

    public async Task<IEnumerable<DocumentTypeModel>> GetDocumentTypesByOfferingAsync(int offeringId)
    {
        using var connection = _dbConnectionFactory.CreateConnection();
        const string sql = @"
            SELECT DISTINCT dt.ID as Id, dt.Name, dt.Code, dt.DateCreated
            FROM Offerings o
            JOIN ImageDocTypeMasterList dt ON o.IndexCode = dt.Code
            WHERE o.OfferingID = @offeringId
            ORDER BY dt.Name";

        return await connection.QueryAsync<DocumentTypeModel>(sql, new { offeringId });
    }

    public async Task<bool> UpdateDocumentClassificationAsync(int documentId, int docTypeId, int userId)
    {
        using var connection = _dbConnectionFactory.CreateConnection();
        const string sql = @"
            UPDATE Image 
            SET DocTypeManualID = @docTypeId,
                DateUpdated = UTC_TIMESTAMP()
            WHERE ID = @documentId AND Deleted = 0";

        var rowsAffected = await connection.ExecuteAsync(sql, new { documentId, docTypeId });
        return rowsAffected > 0;
    }

    public async Task<IEnumerable<ImageOfferingActionModel>> GetOfferingImageActionsAsync(int offeringId)
    {
        using var connection = _dbConnectionFactory.CreateConnection();
        const string sql = @"
            SELECT 
                oa.ID as Id,
                oa.OfferingID as OfferingId,
                oa.DocTypeID as DocTypeId,
                dt.Name as DocTypeName,
                oa.ImageActionTypeID as ImageActionTypeId,
                ia.Name as ImageActionType,
                oa.ActionName,
                oa.ActionNote
            FROM ImageOfferingActions oa
            JOIN ImageActionTypes ia ON oa.ImageActionTypeID = ia.ID
            LEFT JOIN ImageDocTypeMasterList dt ON oa.DocTypeID = dt.ID
            WHERE oa.OfferingID = @offeringId
            ORDER BY dt.Name, oa.ActionName";

        return await connection.QueryAsync<ImageOfferingActionModel>(sql, new { offeringId });
    }
}
