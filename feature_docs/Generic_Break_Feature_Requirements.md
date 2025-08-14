# Generic Break Feature Requirements Document

## Document Information
- **Project:** Esizzle Legacy Integration
- **Component:** Generic Document Break Processing
- **Version:** 1.0
- **Date:** January 2025
- **Author:** Technical Documentation
- **Based on:** Legacy Hydra Due Diligence System Analysis

## Overview

This document defines the requirements for implementing generic document break functionality in the esizzle system, based on detailed analysis of the legacy Hydra Due Diligence system behavior and database structure.

## Current State Analysis

### Legacy System Behavior (Confirmed)
**Source Document:** `twelve-mht` (15 pages total)

**Document Split Results:**
- **Pages 1-1:** 1 page document (generic break - status: Needs Work)
- **Pages 2-4:** 3 page document (generic break - status: Needs Work) 
- **Pages 5-10:** 6 page document (named: "Abstract of Title" - status: Production)
- **Pages 11-15:** 5 page document (generic break - status: Needs Work)

### Database Structure Analysis
**Generic Break Record Example:**
```sql
-- ImageBookmark table record for generic break
ID: 5477587
ImageID: 14324697
PageIndex: 2
Text: " | -1 | "  -- Pipe-delimited format with -1 as DocumentTypeID
ImageDocumentTypeID: -1  -- Special value indicating generic break
ResultImageID: 14371010  -- Populated after document splitting
CreatedBy: 21496
DateCreated: 2025-08-14 14:55:26
Deleted: 0
```

**Normal Break Record Example:**
```sql
-- ImageBookmark table record for normal break
ID: 5477582
ImageID: 14324698
PageIndex: 4
Text: "Abstract of Title | 7082 | |"  -- Pipe-delimited with real name & ID
ImageDocumentTypeID: 7082  -- Real document type ID
ResultImageID: 14371008  -- Populated after document splitting
CreatedBy: 21496
DateCreated: 2025-08-14 13:54:03
Deleted: 0
```

**Key Database Patterns:**
1. **Generic Break Identifier:** `ImageDocumentTypeID = -1` (ONLY indicator - no IsGeneric field exists)
2. **Text Format:** `" | -1 | "` (empty name, -1 ID, empty metadata)
3. **Status Tracking:** Generic documents get `ImageStatusTypeID = 20` (Needs Work)
4. **Named Documents:** Get `ImageStatusTypeID = 1` (Production)

## Functional Requirements

### FR-1: Generic Break Creation
**Priority:** P0 (Must Have)

**Description:** Users can create generic document breaks without document type assignment.

**Acceptance Criteria:**
- Users create generic breaks through UI controls (separate from document type selection)
- Generic breaks display with orange visual indicator  
- Multiple generic breaks can be created in one session before saving
- Generic breaks can be mixed with normal (typed) breaks in same document
- Orange "X" button removes selected generic breaks from thumbnail view

**Database Behavior:**
```sql
INSERT INTO ImageBookmark (ImageID, PageIndex, Text, ImageDocumentTypeID, CreatedBy, DateCreated)
VALUES (@ImageID, @PageIndex, ' | -1 | ', -1, @UserID, UTC_TIMESTAMP())
```

### FR-2: Document Splitting Logic
**Priority:** P0 (Must Have)

**Description:** System splits documents at break positions creating separate PDF files.

**Acceptance Criteria:**
- Each break creates new document starting at break page
- Page ranges calculated: Break at page N splits document at page N
- Last section includes all remaining pages after final break
- Original document remains intact
- Mixed break types handled correctly in same document

**Split Results Example:**
```
Original: 15-page "twelve-mht"
Breaks: Generic@1, Generic@2, Normal@5, Generic@11
Results:
- Document 1: Pages 1-1 (generic, status 20)
- Document 2: Pages 2-4 (generic, status 20) 
- Document 3: Pages 5-10 (named "Abstract of Title", status 1)
- Document 4: Pages 11-15 (generic, status 20)
```

