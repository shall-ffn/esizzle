# ESIZZLE LEGACY INDEXING INTEGRATION PRD

## Document Information
- **Product:** Esizzle PDF Management Platform
- **Feature:** Legacy-Compatible Document Indexing and Breaking System
- **Version:** 1.0
- **Date:** January 2025
- **Author:** Development Team
- **Database:** LoanMaster (Legacy Compatible)

---

## 1. EXECUTIVE SUMMARY

### 1.1 Overview
Integrate legacy Hydra Due Diligence indexing and document breaking workflows into the modern Esizzle application to provide users with familiar, efficient document processing capabilities while maintaining full database compatibility.

### 1.2 Business Value
- **User Retention:** Maintain familiar workflow for existing Hydra users
- **Efficiency:** Reduce training time and increase productivity
- **Feature Parity:** Match critical legacy functionality exactly
- **Migration Path:** Enable smooth transition from legacy system
- **Data Integrity:** Preserve all existing database relationships and constraints

### 1.3 Success Metrics
- User can complete full indexing workflow in same number of steps as legacy
- Document processing time matches or improves upon legacy performance
- Zero data loss during document splitting operations
- 95%+ user satisfaction with workflow familiarity
- 100% database schema compatibility with existing legacy data

---

## 2. PROBLEM STATEMENT

### 2.1 Current State
The existing Esizzle app has basic page breaking functionality but lacks:
- Integrated index selection workflow matching legacy UI
- Centralized bookmark management interface
- Document result visualization after processing
- Legacy-compatible three-panel layout
- Proper integration with existing LoanMaster database schema

### 2.2 User Pain Points
- Disjointed workflow between document type selection and break creation
- No visibility into created bookmarks during workflow
- Missing feedback on document processing results
- Unfamiliar interface compared to legacy Hydra system
- Incomplete utilization of existing database structures

---

## 3. DATABASE SCHEMA REQUIREMENTS

### 3.1 Core Legacy Tables (Full Compatibility Required)

#### 3.1.1 Images Table
**Primary document storage with indexing fields**
```sql
CREATE TABLE Images (
    ID                      INT IDENTITY(1,1) PRIMARY KEY,
    DocTypeManualID         INT NULL,                    -- FK to ImageDocTypeMasterLists.ID (User-assigned)
    DocTypeAutoID           INT NULL,                    -- FK to ImageDocTypeMasterLists.ID (AI-suggested)
    LoanID                  INT NOT NULL,                -- FK to Loans.ID
    DocumentDate            DATETIME NULL,               -- Document date metadata
    Comments                NVARCHAR(MAX) NULL,          -- Additional notes
    ParsedName              NVARCHAR(255) NULL,          -- Processed document name
    OriginalName            NVARCHAR(255) NOT NULL,      -- Original filename
    PageCount               INT NOT NULL DEFAULT 1,      -- Number of pages
    FilePath                NVARCHAR(500) NOT NULL,      -- S3 path or file location
    CreatedBy               INT NOT NULL,                -- FK to Users.ID
    DateCreated             DATETIME NOT NULL DEFAULT GETDATE(),
    LastModified            DATETIME NOT NULL DEFAULT GETDATE(),
    IsDeleted               BIT NOT NULL DEFAULT 0
);

-- Indexing requirements for performance
CREATE INDEX IX_Images_LoanID ON Images (LoanID);
CREATE INDEX IX_Images_DocTypeManualID ON Images (DocTypeManualID);
CREATE INDEX IX_Images_CreatedBy ON Images (CreatedBy);
```

**Key Fields for Indexing Integration:**
- `DocTypeManualID`: Set when user assigns document type (single document or via bookmark)
- `DocTypeAutoID`: Reserved for future AI suggestions
- `DocumentDate`: Populated from right panel date input
- `Comments`: Additional metadata from indexing process
- `ParsedName`: Generated document name after processing

#### 3.1.2 ImageBookmarks Table
**Core table for document breaks and splitting**
```sql
CREATE TABLE ImageBookmarks (
    ID                      INT IDENTITY(1,1) PRIMARY KEY,
    ImageID                 INT NOT NULL,                -- FK to Images.ID (Source document)
    PageIndex               INT NOT NULL,                -- 0-based page number for break
    Text                    NVARCHAR(MAX) NOT NULL,      -- Pipe-delimited: "TypeName | TypeID | Date | Comments"
    ImageDocumentTypeID     INT NOT NULL,                -- FK to ImageDocTypeMasterLists.ID
    ResultImageID           INT NULL,                    -- FK to Images.ID (Created split document after processing)
    CreatedBy               INT NOT NULL,                -- FK to Users.ID
    DateCreated             DATETIME NOT NULL DEFAULT GETDATE(),
    IsDeleted               BIT NOT NULL DEFAULT 0
);

-- Required indexes
CREATE INDEX IX_ImageBookmarks_ImageID ON ImageBookmarks (ImageID);
CREATE INDEX IX_ImageBookmarks_ResultImageID ON ImageBookmarks (ResultImageID);
CREATE INDEX IX_ImageBookmarks_ImageDocumentTypeID ON ImageBookmarks (ImageDocumentTypeID);
```

**Text Field Format (Legacy Compatible):**
```
Format: "[DocumentTypeName] | [DocumentTypeID] | [DocumentDate] | [Comments]"
Example: "Promissory Note | 12 | 2025-01-07 | Borrower signature required"
```

**Processing Logic:**
- `PageIndex`: 0-based page where document split should occur
- `ImageDocumentTypeID`: Document type assigned to resulting split
- `ResultImageID`: Populated after processing with new Image.ID
- Multiple bookmarks = multiple document splits
- Single bookmark at page 0 = document naming only (no split)

#### 3.1.3 ImageDocTypeMasterLists Table
**Master catalog of all document types**
```sql
CREATE TABLE ImageDocTypeMasterLists (
    ID                      INT IDENTITY(1,1) PRIMARY KEY,
    Name                    NVARCHAR(255) NOT NULL,      -- Human-readable type name
    Code                    NVARCHAR(50) NOT NULL,       -- IndexCode: "ffn", "UPB", "strategic", etc.
    DateCreated             DATETIME NOT NULL DEFAULT GETDATE(),
    IsUsed                  BIT NOT NULL DEFAULT 1,       -- Active/inactive flag
    IsGeneric               BIT NOT NULL DEFAULT 0        -- Orange vs green display
);

-- Required indexes
CREATE INDEX IX_ImageDocTypeMasterLists_Code ON ImageDocTypeMasterLists (Code);
CREATE INDEX IX_ImageDocTypeMasterLists_IsUsed ON ImageDocTypeMasterLists (IsUsed);
```

