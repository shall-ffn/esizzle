# Lambda Callback Payload Mismatch Fix

**Issue ID**: ESIZZLE-CALLBACK-001  
**Priority**: Critical  
**Component**: Lambda → API Integration  
**Date**: 2025-08-15

## Problem Summary

Lambda callback payload structure doesn't match API endpoint expectations, causing processing result linking to fail.

## Current State vs Expected State

### What Lambda Sends (`api_callbacks.py:194-213`)
```python
# Lambda sends rich result object
{
  'originalImageId': result['originalImageId'],
  'resultImageId': result['resultImageId'], 
  'startPage': result['startPage'],
  'endPage': result['endPage'],
  'pageCount': result['pageCount'],
  'documentTypeId': result['documentTypeId'],
  'documentTypeName': result['documentTypeName'],
  'filename': result['filename'],
  'processingStatus': result['processingStatus'],
  'bookmarkId': result['bookmarkId']  # optional
}
```

### What API Currently Expects (`IndexingController.cs:476-485`)
```csharp
// API expects simplified structure
public class LinkResultsRequest
{
    public List<BookmarkResult> BookmarkResults { get; set; }
}

public class BookmarkResult
{
    public int BookmarkId { get; set; }
    public int ResultDocumentId { get; set; }
}
```

## Impact
- **Lambda callbacks fail** when attempting to link split documents
- **Processing results are not linked** to original bookmarks
- **Split documents remain orphaned** in the database
- **UI doesn't show processing completion** properly

## Root Cause
Data contract mismatch between Lambda callback implementation and API endpoint design. Lambda was designed to send comprehensive processing metadata, but API only accepts minimal bookmark linking data.

## Solution

### 1. Update API Data Contracts

**File**: `/esizzle-api/src/EsizzleAPI/Controllers/IndexingController.cs`

```csharp
public class LinkResultsRequest
{
    public List<ProcessingResult> Results { get; set; } = new();
    public DateTime Timestamp { get; set; }
    public int TotalResults { get; set; }
}

public class ProcessingResult
{
    public int OriginalImageId { get; set; }
    public int ResultImageId { get; set; }
    public int StartPage { get; set; }
    public int EndPage { get; set; }
    public int PageCount { get; set; }
    public int DocumentTypeId { get; set; }
    public string DocumentTypeName { get; set; } = string.Empty;
    public string Filename { get; set; } = string.Empty;
    public string ProcessingStatus { get; set; } = string.Empty;
    public int? BookmarkId { get; set; } // Optional for generic breaks
}
```

### 2. Update LinkProcessingResults Method

```csharp
[HttpPost("{documentId}/link-results")]
[AllowAnonymous] // Lambda function access
public async Task<ActionResult> LinkProcessingResults(
    int documentId,
    [FromBody] LinkResultsRequest request)
{
    try
    {
        foreach (var result in request.Results)
        {
            if (result.BookmarkId.HasValue)
            {
                await _indexingRepository.LinkBookmarkToResultAsync(
                    result.BookmarkId.Value, 
                    result.ResultImageId);
            }
        }

        return NoContent();
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Failed to link processing results for document {DocumentId}", documentId);
        return StatusCode(500, new { message = "Failed to link processing results" });
    }
}
```

## Implementation Steps

### Phase 1: API Updates
1. ✅ Update `LinkResultsRequest` and `ProcessingResult` classes
2. ✅ Modify `LinkProcessingResults` method to handle new structure
3. ⏳ Test API endpoint with new payload structure
4. ⏳ Deploy API changes to development environment

### Phase 2: Validation  
1. ⏳ Test Lambda callback with updated API
2. ⏳ Verify bookmark-to-result linking works correctly
3. ⏳ Confirm processing status updates in UI
4. ⏳ Validate with both bookmark splitting and generic breaks

### Phase 3: Production Deployment
1. ⏳ Deploy API changes to staging
2. ⏳ Run end-to-end integration tests
3. ⏳ Deploy to production
4. ⏳ Monitor Lambda callback success rates

## Testing Checklist

- [ ] Lambda callback succeeds with new payload structure
- [ ] Bookmark linking works for document splitting
- [ ] Generic breaks (no bookmarkId) are handled correctly
- [ ] Processing status updates propagate to frontend
- [ ] UI shows completion status properly
- [ ] Error handling works for malformed payloads

## Risk Assessment

**Risk Level**: Low  
**Backwards Compatibility**: Breaking change for internal API only  
**Rollback Plan**: Revert API changes if Lambda integration fails

## Related Components

- **Lambda**: `api_callbacks.py` - No changes needed
- **API**: `IndexingController.cs` - Data contract updates required
- **Frontend**: No changes needed (polls processing status separately)
- **Database**: No schema changes required

## Success Criteria

✅ Lambda callbacks complete successfully  
✅ Split documents are properly linked to bookmarks  
✅ UI shows processing completion status  
✅ No orphaned documents in database  
✅ Error rates < 1% for callback operations

---

**Author**: Esizzle Development Team  
**Reviewer**: [To be assigned]  
**Implementation Target**: Next sprint