### FR-3: File Naming and Storage
**Priority:** P0 (Must Have)

**Description:** System applies consistent S3 storage and naming for split documents.

**Acceptance Criteria:**

**S3 Storage Pattern:**
- Path: `IProcessing/Images/<new_image_id>.pdf`
- All documents (generic and named) follow same S3 path pattern
- No special naming suffixes in S3 filenames

**Database Naming:**
- `OriginalName`: Retains same name as source document
- Generic documents: `DocumentType` remains NULL
- Named documents: `DocumentType` set to selected type name

### FR-4: Status Management
**Priority:** P0 (Must Have)

**Description:** System properly manages document status lifecycle.

**Acceptance Criteria:**

**Status Transitions:**
- Generic documents: `ImageStatusTypeID = 20` (Needs Work)
- Named documents: `ImageStatusTypeID = 1` (Production)  
- Grid display: Production documents show green checkmark
- Search behavior: Both statuses appear in document grid

**Post-Processing Updates:**
- Users can later assign document types to generic documents
- Status transitions from 20 → 1 when document gets named/indexed
- Updates existing `Image` record (no new record creation)

### FR-5: Database Integration
**Priority:** P0 (Must Have)

**Description:** System correctly handles generic break data throughout the stack.

**Acceptance Criteria:**

**Backend Repository Fixes:**
```csharp
// Current (BROKEN) - references non-existent IsGeneric field
SELECT ID, Name, 0 as IsGeneric, Code FROM ImageDocTypeMasterList

// Required (CORRECT) - remove IsGeneric entirely
SELECT ID, Name, Code FROM ImageDocTypeMasterList

// Generic break detection in bookmarks (this is the ONLY way to detect generic breaks)
SELECT *, CASE WHEN ImageDocumentTypeID = -1 THEN 1 ELSE 0 END as IsGenericBreak
FROM ImageBookmark
```

**Document Creation Logic:**
- Generic breaks: Set `ImageDocumentTypeID = -1`, `ImageStatusTypeID = 20`
- Normal breaks: Set `ImageDocumentTypeID = selected_type`, `ImageStatusTypeID = 1`
- **No IsGeneric field exists** - detection is purely based on the -1 pattern

### FR-6: UI Visual Consistency
**Priority:** P0 (Must Have)

**Description:** Match legacy system visual design exactly.

**Acceptance Criteria:**
- Orange breaks: `#FFA500` / `Color.FromArgb(90, Color.Orange)`
- Green breaks: `#008000` / `Color.FromArgb(90, Color.Green)`
- Break text: "---GENERIC BREAK---" for generic, document type name for normal
- Thumbnail indicators: Orange border/accent for generic break pages
- Consistent bar styling, thickness, and positioning

## Technical Implementation

### Backend Changes Required

**1. Repository Layer Fixes:**
```csharp
// IndexingRepository.cs - Remove non-existent IsGeneric field
public async Task<List<DocumentTypeDto>> GetDocumentTypesByOfferingAsync(int offeringId)
{
    const string sql = @"
        SELECT ID as Id, Name, Code 
        FROM ImageDocTypeMasterList 
        WHERE Code = (SELECT IndexCode FROM Offerings WHERE OfferingID = @offeringId)";
    // REMOVED: "0 as IsGeneric" (field doesn't exist)
}

public async Task<List<BookmarkDto>> GetBookmarksByDocumentAsync(int documentId)
{
    const string sql = @"
        SELECT b.*, dt.Name as DocumentTypeName,
               CASE WHEN b.ImageDocumentTypeID = -1 THEN 1 ELSE 0 END as IsGenericBreak
        FROM ImageBookmark b
        LEFT JOIN ImageDocTypeMasterList dt ON b.ImageDocumentTypeID = dt.ID
        WHERE b.ImageID = @documentId";
    // Generic detection based purely on ImageDocumentTypeID = -1
}
```