**Document Type Categories by Code:**
- `ffn`: Standard FFN document types
- `UPB`: UPB-specific document classifications  
- `strategic`: Strategic Capital document types
- `CITIBCA`: Citibank BCA document types

#### 3.1.4 Offerings Table
**Project configurations that control available document types**
```sql
CREATE TABLE Offerings (
    OfferingID              INT IDENTITY(1,1) PRIMARY KEY,
    IndexCode               NVARCHAR(50) NOT NULL,       -- Controls available doc types
    OfferingName            NVARCHAR(255) NOT NULL,      -- Project name
    OfferingDescription     NVARCHAR(500) NULL,
    IsActive                BIT NOT NULL DEFAULT 1
);

-- Index for document type filtering
CREATE INDEX IX_Offerings_IndexCode ON Offerings (IndexCode);
```

**IndexCode Integration:**
- Each offering has specific IndexCode
- Document types filtered by matching Code field
- Right panel shows only relevant document types for current offering

### 3.2 Supporting Tables

#### 3.2.1 Loans Table
```sql
CREATE TABLE Loans (
    ID                      INT IDENTITY(1,1) PRIMARY KEY,
    AssetName               NVARCHAR(255) NOT NULL,
    AssetNo                 NVARCHAR(100) NOT NULL,
    AssetName2              NVARCHAR(255) NULL,
    BookBalance             DECIMAL(15,2) NULL,
    LoadedOn                DATETIME NOT NULL,
    SaleID                  INT NOT NULL                 -- FK to Sales.ID
);
```

#### 3.2.2 Sales Table
```sql
CREATE TABLE Sales (
    SaleID                  INT IDENTITY(1,1) PRIMARY KEY,
    SaleDesc                NVARCHAR(255) NOT NULL,
    OfferingID              INT NOT NULL,                -- FK to Offerings.OfferingID
    LoansCount              INT NOT NULL DEFAULT 0
);
```

### 3.3 Database Relationships

```
Offerings (1) -----> (M) Sales -----> (M) Loans -----> (M) Images
    |                                                       |
    |                                                       |
    |                                                       v
    +---> ImageDocTypeMasterLists (M) <------------- ImageBookmarks
         (filtered by IndexCode)                     (M bookmarks per image)
```

**Critical Relationships:**
1. `Offerings.IndexCode` filters `ImageDocTypeMasterLists.Code`
2. `Images.LoanID` → `Loans.ID` → `Sales.SaleID` → `Offerings.OfferingID`
3. `ImageBookmarks.ImageID` → `Images.ID` (source document)
4. `ImageBookmarks.ResultImageID` → `Images.ID` (created split document)
5. `ImageBookmarks.ImageDocumentTypeID` → `ImageDocTypeMasterLists.ID`

---

## 4. FUNCTIONAL REQUIREMENTS

### 4.1 Core Functional Requirements

#### FR-1: Integrated Index Selection Interface
**Priority:** P0 (Must Have)
**Description:** Right panel contains document type selection interface identical to legacy
**Database Operations:**
```sql
-- Load available document types for offering
SELECT idtml.ID, idtml.Name, idtml.IsGeneric
FROM ImageDocTypeMasterLists idtml
INNER JOIN Offerings o ON idtml.Code = o.IndexCode
WHERE o.OfferingID = @OfferingId AND idtml.IsUsed = 1
ORDER BY idtml.Name;
```

**Acceptance Criteria:**
- Display document types filtered by current offering's IndexCode
- Show search/filter functionality like legacy `txtIXFilter`
- Enable/disable "Set Break" mode based on selection
- Clear selection after bookmark creation
- Match legacy visual layout and behavior exactly

#### FR-2: Bookmark Creation Workflow  
**Priority:** P0 (Must Have)
**Description:** Click-to-create bookmarks with selected document type
**Database Operations:**
```sql
-- Create bookmark/break
INSERT INTO ImageBookmarks (ImageID, PageIndex, Text, ImageDocumentTypeID, CreatedBy)
VALUES (@ImageId, @PageIndex, @FormattedText, @DocumentTypeId, @UserId);

-- Update source image with document type if single bookmark at page 0
UPDATE Images 
SET DocTypeManualID = @DocumentTypeId, DocumentDate = @DocumentDate, Comments = @Comments
WHERE ID = @ImageId AND @PageIndex = 0 AND (SELECT COUNT(*) FROM ImageBookmarks WHERE ImageID = @ImageId) = 1;
```

**Text Field Population:**
```csharp
private string BuildBreakData(DocumentType docType, DateTime? docDate, string comments)
{
    return $"{docType.Name} | {docType.ID} | {docDate?.ToString("yyyy-MM-dd") ?? ""} | {comments ?? ""}";
}
```

**Acceptance Criteria:**
- Navigate to desired page in document viewer
- Select document type from right panel  
- Click "Set Break" or click at top of page to create bookmark
- Bookmark appears immediately in bottom panel
- Document type selection clears after bookmark creation
- PageBreakVisualization shows green (normal) or orange (generic) bookmark

#### FR-3: Bookmark Management Interface
**Priority:** P0 (Must Have)  
**Description:** Bottom panel displays and manages all bookmarks
**Database Operations:**
```sql
-- Load all bookmarks for document
SELECT ib.ID, ib.PageIndex, ib.Text, ib.ImageDocumentTypeID, 
       idtml.Name as DocumentTypeName, idtml.IsGeneric,
       ib.DateCreated, ib.ResultImageID
FROM ImageBookmarks ib
INNER JOIN ImageDocTypeMasterLists idtml ON ib.ImageDocumentTypeID = idtml.ID
WHERE ib.ImageID = @ImageId AND ib.IsDeleted = 0
ORDER BY ib.PageIndex;

-- Update bookmark
UPDATE ImageBookmarks 
SET ImageDocumentTypeID = @NewDocumentTypeId, 
    Text = @NewFormattedText
WHERE ID = @BookmarkId;

-- Delete bookmark
UPDATE ImageBookmarks SET IsDeleted = 1 WHERE ID = @BookmarkId;
```

