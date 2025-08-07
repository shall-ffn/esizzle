using Dapper;
using EsizzleAPI.Models;
using EsizzleAPI.DTOs;
using System.Globalization;

namespace EsizzleAPI.Repositories
{
    /// <summary>
    /// Repository implementation for legacy indexing operations
    /// </summary>
    public class IndexingRepository : IIndexingRepository
    {
        private readonly IDbConnectionFactory _dbConnectionFactory;

        public IndexingRepository(IDbConnectionFactory dbConnectionFactory)
        {
            _dbConnectionFactory = dbConnectionFactory;
        }

        // === DOCUMENT TYPES ===

        public async Task<List<DocumentTypeDto>> GetDocumentTypesByOfferingAsync(int offeringId, string? search = null)
        {
            using var connection = _dbConnectionFactory.CreateConnection();
            
            var sql = @"
                SELECT dt.ID as Id, dt.Name, dt.IsGeneric, dt.Code
                FROM ImageDocTypeMasterLists dt
                INNER JOIN Offerings o ON dt.Code = o.IndexCode
                WHERE o.OfferingID = @offeringId AND dt.IsUsed = 1";
            
            object parameters;
            
            if (!string.IsNullOrWhiteSpace(search))
            {
                sql += " AND dt.Name LIKE @search";
                parameters = new { offeringId, search = $"%{search}%" };
            }
            else
            {
                parameters = new { offeringId };
            }
            
            sql += " ORDER BY dt.Name";

            var results = await connection.QueryAsync<DocumentTypeDto>(sql, parameters);
            return results.ToList();
        }

        public async Task<DocumentTypeDto?> GetDocumentTypeByIdAsync(int documentTypeId)
        {
            using var connection = _dbConnectionFactory.CreateConnection();
            
            const string sql = @"
                SELECT ID as Id, Name, IsGeneric, Code
                FROM ImageDocTypeMasterLists
                WHERE ID = @documentTypeId AND IsUsed = 1";

            return await connection.QueryFirstOrDefaultAsync<DocumentTypeDto>(sql, new { documentTypeId });
        }

        // === BOOKMARKS ===

        public async Task<List<BookmarkDto>> GetBookmarksByDocumentAsync(int documentId)
        {
            using var connection = _dbConnectionFactory.CreateConnection();
            
            const string sql = @"
                SELECT 
                    b.ID as Id,
                    b.ImageID as ImageId,
                    b.PageIndex,
                    b.Text,
                    b.ImageDocumentTypeID as ImageDocumentTypeId,
                    dt.Name as DocumentTypeName,
                    dt.IsGeneric,
                    b.DateCreated,
                    b.ResultImageID as ResultImageId,
                    CASE WHEN b.ResultImageID IS NULL THEN 1 ELSE 0 END as CanEdit,
                    b.CreatedBy
                FROM ImageBookmarks b
                INNER JOIN ImageDocTypeMasterLists dt ON b.ImageDocumentTypeID = dt.ID
                WHERE b.ImageID = @documentId AND b.IsDeleted = 0
                ORDER BY b.PageIndex";

            var results = await connection.QueryAsync<BookmarkDto>(sql, new { documentId });
            return results.ToList();
        }

        public async Task<BookmarkDto?> GetBookmarkByIdAsync(int bookmarkId)
        {
            using var connection = _dbConnectionFactory.CreateConnection();
            
            const string sql = @"
                SELECT 
                    b.ID as Id,
                    b.ImageID as ImageId,
                    b.PageIndex,
                    b.Text,
                    b.ImageDocumentTypeID as ImageDocumentTypeId,
                    dt.Name as DocumentTypeName,
                    dt.IsGeneric,
                    b.DateCreated,
                    b.ResultImageID as ResultImageId,
                    CASE WHEN b.ResultImageID IS NULL THEN 1 ELSE 0 END as CanEdit,
                    b.CreatedBy
                FROM ImageBookmarks b
                INNER JOIN ImageDocTypeMasterLists dt ON b.ImageDocumentTypeID = dt.ID
                WHERE b.ID = @bookmarkId AND b.IsDeleted = 0";

            return await connection.QueryFirstOrDefaultAsync<BookmarkDto>(sql, new { bookmarkId });
        }