**2. Lambda Processing Service:**
```csharp
public class DocumentSplittingService 
{
    public async Task<List<SplitResult>> ProcessDocumentBreaks(int documentId, List<BookmarkInfo> breaks)
    {
        foreach (var bookmark in breaks.OrderBy(b => b.PageIndex))
        {
            var newImageId = await CreateSplitDocument(documentId, pageRange);
            
            // Set status based on break type
            var status = bookmark.ImageDocumentTypeID == -1 ? 20 : 1; // Needs Work : Production
            var docType = bookmark.ImageDocumentTypeID == -1 ? null : bookmark.DocumentTypeName;
            
            await UpdateDocumentStatus(newImageId, status, docType);
        }
    }
}
```

**3. Generic Break Detection:**
```csharp
public static bool IsGenericBreak(BookmarkDto bookmark)
{
    return bookmark.ImageDocumentTypeId == -1;
}

public static string GetBreakDisplayText(BookmarkDto bookmark)
{
    return IsGenericBreak(bookmark) ? "---GENERIC BREAK---" : bookmark.DocumentTypeName;
}
```

### Frontend Changes Required

**1. Break Creation UI:**
```typescript
// Add generic break creation method
async createGenericBreak(pageIndex: number): Promise<BookmarkDto> {
  const request = {
    pageIndex: pageIndex,
    imageDocumentTypeId: -1,  // Special generic break ID
    text: " | -1 | ",         // Generic break text format
    documentDate: null,
    comments: ""
  }
  
  return await indexingApi.createBookmark(documentId, request)
}
```

**2. Visual Rendering Updates:**
```vue
<!-- PageBreakVisualization.vue -->
<template>
  <div :class="getBreakClass(pageBreak)">
    <span>{{ getBreakText(pageBreak) }}</span>
  </div>
</template>

<script>
const getBreakClass = (pageBreak: PageBreakAnnotation) => {
  const isGeneric = pageBreak.imageDocumentTypeId === -1
  return isGeneric ? 'page-break-generic bg-orange-500' : 'page-break-normal bg-green-500'
}

const getBreakText = (pageBreak: PageBreakAnnotation) => {
  return pageBreak.imageDocumentTypeId === -1 ? '---GENERIC BREAK---' : pageBreak.displayText
}
</script>
```

## Data Flow

### Generic Break Creation Flow
```
1. User clicks generic break control → UI captures current page
2. Create bookmark with ImageDocumentTypeID = -1
3. Save to database: Text = " | -1 | ", ImageDocumentTypeID = -1
4. UI renders orange break indicator at page location
5. User can repeat for multiple pages before saving document
```

### Document Splitting Flow
```
1. User clicks save → Lambda processes all bookmarks
2. For each break:
   - Split PDF at PageIndex
   - Create new Image record with same OriginalName
   - Generic break: Status = 20, DocumentType = NULL
   - Normal break: Status = 1, DocumentType = assigned type
   - Store in S3: IProcessing/Images/<new_image_id>.pdf
3. Update ResultImageID in bookmark records
4. Return processing results to frontend
```

### Post-Split Naming Flow
```
1. User selects generic document from grid
2. User assigns document type through indexing UI
3. Update Image record: DocumentType = selected, ImageStatusTypeID = 1
4. Document status changes to Production (green checkmark in grid)
```

## Testing Requirements

### Test Cases

**TC-1: Single Generic Break**
- Create 1 generic break in 10-page document
- Verify: 2 documents created, generic has status 20, named has status 1
- Verify: Generic document shows "Needs Work", named shows "Production"

**TC-2: Mixed Break Types**
- Create mix of generic and normal breaks (matching legacy screenshot pattern)
- Verify: Correct page distributions (1pg, 3pg, 6pg, 5pg)
- Verify: Status assignments (20 for generic, 1 for named)
- Verify: Visual indicators (orange for generic, green for normal)

**TC-3: Database Consistency**
- Verify: ImageDocumentTypeID = -1 for generic breaks
- Verify: Text format " | -1 | " for generic breaks
- Verify: Repository queries remove non-existent IsGeneric field references
- Verify: Generic detection works purely on ImageDocumentTypeID = -1 pattern