**Acceptance Criteria:**
- List all bookmarks with format: "DocumentTypeName - Page X" 
- Allow bookmark editing (change document type, add comments)
- Support bookmark deletion with confirmation
- Click bookmark to navigate to page in viewer
- Show bookmark processing status after save operation

#### FR-4: Document Processing Results
**Priority:** P0 (Must Have)
**Description:** Display split document results after processing
**Database Operations:**
```sql
-- Process bookmarks and create split documents
-- (This is complex multi-step process handled by lambda function)

-- Load processing results
SELECT i.ID, i.OriginalName, i.ParsedName, i.PageCount, 
       idtml.Name as DocumentType, ib.PageIndex
FROM Images i
INNER JOIN ImageBookmarks ib ON i.ID = ib.ResultImageID
INNER JOIN ImageDocTypeMasterLists idtml ON ib.ImageDocumentTypeID = idtml.ID
WHERE ib.ImageID = @SourceImageId AND ib.IsDeleted = 0
ORDER BY ib.PageIndex;
```

**Processing Logic (Legacy Compatible):**
1. **No Bookmarks:** Update `Images.DocTypeManualID` only
2. **Single Bookmark at Page 0:** Update document type, no splitting
3. **Multiple Bookmarks:** Split document, create new Image records for each split
4. **Populate ResultImageID:** Link bookmarks to created documents

**Acceptance Criteria:**
- Show list of created documents with names and page counts
- Display processing status for each result
- Allow navigation to view individual split documents  
- Show error states for failed processing
- Match legacy result display format

#### FR-5: Save and Processing Controls
**Priority:** P0 (Must Have)
**Description:** Execute bookmark processing and document splitting
**Database Operations:**
```sql
-- Complete indexing actions (legacy compatibility)
UPDATE ImageActions 
SET CompletedDate = GETDATE(), CompletedBy = @UserId
WHERE ImageID = @ImageId AND ActionType = 'RequireIndex';
```

**Button State Logic (Exact Legacy Match):**
```typescript
const buttonText = computed(() => {
  if (selectedDocumentType.value && currentPage.value > 0) {
    return 'Set Break'
  }
  return 'Save Image Data'
})

const handleButtonClick = () => {
  if (selectedDocumentType.value) {
    createBookmarkAtCurrentPage()
  } else {
    saveImageDataOnly()
  }
}
```

**Acceptance Criteria:**
- "Save Image Data" button when no document type selected
- "Set Break" button when document type selected 
- Process all bookmarks to create split documents via lambda
- Show processing progress and completion status
- Match exact legacy button behavior and text

#### FR-6: Thumbnail View with Bookmark Indicators
**Priority:** P0 (Must Have)
**Description:** Right panel thumbnail view showing page previews with bookmark visual indicators
**Database Operations:**
```sql
-- Load bookmarks for thumbnail display
SELECT ib.PageIndex, ib.ImageDocumentTypeID, idtml.Name as DocumentTypeName, idtml.IsGeneric
FROM ImageBookmarks ib
INNER JOIN ImageDocTypeMasterLists idtml ON ib.ImageDocumentTypeID = idtml.ID
WHERE ib.ImageID = @ImageId AND ib.IsDeleted = 0;

-- Generate thumbnail URLs for pages
SELECT PageNumber, ThumbnailUrl, Width, Height
FROM PageThumbnails 
WHERE ImageID = @ImageId 
ORDER BY PageNumber;
```

**Visual Indicators (Legacy Compatible):**
- **Red border:** Pages with bookmarks/breaks (matching legacy styling)
- **Green accent:** Normal document type bookmarks
- **Orange accent:** Generic document type bookmarks
- **Page numbers:** Displayed below each thumbnail
- **Current page highlight:** Visual indicator for currently viewed page

**Acceptance Criteria:**
- Display page thumbnails in scrollable vertical panel
- Show colored borders around pages containing bookmarks
- Click thumbnail to navigate to that page in main viewer
- Automatically scroll to current page thumbnail when page changes
- Update bookmark indicators in real-time as bookmarks are added/removed
- Match legacy thumbnail size and spacing

### 4.2 UI/UX Requirements

#### UX-1: Legacy Layout Compatibility
**Priority:** P0 (Must Have)
**Layout Structure:**
- **Left Panel:** Loan navigation + Document results after processing
- **Center Panel:** PDF viewer with bookmark visualization
- **Right Panel:** Project info + Index selection + Document data
- **Bottom Panel:** Bookmarks list and management

**Panel Sizing (Match Legacy):**
- Left panel: ~300px width
- Right panel: ~350px width  
- Center panel: Remaining width
- Bottom panel: ~150px height (resizable)

#### UX-2: Visual Consistency with Legacy
**Priority:** P0 (Must Have)
- **Green bookmarks** for normal document types (`IsGeneric = 0`)
- **Orange bookmarks** for generic document types (`IsGeneric = 1`)
- Identical color schemes: #008000 (green), #FFA500 (orange)
- Legacy button styling and text
- Consistent font sizes and spacing

#### UX-3: Workflow State Management
**Priority:** P0 (Must Have)
- Clear visual feedback for current workflow state
- Disable irrelevant controls based on current state
- Proper validation before allowing operations
- Match legacy keyboard shortcuts and interactions

---

## 5. TECHNICAL SPECIFICATIONS

### 5.1 Component Architecture

#### 5.1.1 IndexingToolbar Component
**Location:** `esizzle-frontend/src/components/indexing/IndexingToolbar.vue`
**Purpose:** Replace DocumentClassifier with legacy-style index selection
**Database Integration:**
```typescript
interface DocumentTypeDto {
  id: number
  name: string
  isGeneric: boolean
  code: string
}

// API call to get filtered document types
async loadDocumentTypes(offeringId: number): Promise<DocumentTypeDto[]> {
  const response = await api.get(`/api/documents/document-types?offeringId=${offeringId}`)
  return response.data
}
```

