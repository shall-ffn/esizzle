# Hydra Due Diligence App - Document Indexing & Naming System Design Document

## Document Information
- **Project:** Hydra Due Diligence Application
- **Component:** Document Indexing and Naming System
- **Version:** 1.0
- **Date:** January 2025
- **Author:** Technical Documentation

## Table of Contents
1. [System Overview](#system-overview)
2. [Architecture Components](#architecture-components)
3. [Database Schema](#database-schema)
4. [Document Indexing Workflow](#document-indexing-workflow)
5. [Detailed Index Selection & Save Process](#detailed-index-selection--save-process)
6. [Constellation Matching System](#constellation-matching-system)
7. [Document Breaking/Splitting](#document-breakingsplitting)
8. [Index Code Management](#index-code-management)
9. [Security & Permissions](#security--permissions)
10. [Technical Implementation](#technical-implementation)
11. [UI Components](#ui-components)

---

## System Overview

The indexing system in the Hydra Due Diligence App is a sophisticated document classification and naming system that combines manual user input with automated constellation matching to properly identify and categorize loan documents as they are processed.

### Purpose
- Classify and name loan documents as they enter the system
- Provide intelligent document type suggestions using AI-powered matching
- Enable efficient document processing and retrieval
- Maintain consistency in document categorization across projects

### Key Features
- **Intelligent Suggestions:** AI-powered constellation matching suggests document types
- **Manual Override:** Users can select from available document types or add new ones
- **Project-Specific Types:** Document types filtered by offering/project codes
- **Document Splitting:** Single images can be split into multiple typed documents
- **Audit Trail:** Complete tracking of indexing decisions and changes

---

## Architecture Components

### Core Components
1. **eStacker Application** - Main UI for document processing
2. **StackerProc** - Business logic processor
3. **Constellation Matching Engine** - AI-powered document similarity matching
4. **Database Layer** - Aurora/LoanMaster database with indexing tables
5. **Document Viewer** - PDF/image viewing and manipulation component

### System Flow
```
Loan Image → Index Suggestion → User Selection → Document Assignment → Save Process → Database Update
```

---

## Database Schema

### Key Tables

#### `Images`
Primary document storage with indexing fields:
- **`ID`** - Unique image identifier
- **`DocTypeManualID`** - User-assigned document type (FK to ImageDocTypeMasterLists)
- **`DocTypeAutoID`** - AI-suggested document type (FK to ImageDocTypeMasterLists)
- **`LoanID`** - Associated loan (FK to Loans)
- **`DocumentDate`** - Document date metadata
- **`Comments`** - Additional notes
- **`ParsedName`** - Processed document name

#### `ImageDocTypeMasterLists`
Master catalog of all document types:
- **`ID`** - Unique document type identifier
- **`Name`** - Human-readable document type name
- **`Code`** - Project/offering association code
- **`DateCreated`** - Creation timestamp

#### `ImageBookmarks`
Document breaks/separators with type assignments:
- **`ID`** - Unique bookmark identifier
- **`ImageID`** - Source image (FK to Images)
- **`PageIndex`** - Page location of break
- **`Text`** - Break data (pipe-delimited format)
- **`ImageDocumentTypeID`** - Assigned document type
- **`ResultImageID`** - Created document after split
- **`CreatedBy`** - User who created break

#### `ConstellationImageMatches`
AI similarity matching results:
- **`Image1`**, **`Image2`** - Matched image pair
- **`VarVal`** - Variance value (lower = better match)
- **`PercVal`** - Percentage match score
- **`TotalMissingChars`** - Missing characters count

#### `Offerings`
Project configurations:
- **`OfferingID`** - Unique offering identifier
- **`IndexCode`** - Controls available document types
- **`OfferingName`** - Project name

---

## Document Indexing Workflow

### 1. Image Loading & Initial Processing
When a loan image is loaded into the system:
- Image assigned unique ImageID
- System determines relevant document types based on offering's IndexCode
- Constellation matching runs to find similar documents and suggest types
- Available document types populated in UI

### 2. Index Type Population
The `BindIndexTree()` method populates available document types:
- Calls `StackerProc.GetFullDocTypeList(imageID)`
- Filters document types by offering's IndexCode (e.g., "ffn", "UPB", "strategic")
- Retrieves constellation match statistics for ranking suggestions
- Displays types in two categories:
  - **Matched Types** (with constellation matches) - bold, sorted by confidence
  - **Unmatched Types** - regular text, alphabetically sorted

### 3. Index Selection Process
Users interact through:
- **Document Type ListView** (`lvDocTypes`) - displays available types with statistics
- **Search Filter** (`txtIXFilter`) - real-time filtering of document types
- **Document Info Panel** (`ucDocumentInfo`) - additional metadata entry

### 4. Document Type Assignment
Selection triggers:
- Creates `ImageBookmark` record marking document separator
- Sets `DocTypeManualID` field on `Image` record
- Updates constellation cache with new assignment
- Triggers related image re-evaluation for improved matching

---

## Detailed Index Selection & Save Process

### Step-by-Step User Workflow

#### 1. Image Loading and Index List Population

**`LoadDocument(int imageid, int pageindex)` Method Flow:**
```csharp
// 1. Load the actual document file
var filepath = Process.CurrentWorkItem.SetCurrentImage(imageid);

// 2. Initialize the document viewer
ucViewer.OpenDocument(Process.CurrentWorkItem.WorkingImagePath, filepath, imageid, ...);

// 3. Populate the indexing list box
BindIndexView(Process.CurrentWorkItem.CurrentImageID.Value);
```

**`BindIndexView(int imageID)` Method:**
- Enables the `lvDocTypes` ListView control (indexing list box on bottom right)
- Calls `BindIndexTree(imageID)` to populate available document types
- Calls `ClearSelectedIndexValues()` to reset previous selections

#### 2. Index List Box Population (`BindIndexTree()`)

**Document Type Retrieval:**
```csharp
// Get all available document types with constellation matching stats
var types = StackerProc.GetFullDocTypeList(imageID);

// Apply search filter if user has typed in filter box
if (!string.IsNullOrEmpty(txtIXFilter.Text))
{
    types = types.Where(a => a.Name.ToLower().Contains(txtIXFilter.Text.ToLower())).ToList();
}
```

**List Population Logic:**
- **Unmatched Types First:** Document types with no constellation matches added first
- **Matched Types Second:** Document types with matches inserted at top (position 0), sorted by confidence

**Display Format:**
- **Bold Text:** Document types with constellation matches
- **Regular Text:** Unused document types (italic formatting)
- **Match Statistics:** Format: "85.5% | 12 | 150 | 92.3 | 8"
  - Percentage match | Total matches | VarVal | PercVal | Missing chars

#### 3. User Selection Process

**When User Clicks on Index List Box Item:**

```csharp
private void lvDocTypes_ItemSelectionChanged(object sender, ListViewItemSelectionChangedEventArgs e)
{
    HandleDocTypeSelectionValues();
}
```

**`HandleDocTypeSelectionValues()` Method:**
- Checks if document type selected using `IsDocTypeSelected` property
- Updates button text in document info panel:
  - If index selected: Button shows **"Set Break"**
  - If no index selected: Button shows **"Save Image Data"**

**Selection State Properties:**
```csharp
private bool IsDocTypeSelected
{
    get
    {
        return (lvDocTypes != null && lvDocTypes.SelectedItems != null && lvDocTypes.SelectedItems.Count > 0);
    }
}

private KeyValuePair<string, string>? SelectedIndexNodeData
{
    get
    {
        if (IsDocTypeSelected)
        {
            var item = lvDocTypes.SelectedItems[0];
            var img = item.Tag as ImageDocTypeWithStats;
            if (img != null)
                return new KeyValuePair<string, string>(img.ID.ToString(), item.Text);
        }
        return null;
    }
}
```

#### 4. Save Process Initiation

**When User Clicks "Set Break" Button:**

1. **Button Click Handler in `ucDocumentInfo`:**
```csharp
private void btnSetBreak_Click(object sender, EventArgs e)
{
    if(BreakClicked != null)
    {
        btnSetBreak.Enabled = false;
        var arg = new ItemEventArgs<string>(GetDataString());
        BreakClicked(arg); // Fire event to main form
    }
    btnSetBreak.Enabled = true;
}
```

2. **Event Handler in Main eStacker Form:**
```csharp
private void ucDocInfo_BreakClicked(ItemEventArgs<string> e)
{
    var data = GetBreakData(); // Gets selected index data
    if (data.HasValue)
        SaveBreak(data.Value, e.Item); // Initiate save process
}
```

#### 5. Index Assignment Process (`SaveBreak()` Method)

**Break Data Construction:**
```csharp
private string BuildBreakData(KeyValuePair<string, string> node, string extra)
{
    // Format: "DocumentTypeName | DocumentTypeID | AdditionalData"
    var str = (node.Value ?? "") + " | " + (node.Key ?? "") + " | " + extra;
    return str;
}
```

**Document Break Creation:**
```csharp
private void SaveBreak(KeyValuePair<string, string> dt, string extra)
{
    if (ValidateBreak(dt, extra, false))
    {
        var data = BuildBreakData(dt, extra);
        var pageIndex = ucViewer.Viewer.CurrentPageIndex;
        var docTypeID = int.Parse(dt.Key);

        // Create bookmark/break in database
        int bkid = Process.CurrentWorkItem.SaveBookmark(pageIndex, data, docTypeID);

        // Update viewer UI
        ucViewer.AddBookmark(bkid, new BookmarkInfo(bkid, pageIndex, data, docTypeID));
        
        // Refresh bookmarks tree
        BindBookmarks();
        
        // Clear selection and focus viewer
        ucDocInfo.Clear();
        ucViewer.SetViewerFocus();
    }
}
```

#### 6. Full Document Save Process

**Save Button Click:**
```csharp
private void rbtnSave_Click(object sender, EventArgs e)
{
    ButtonSave();
}

private void ButtonSave()
{
    if (AllowSave)
    {
        RefreshDocList = true;
        SaveDocument();
    }
}
```

**Background Save Process (`wrkSave_DoWork`):**

The system processes different scenarios:

1. **Single Break at Top (Simple Index Assignment):**
   - Updates `Image.DocTypeManualID` with selected document type
   - Marks break as deleted (processed)
   - Updates constellation cache with new index assignment
   - Completes index-related image actions

2. **No Breaks (Index Only):**
   - Updates image with selected document type
   - No document splitting required

3. **Multiple Breaks (Document Splitting):**
   - Splits original document into separate documents
   - Creates new `Image` records for each split
   - Assigns appropriate document types to each new document
   - Marks original image as obsolete

**Database Updates:**
```csharp
// Update image with document type
curimage.DocTypeManualID = docdata.ImageDocTypeID;
curimage.DocumentDate = docdata.DocDate;
curimage.Comments = docdata.Comments;

// Complete indexing actions
Process.CurrentWorkItem.CompleteImageAction(
    global.DAL.Aurora.ImageActionTypeEnum.RequireIndex, 
    CurrentUser.UserID, 
    overRideIndexCompletion);

// Update constellation cache
var dt = new DocTypeInfo { 
    ID = docdata.ImageDocTypeID.Value.ToString(), 
    Name = docdata.ImageDocTypeName 
};
_services.CSTCache.SaveImage(curimage.ID, image => {
    image.DocType = dt;
    return image;
});

// Queue related images for re-evaluation
var imgLib = new ImageNamingLibrary();
imgLib.QueueRelatedImages(curimage.ID, saveInfo.CurrentOfferingID);
```

#### 7. Post-Save Processing

**After successful save:**
- Document list refreshes to show updated document types
- Image actions updated to reflect completion
- Constellation matching system learns from new assignment
- Related images queued for re-evaluation
- UI elements re-enabled and refreshed

---

## Constellation Matching System

### Purpose
Provides intelligent document type suggestions by:
- Comparing new documents to previously classified documents
- Using OCR text similarity algorithms
- Ranking matches by confidence metrics

### Matching Metrics
- **VarVal** - Variance value (lower = better match)
- **PercVal** - Percentage match score
- **MissingChars** - Number of missing characters (lower = better)
- **TotalMatches** - Number of similar documents found

### Display Logic
- Matched document types appear at top in **bold formatting**
- Match statistics format: "85.5% | 12 | 150 | 92.3 | 8"
  - Confidence% | Total Matches | VarVal | PercVal | Missing Chars
- Unmatched types appear below in regular formatting

### Machine Learning Integration
- System learns from user selections
- Related images re-evaluated after new assignments
- Matching confidence improves over time
- Feedback loop enhances future suggestions

---

## Document Breaking/Splitting

### Break Creation Process
1. User navigates to page where new document should begin
2. Selects appropriate document type from index list
3. Clicks "Set Break" button
4. System creates `ImageBookmark` at current page with:
   - Page index location
   - Selected document type ID
   - Additional metadata (date, comments)
   - User ID who created break

### Break Data Format
Break information stored as pipe-delimited string:
```
[DocumentTypeName] | [DocumentTypeID] | [DocumentDate] | [Comments]
```

### Save Process
When document saved with breaks:
- Original image split into separate documents
- Each new document inherits appropriate document type
- New `Image` records created for each split
- Original image marked as obsolete
- Document type assignments preserved in new records

---

## Index Code Management

### Purpose
Index codes control which document types are available for each offering/project:
- Each `Offering` has `IndexCode` field (e.g., "ffn", "UPB", "strategic")
- Document types in `ImageDocTypeMasterLists` have matching `Code` fields
- Only document types matching offering's code shown to users

### Code Examples
- **"ffn"** - Standard FFN document types
- **"UPB"** - UPB-specific document classifications
- **"strategic"** - Strategic Capital document types
- **"CITIBCA"** - Citibank BCA document types

### Document Type Management

#### Adding New Document Types
- Users with proper permissions can add new document types
- New types created with current offering's IndexCode
- System validates uniqueness before creation

#### Document Type Structure
- **ID** - Unique identifier
- **Name** - Human-readable document type name
- **Code** - Offering code association
- **DateCreated** - Creation timestamp
- **IsUsed** - Whether type is actively used (affects display)

---

## Security & Permissions

### User Permissions
- **Basic Users** - Can select from available document types
- **Project Managers** - Can add/delete document types
- **Super Users** - Full system access

### Action Permissions
- **EstackerAction.Name** - Can set document names/types
- **EstackerAction.ClusterName** - Can manage document type catalog
- **EstackerAction.Split** - Can create document breaks

### Security Implementation
- Role-based access control
- Action-level permissions
- Audit logging for all changes
- User context tracking

---

## Technical Implementation

### Key Methods
- **`BindIndexTree(int imageID)`** - Populates document type list
- **`SaveBreak(KeyValuePair<string, string> dt, string extra)`** - Creates document breaks
- **`GetFullDocTypeList(int imageID)`** - Retrieves filtered document types with stats
- **`ParseBKText(string text)`** - Parses break data string

### Database Integration
- Entity Framework for data access
- Audit logging for all document type changes
- Referential integrity between images and document types
- Transaction management for complex operations

### Performance Considerations
- Constellation matching cached for performance
- Background processing for document splitting
- Lazy loading of document type statistics
- Optimized queries for large datasets

---

## UI Components

### Primary Controls
- **`lvDocTypes`** - ListView containing available document types (bottom right panel)
- **`btnSetBreak`** - Button changes text based on selection ("Set Break" vs "Save Image Data")
- **`rbtnSave`** - Main save button that processes all changes
- **`txtIXFilter`** - Filter textbox for searching document types

### Control States
- **Index Selected:** Button shows "Set Break", ready for break creation
- **No Index Selected:** Button shows "Save Image Data", for metadata only
- **Document Modified:** Save button becomes visible and enabled
- **Save In Progress:** UI controls disabled, progress indicators shown

### User Experience Features
- Real-time search filtering
- Visual feedback for matching confidence
- Keyboard shortcuts for common operations
- Context-sensitive help and tooltips
- Progress indicators during save operations

---

## Conclusion

The Hydra Due Diligence App indexing system provides a sophisticated, user-friendly approach to document classification that combines the power of AI-driven suggestions with human oversight and control. The system's architecture ensures scalability, maintainability, and accurate document processing while providing users with the tools they need to efficiently categorize and manage large volumes of loan documents.

The detailed workflow ensures that every index assignment is properly validated, saved, and propagated throughout the constellation matching system for improved future document classification, creating a continuously learning and improving system.