        public async Task<BookmarkDto> CreateBookmarkAsync(CreateBookmarkRequest request, int userId)
        {
            using var connection = _dbConnectionFactory.CreateConnection();
            
            // Build pipe-delimited text field
            var text = BuildBookmarkText(request.DocumentTypeName, request.DocumentTypeId, 
                                       request.DocumentDate, request.Comments);

            const string sql = @"
                INSERT INTO ImageBookmarks (ImageID, PageIndex, Text, ImageDocumentTypeID, CreatedBy, DateCreated, IsDeleted)
                VALUES (@ImageId, @PageIndex, @Text, @DocumentTypeId, @UserId, @DateCreated, 0);
                SELECT LAST_INSERT_ID();";

            var bookmarkId = await connection.QuerySingleAsync<int>(sql, new
            {
                ImageId = request.ImageId,
                PageIndex = request.PageIndex,
                Text = text,
                DocumentTypeId = request.DocumentTypeId,
                UserId = userId,
                DateCreated = DateTime.UtcNow
            });

            // Return the created bookmark
            var result = await GetBookmarkByIdAsync(bookmarkId);
            return result ?? throw new InvalidOperationException("Failed to retrieve created bookmark");
        }

        public async Task<BookmarkDto?> UpdateBookmarkAsync(int documentId, int bookmarkId, UpdateBookmarkRequest request)
        {
            using var connection = _dbConnectionFactory.CreateConnection();
            
            // First check if bookmark exists and can be updated
            const string checkSql = @"
                SELECT ID, ResultImageID
                FROM ImageBookmarks
                WHERE ID = @bookmarkId AND ImageID = @documentId AND IsDeleted = 0";

            var bookmark = await connection.QueryFirstOrDefaultAsync<dynamic>(checkSql, new { bookmarkId, documentId });
            
            if (bookmark == null || bookmark.ResultImageID != null)
                return null; // Cannot update processed bookmarks

            // Update text field with new values
            var text = BuildBookmarkText(request.DocumentTypeName, request.DocumentTypeId,
                                       request.DocumentDate, request.Comments);

            const string updateSql = @"
                UPDATE ImageBookmarks 
                SET Text = @text, ImageDocumentTypeID = @documentTypeId
                WHERE ID = @bookmarkId";

            await connection.ExecuteAsync(updateSql, new
            {
                text,
                documentTypeId = request.DocumentTypeId,
                bookmarkId
            });

            return await GetBookmarkByIdAsync(bookmarkId);
        }

        public async Task<bool> DeleteBookmarkAsync(int documentId, int bookmarkId)
        {
            using var connection = _dbConnectionFactory.CreateConnection();
            
            // First check if bookmark exists and can be deleted
            const string checkSql = @"
                SELECT ID, ResultImageID
                FROM ImageBookmarks
                WHERE ID = @bookmarkId AND ImageID = @documentId AND IsDeleted = 0";

            var bookmark = await connection.QueryFirstOrDefaultAsync<dynamic>(checkSql, new { bookmarkId, documentId });
            
            if (bookmark == null || bookmark.ResultImageID != null)
                return false; // Cannot delete processed bookmarks

            const string deleteSql = @"
                UPDATE ImageBookmarks 
                SET IsDeleted = 1
                WHERE ID = @bookmarkId";

            var rowsAffected = await connection.ExecuteAsync(deleteSql, new { bookmarkId });
            return rowsAffected > 0;
        }