**Props:**
```typescript
interface Props {
  selectedDocument: DocumentSummary | null
  availableDocumentTypes: DocumentTypeDto[]
  selectedDocumentType: DocumentTypeDto | null
  currentOffering: Offering | null
  loading: boolean
}
```

**Emits:**
```typescript
interface Emits {
  (e: 'document-type-selected', documentType: DocumentTypeDto): void
  (e: 'document-type-cleared'): void
  (e: 'set-break-clicked', pageIndex: number): void
  (e: 'save-image-data-clicked', data: DocumentMetadata): void
}
```

#### 5.1.2 BookmarksList Component
**Location:** `esizzle-frontend/src/components/indexing/BookmarksList.vue`
**Purpose:** Bottom panel bookmark management with full database integration

**Database Models:**
```typescript
interface BookmarkDto {
  id: number
  imageId: number
  pageIndex: number
  text: string
  imageDocumentTypeId: number
  documentTypeName: string
  isGeneric: boolean
  dateCreated: Date
  resultImageId?: number
  canEdit: boolean
}
```

**API Integration:**
```typescript
// CRUD operations for bookmarks
async createBookmark(bookmark: CreateBookmarkRequest): Promise<BookmarkDto>
async updateBookmark(bookmarkId: number, updates: UpdateBookmarkRequest): Promise<BookmarkDto>  
async deleteBookmark(bookmarkId: number): Promise<void>
async loadBookmarks(imageId: number): Promise<BookmarkDto[]>
```

**Features:**
- Real-time bookmark list with database sync
- Inline editing with immediate database updates
- Page navigation on bookmark click
- Delete confirmation with database soft-delete
- Processing status indicators

#### 5.1.3 DocumentResultsList Component
**Location:** `esizzle-frontend/src/components/indexing/DocumentResultsList.vue`
**Purpose:** Show processing results in left panel

**Database Models:**
```typescript
interface ProcessingResultDto {
  originalImageId: number
  resultImageId: number
  documentName: string
  documentType: string
  pageCount: number
  pageRange: [number, number]
  processingStatus: 'pending' | 'completed' | 'error'
  filePath?: string
}
```

#### 5.1.4 ThumbnailView Component
**Location:** `esizzle-frontend/src/components/indexing/ThumbnailView.vue`
**Purpose:** Right panel thumbnail view with bookmark indicators (legacy compatible)

**Database Models:**
```typescript
interface PageThumbnailDto {
  pageNumber: number
  thumbnailUrl: string
  width: number
  height: number
  hasBookmark: boolean
  bookmarkType?: 'normal' | 'generic'
  documentTypeName?: string
}

interface ThumbnailBookmarkDto {
  pageIndex: number
  documentTypeName: string
  isGeneric: boolean
  documentTypeId: number
}
```

**Props:**
```typescript
interface Props {
  selectedDocument: DocumentSummary | null
  currentPage: number
  totalPages: number
  bookmarks: BookmarkDto[]
  loading: boolean
}
```

**Emits:**
```typescript
interface Emits {
  (e: 'page-selected', pageNumber: number): void
  (e: 'thumbnail-loaded', pageNumber: number): void
}
```

**Features:**
- **Responsive thumbnail grid:** Automatically sized based on panel width
- **Real-time bookmark indicators:** Red borders with green/orange accents
- **Current page highlighting:** Visual indicator for active page
- **Smooth scrolling:** Auto-scroll to current page thumbnail
- **Lazy loading:** Load thumbnails on demand for performance
- **Click navigation:** Navigate to page on thumbnail click

**Visual Styling (Legacy Match):**
```scss
.thumbnail-container {
  border: 2px solid transparent;
  
  &.has-bookmark {
    border-color: #dc2626; // Red border for bookmarked pages
    
    &.normal-bookmark {
      box-shadow: 0 0 0 2px #22c55e; // Green accent
    }
    
    &.generic-bookmark {
      box-shadow: 0 0 0 2px #f97316; // Orange accent  
    }
  }
  
  &.current-page {
    border-color: #3b82f6; // Blue border for current page
    box-shadow: 0 0 8px rgba(59, 130, 246, 0.4);
  }
}
```

**API Integration:**
```typescript
// Load page thumbnails
async loadThumbnails(imageId: number): Promise<PageThumbnailDto[]> {
  const response = await api.get(`/api/documents/${imageId}/thumbnails`)
  return response.data
}

// Load bookmark indicators
async loadThumbnailBookmarks(imageId: number): Promise<ThumbnailBookmarkDto[]> {
  const response = await api.get(`/api/documents/${imageId}/bookmarks/thumbnails`)
  return response.data
}
```

### 5.2 API Endpoints (Full Database Integration)

#### 5.2.1 Document Types Endpoint
```csharp
[HttpGet("documents/document-types")]
public async Task<ActionResult<List<DocumentTypeDto>>> GetDocumentTypes(
    [FromQuery] int offeringId,
    [FromQuery] string? search = null)
{
    var query = @"
        SELECT idtml.ID, idtml.Name, idtml.IsGeneric, idtml.Code
        FROM ImageDocTypeMasterLists idtml
        INNER JOIN Offerings o ON idtml.Code = o.IndexCode
        WHERE o.OfferingID = @OfferingId 
          AND idtml.IsUsed = 1
          AND (@Search IS NULL OR idtml.Name LIKE '%' + @Search + '%')
        ORDER BY idtml.Name";
    
    // Execute query and return results
}
```

#### 5.2.2 Bookmark CRUD Endpoints
```csharp
[HttpPost("documents/{documentId}/bookmarks")]
public async Task<ActionResult<BookmarkDto>> CreateBookmark(
    int documentId, 
    [FromBody] CreateBookmarkRequest request)
{
    var formattedText = $"{request.DocumentTypeName} | {request.DocumentTypeId} | {request.DocumentDate:yyyy-MM-dd} | {request.Comments}";
    
    var query = @"
        INSERT INTO ImageBookmarks (ImageID, PageIndex, Text, ImageDocumentTypeID, CreatedBy)
        OUTPUT INSERTED.ID, INSERTED.DateCreated
        VALUES (@ImageId, @PageIndex, @FormattedText, @DocumentTypeId, @UserId)";
    
    // Execute and return created bookmark
}

[HttpPut("documents/{documentId}/bookmarks/{bookmarkId}")]
public async Task<ActionResult<BookmarkDto>> UpdateBookmark(
    int documentId,
    int bookmarkId, 
    [FromBody] UpdateBookmarkRequest request)
{
    // Update bookmark with new document type and regenerate Text field
}

[HttpDelete("documents/{documentId}/bookmarks/{bookmarkId}")]
public async Task<ActionResult> DeleteBookmark(int documentId, int bookmarkId)
{
    var query = "UPDATE ImageBookmarks SET IsDeleted = 1 WHERE ID = @BookmarkId";
    // Soft delete bookmark
}
```

