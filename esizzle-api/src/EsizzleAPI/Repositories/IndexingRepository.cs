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
            Console.WriteLine($"[DEBUG] GetDocumentTypesByOfferingAsync called with offeringId: {offeringId}");
            
            try
            {
                using var connection = _dbConnectionFactory.CreateConnection();
                Console.WriteLine($"[DEBUG] Database connection created successfully");
                
                // First, check if the offering exists
                var offeringCheck = await connection.QueryFirstOrDefaultAsync<(int OfferingID, string IndexCode)>(
                    "SELECT OfferingID, IndexCode FROM Offerings WHERE OfferingID = @offeringId", 
                    new { offeringId });
                
                if (offeringCheck.OfferingID == 0)
                {
                    Console.WriteLine($"[DEBUG] ERROR: OfferingID {offeringId} not found in Offerings table");
                    return new List<DocumentTypeDto>();
                }
                
                Console.WriteLine($"[DEBUG] Found offering {offeringCheck.OfferingID} with IndexCode: '{offeringCheck.IndexCode}'");
                
                // Check how many document types exist for this IndexCode
                var typeCount = await connection.QuerySingleAsync<int>(
                    "SELECT COUNT(*) FROM ImageDocTypeMasterList WHERE Code = @indexCode",
                    new { indexCode = offeringCheck.IndexCode });
                
                Console.WriteLine($"[DEBUG] Found {typeCount} document types with IndexCode '{offeringCheck.IndexCode}'");
                
                // Now run the main query
                var sql = @"
                    SELECT dt.ID as Id, dt.Name, dt.Code
                    FROM ImageDocTypeMasterList dt
                    INNER JOIN Offerings o ON dt.Code = o.IndexCode
                    WHERE o.OfferingID = @offeringId";
                
                object parameters;
                
                if (!string.IsNullOrWhiteSpace(search))
                {
                    sql += " AND dt.Name LIKE @search";
                    parameters = new { offeringId, search = $"%{search}%" };
                    Console.WriteLine($"[DEBUG] Using search filter: '{search}'");
                }
                else
                {
                    parameters = new { offeringId };
                }
                
                sql += " ORDER BY dt.Name";
                
                Console.WriteLine($"[DEBUG] Executing main query...");
                var results = await connection.QueryAsync<DocumentTypeDto>(sql, parameters);
                var resultList = results.ToList();
                
                Console.WriteLine($"[DEBUG] Query completed successfully. Found {resultList.Count} document types");
                
                return resultList;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DEBUG] EXCEPTION in GetDocumentTypesByOfferingAsync: {ex.GetType().Name}: {ex.Message}");
                Console.WriteLine($"[DEBUG] Stack trace: {ex.StackTrace}");
                throw;
            }
        }

        public async Task<DocumentTypeDto?> GetDocumentTypeByIdAsync(int documentTypeId)
        {
            using var connection = _dbConnectionFactory.CreateConnection();
            
            const string sql = @"
                SELECT ID as Id, Name, Code
                FROM ImageDocTypeMasterList
                WHERE ID = @documentTypeId";

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
                    CASE WHEN b.ImageDocumentTypeID = -1 THEN 1 ELSE 0 END as IsGeneric,
                    b.DateCreated,
                    b.ResultImageID as ResultImageId,
                    CASE WHEN b.ResultImageID IS NULL THEN 1 ELSE 0 END as CanEdit,
                    b.CreatedBy
                FROM ImageBookmark b
                LEFT JOIN ImageDocTypeMasterList dt ON b.ImageDocumentTypeID = dt.ID
                WHERE b.ImageID = @documentId AND b.Deleted = 0
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
                    CASE WHEN b.ImageDocumentTypeID = -1 THEN 1 ELSE 0 END as IsGeneric,
                    b.DateCreated,
                    b.ResultImageID as ResultImageId,
                    CASE WHEN b.ResultImageID IS NULL THEN 1 ELSE 0 END as CanEdit,
                    b.CreatedBy
                FROM ImageBookmark b
                LEFT JOIN ImageDocTypeMasterList dt ON b.ImageDocumentTypeID = dt.ID
                WHERE b.ID = @bookmarkId AND b.Deleted = 0";

            return await connection.QueryFirstOrDefaultAsync<BookmarkDto>(sql, new { bookmarkId });
        }

        public async Task<BookmarkDto> CreateBookmarkAsync(CreateBookmarkRequest request, int userId)
        {
            using var connection = _dbConnectionFactory.CreateConnection();
            
            // Build pipe-delimited text field
            var text = BuildBookmarkText(request.DocumentTypeName, request.DocumentTypeId, 
                                       request.DocumentDate, request.Comments);

            const string sql = @"
                INSERT INTO ImageBookmark (ImageID, PageIndex, Text, ImageDocumentTypeID, CreatedBy, DateCreated, Deleted)
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

        /// <summary>
        /// Creates a generic document break (ImageDocumentTypeID = -1)
        /// </summary>
        public async Task<BookmarkDto> CreateGenericBreakAsync(int imageId, int pageIndex, int userId)
        {
            var request = new CreateBookmarkRequest
            {
                ImageId = imageId,
                PageIndex = pageIndex,
                DocumentTypeId = -1, // Generic break identifier
                DocumentTypeName = "", // Empty for generic breaks
                DocumentDate = null,
                Comments = null
            };
            
            return await CreateBookmarkAsync(request, userId);
        }

        public async Task<BookmarkDto?> UpdateBookmarkAsync(int documentId, int bookmarkId, UpdateBookmarkRequest request)
        {
            using var connection = _dbConnectionFactory.CreateConnection();
            
            // First check if bookmark exists and can be updated
            const string checkSql = @"
                SELECT ID, ResultImageID
                FROM ImageBookmark
                WHERE ID = @bookmarkId AND ImageID = @documentId AND Deleted = 0";

            var bookmark = await connection.QueryFirstOrDefaultAsync<BookmarkCheckRow>(checkSql, new { bookmarkId, documentId });
            
            if (bookmark == null)
                return null; // Not found
            if (bookmark.ResultImageID != null)
                return null; // Cannot update processed bookmarks

            // Update text field with new values
            var text = BuildBookmarkText(request.DocumentTypeName, request.DocumentTypeId,
                                       request.DocumentDate, request.Comments);

            const string updateSql = @"
                UPDATE ImageBookmark 
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
                FROM ImageBookmark
                WHERE ID = @bookmarkId AND ImageID = @documentId AND Deleted = 0";

            var bookmark = await connection.QueryFirstOrDefaultAsync<BookmarkCheckRow>(checkSql, new { bookmarkId, documentId });
            
            if (bookmark == null)
                return false; // Not found
            if (bookmark.ResultImageID != null)
                return false; // Cannot delete processed bookmarks

            const string deleteSql = @"
                UPDATE ImageBookmark 
                SET Deleted = 1
                WHERE ID = @bookmarkId";

            var rowsAffected = await connection.ExecuteAsync(deleteSql, new { bookmarkId });
            return rowsAffected > 0;
        }

        public async Task<BookmarkValidationResult> ValidateBookmarksAsync(int documentId, List<int> bookmarkIds)
        {
            using var connection = _dbConnectionFactory.CreateConnection();
            
            var result = new BookmarkValidationResult { IsValid = true };

            // If no bookmarks were provided, there's nothing to validate.
            // Avoid issuing a SQL query with an empty IN () clause which can cause a syntax error.
            if (bookmarkIds == null || bookmarkIds.Count == 0)
            {
                return result;
            }

            // Check if all bookmarks exist and belong to the document
            const string sql = @"
                SELECT ID, PageIndex, ResultImageID
                FROM ImageBookmark
                WHERE ID IN @bookmarkIds AND ImageID = @documentId AND Deleted = 0";

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
                UPDATE Image 
                SET DocTypeManualID = @DocumentTypeId, 
                    DocumentDate = @DocumentDate, 
                    Comments = @Comments, 
                    DateUpdated = @DateUpdated
                WHERE ID = @DocumentId";

            var rowsUpdated = await connection.ExecuteAsync(updateDocSql, new
            {
                DocumentTypeId = metadata.DocumentTypeId,
                DocumentDate = metadata.DocumentDate,
                Comments = metadata.Comments,
                DateUpdated = DateTime.UtcNow,
                DocumentId = documentId
            });

            if (rowsUpdated == 0)
                throw new ArgumentException("Document not found", nameof(documentId));

            // Create processing session record (optional table in legacy DB)
            var sessionId = Guid.NewGuid().ToString();
            var dateCreated = DateTime.UtcNow;

            try
            {
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
            }
            catch
            {
                // ProcessingSessions table may not exist in legacy DB; ignore
            }

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

            try
            {
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
            }
            catch
            {
                // ProcessingSessions table may not exist in legacy DB; ignore
            }

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
            const int maxRetries = 3;
            const int baseDelayMs = 500;
            
            for (int attempt = 0; attempt <= maxRetries; attempt++)
            {
                try
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

                    if (rowsAffected > 0)
                    {
                        if (attempt > 0)
                        {
                            Console.WriteLine($"[UpdateProcessingSessionAsync] Successfully updated session {sessionId} after {attempt + 1} attempts");
                        }
                        return true;
                    }

                    // Session not found - check if it exists before retrying
                    var exists = await SessionExistsAsync(sessionId);
                    if (!exists && attempt < maxRetries)
                    {
                        Console.WriteLine($"[UpdateProcessingSessionAsync] Session {sessionId} not found (attempt {attempt + 1}), retrying...");
                        
                        // Exponential backoff
                        var delay = baseDelayMs * (int)Math.Pow(2, attempt);
                        await Task.Delay(delay);
                        continue;
                    }

                    Console.WriteLine($"[UpdateProcessingSessionAsync] Session {sessionId} not found after {attempt + 1} attempts");
                    return false;
                }
                catch (Exception ex)
                {
                    if (attempt == maxRetries)
                    {
                        Console.WriteLine($"[UpdateProcessingSessionAsync] Failed to update session {sessionId} after {maxRetries + 1} attempts: {ex.Message}");
                        return false;
                    }
                    
                    Console.WriteLine($"[UpdateProcessingSessionAsync] Error updating session {sessionId} (attempt {attempt + 1}): {ex.Message}, retrying...");
                    
                    // Exponential backoff
                    var delay = baseDelayMs * (int)Math.Pow(2, attempt);
                    await Task.Delay(delay);
                }
            }

            return false;
        }

        public async Task<bool> SessionExistsAsync(string sessionId)
        {
            using var connection = _dbConnectionFactory.CreateConnection();
            
            try
            {
                const string sql = @"
                    SELECT COUNT(1) 
                    FROM ProcessingSessions 
                    WHERE SessionId = @sessionId";
                var count = await connection.QuerySingleAsync<int>(sql, new { sessionId });
                return count > 0;
            }
            catch
            {
                // Table may not exist; return false for legacy DB
                return false;
            }
        }

        public async Task<ProcessingSessionDto?> GetProcessingSessionAsync(string sessionId)
        {
            using var connection = _dbConnectionFactory.CreateConnection();
            
            try
            {
                const string sql = @"
                    SELECT SessionId, ImageID as ImageId, ProcessingType, Status, ErrorMessage, DateCreated, CompletedDate
                    FROM ProcessingSessions
                    WHERE SessionId = @sessionId";

                return await connection.QueryFirstOrDefaultAsync<ProcessingSessionDto>(sql, new { sessionId });
            }
            catch
            {
                // Table may not exist; return null for legacy DB
                return null;
            }
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
                    ri.Path as FilePath
                FROM ImageBookmark b
                INNER JOIN Image ri ON b.ResultImageID = ri.ID
                INNER JOIN ImageDocTypeMasterList dt ON b.ImageDocumentTypeID = dt.ID
                WHERE b.ImageID = @documentId AND b.ResultImageID IS NOT NULL AND b.Deleted = 0
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
                       OriginalName, PageCount, Path as FilePath, CreatedBy, DateCreated, DateUpdated as LastModified, Deleted as IsDeleted
                FROM Image
                WHERE ID = @documentId";

            return await connection.QueryFirstOrDefaultAsync<Image>(sql, new { documentId });
        }

        public async Task<bool> UpdateDocumentTypeAsync(int documentId, int? documentTypeId, DateTime? documentDate, string? comments)
        {
            using var connection = _dbConnectionFactory.CreateConnection();
            
            const string sql = @"
                UPDATE Image 
                SET DocTypeManualID = @documentTypeId,
                    DocumentDate = @documentDate,
                    Comments = @comments,
                    DateUpdated = @lastModified
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
            const string getLoanSql = "SELECT LoanID FROM Image WHERE ID = @originalDocumentId";
            var loanId = await connection.QueryFirstOrDefaultAsync<int?>(getLoanSql, new { originalDocumentId });
            
            if (!loanId.HasValue)
                throw new ArgumentException("Original document not found", nameof(originalDocumentId));

            // Determine status based on document type (generic vs normal)
            var statusId = documentTypeId == -1 ? 20 : 1; // 20 = Needs Work, 1 = Production
            var docTypeId = documentTypeId == -1 ? (int?)null : documentTypeId; // NULL for generic breaks
            
            const string insertSql = @"
                INSERT INTO Image (OriginalName, ParsedName, PageCount, LoanID, DocTypeManualID, ImageStatusTypeID, Path, CreatedBy, DateCreated, DateUpdated, Deleted)
                VALUES (@OriginalName, @ParsedName, @PageCount, @LoanID, @DocTypeManualID, @ImageStatusTypeID, @Path, @CreatedBy, @DateCreated, @DateUpdated, 0);
                SELECT LAST_INSERT_ID();";

            var now = DateTime.UtcNow;
            var splitDocId = await connection.QuerySingleAsync<int>(insertSql, new
            {
                OriginalName = newName,
                ParsedName = newName,
                PageCount = pageCount,
                LoanID = loanId.Value,
                DocTypeManualID = docTypeId,
                ImageStatusTypeID = statusId,
                Path = filePath,
                CreatedBy = userId,
                DateCreated = now,
                DateUpdated = now
            });

            return splitDocId;
        }

        public async Task<bool> LinkBookmarkToResultAsync(int bookmarkId, int resultDocumentId)
        {
            using var connection = _dbConnectionFactory.CreateConnection();
            
            const string sql = @"
                UPDATE ImageBookmark 
                SET ResultImageID = @resultDocumentId
                WHERE ID = @bookmarkId";

            var rowsAffected = await connection.ExecuteAsync(sql, new { resultDocumentId, bookmarkId });
            return rowsAffected > 0;
        }

        // === THUMBNAILS ===

        public async Task<List<PageThumbnailDto>> GetThumbnailsAsync(int documentId)
        {
            using var connection = _dbConnectionFactory.CreateConnection();
            
            try
            {
                const string sql = @"
                    SELECT 
                        t.PageNumber,
                        t.ThumbnailUrl,
                        t.Width,
                        t.Height,
                        CASE WHEN b.ID IS NOT NULL THEN 1 ELSE 0 END as HasBookmark,
                        CASE WHEN b.ID IS NOT NULL THEN 'normal' ELSE NULL END as BookmarkType,
                        dt.Name as DocumentTypeName
                    FROM PageThumbnails t
                    LEFT JOIN ImageBookmark b ON t.ImageID = b.ImageID AND t.PageNumber = b.PageIndex + 1 AND b.Deleted = 0
                    LEFT JOIN ImageDocTypeMasterList dt ON b.ImageDocumentTypeID = dt.ID
                    WHERE t.ImageID = @documentId
                    ORDER BY t.PageNumber";

                var results = await connection.QueryAsync<PageThumbnailDto>(sql, new { documentId });
                return results.ToList();
            }
            catch
            {
                // PageThumbnails may not exist in legacy DB
                return new List<PageThumbnailDto>();
            }
        }

        public async Task<List<ThumbnailBookmarkDto>> GetThumbnailBookmarksAsync(int documentId)
        {
            using var connection = _dbConnectionFactory.CreateConnection();
            
            try
            {
                const string sql = @"
                    SELECT 
                        b.PageIndex,
                        dt.Name as DocumentTypeName,
                        CASE WHEN b.ImageDocumentTypeID = -1 THEN 1 ELSE 0 END as IsGeneric,
                        b.ImageDocumentTypeID as DocumentTypeId
                    FROM ImageBookmark b
                    LEFT JOIN ImageDocTypeMasterList dt ON b.ImageDocumentTypeID = dt.ID
                    WHERE b.ImageID = @documentId AND b.Deleted = 0";

                var results = await connection.QueryAsync<ThumbnailBookmarkDto>(sql, new { documentId });
                return results.ToList();
            }
            catch
            {
                // In case of schema differences, fail gracefully
                return new List<ThumbnailBookmarkDto>();
            }
        }

        // === UTILITIES ===

        private sealed class BookmarkCheckRow
        {
            public int ID { get; set; }
            public int? ResultImageID { get; set; }
        }

        public string BuildBookmarkText(string documentTypeName, int documentTypeId, DateTime? documentDate, string? comments)
        {
            // Handle generic breaks with special format
            if (documentTypeId == -1)
            {
                return " | -1 | ";
            }
            
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
        
        /// <summary>
        /// Helper method to determine if a bookmark is a generic break
        /// </summary>
        public static bool IsGenericBreak(BookmarkDto bookmark)
        {
            return bookmark.ImageDocumentTypeId == -1;
        }
        
        /// <summary>
        /// Get display text for a break (generic or normal)
        /// </summary>
        public static string GetBreakDisplayText(BookmarkDto bookmark)
        {
            return IsGenericBreak(bookmark) ? "---GENERIC BREAK---" : bookmark.DocumentTypeName;
        }
    }
}