        public async Task<BookmarkValidationResult> ValidateBookmarksAsync(int documentId, List<int> bookmarkIds)
        {
            using var connection = _dbConnectionFactory.CreateConnection();
            
            var result = new BookmarkValidationResult { IsValid = true };

            // Check if all bookmarks exist and belong to the document
            const string sql = @"
                SELECT ID, PageIndex, ResultImageID
                FROM ImageBookmarks
                WHERE ID IN @bookmarkIds AND ImageID = @documentId AND IsDeleted = 0";

            var bookmarks = await connection.QueryAsync<dynamic>(sql, new { bookmarkIds, documentId });
            var bookmarkList = bookmarks.ToList();

            if (bookmarkList.Count != bookmarkIds.Count)
            {
                result.IsValid = false;
                result.Errors.Add("One or more bookmarks not found or already deleted");
            }

            // Check for processed bookmarks
            var processedBookmarks = bookmarkList.Where(b => b.ResultImageID != null).ToList();
            if (processedBookmarks.Any())
            {
                result.IsValid = false;
                result.Errors.Add($"{processedBookmarks.Count} bookmark(s) have already been processed");
            }

            // Check for overlapping page ranges
            var sortedBookmarks = bookmarkList.OrderBy(b => (int)b.PageIndex).ToList();
            for (int i = 1; i < sortedBookmarks.Count; i++)
            {
                if ((int)sortedBookmarks[i].PageIndex == (int)sortedBookmarks[i - 1].PageIndex)
                {
                    result.Warnings.Add($"Multiple bookmarks on page {(int)sortedBookmarks[i].PageIndex + 1}");
                }
            }

            return result;
        }

        // === PROCESSING ===

        public async Task<ProcessingSessionDto> SaveImageDataAsync(int documentId, DocumentMetadata metadata, int userId)
        {
            using var connection = _dbConnectionFactory.CreateConnection();
            
            // Update document with metadata only
            const string updateDocSql = @"
                UPDATE Images 
                SET DocTypeManualID = @DocumentTypeId, 
                    DocumentDate = @DocumentDate, 
                    Comments = @Comments, 
                    LastModified = @LastModified
                WHERE ID = @DocumentId";

            var rowsUpdated = await connection.ExecuteAsync(updateDocSql, new
            {
                DocumentTypeId = metadata.DocumentTypeId,
                DocumentDate = metadata.DocumentDate,
                Comments = metadata.Comments,
                LastModified = DateTime.UtcNow,
                DocumentId = documentId
            });

            if (rowsUpdated == 0)
                throw new ArgumentException("Document not found", nameof(documentId));

            // Create processing session record
            var sessionId = Guid.NewGuid().ToString();
            var dateCreated = DateTime.UtcNow;

            const string insertSessionSql = @"
                INSERT INTO ProcessingSessions (SessionId, ImageID, ProcessingType, Status, CreatedBy, DateCreated, CompletedDate)
                VALUES (@SessionId, @ImageID, @ProcessingType, @Status, @CreatedBy, @DateCreated, @CompletedDate)";

            await connection.ExecuteAsync(insertSessionSql, new
            {
                SessionId = sessionId,
                ImageID = documentId,
                ProcessingType = "SimpleIndexing",
                Status = "Completed",
                CreatedBy = userId,
                DateCreated = dateCreated,
                CompletedDate = dateCreated
            });

            return new ProcessingSessionDto
            {
                SessionId = sessionId,
                ImageId = documentId,
                ProcessingType = "SimpleIndexing",
                Status = "Completed",
                DateCreated = dateCreated,
                CompletedDate = dateCreated
            };
        }

        public async Task<ProcessingSessionDto> CreateProcessingSessionAsync(int documentId, string processingType, int userId)
        {
            using var connection = _dbConnectionFactory.CreateConnection();
            
            var sessionId = Guid.NewGuid().ToString();
            var dateCreated = DateTime.UtcNow;

            const string sql = @"
                INSERT INTO ProcessingSessions (SessionId, ImageID, ProcessingType, Status, CreatedBy, DateCreated)
                VALUES (@SessionId, @ImageID, @ProcessingType, @Status, @CreatedBy, @DateCreated)";

            await connection.ExecuteAsync(sql, new
            {
                SessionId = sessionId,
                ImageID = documentId,
                ProcessingType = processingType,
                Status = "Queued",
                CreatedBy = userId,
                DateCreated = dateCreated
            });

            return new ProcessingSessionDto
            {
                SessionId = sessionId,
                ImageId = documentId,
                ProcessingType = processingType,
                Status = "Queued",
                DateCreated = dateCreated
            };
        }