#### 5.2.3 Bookmark Processing Endpoint
```csharp
[HttpPost("documents/{documentId}/process-bookmarks")]
public async Task<ActionResult<ProcessingSessionDto>> ProcessBookmarks(
    int documentId,
    [FromBody] ProcessBookmarksRequest request)
{
    // Validate bookmarks exist and are valid
    // Queue lambda function for document processing
    // Return session ID for progress tracking
    
    var sessionId = Guid.NewGuid().ToString();
    await _lambdaService.InvokeAsync("pdf-processor", new
    {
        documentId = documentId,
        sessionId = sessionId,
        operation = "split_document",
        bookmarks = request.Bookmarks
    });
    
    return Ok(new ProcessingSessionDto { SessionId = sessionId, Status = "queued" });
}
```

### 5.3 State Management (Pinia Store)

#### 5.3.1 Indexing Store Module
```typescript
interface IndexingState {
  // Current workflow state
  indexingMode: boolean
  selectedDocumentType: DocumentTypeDto | null
  
  // Document types (filtered by offering)
  availableDocumentTypes: DocumentTypeDto[]
  documentTypesLoading: boolean
  
  // Bookmarks management
  pendingBookmarks: BookmarkDto[]
  bookmarksLoading: boolean
  
  // Processing results
  processingResults: ProcessingResultDto[]
  processingSession: ProcessingSessionDto | null
  
  // UI state
  currentBookmarkPage: number | null
  showBookmarksList: boolean
  showProcessingResults: boolean
}

// Store actions with direct database integration
const indexingStore = defineStore('indexing', () => {
  // Load document types for current offering
  async function loadDocumentTypesForOffering(offeringId: number) {
    documentTypesLoading.value = true
    try {
      const types = await api.get(`/api/documents/document-types?offeringId=${offeringId}`)
      availableDocumentTypes.value = types.data
    } finally {
      documentTypesLoading.value = false
    }
  }
  
  // Create bookmark with immediate database persistence
  async function createBookmark(imageId: number, pageIndex: number) {
    if (!selectedDocumentType.value) return
    
    const request: CreateBookmarkRequest = {
      imageId,
      pageIndex,
      documentTypeId: selectedDocumentType.value.id,
      documentTypeName: selectedDocumentType.value.name,
      documentDate: new Date(),
      comments: ''
    }
    
    const bookmark = await api.post(`/api/documents/${imageId}/bookmarks`, request)
    pendingBookmarks.value.push(bookmark.data)
    
    // Clear selection after creating bookmark (legacy behavior)
    selectedDocumentType.value = null
  }
  
  // Process all bookmarks
  async function processBookmarks(documentId: number) {
    const session = await api.post(`/api/documents/${documentId}/process-bookmarks`, {
      bookmarks: pendingBookmarks.value
    })
    
    processingSession.value = session.data
    
    // Poll for results
    await pollProcessingStatus(session.data.sessionId)
  }
  
  return {
    // State
    indexingMode,
    selectedDocumentType,
    availableDocumentTypes,
    pendingBookmarks,
    processingResults,
    
    // Actions  
    loadDocumentTypesForOffering,
    createBookmark,
    processBookmarks,
    // ... other actions
  }
})
```

---

## 6. USER WORKFLOWS

### 6.1 Primary Workflow: Document Indexing with Breaks
**Database Operations at Each Step:**

1. **Load Document**
   ```sql
   SELECT i.ID, i.OriginalName, i.PageCount, i.DocTypeManualID,
          l.AssetName, s.SaleDesc, o.IndexCode
   FROM Images i
   INNER JOIN Loans l ON i.LoanID = l.ID
   INNER JOIN Sales s ON l.SaleID = s.SaleID  
   INNER JOIN Offerings o ON s.OfferingID = o.OfferingID
   WHERE i.ID = @ImageId;
   ```

2. **Load Available Document Types**
   ```sql
   SELECT idtml.ID, idtml.Name, idtml.IsGeneric
   FROM ImageDocTypeMasterLists idtml
   INNER JOIN Offerings o ON idtml.Code = o.IndexCode
   WHERE o.OfferingID = @OfferingId AND idtml.IsUsed = 1;
   ```

3. **Create Bookmark**
   ```sql
   INSERT INTO ImageBookmarks (ImageID, PageIndex, Text, ImageDocumentTypeID, CreatedBy)
   VALUES (@ImageId, @PageIndex, @FormattedText, @DocumentTypeId, @UserId);
   ```

4. **Process Bookmarks** (Lambda Function with Database Updates)
   ```sql
   -- Create new image records for splits
   INSERT INTO Images (OriginalName, ParsedName, PageCount, LoanID, DocTypeManualID, CreatedBy)
   VALUES (@GeneratedName, @GeneratedName, @PageCount, @LoanId, @DocumentTypeId, @UserId);
   
   -- Link bookmarks to result images
   UPDATE ImageBookmarks SET ResultImageID = @NewImageId WHERE ID = @BookmarkId;
   
   -- Mark original as processed
   UPDATE Images SET DocTypeManualID = @DocumentTypeId WHERE ID = @OriginalImageId;
   ```

### 6.2 Save Document Button Complete Workflow
**Critical Legacy Process Implementation**

#### 6.2.1 Save Button Trigger Conditions
Based on the legacy design document, the save process is triggered when user clicks the "Save Document" button and handles three distinct scenarios:

**Scenario 1: Simple Document Naming (No Bookmarks)**
- User has not created any bookmarks
- Document type may be selected in right panel
- Updates only `Images.DocTypeManualID` field
- No document splitting occurs

**Scenario 2: Single Bookmark at Page 0 (Index Only)**
- One bookmark exists at page 0 
- Updates `Images.DocTypeManualID` with bookmark's document type
- Marks bookmark as processed (soft delete)
- No physical document splitting

