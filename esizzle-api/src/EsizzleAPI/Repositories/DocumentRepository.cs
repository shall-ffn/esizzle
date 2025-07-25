using Dapper;
using EsizzleAPI.Models;

namespace EsizzleAPI.Repositories;

public class DocumentRepository : IDocumentRepository
{
    private readonly IDbConnectionFactory _dbConnectionFactory;

    public DocumentRepository(IDbConnectionFactory dbConnectionFactory)
    {
        _dbConnectionFactory = dbConnectionFactory;
    }

    public async Task<IEnumerable<DocumentSummaryModel>> GetByLoanIdAsync(int loanId)
    {
        using var connection = _dbConnectionFactory.CreateConnection();
        const string sql = @"
            SELECT 
                ID as Id,
                OriginalName,
                DocumentType,
                PageCount,
                Length,
                DateCreated,
                DateUpdated,
                ImageStatusTypeID as ImageStatusTypeId,
                Corrupted,
                IsRedacted,
                Comments,
                LoanID as LoanId,
                AssetNumber
            FROM Image 
            WHERE LoanID = @loanId 
                AND Deleted = 0
            ORDER BY ProcessOrder, DateCreated";

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

    public async Task<string> GenerateDocumentUrlAsync(int documentId)
    {
        // Get document path from database
        var document = await GetByIdAsync(documentId);
        if (document == null)
            return string.Empty;

        // For local development, we'll return a mock URL
        // In production, this would generate a presigned S3 URL
        return $"https://localhost:5001/api/v1/documents/{documentId}/content";
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
            SELECT DISTINCT DocumentType 
            FROM Image 
            WHERE DocumentType IS NOT NULL 
                AND DocumentType != ''
                AND Deleted = 0
            ORDER BY DocumentType";

        return await connection.QueryAsync<string>(sql);
    }
}