        public async Task<bool> UpdateProcessingSessionAsync(string sessionId, string status, string? errorMessage = null)
        {
            using var connection = _dbConnectionFactory.CreateConnection();
            
            var completedDate = (status == "Completed" || status == "Failed") ? DateTime.UtcNow : (DateTime?)null;

            const string sql = @"
                UPDATE ProcessingSessions 
                SET Status = @status, 
                    ErrorMessage = @errorMessage,
                    CompletedDate = @completedDate
                WHERE SessionId = @sessionId";

            var rowsAffected = await connection.ExecuteAsync(sql, new
            {
                status,
                errorMessage,
                completedDate,
                sessionId
            });

            return rowsAffected > 0;
        }

        public async Task<ProcessingSessionDto?> GetProcessingSessionAsync(string sessionId)
        {
            using var connection = _dbConnectionFactory.CreateConnection();
            
            const string sql = @"
                SELECT SessionId, ImageID as ImageId, ProcessingType, Status, ErrorMessage, DateCreated, CompletedDate
                FROM ProcessingSessions
                WHERE SessionId = @sessionId";

            return await connection.QueryFirstOrDefaultAsync<ProcessingSessionDto>(sql, new { sessionId });
        }

        public async Task<List<ProcessingResultDto>> GetProcessingResultsAsync(int documentId)
        {
            using var connection = _dbConnectionFactory.CreateConnection();
            
            const string sql = @"
                SELECT 
                    @documentId as OriginalImageId,
                    b.ResultImageID as ResultImageId,
                    ri.OriginalName as DocumentName,
                    dt.Name as DocumentType,
                    ri.PageCount,
                    b.PageIndex + 1 as StartPage,
                    b.PageIndex + ri.PageCount as EndPage,
                    'completed' as ProcessingStatus,
                    ri.FilePath
                FROM ImageBookmarks b
                INNER JOIN Images ri ON b.ResultImageID = ri.ID
                INNER JOIN ImageDocTypeMasterLists dt ON b.ImageDocumentTypeID = dt.ID
                WHERE b.ImageID = @documentId AND b.ResultImageID IS NOT NULL AND b.IsDeleted = 0
                ORDER BY b.PageIndex";

            var results = await connection.QueryAsync<ProcessingResultDto>(sql, new { documentId });
            return results.ToList();
        }

        // === DOCUMENT OPERATIONS ===

        public async Task<Image?> GetDocumentAsync(int documentId)
        {
            using var connection = _dbConnectionFactory.CreateConnection();
            
            const string sql = @"
                SELECT ID, DocTypeManualID, DocTypeAutoID, LoanID, DocumentDate, Comments, ParsedName,
                       OriginalName, PageCount, FilePath, CreatedBy, DateCreated, LastModified, IsDeleted
                FROM Images
                WHERE ID = @documentId";

            return await connection.QueryFirstOrDefaultAsync<Image>(sql, new { documentId });
        }

        public async Task<bool> UpdateDocumentTypeAsync(int documentId, int? documentTypeId, DateTime? documentDate, string? comments)
        {
            using var connection = _dbConnectionFactory.CreateConnection();
            
            const string sql = @"
                UPDATE Images 
                SET DocTypeManualID = @documentTypeId,
                    DocumentDate = @documentDate,
                    Comments = @comments,
                    LastModified = @lastModified
                WHERE ID = @documentId";

            var rowsAffected = await connection.ExecuteAsync(sql, new
            {
                documentTypeId,
                documentDate,
                comments,
                lastModified = DateTime.UtcNow,
                documentId
            });

            return rowsAffected > 0;
        }

        public async Task<int> CreateSplitDocumentAsync(int originalDocumentId, string newName, int pageCount, int documentTypeId, string filePath, int userId)
        {
            using var connection = _dbConnectionFactory.CreateConnection();
            
            // Get original document loan ID
            const string getLoanSql = "SELECT LoanID FROM Images WHERE ID = @originalDocumentId";
            var loanId = await connection.QueryFirstOrDefaultAsync<int?>(getLoanSql, new { originalDocumentId });
            
            if (!loanId.HasValue)
                throw new ArgumentException("Original document not found", nameof(originalDocumentId));

            const string insertSql = @"
                INSERT INTO Images (OriginalName, ParsedName, PageCount, LoanID, DocTypeManualID, FilePath, CreatedBy, DateCreated, LastModified, IsDeleted)
                VALUES (@OriginalName, @ParsedName, @PageCount, @LoanID, @DocTypeManualID, @FilePath, @CreatedBy, @DateCreated, @LastModified, 0);
                SELECT LAST_INSERT_ID();";

            var now = DateTime.UtcNow;
            var splitDocId = await connection.QuerySingleAsync<int>(insertSql, new
            {
                OriginalName = newName,
                ParsedName = newName,
                PageCount = pageCount,
                LoanID = loanId.Value,
                DocTypeManualID = documentTypeId,
                FilePath = filePath,
                CreatedBy = userId,
                DateCreated = now,
                LastModified = now
            });

            return splitDocId;
        }