**Scenario 3: Multiple Bookmarks (Document Splitting)**
- Multiple bookmarks exist throughout document
- Splits PDF into separate documents at bookmark locations
- Creates new `Images` records for each split
- Updates all bookmarks with `ResultImageID`

#### 6.2.2 Complete Save Process Database Transaction
**Critical for Data Integrity:**

```sql
BEGIN TRANSACTION;

-- Step 1: Get current document info
DECLARE @imageId INT = @DocumentId;
DECLARE @loanId INT, @originalName NVARCHAR(255), @filePath NVARCHAR(500);

SELECT @loanId = LoanID, @originalName = OriginalName, @filePath = FilePath
FROM Images WHERE ID = @imageId;

-- Step 2: Get all active bookmarks
DECLARE @bookmarkCount INT;
SELECT @bookmarkCount = COUNT(*) 
FROM ImageBookmarks 
WHERE ImageID = @imageId AND IsDeleted = 0;

-- Step 3: Process based on bookmark count
IF @bookmarkCount = 0
BEGIN
    -- No bookmarks: Simple document type assignment
    UPDATE Images 
    SET DocTypeManualID = @SelectedDocumentTypeId,
        DocumentDate = @DocumentDate,
        Comments = @Comments,
        LastModified = GETDATE()
    WHERE ID = @imageId;
    
    -- Complete indexing action
    UPDATE ImageActions 
    SET CompletedDate = GETDATE(), CompletedBy = @UserId
    WHERE ImageID = @imageId AND ActionType = 'RequireIndex';
END
ELSE IF @bookmarkCount = 1 AND EXISTS (SELECT 1 FROM ImageBookmarks WHERE ImageID = @imageId AND PageIndex = 0 AND IsDeleted = 0)
BEGIN
    -- Single bookmark at page 0: Index only, no splitting
    DECLARE @singleDocTypeId INT;
    SELECT @singleDocTypeId = ImageDocumentTypeID 
    FROM ImageBookmarks 
    WHERE ImageID = @imageId AND PageIndex = 0 AND IsDeleted = 0;
    
    UPDATE Images 
    SET DocTypeManualID = @singleDocTypeId,
        DocumentDate = @DocumentDate,
        Comments = @Comments,
        LastModified = GETDATE()
    WHERE ID = @imageId;
    
    -- Mark bookmark as processed
    UPDATE ImageBookmarks 
    SET IsDeleted = 1 
    WHERE ImageID = @imageId AND PageIndex = 0;
    
    -- Complete indexing action
    UPDATE ImageActions 
    SET CompletedDate = GETDATE(), CompletedBy = @UserId
    WHERE ImageID = @imageId AND ActionType = 'RequireIndex';
END
ELSE
BEGIN
    -- Multiple bookmarks: Document splitting required
    -- This triggers lambda function for PDF processing
    
    -- Insert processing session record
    DECLARE @sessionId NVARCHAR(50) = NEWID();
    INSERT INTO ProcessingSessions (SessionId, ImageID, ProcessingType, Status, CreatedBy, DateCreated)
    VALUES (@sessionId, @imageId, 'DocumentSplitting', 'Queued', @UserId, GETDATE());
    
    -- Queue lambda function (handled by API endpoint)
    -- Lambda will handle the actual PDF splitting and database updates
    
    -- Update original image status
    UPDATE Images 
    SET LastModified = GETDATE()
    WHERE ID = @imageId;
END

COMMIT TRANSACTION;
```

#### 6.2.3 Lambda Function Processing (For Document Splitting)
**When multiple bookmarks exist, lambda function executes:**

```sql
BEGIN TRANSACTION;

-- Create split documents for each bookmark
DECLARE @bookmark_cursor CURSOR;
DECLARE @bookmark_id INT, @page_index INT, @doc_type_id INT, @doc_type_name NVARCHAR(255);

SET @bookmark_cursor = CURSOR FOR
    SELECT ib.ID, ib.PageIndex, ib.ImageDocumentTypeID, idtml.Name
    FROM ImageBookmarks ib
    INNER JOIN ImageDocTypeMasterLists idtml ON ib.ImageDocumentTypeID = idtml.ID
    WHERE ib.ImageID = @imageId AND ib.IsDeleted = 0
    ORDER BY ib.PageIndex;

OPEN @bookmark_cursor;
FETCH NEXT FROM @bookmark_cursor INTO @bookmark_id, @page_index, @doc_type_id, @doc_type_name;

DECLARE @splitIndex INT = 1;

WHILE @@FETCH_STATUS = 0
BEGIN
    -- Generate new document name
    DECLARE @newDocumentName NVARCHAR(255);
    SET @newDocumentName = @originalName + '_Split_' + CAST(@splitIndex AS NVARCHAR(10)) + '_' + @doc_type_name;
    
    -- Calculate page range for this split
    DECLARE @startPage INT = @page_index;
    DECLARE @endPage INT;
    
    -- Get next bookmark's page or end of document
    SELECT TOP 1 @endPage = PageIndex - 1 
    FROM ImageBookmarks 
    WHERE ImageID = @imageId AND PageIndex > @page_index AND IsDeleted = 0
    ORDER BY PageIndex;
    
    IF @endPage IS NULL
        SELECT @endPage = PageCount - 1 FROM Images WHERE ID = @imageId;
    
    DECLARE @splitPageCount INT = @endPage - @startPage + 1;
    
    -- Create new image record for split
    INSERT INTO Images (
        OriginalName, ParsedName, PageCount, LoanID, 
        DocTypeManualID, FilePath, CreatedBy, DateCreated
    )
    VALUES (
        @newDocumentName, @newDocumentName, @splitPageCount, @loanId,
        @doc_type_id, @filePath + '_split_' + CAST(@splitIndex AS NVARCHAR(10)), 
        @UserId, GETDATE()
    );
    
    DECLARE @new_image_id INT = SCOPE_IDENTITY();
    
    -- Update bookmark with result image
    UPDATE ImageBookmarks 
    SET ResultImageID = @new_image_id 
    WHERE ID = @bookmark_id;
    
    SET @splitIndex = @splitIndex + 1;
    FETCH NEXT FROM @bookmark_cursor INTO @bookmark_id, @page_index, @doc_type_id, @doc_type_name;
END

CLOSE @bookmark_cursor;
DEALLOCATE @bookmark_cursor;

-- Mark original image as processed but not deleted (for audit trail)
UPDATE Images 
SET DocTypeManualID = (SELECT TOP 1 ImageDocumentTypeID FROM ImageBookmarks WHERE ImageID = @imageId AND IsDeleted = 0),
    LastModified = GETDATE()
WHERE ID = @imageId;

-- Complete indexing action
UPDATE ImageActions 
SET CompletedDate = GETDATE(), CompletedBy = @UserId
WHERE ImageID = @imageId AND ActionType = 'RequireIndex';

-- Update processing session status
UPDATE ProcessingSessions 
SET Status = 'Completed', CompletedDate = GETDATE()
WHERE SessionId = @sessionId;

COMMIT TRANSACTION;
```