**TC-4: Post-Split Naming**
- Create generic document, then assign document type
- Verify: Status transition 20 → 1
- Verify: DocumentType field updated from NULL to type name
- Verify: Same Image record updated (not new record)

**TC-5: S3 Storage Verification**
- Verify: All documents stored as IProcessing/Images/<image_id>.pdf
- Verify: OriginalName preserved in database
- Verify: No special file naming for generic documents

## Success Criteria

1. **Functional Parity:** System behavior exactly matches legacy screenshots and database patterns
2. **Database Integrity:** ImageDocumentTypeID = -1 pattern correctly implemented
3. **Status Management:** Proper 20/1 status assignments and transitions
4. **Visual Consistency:** Orange/green break indicators match legacy colors and styling
5. **Naming Logic:** S3 storage follows IProcessing/Images/<id>.pdf pattern
6. **Repository Fixes:** Remove non-existent IsGeneric field references from queries

## Implementation Priority

**Phase 1 (Critical):**
- ✅ **FIXED: Single Bookmark at Page 0 Logic** - Updated `lambda_function.py` to properly handle IndexOnly cases
- Fix hardcoded IsGeneric queries in IndexingRepository (remove non-existent field)
- Implement generic break detection (ImageDocumentTypeID = -1)
- Add Lambda processing logic for status assignment

## Recent Fixes Applied

### ✅ Single Bookmark at Page 0 Logic Fix (January 2025)
**Issue:** Lambda function incorrectly processed single bookmarks at page 0 as document splits instead of index-only operations.

**Root Cause:** The `calculate_split_ranges()` function in `lambda_function.py` treated every bookmark as a split point, ignoring the special case documented in the requirements.

**Fix Applied:**
- Modified `calculate_split_ranges()` to detect single bookmark at page 0
- Returns empty ranges for IndexOnly cases with warning log
- Added early return in `process_document_splitting()` when no ranges are generated
- API controller already correctly handles IndexOnly cases locally

**Files Modified:**
- `/lambda/pdf-processor/lambda_function.py` (lines 314-319, 224-229)

**Behavior After Fix:**
- Single bookmark at page 0 → API handles locally (no Lambda call)
- If somehow sent to Lambda → Returns empty ranges, logs warning, completes with no processing
- Multiple bookmarks → Normal Lambda splitting behavior
- Single bookmark not at page 0 → Normal Lambda splitting behavior

**Phase 2 (Core Features):**
- UI controls for generic break creation/removal  
- Visual rendering with orange/green indicators
- Document splitting with proper status assignments

**Phase 3 (Polish):**
- Post-split naming workflow
- Grid status indicators (green checkmarks)
- Comprehensive testing and validation

## Dependencies

- Backend repository layer fixes (remove IsGeneric references) - **CRITICAL**
- Lambda PDF splitting service with status logic
- Frontend break creation UI components
- S3 document storage integration
- Database schema validation (ImageDocumentTypeID = -1 support)

## Out of Scope

- Changing S3 storage patterns or naming conventions
- Bulk operations on generic documents
- Advanced generic document type categorization
- Migration of existing generic documents

## Key Findings Summary

Based on comprehensive legacy system analysis:

1. **No `IsGeneric` field exists** in `ImageDocTypeMasterList` - this was incorrectly assumed
2. **Generic break detection** relies solely on `ImageDocumentTypeID = -1` in `ImageBookmark`
3. **Database patterns** are consistent: `-1` = generic, real ID = typed document
4. **Visual styling** uses orange (#FFA500) for generic, green (#008000) for normal breaks
5. **Status management** follows 20 (Needs Work) vs 1 (Production) pattern
6. **File storage** uses identical S3 paths for both break types

This feature maintains strict compatibility with the legacy Hydra Due Diligence system while integrating seamlessly with the modern esizzle architecture.
