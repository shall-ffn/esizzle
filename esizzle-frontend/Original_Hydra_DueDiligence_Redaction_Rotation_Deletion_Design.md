# Hydra.DueDiligence.App - Redaction, Rotation, and Deletion Design Document

## Table of Contents
1. [System Overview](#system-overview)
2. [Database Schema](#database-schema)
3. [Process Flow](#process-flow)
4. [S3 Bucket Structure](#s3-bucket-structure)
5. [Plugin Architecture](#plugin-architecture)
6. [Error Handling and Recovery](#error-handling-and-recovery)
7. [API and Integration Points](#api-and-integration-points)

## System Overview

The Hydra.DueDiligence.App (eStacker) system provides document image manipulation capabilities including redaction, rotation, and page deletion. The system uses a distributed architecture with the following key components:

- **UI Layer**: Windows Forms application (eStacker)
- **Service Layer**: Watchman plugins for orchestration
- **Processing Layer**: Workman plugins for image manipulation
- **Data Layer**: SQL Server Aurora LoanMaster database
- **Storage Layer**: AWS S3 bucket infrastructure

## Database Schema

### Core Tables

#### Image Table
```sql
-- Main image record with status tracking
Image (
    ID int PRIMARY KEY,
    OfferingID int,
    LoanID int,
    ImageStatusTypeID int, -- Controls workflow state
    DocTypeManualID int,
    PageCount int,
    IsRedacted bit,
    Deleted bit,
    BucketPrefix nvarchar(50), -- S3 bucket organization
    Path nvarchar(500), -- Base file path
    -- ... additional metadata fields
)
```

#### ImageRedaction Table
```sql
-- Stores redaction coordinates and metadata
ImageRedaction (
    ID int PRIMARY KEY IDENTITY,
    ImageID int FOREIGN KEY REFERENCES Image(ID),
    PageNumber int, -- Zero-based page index
    PageX float, -- X coordinate of redaction box
    PageY float, -- Y coordinate of redaction box  
    PageWidth float, -- Width of redaction box
    PageHeight float, -- Height of redaction box
    Guid nvarchar(50), -- Unique identifier for UI tracking
    Text nvarchar(max), -- Optional text for the redaction
    CreatedBy int FOREIGN KEY REFERENCES Users(ID),
    DateCreated datetime,
    Deleted bit DEFAULT 0,
    Applied bit, -- Whether redaction has been processed
    DrawOrientation int -- Orientation when redaction was drawn
)
```

#### ImageRotation Table
```sql
-- Stores page rotation information
ImageRotation (
    ID int PRIMARY KEY IDENTITY,
    ImageID int FOREIGN KEY REFERENCES Image(ID),
    PageIndex int, -- Zero-based page index
    Rotate int -- Rotation value: 0, 90, 180, 270 degrees
)
```

#### ImagePageDeletion Table
```sql
-- Tracks pages marked for deletion
ImagePageDeletion (
    ID int PRIMARY KEY IDENTITY,
    ImageID int FOREIGN KEY REFERENCES Image(ID),
    PageIndex int, -- Zero-based page index
    CreatedBy int FOREIGN KEY REFERENCES Users(ID),
    DateCreated datetime
)
```

#### ImageManipulationQueue Table
```sql
-- Processing queue management
ImageManipulationQueue (
    ID int PRIMARY KEY IDENTITY,
    ImageID int FOREIGN KEY REFERENCES Image(ID),
    DateRequested datetime,
    DateFinished datetime NULL -- NULL while processing
)
```

#### ImageAction and ImageActionType Tables
```sql
-- Action management system
ImageActionType (
    ID int PRIMARY KEY,
    Name nvarchar(50)
    -- Types: Redact (3), Requires Indexing (1), etc.
)

ImageAction (
    ID int PRIMARY KEY IDENTITY,
    ImageID int FOREIGN KEY REFERENCES Image(ID),
    ImageActionTypeID int FOREIGN KEY REFERENCES ImageActionType(ID),
    CompletedBy int NULL, -- User who completed the action
    DateCompleted datetime NULL,
    ActionName nvarchar(255),
    ActionNote nvarchar(max)
)
```

### Supporting Tables

#### ImageBookmark Table
```sql
-- Document breaks/separations
ImageBookmark (
    ID int PRIMARY KEY IDENTITY,
    ImageID int FOREIGN KEY REFERENCES Image(ID),
    PageIndex int,
    Text nvarchar(max), -- Contains doc type and metadata
    ImageDocumentTypeID int,
    ResultImageID int, -- ID of split result image
    Deleted bit DEFAULT 0
)
```

#### ImageChangesPending Table
```sql
-- Tracks images with unsaved changes
ImageChangesPending (
    ImageID int PRIMARY KEY FOREIGN KEY REFERENCES Image(ID),
    FirstChanged datetime,
    LastChanged datetime
)
```

## Process Flow

### 1. User Interface Actions (eStacker Application)

#### Redaction Process
```csharp
// In eStacker.cs - ucViewer_RedactionChanged event
private void ucViewer_RedactionChanged(BoxEventArgs e)
{
    var red = e.Item;
    
    // Notify the process that redaction needs to be updated
    Process.CurrentWorkItem.ChangeRedaction(CurrentUser,
        red.Guid,
        red.PageNumber,
        red.PageCoords,
        e.Finished,
        red.DrawOrientation
    );
    
    // Complete the redaction action
    Process.CurrentWorkItem.CompleteImageAction(
        global.DAL.Aurora.ImageActionTypeEnum.Redact, 
        CurrentUser.UserID, 
        true
    );
}
```

**Database Records Created:**
```sql
-- New redaction record
INSERT INTO ImageRedaction (ImageID, PageNumber, PageX, PageY, PageWidth, PageHeight, Guid, CreatedBy, DateCreated, DrawOrientation)
VALUES (@ImageID, @PageNumber, @PageX, @PageY, @PageWidth, @PageHeight, @Guid, @UserID, GETUTCDATE(), @DrawOrientation)

-- Mark image as having pending changes
INSERT INTO ImageChangesPending (ImageID, FirstChanged, LastChanged)
VALUES (@ImageID, GETUTCDATE(), GETUTCDATE())

-- Update image status for processing
UPDATE Image SET ImageStatusTypeID = @NeedsImageManipulation WHERE ID = @ImageID
```

#### Rotation Process  
```csharp
// In eStacker.cs - ucViewer_PageRotated event
private void ucViewer_PageRotated(PageRotationEventArgs e)
{
    // Record the rotation change
    var pageRotated = Process.CurrentWorkItem.ChangeRotation(e.PageIndex, e.CurrentRotate);
    
    // Handle billing/ledger entries for rotations
    if (e.CurrentRotate == 0) // Rotating back to original
    {
        ImageLedgerManager.Credit(CurrentUser, Process.CurrentWorkItem.CurrentImage.Image, 
            ImageLedgerEntryTypeEnum.Rotation, "Page rotated to original position", 1);
    }
    else if (pageRotated) // New rotation applied
    {
        ImageLedgerManager.Debit(CurrentUser, Process.CurrentWorkItem.CurrentImage.Image, 
            ImageLedgerEntryTypeEnum.Rotation, "Page rotated", 1);
    }
}
```

**Database Records Created:**
```sql
-- New or updated rotation record
IF EXISTS (SELECT 1 FROM ImageRotation WHERE ImageID = @ImageID AND PageIndex = @PageIndex)
    UPDATE ImageRotation SET Rotate = @RotationValue WHERE ImageID = @ImageID AND PageIndex = @PageIndex
ELSE
    INSERT INTO ImageRotation (ImageID, PageIndex, Rotate) VALUES (@ImageID, @PageIndex, @RotationValue)

-- Update image status
UPDATE Image SET ImageStatusTypeID = @NeedsImageManipulation WHERE ID = @ImageID
```

#### Page Deletion Process
```csharp
// In eStacker.cs - ucViewer_PageDeleteAction event  
private void ucViewer_PageDeleteAction(PageDeleteActionEventArgs e)
{
    bool delete = e.Action == PageDeleteActionEnum.Deleted ? true : false;
    Process.CurrentWorkItem.PageDeleteAction(e.PageIndex, delete, CurrentUser.UserID);
}
```

**Database Records Created:**
```sql
-- Page deletion record
INSERT INTO ImagePageDeletion (ImageID, PageIndex, CreatedBy, DateCreated)
VALUES (@ImageID, @PageIndex, @UserID, GETUTCDATE())

-- Update image status  
UPDATE Image SET ImageStatusTypeID = @NeedsImageManipulation WHERE ID = @ImageID
```

### 2. Image Status Workflow

Images progress through these status states:

1. **Sync (1)** - Initial synchronized state
2. **NeedsImageManipulation (7)** - Has pending redactions/rotations/deletions
3. **PendingWorkman (8)** - Queued for processing
4. **InWorkman (9)** - Currently being processed by Workman
5. **NeedsProcessing (3)** - Ready for next workflow step

### 3. Plugin Processing Architecture

#### Watchman Service (ImageManipulationService.cs)

The Watchman service runs as a Windows service and orchestrates image processing:

```csharp
public class ImageManipulationService : IServiceInterface
{
    public TimeSpan RunInterval => new TimeSpan(0, 1, 0); // Runs every minute
    
    public void Execute()
    {
        ClearStuckImages(); // Clean up processing queue
        
        var threadPool = new SmartThreadPool(1000, 10);
        foreach (var img in GetManipulationImages())
        {
            // Queue for Workman processing
            threadPool.QueueWorkItem(new WorkItemCallback(QueueManipulation), new object[] { img });
        }
    }
    
    private List<Image> GetManipulationImages()
    {
        // Query for images needing manipulation
        return (from im in db.Images
                where !im.Corrupted
                where !im.Deleted  
                where im.ImageStatusTypeID == (int)ImageStatusTypeEnum.NeedsImageManipulation
                where !(from imq in db.ImageManipulationQueues where imq.DateFinished == null select imq.ImageID).Contains(im.ID)
                orderby im.ProcessOrder descending, im.DateUpdated ascending
                select im).ToList();
    }
}
```

**Processing Queue Management:**
```sql
-- Create queue entry
INSERT INTO ImageManipulationQueue (ImageID, DateRequested) VALUES (@ImageID, GETUTCDATE())

-- Update image status
UPDATE Image SET ImageStatusTypeID = @PendingWorkman WHERE ID = @ImageID
```

#### Workman Plugin (ImageManipulationWorkman.cs)

The Workman plugin performs the actual file manipulation:

```csharp
public class ImageManipulationWorkman : BaseWorkman<Image, ImageManipulationInput, ImageWorkmanRunLogHolder>
{
    public override WorkmanResult DoWork(Stream inputStream, ImageManipulationInput details, dynamic arguments)
    {
        // Save original copy if manipulations are needed
        if (details.Redactions.Count > 0 || details.Rotations.Count > 0 || details.Deletions.Count > 0)
        {
            CreateRedactOriginal(details.Image, inputStream);
        }
        
        // Process redactions
        if (details.Redactions.Count > 0)
        {
            PerformRedactions(inputStream, details);
            inputStream.Position = 0;
        }
        
        // Process rotations  
        if (details.Rotations.Count > 0)
        {
            PerformRotations(inputStream, details);
            inputStream.Position = 0;
        }
        
        // Process page deletions
        if (details.Deletions.Count > 0)
        {
            PerformPageDeletions(inputStream, details);
            inputStream.Position = 0;
        }
        
        return new WorkmanResult { OutputStream = inputStream };
    }
}
```

## S3 Bucket Structure

Files are organized in AWS S3 with the following structure:

```
s3://{bucket-name}/
├── {BucketPrefix}/           # Organizational prefix (e.g., by offering/project)
│   ├── Original/             # Original uploaded files (never modified)
│   │   └── {Path}/          # Base path from Image.Path field
│   │       └── {ImageID}/   # Individual image directory
│   │           └── {ImageID}.pdf
│   │
│   ├── Processing/           # Working copies for manipulation
│   │   └── {Path}/
│   │       └── {ImageID}/
│   │           └── {ImageID}.pdf
│   │
│   ├── RedactOriginal/       # Pre-redaction backup copies
│   │   └── {Path}/
│   │       └── {ImageID}/
│   │           └── {ImageID}.pdf
│   │
│   └── Production/           # Final processed files
│       └── {Path}/
│           └── {ImageID}/
│               └── {ImageID}.pdf
```

### File Path Methods

```csharp
// Key methods for S3 path generation
public string GetImagePath(ImageStatusTypeEnum status)
{
    switch (status)
    {
        case ImageStatusTypeEnum.NeedsConversion:
            return $"Original/{Path}/{ID}/{ID}.pdf";
        case ImageStatusTypeEnum.NeedsProcessing:  
            return $"Processing/{Path}/{ID}/{ID}.pdf";
        case ImageStatusTypeEnum.Sync:
            return $"Production/{Path}/{ID}/{ID}.pdf";
        default:
            return $"Processing/{Path}/{ID}/{ID}.pdf";
    }
}

public string GetRedactPath()
{
    return $"RedactOriginal/{Path}/{ID}/{ID}.pdf";
}

public string GetProcessingPath()
{
    return $"Processing/{Path}/{ID}/{ID}.pdf";
}
```

### File Manipulation Process

1. **Retrieve File**: Get processing copy from S3
2. **Create Backup**: Save RedactOriginal if manipulations needed
3. **Apply Changes**: Perform redactions, rotations, deletions
4. **Save Result**: Write back to Processing path
5. **Update Status**: Mark as ready for next workflow step

## Plugin Architecture

### Redaction Processing

```csharp
private void PerformRedactions(Stream inputStream, ImageManipulationInput details)
{
    var doc = new TallComponents.PDF.Document(inputStream);
    var rddoc = new TallComponents.PDF.Document();
    
    // Process each page
    for (int i = 0; i < doc.Pages.Count; i++)
    {
        var tpage = doc.Pages[i].Clone();
        var redacts = details.Redactions.Where(r => r.PageNumber == i).ToList();
        
        if (redacts.Any())
        {
            foreach (var redact in redacts)
            {
                // Draw black rectangle over redacted area
                var rshape = new RectangleShape(
                    redact.PageX, redact.PageY, 
                    redact.PageWidth, redact.PageHeight,
                    new Pen(Color.Black), 
                    new SolidBrush(Color.Black)
                );
                tpage.Overlay.Add(rshape);
            }
            rddoc.Pages.Add(tpage);
        }
    }
    
    // Rasterize redacted pages to prevent text extraction
    if (rddoc.Pages.Count > 0)
    {
        var docStream = new MemoryStream();
        rddoc.Write(docStream, true);
        docStream.Position = 0;
        ImagingUtilities.RasterizeDocument(docStream, sizes);
    }
}
```

### Rotation Processing  

```csharp
private void PerformRotations(Stream inputStream, ImageManipulationInput details)
{
    var rotateDoc = new TallComponents.PDF.Document(inputStream);
    var rotates = details.Rotations.ToList();
    
    for (int i = 0; i < rotateDoc.Pages.Count; i++)
    {
        var rotation = rotates.FirstOrDefault(r => r.PageIndex == i);
        if (rotation != null)
        {
            // Apply rotation (0, 90, 180, 270 degrees)
            rotateDoc.Pages[i].Orientation = (TallComponents.PDF.Orientation)rotation.Rotate;
        }
    }
    
    inputStream.Position = 0;
    inputStream.SetLength(0);
    rotateDoc.Write(inputStream, true);
}
```

### Page Deletion Processing

```csharp
private void PerformPageDeletions(Stream inputStream, ImageManipulationInput details)
{
    var dels = details.Deletions.OrderByDescending(d => d.PageIndex).Select(d => d.PageIndex).ToList();
    
    // If all pages are being deleted, mark entire image as deleted
    if (dels.Count >= details.Image.PageCount)
    {
        details.Image.Deleted = true;
    }
    else
    {
        var delDoc = new TallComponents.PDF.Document(inputStream);
        
        // Remove pages in reverse order to maintain indices
        foreach (var pageIndex in dels)
        {
            if (pageIndex < delDoc.Pages.Count)
                delDoc.Pages.RemoveAt(pageIndex);
        }
        
        inputStream.Position = 0;
        inputStream.SetLength(0);
        delDoc.Write(inputStream, true);
        newPageCount = delDoc.Pages.Count;
    }
}
```

## Error Handling and Recovery

### Stuck Image Cleanup

```csharp
private void ClearStuckImages()
{
    using (var db = new LoanmasterEntities())
    {
        var fourHoursAgo = DateTime.UtcNow.AddHours(-4);
        var imgs = (from imq in db.ImageManipulationQueues
                    where imq.DateFinished == null
                    where imq.DateRequested < fourHoursAgo
                    select imq);
        
        db.ImageManipulationQueues.DeleteAllOnSubmit(imgs);
        db.SaveChanges();
    }
}
```

### Processing Error Recovery

The system includes comprehensive error recovery in the `SaveComplete` method:

```csharp
private void HandleImageSaveError(SaveResult result)
{
    using (var db = new LoanmasterEntities())
    {
        // Reset fields on original image
        var image = db.Images.Single(i => i.ID == result.OriginalImageID);
        var originalImage = result.SaveInfo.OriginalImage;
        
        // Restore original state
        image.DocTypeManualID = originalImage.DocTypeManualID;
        image.DocumentDate = originalImage.DocumentDate;
        image.Comments = originalImage.Comments;
        image.ImageStatusTypeID = originalImage.ImageStatusTypeID;
        
        // Reset redactions, rotations, deletions
        // Remove any split images created during failed process
        // Delete files from S3 storage
        
        db.SaveChanges();
    }
}
```

## Document Page Break and Splitting Process

### Overview

The page break functionality in eStacker allows users to split multi-page PDF documents into separate documents. **Important**: Page breaks do NOT automatically split PDFs when added - they are stored as bookmarks and processed during the save operation.

### Page Break Database Schema

#### ImageBookmark Table
```sql
-- Stores document page breaks/separations
ImageBookmark (
    ID int PRIMARY KEY IDENTITY,
    ImageID int FOREIGN KEY REFERENCES Image(ID),
    PageIndex int, -- Zero-based page index where break occurs
    Text nvarchar(max), -- Contains document type and metadata 
    ImageDocumentTypeID int, -- Document type for the new split
    ResultImageID int NULL, -- ID of the created split image (populated after save)
    Deleted bit DEFAULT 0
)
```

#### ImageSplitLog Table
```sql
-- Audit trail of document splits
ImageSplitLog (
    ID int PRIMARY KEY IDENTITY,
    OriginalImageID int FOREIGN KEY REFERENCES Image(ID),
    SplitImageID int FOREIGN KEY REFERENCES Image(ID), 
    SplitBy int FOREIGN KEY REFERENCES Users(ID),
    DateCreated datetime DEFAULT GETUTCDATE()
)
```

### Page Break Process Flow

#### 1. Adding Page Breaks (User Interface)

When users add page breaks in the eStacker viewer:

```csharp
// In eStacker.cs - ucViewer_BreakAdded event
private void ucViewer_BreakAdded(BreakEventArgs e)
{
    SaveBreak(new KeyValuePair<string, string>("-1", null), null);
}

// SaveBreak method creates bookmark record
private void SaveBreak(KeyValuePair<string, string> dt, string extra)
{
    if (ValidateBreak(dt, extra, false))
    {
        var data = BuildBreakData(dt, extra);
        var pageIndex = ucViewer.Viewer.CurrentPageIndex;
        var docTypeID = int.Parse(dt.Key);
        
        // Save bookmark to database - NOT split yet
        int bkid = Process.CurrentWorkItem.SaveBookmark(pageIndex, data, docTypeID);
        
        // Add visual bookmark to viewer
        ucViewer.AddBookmark(bkid, new BookmarkInfo(bkid, pageIndex, data, docTypeID));
    }
}
```

**Database Records Created:**
```sql
-- Creates bookmark record (split happens later during save)
INSERT INTO ImageBookmark (ImageID, PageIndex, Text, ImageDocumentTypeID, Deleted)
VALUES (@ImageID, @PageIndex, @BookmarkData, @DocTypeID, 0)

-- Mark image as having pending changes
INSERT INTO ImageChangesPending (ImageID, FirstChanged, LastChanged) 
VALUES (@ImageID, GETUTCDATE(), GETUTCDATE())
```

**Key Point**: At this stage, the PDF is **NOT split**. The break is stored as a bookmark record and displayed visually in the viewer.

#### 2. Document Splitting During Save Operation

The actual PDF splitting occurs when the user saves the document:

```csharp
// In wrkSave_DoWork method
private void wrkSave_DoWork(object sender, DoWorkEventArgs e)
{
    SaveInfo saveInfo = e.Argument as SaveInfo;
    
    // Process document splits based on bookmark breaks
    if (saveInfo.BreaksOrdered.Count > 0) // One or more breaks
    {
        bool hasRootBreak = false;
        
        for (int i = 0; i < saveInfo.BreaksOrdered.Count; i++)
        {
            var bk = saveInfo.BreaksOrdered[i]; // Ordered by PageIndex
            
            // Determine page range for this split
            int startIndex = bk.PageIndex;
            int endIndex = (i + 1 == saveInfo.BreaksOrdered.Count) 
                ? totalPageCount 
                : saveInfo.BreaksOrdered[i + 1].PageIndex;
            
            // Create new document from page range
            var newImage = ProcessNewDocument(filePath, startIndex, endIndex, 
                originalImage, bookmarkData, saveInfo);
            
            // Link bookmark to resulting image
            bk.ResultImageID = newImage.ID;
        }
        
        // Original image becomes obsolete after splitting
        originalImage.ImageStatusTypeID = (int)ImageStatusTypeEnum.Obsolete;
    }
}
```

#### 3. PDF File Splitting Process

The `ProcessNewDocument` method handles the actual PDF manipulation:

```csharp
private global.DAL.Aurora.Image ProcessNewDocument(string filePath, 
    int startIndex, int endIndex, Image baseImage, BKData docData, SaveInfo saveInfo)
{
    using (var db = new LoanmasterEntities())
    {
        // Use DocOperator to physically split the PDF
        var dop = new DocOperator();
        var splitFilePath = dop.SplitDocument(filePath, startIndex, endIndex, TEMP_FOLDER);
        
        // Create new Image database record
        var newImage = CreateNewImage(baseImage, docData, splitFilePath, hasRedactions);
        newImage.ImageStatusTypeID = (int)ImageStatusTypeEnum.AwaitingFileCopy;
        
        db.Images.AddObject(newImage);
        db.SaveChanges();
        
        // Save split files to S3 storage
        using(var storMan = new S3StorageManager())
        using (var fileStream = File.Open(splitFilePath, FileMode.Open, FileAccess.Read))
        {
            // Save to Original path
            storMan.SaveFile(fileStream, newImage.GetImagePath(ImageStatusTypeEnum.NeedsConversion), 
                newImage.BucketPrefix);
            
            // Save to Processing path  
            fileStream.Position = 0;
            storMan.SaveFile(fileStream, newImage.GetImagePath(ImageStatusTypeEnum.NeedsProcessing),
                newImage.BucketPrefix);
        }
        
        // Create audit trail record
        var splitLog = new ImageSplitLog();
        splitLog.OriginalImageID = baseImage.ID;
        splitLog.SplitImageID = newImage.ID;
        splitLog.SplitBy = CurrentUser.UserID;
        db.ImageSplitLogs.AddObject(splitLog);
        
        // Copy redactions, rotations, deletions to new image (adjusted for page ranges)
        CopyManipulationsToSplitImage(newImage, saveInfo, startIndex);
        
        db.SaveChanges();
        return newImage;
    }
}
```

### Split Scenarios

The system handles three different splitting scenarios:

#### Scenario 1: Single Break at Top (Page 0)
```csharp
if (saveInfo.HasOneBreakAtTop)
{
    // Simple case - just rename/reindex the document
    // No physical PDF splitting required
    originalImage.DocTypeManualID = bookmarkData.ImageDocTypeID;
    originalImage.DocumentDate = bookmarkData.DocDate;
    originalImage.Comments = bookmarkData.Comments;
    
    // Mark bookmark as processed
    bookmark.Deleted = true;
}
```
**Result**: Original PDF remains intact, only metadata is updated.

#### Scenario 2: No Breaks
```csharp
else if (saveInfo.BreaksOrdered.Count == 0)
{
    // Process redactions/rotations/deletions only
    // No document splitting
}
```
**Result**: Original PDF processed for manipulations only.

#### Scenario 3: Multiple Breaks
```csharp
else // One or more breaks
{
    // Split PDF into multiple documents
    foreach (var break in saveInfo.BreaksOrdered)
    {
        var newImage = ProcessNewDocument(/* page range */);
        result.NewImageIDList.Add(newImage.ID);
    }
    
    // Handle case where first break is not at page 0
    if (!hasRootBreak)
    {
        // Create additional document for pages 0 to first break
        var frontPages = ProcessNewDocument(0, firstBreakPageIndex, /* ... */);
    }
    
    // Mark original as obsolete
    originalImage.ImageStatusTypeID = (int)ImageStatusTypeEnum.Obsolete;
}
```
**Result**: Original PDF split into multiple separate Image records and PDF files.

### File Storage After Splitting

Each split creates separate files in S3:

```
s3://{bucket}/
├── {BucketPrefix}/
│   ├── Original/
│   │   ├── {OriginalImageID}.pdf          # Original multi-page PDF
│   │   ├── {SplitImage1ID}.pdf           # First split document
│   │   └── {SplitImage2ID}.pdf           # Second split document
│   │
│   ├── Processing/
│   │   ├── {SplitImage1ID}.pdf           # Processing copies
│   │   └── {SplitImage2ID}.pdf
│   │
│   └── Production/
│       ├── {SplitImage1ID}.pdf           # Final processed files
│       └── {SplitImage2ID}.pdf
```

### Database State After Splitting

**Original Image Record:**
```sql
UPDATE Image 
SET ImageStatusTypeID = @Obsolete 
WHERE ID = @OriginalImageID
```

**New Split Image Records:**
```sql
-- Each split creates new Image record
INSERT INTO Image (LoanID, OfferingID, DocTypeManualID, PageCount, /* ... */)
VALUES (@LoanID, @OfferingID, @DocTypeFromBookmark, @SplitPageCount, /* ... */)

-- Bookmark links to resulting image
UPDATE ImageBookmark 
SET ResultImageID = @NewImageID, Deleted = 1
WHERE ID = @BookmarkID

-- Audit trail
INSERT INTO ImageSplitLog (OriginalImageID, SplitImageID, SplitBy)
VALUES (@OriginalImageID, @NewImageID, @UserID)
```

### Manipulation Inheritance

When documents are split, redactions, rotations, and page deletions are copied to the appropriate split documents:

```csharp
// Copy relevant redactions to new image (page numbers adjusted)
foreach (var redaction in redactionsBetweenPages)
{
    var newRedaction = new ImageRedaction();
    newRedaction.PageNumber = redaction.PageNumber - startIndex; // Adjust page number
    newRedaction.ImageID = newImage.ID;
    // Copy coordinates and other properties...
    
    db.ImageRedactions.AddObject(newRedaction);
    originalRedaction.Deleted = true; // Mark original as processed
}
```

### Error Recovery

If splitting fails, the system includes comprehensive rollback:

```csharp
private void HandleImageSaveError(SaveResult result)
{
    // Reset original image state
    // Delete any partially created split images
    // Remove split files from S3
    // Restore bookmark records
    // Clean up ImageSplitLog entries
}
```

### Performance Considerations

1. **Lazy Processing**: Bookmarks are stored immediately but splitting is deferred until save
2. **Background Processing**: PDF splitting occurs in background worker thread
3. **Progress Reporting**: User sees progress bar during multi-document splits
4. **File Streaming**: Large PDFs processed via streams to minimize memory usage
5. **Atomic Operations**: Database transactions ensure consistency

### User Experience Flow

1. **Add Break**: User adds page break → Bookmark created → Visual indicator shown
2. **Multiple Breaks**: User can add multiple breaks → All stored as bookmarks  
3. **Save Document**: User clicks Save → Background processing begins
4. **Progress Display**: Progress bar shows split progress
5. **Completion**: Original document becomes obsolete, user sees new split documents

This design provides a flexible document splitting system that allows users to plan their splits before committing to the operation, while maintaining data integrity and providing comprehensive error recovery.

## User Interface Visual Display

### Overview

The eStacker application provides visual feedback for all document manipulations through the ucPDFViewer control. This section details how redactions, page breaks, rotations, and deletions are visually represented to users in both the main document viewer and thumbnail panel.

### Redaction Visual Display

#### Main Viewer Display
```csharp
// In PaintExistingRedacts() method
private void PaintExistingRedacts(Graphics g)
{
    if (redacts != null)
    {
        var b = new SolidBrush(Color.Black);      // Black border
        var p = new Pen(b);
        
        var en = redacts.GetEnumerator();
        while (en.MoveNext())
        {
            DrawBox(g, b, p, Color.Yellow, en.Current); // Yellow fill
        }
    }
}

// Redaction boxes drawn with 50% transparency
var yb = new SolidBrush(Color.FromArgb(50, Color.Yellow));
g.FillRectangle(yb, client);
```

**Visual Characteristics:**
- **Color**: Yellow with 50% transparency (`Color.FromArgb(50, Color.Yellow)`)
- **Border**: Black solid line around the rectangle
- **Behavior**: Semi-transparent overlay allowing document content to show through
- **Interactive**: Can be selected, moved, and resized by clicking and dragging

#### Thumbnail Display
```csharp
// In PaintBookmarkRedacts() method for thumbnail viewer
private void PaintBookmarkRedacts(Graphics g)
{
    // Same yellow overlay applied to thumbnail representations
    var yb = new SolidBrush(Color.FromArgb(50, Color.Yellow));
    g.FillRectangle(yb, client);
}
```

### Page Break Visual Display

#### Main Viewer Display
```csharp
// In PaintExistingBreaks() method
private void PaintExistingBreaks(Graphics g)
{
    // Calculate break bar positioning
    client.Height = (int)(pvDocument.Spacing * pvDocument.ZoomFactor);
    client.Y -= (int)(pvDocument.Spacing * pvDocument.ZoomFactor);
    
    // Draw break bar with appropriate color
    SolidBrush yb = new SolidBrush(Color.FromArgb(90, Color.Green));
    if (en.Current.Value.IsGenericBreak)
        yb = new SolidBrush(Color.FromArgb(90, Color.Orange));
    
    g.FillRectangle(yb, client);
    
    // Display break text in white bold font
    var text = en.Current.Value.GetDisplayText();
    g.DrawString(text, font, new SolidBrush(Color.White), client.X, client.Y);
}
```

**Visual Characteristics:**
- **Normal Breaks**: Green horizontal bar (`Color.FromArgb(90, Color.Green)`)
- **Generic Breaks**: Orange horizontal bar (`Color.FromArgb(90, Color.Orange)`)
- **Position**: Horizontal bar positioned above the page where break occurs
- **Text**: White bold font displaying document type or "---GENERIC BREAK---"
- **Border**: Black outline around the colored bar
- **Width**: Spans full width of the document viewer

#### Break Text Display Rules
```csharp
public string GetDisplayText()
{
    if (IsGenericBreak)
    {
        return "---GENERIC BREAK---";
    }
    else
    {
        var split = Text.Split(new[] { '|' });
        var name = split[0];
        var rx = new Regex(@"\([0-9]+\)");
        var final = rx.Replace(name, ""); // Remove ID numbers
        return final;
    }
}
```

#### Thumbnail Display
```csharp
// In PaintBookmarkThumb() method
private void PaintBookmarkThumb(Graphics g)
{
    // Same color scheme applied to thumbnail viewer
    // Positioned above thumbnail pages
    trect.Height = (int)(tvRight.Spacing * tvRight.ZoomFactor);
    trect.Y -= (int)(tvRight.Spacing * tvRight.ZoomFactor);
    trect.Width = tvRight.ClientRectangle.Width; // Full width
}
```

### Page Deletion Visual Display

#### Main Viewer Display
```csharp
// In PaintDeletions() method
private void PaintDeletions(Graphics g)
{
    var b = new SolidBrush(Color.FromArgb(75, Color.Red)); // Semi-transparent red
    var p = new Pen(b);
    p.Width = 5; // Thick lines
    
    // Draw X pattern across deleted pages
    DrawDeletePageRectangle(g, p, client);
}

// X-pattern drawing implementation
private void DrawDeletePageRectangle(Graphics g, Pen p, RectangleF client)
{
    // Diagonal line from top-left to bottom-right
    var point1 = new PointF(client.X, client.Y);
    var point2 = new PointF(client.X + client.Width, client.Y + client.Height);
    g.DrawLine(p, point1, point2);
    
    // Diagonal line from top-right to bottom-left
    var point3 = new PointF(client.X + client.Width, client.Y);
    var point4 = new PointF(client.X, client.Y + client.Height);
    g.DrawLine(p, point3, point4);
}
```

**Visual Characteristics:**
- **Color**: Semi-transparent red (`Color.FromArgb(75, Color.Red)`)
- **Pattern**: Two diagonal lines forming an X across the entire page
- **Line Width**: 5 pixels thick for visibility
- **Coverage**: Entire page area covered by the X pattern
- **Behavior**: Applied to individual pages or entire document if marked as deleted

#### Thumbnail Display
```csharp
// In PaintDeletionsThumb() method - same X pattern applied to thumbnails
private void PaintDeletionsThumb(Graphics g)
{
    var p = new Pen(b);
    p.Width = 2; // Thinner lines for thumbnails
    DrawDeletePageRectangle(g, p, client);
}
```

### Page Rotation Visual Display

#### Implementation
```csharp
// Rotation applied directly to PDF page orientation
for (int i = 0; i < document.Pages.Count; i++)
{
    int rot = 0;
    if (rotations.ContainsKey(i))
        rot = rotations[i];
    else
        rot = (int)document.Pages[i].Orientation;
    
    if(rot != (int)document.Pages[i].Orientation)
        document.Pages[i].Orientation = (TallComponents.PDF.Orientation)rot;
}
```

**Visual Characteristics:**
- **Display**: No overlay graphics - page content itself is rotated
- **Rotation Values**: 0°, 90°, 180°, 270° (stored as 0, 1, 2, 3)
- **Immediate Feedback**: Rotation visible immediately in both main and thumbnail viewers
- **Persistence**: Rotation state maintained until document save operation

#### Rotation Controls
```csharp
// Toolbar buttons for rotation
private void btnRotateClockwise_Click(object sender, EventArgs e)
{
    RotateRight(); // 90° clockwise rotation
}

private void btnRotateCounterClockwise_Click(object sender, EventArgs e)
{
    RotateLeft(); // 90° counter-clockwise rotation
}
```

### Annotation Visual Display

#### Blue Overlay System
```csharp
// In PaintAnnotations() method
private void PaintAnnotations(Graphics g)
{
    var b = new SolidBrush(Color.Black);
    var p = new Pen(b);
    
    // Blue semi-transparent rectangles
    DrawBox(g, b, p, Color.Blue, en.Current);
}

// Blue fill with 50% transparency
var yb = new SolidBrush(Color.FromArgb(50, Color.Blue));
g.FillRectangle(yb, client);
```

**Visual Characteristics:**
- **Color**: Blue with 50% transparency (`Color.FromArgb(50, Color.Blue)`)
- **Purpose**: Different from redactions, used for highlighting/annotation
- **Behavior**: Similar interaction to redactions but different color coding

### Viewer Mode System

#### Mode-Based Visual Behavior
```csharp
public enum ViewerModeEnum
{
    Selection = 1,    // Text selection mode
    Drag = 2,        // Pan/navigate mode
    Redaction = 3,   // Redaction drawing mode
    Break = 5,       // Page break mode
    Annotation = 6   // Annotation mode
}

private void SetMode(ViewerModeEnum mode)
{
    currentMode = mode;
    lblMode.Text = currentMode.ToString("f") + " Mode";
    
    switch (currentMode)
    {
        case ViewerModeEnum.Redaction:
            pvDocument.Cursor = System.Windows.Forms.Cursors.Arrow;
            pvDocument.CursorMode = CursorMode.Custom;
            break;
        case ViewerModeEnum.Selection:
            pvDocument.Cursor = System.Windows.Forms.Cursors.IBeam;
            pvDocument.CursorMode = CursorMode.SelectText;
            break;
        case ViewerModeEnum.Drag:
            pvDocument.Cursor = System.Windows.Forms.Cursors.Hand;
            pvDocument.CursorMode = CursorMode.Normal;
            break;
    }
}
```

### Interactive Selection System

#### Selection Box Highlighting
```csharp
// When user clicks on existing redaction/annotation
private void HighlightSelectionBox(BoxCoordinates box)
{
    Color drawColor = Color.Red;
    if (currentMode == ViewerModeEnum.Annotation)
        drawColor = Color.Blue;
    
    selectionBox = new UserRectangle(drawColor);
    selectionBox.SetDrawCoords(client);
    selectionBox.BoxChanged += selectionBox_BoxChanged;
    selectionBox.BoxChangeCompleted += selectionBox_BoxChangeCompleted;
}
```

**Selection Visual Feedback:**
- **Redaction Selection**: Red border around selected redaction
- **Annotation Selection**: Blue border around selected annotation
- **Resize Handles**: Corner handles for resizing selected boxes
- **Move Capability**: Click and drag to reposition selected items

### UI Control Integration

#### Toolbar Controls
- **Rotation Buttons**: `btnRotateClockwise`, `btnRotateCounterClockwise`
- **Page Break Buttons**: `btnGenericBreak`, `btnRemoveBreak`
- **Page Delete Toggle**: `rbtnDeletePage` (toggle button)
- **Mode Selection**: Context menu with redaction/selection/pan modes

#### Status Display
```csharp
// Mode indicator
lblMode.Text = currentMode.ToString("f") + " Mode";

// Page counter
lblPageCount.Text = " of " + pages;
```

### Performance Optimizations

#### Efficient Redrawing
```csharp
// Only redraw when necessary
private void pvDocument_PostPaint(object sender, PaintEventArgs e)
{
    if (document != null && document.HasDocument)
    {
        PaintExistingRedacts(e.Graphics);
        PaintAnnotations(e.Graphics);
        PaintExistingBreaks(e.Graphics);
        PaintDeletions(e.Graphics);
        PaintCustomBoxes(e.Graphics);
    }
}
```

#### Coordinate Translation
```csharp
// Convert between page coordinates and display coordinates
private RectangleF FromClientToPage(int pageIndex, RectangleF client)
{
    var docarea = pvDocument.ClientToDocument(client);
    var page = pvDocument.DocumentToPage(pageIndex, docarea);
    return page;
}
```

This comprehensive UI system provides immediate visual feedback for all document manipulations, allowing users to see exactly what changes will be applied before saving the document.

## API and Integration Points

### Key Service Interfaces

```csharp
// Main processing interface
public interface IServiceInterface
{
    DateTime? StartTime { get; }
    TimeSpan RunInterval { get; }
    void Execute();
}

// Image manipulation input structure
public class ImageManipulationInput
{
    public ImageHolder Image { get; set; }
    public List<ImageRedactionHolder> Redactions { get; set; }
    public List<ImageRotationHolder> Rotations { get; set; }
    public List<ImagePageDeletionHolder> Deletions { get; set; }
}
```

### External Dependencies

- **TallComponents.PDF**: PDF manipulation library
- **AWS S3**: File storage
- **Aspose.PDF**: Document processing
- **SmartThreadPool**: Multithreading management

### Configuration Points

```xml
<!-- Key configuration settings -->
<appSettings>
    <add key="Global.ImpersonateUser" value="service_account" />
    <add key="Global.ImpersonateDomain" value="domain" />
    <add key="Global.ImpersonatePWD" value="password" />
</appSettings>
```

## Performance Considerations

1. **Threading**: Uses SmartThreadPool with max 10 concurrent threads
2. **File Streaming**: Processes files in memory streams to avoid disk I/O
3. **Queue Management**: Processes images by priority order
4. **Cleanup**: Regular cleanup of stuck processing entries
5. **Rasterization**: Only rasterizes pages that contain redactions

## Security Considerations

1. **Access Control**: User permissions checked before allowing actions
2. **Audit Trail**: All actions logged with user ID and timestamp
3. **File Integrity**: Original files preserved for recovery
4. **Redaction Security**: Redacted content is rasterized to prevent extraction

This architecture provides a robust, scalable solution for document image manipulation with comprehensive error handling and recovery capabilities.