#### 6.2.4 UI State Changes During Save Process

**Pre-Save Validation:**
```typescript
const validateSaveProcess = (): ValidationResult => {
  const errors: string[] = []
  const warnings: string[] = []
  
  // Check if document is loaded
  if (!selectedDocument.value) {
    errors.push('No document selected')
  }
  
  // Check bookmark validity
  const invalidBookmarks = pendingBookmarks.value.filter(b => !b.imageDocumentTypeId)
  if (invalidBookmarks.length > 0) {
    errors.push(`${invalidBookmarks.length} bookmarks missing document type`)
  }
  
  // Check for overlapping page ranges
  const sortedBookmarks = [...pendingBookmarks.value].sort((a, b) => a.pageIndex - b.pageIndex)
  for (let i = 1; i < sortedBookmarks.length; i++) {
    if (sortedBookmarks[i].pageIndex === sortedBookmarks[i-1].pageIndex) {
      errors.push(`Multiple bookmarks on page ${sortedBookmarks[i].pageIndex + 1}`)
    }
  }
  
  return { valid: errors.length === 0, errors, warnings }
}
```

**Save Process UI Flow:**
```typescript
const executeSaveProcess = async () => {
  try {
    // 1. Disable UI and show progress
    saving.value = true
    saveProgress.value = { current: 0, total: 4, message: 'Validating document...' }
    
    // 2. Validate before save
    const validation = validateSaveProcess()
    if (!validation.valid) {
      throw new Error(validation.errors.join(', '))
    }
    
    saveProgress.value = { current: 1, total: 4, message: 'Processing bookmarks...' }
    
    // 3. Determine processing type based on bookmarks
    const processingType = getProcessingType()
    
    // 4. Execute appropriate save workflow
    let result: ProcessingResult
    
    switch (processingType) {
      case 'simple':
        result = await saveImageDataOnly()
        break
      case 'index_only':
        result = await saveWithIndexOnly()
        break
      case 'document_splitting':
        result = await processDocumentSplitting()
        break
    }
    
    saveProgress.value = { current: 3, total: 4, message: 'Updating interface...' }
    
    // 5. Update UI with results
    await refreshDocumentResults(result)
    await refreshBookmarksList()
    await clearWorkingState()
    
    saveProgress.value = { current: 4, total: 4, message: 'Save completed successfully' }
    
    // 6. Show success notification
    showSuccessNotification('Document processed successfully')
    
  } catch (error) {
    console.error('Save process failed:', error)
    showErrorNotification(`Save failed: ${error.message}`)
  } finally {
    saving.value = false
    saveProgress.value = null
  }
}
```

**Post-Save UI Updates:**
1. **Left Panel:** Refresh with new split documents if applicable
2. **Bottom Panel:** Clear processed bookmarks, show processing status  
3. **Main Viewer:** Refresh document list, navigate to first result if split occurred
4. **Right Panel:** Clear document type selection, reset to initial state
5. **Thumbnail View:** Update bookmark indicators, highlight any remaining bookmarks

#### 6.2.5 Error Handling and Recovery

**Common Save Errors:**
```typescript
interface SaveError {
  type: 'validation' | 'processing' | 'database' | 'file_system'
  code: string
  message: string
  recoverable: boolean
  recovery_action?: string
}

const handleSaveError = (error: SaveError) => {
  switch (error.type) {
    case 'validation':
      // Show validation errors inline
      showValidationErrors(error.message)
      break
      
    case 'processing':
      // PDF processing failed
      showProcessingError(error.message)
      if (error.recoverable) {
        showRetryOption()
      }
      break
      
    case 'database':
      // Database transaction failed
      showDatabaseError()
      // Attempt to reload document state
      reloadDocumentState()
      break
      
    case 'file_system':
      // S3 or file system error
      showFileSystemError()
      // Check if original document still accessible
      validateDocumentAccess()
      break
  }
}
```

#### 6.2.6 Success State and Navigation
**After successful save:**
- Document list refreshes showing split results
- User can navigate to view individual split documents  
- Original document remains accessible but marked as processed
- All bookmarks linked to their respective result documents
- System ready for next document indexing workflow

---

## 7. IMPLEMENTATION STATUS & PHASES

### ✅ COMPLETED (Phase 1-2): Frontend Foundation 

**Completed Components:**
- ✅ **`esizzle-frontend/src/types/indexing.ts`** - Complete TypeScript interfaces for legacy compatibility
- ✅ **`esizzle-frontend/src/components/indexing/IndexingToolbar.vue`** - Legacy-style document type selection
- ✅ **`esizzle-frontend/src/components/indexing/BookmarksList.vue`** - Bottom panel bookmark management  
- ✅ **`esizzle-frontend/src/components/indexing/ThumbnailView.vue`** - Right panel with bookmark indicators
- ✅ **`esizzle-frontend/src/stores/indexing.ts`** - Complete Pinia store with validation and save processing
- ✅ **`esizzle-frontend/src/services/indexing-api.ts`** - API service layer for all indexing operations
- ✅ **`esizzle-frontend/src/components/layout/RightPanel.vue`** - Integration of IndexingToolbar and ThumbnailView

**Features Implemented:**
- Document type filtering by offering (IndexCode compatibility)
- Bookmark CRUD operations with pipe-delimited text format
- Three-scenario save processing (Simple, Index-only, Document Splitting)
- Visual bookmark indicators (green/orange/red) matching legacy styling
- "Set Break" vs "Save Image Data" button logic
- Complete state management with error handling
- Real-time bookmark synchronization