        public async Task<bool> LinkBookmarkToResultAsync(int bookmarkId, int resultDocumentId)
        {
            using var connection = _dbConnectionFactory.CreateConnection();
            
            const string sql = @"
                UPDATE ImageBookmarks 
                SET ResultImageID = @resultDocumentId
                WHERE ID = @bookmarkId";

            var rowsAffected = await connection.ExecuteAsync(sql, new { resultDocumentId, bookmarkId });
            return rowsAffected > 0;
        }

        // === THUMBNAILS ===

        public async Task<List<PageThumbnailDto>> GetThumbnailsAsync(int documentId)
        {
            using var connection = _dbConnectionFactory.CreateConnection();
            
            const string sql = @"
                SELECT 
                    t.PageNumber,
                    t.ThumbnailUrl,
                    t.Width,
                    t.Height,
                    CASE WHEN b.ID IS NOT NULL THEN 1 ELSE 0 END as HasBookmark,
                    CASE 
                        WHEN b.ID IS NOT NULL THEN 
                            CASE WHEN dt.IsGeneric = 1 THEN 'generic' ELSE 'normal' END
                        ELSE NULL 
                    END as BookmarkType,
                    dt.Name as DocumentTypeName
                FROM PageThumbnails t
                LEFT JOIN ImageBookmarks b ON t.ImageID = b.ImageID AND t.PageNumber = b.PageIndex + 1 AND b.IsDeleted = 0
                LEFT JOIN ImageDocTypeMasterLists dt ON b.ImageDocumentTypeID = dt.ID
                WHERE t.ImageID = @documentId
                ORDER BY t.PageNumber";

            var results = await connection.QueryAsync<PageThumbnailDto>(sql, new { documentId });
            return results.ToList();
        }

        public async Task<List<ThumbnailBookmarkDto>> GetThumbnailBookmarksAsync(int documentId)
        {
            using var connection = _dbConnectionFactory.CreateConnection();
            
            const string sql = @"
                SELECT 
                    b.PageIndex,
                    dt.Name as DocumentTypeName,
                    dt.IsGeneric,
                    b.ImageDocumentTypeID as DocumentTypeId
                FROM ImageBookmarks b
                INNER JOIN ImageDocTypeMasterLists dt ON b.ImageDocumentTypeID = dt.ID
                WHERE b.ImageID = @documentId AND b.IsDeleted = 0";

            var results = await connection.QueryAsync<ThumbnailBookmarkDto>(sql, new { documentId });
            return results.ToList();
        }

        // === UTILITIES ===

        public string BuildBookmarkText(string documentTypeName, int documentTypeId, DateTime? documentDate, string? comments)
        {
            var dateString = documentDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "";
            return $"{documentTypeName} | {documentTypeId} | {dateString} | {comments ?? ""}";
        }

        public (string DocumentTypeName, int DocumentTypeId, DateTime? DocumentDate, string? Comments) ParseBookmarkText(string text)
        {
            var parts = text.Split('|', StringSplitOptions.None)
                          .Select(p => p.Trim())
                          .ToArray();

            if (parts.Length < 4)
                return (string.Empty, 0, null, null);

            var documentTypeName = parts[0];
            var documentTypeId = int.TryParse(parts[1], out var id) ? id : 0;
            var documentDate = DateTime.TryParseExact(parts[2], "yyyy-MM-dd", 
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ? date : (DateTime?)null;
            var comments = string.IsNullOrWhiteSpace(parts[3]) ? null : parts[3];

            return (documentTypeName, documentTypeId, documentDate, comments);
        }
    }
}