### ✅ COMPLETED (Phase 3): Backend Implementation & Database Integration

**Backend Models & Data Layer:**
- ✅ **`esizzle-api/src/EsizzleAPI/Models/IndexingModels.cs`** - Complete legacy database models matching exact schema
- ✅ **`esizzle-api/src/EsizzleAPI/DTOs/IndexingDtos.cs`** - Full API DTOs for request/response handling
- ✅ **`esizzle-api/src/EsizzleAPI/Repositories/IIndexingRepository.cs`** - Repository interface with all operations
- ✅ **`esizzle-api/src/EsizzleAPI/Repositories/IndexingRepository.cs`** - Complete repository implementation
- ✅ **`esizzle-api/src/EsizzleAPI/Controllers/IndexingController.cs`** - Full API controller with all endpoints

**Database Schema Implementation:**
- ✅ **Images Table**: Complete model with legacy compatibility (DocTypeManualID, DocTypeAutoID, etc.)
- ✅ **ImageBookmarks Table**: Full bookmark management with pipe-delimited text format
- ✅ **ImageDocTypeMasterLists Table**: Document type master catalog with IndexCode filtering
- ✅ **Offerings/Sales/Loans Tables**: Complete relationship hierarchy
- ✅ **ProcessingSessions Table**: Async operation tracking
- ✅ **PageThumbnails Table**: Thumbnail management with bookmark indicators

**API Endpoints Implemented:**
- ✅ **GET /api/documents/document-types** - Document type filtering by offering
- ✅ **GET /api/documents/{id}/bookmarks** - Load all bookmarks for document
- ✅ **POST /api/documents/{id}/bookmarks** - Create new bookmark
- ✅ **PUT /api/documents/{id}/bookmarks/{bookmarkId}** - Update bookmark
- ✅ **DELETE /api/documents/{id}/bookmarks/{bookmarkId}** - Delete bookmark (soft)
- ✅ **POST /api/documents/{id}/save-image-data** - Simple document metadata save
- ✅ **POST /api/documents/{id}/process-bookmarks** - Full bookmark processing workflow
- ✅ **GET /api/documents/{id}/thumbnails** - Page thumbnails with bookmark indicators
- ✅ **GET /api/processing/{sessionId}/status** - Processing session status tracking

**Core Features Implemented:**
- ✅ **Three-Scenario Processing Logic**: Simple, Index-only, Document Splitting
- ✅ **Pipe-Delimited Text Format**: Legacy-compatible bookmark text handling
- ✅ **Validation System**: Complete bookmark validation with warnings/errors
- ✅ **Processing Session Tracking**: Async operation status management
- ✅ **Legacy Database Compatibility**: Exact schema matching with all relationships

### 🔄 IN PROGRESS: Lambda Integration & Processing

### Phase 1: Database Integration Foundation (Week 1) - ✅ COMPLETED
**Deliverables:**
- ✅ Complete API endpoints with full database integration
- ✅ Database models and DTOs matching legacy schema exactly  
- ✅ Repository layer with all CRUD operations
- ✅ Processing session management system

**Database Work:**
```sql
-- Ensure all required indexes exist
CREATE INDEX IF NOT EXISTS IX_Images_DocTypeManualID ON Images (DocTypeManualID);
CREATE INDEX IF NOT EXISTS IX_ImageBookmarks_ImageID_PageIndex ON ImageBookmarks (ImageID, PageIndex);
CREATE INDEX IF NOT EXISTS IX_ImageDocTypeMasterLists_Code_IsUsed ON ImageDocTypeMasterLists (Code, IsUsed);

-- Add any missing constraints
ALTER TABLE ImageBookmarks ADD CONSTRAINT FK_ImageBookmarks_Images 
    FOREIGN KEY (ImageID) REFERENCES Images(ID);
ALTER TABLE ImageBookmarks ADD CONSTRAINT FK_ImageBookmarks_ImageDocTypeMasterLists
    FOREIGN KEY (ImageDocumentTypeID) REFERENCES ImageDocTypeMasterLists(ID);
```

### Phase 2: Core UI Components (Week 2)  
**Deliverables:**
- IndexingToolbar.vue with full database integration
- BookmarksList.vue with real-time database sync
- DocumentResultsList.vue showing actual processing results
- Enhanced store with proper database state management

### Phase 3: Layout Integration (Week 3)
**Deliverables:**
- Modified AppShell.vue with proper three-panel layout
- Updated RightPanel.vue with IndexingToolbar integration
- New LeftPanel.vue component for results display
- Bottom panel integration for BookmarksList

### Phase 4: End-to-End Workflow (Week 4)
**Deliverables:**
- Complete workflow integration with database
- Lambda function updates for bookmark processing
- Error handling and validation
- Performance optimization and testing

### Phase 5: Testing and Polish (Week 5)
**Deliverables:**
- Comprehensive testing of all database operations
- Visual regression testing against legacy screenshots
- Performance testing with large documents
- User acceptance testing with legacy users

---

## 8. QUALITY ASSURANCE

### 8.1 Database Testing Requirements
```sql
-- Test data setup scripts
INSERT INTO Offerings (OfferingID, IndexCode, OfferingName) VALUES (1, 'ffn', 'Test FFN Offering');

INSERT INTO ImageDocTypeMasterLists (Name, Code, IsGeneric) VALUES 
('Promissory Note', 'ffn', 0),
('Deed of Trust', 'ffn', 0),
('Generic Document', 'ffn', 1);

-- Test scenarios
-- 1. Single bookmark at page 0 (document naming only)
-- 2. Multiple bookmarks (document splitting)  
-- 3. Bookmark CRUD operations
-- 4. Document type filtering by offering
-- 5. Processing results accuracy
```

### 8.2 Integration Testing Scenarios
1. **Create Bookmark → Verify Database Record**
2. **Process Bookmarks → Verify Split Documents Created**
3. **Edit Bookmark → Verify Database Update and UI Sync**
4. **Delete Bookmark → Verify Soft Delete and UI Update**
5. **Load Document Types → Verify Offering Filtering**
6. **Complete Workflow → Verify All Database Relationships**

---

##
