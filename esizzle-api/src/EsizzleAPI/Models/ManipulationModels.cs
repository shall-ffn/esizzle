using System.ComponentModel.DataAnnotations;

namespace EsizzleAPI.Models;

// Request/Response models for PDF manipulation operations

public class SaveRedactionRequest
{
    [Required]
    public int DocumentId { get; set; }
    
    [Required]
    public List<RedactionAnnotationRequest> Redactions { get; set; } = new();
}

public class RedactionAnnotationRequest
{
    [Required]
    public int ImageId { get; set; }
    
    [Required]
    public int PageNumber { get; set; }  // 0-based
    
    [Required]
    public float PageX { get; set; }
    
    [Required]
    public float PageY { get; set; }
    
    [Required]
    public float PageWidth { get; set; }
    
    [Required]
    public float PageHeight { get; set; }
    
    public string? Text { get; set; }
    
    public int DrawOrientation { get; set; } = 0;
    
    [Required]
    public int CreatedBy { get; set; }
    
    public string Guid { get; set; } = System.Guid.NewGuid().ToString();
}

public class SaveRotationRequest
{
    [Required]
    public int DocumentId { get; set; }
    
    [Required]
    public List<RotationAnnotationRequest> Rotations { get; set; } = new();
}

public class RotationAnnotationRequest
{
    [Required]
    public int ImageId { get; set; }
    
    [Required]
    public int PageIndex { get; set; }  // 0-based
    
    [Required]
    public int Rotate { get; set; }  // 0, 90, 180, 270
}

public class SavePageBreakRequest
{
    [Required]
    public int DocumentId { get; set; }
    
    [Required]
    public List<PageBreakAnnotationRequest> PageBreaks { get; set; } = new();
}

public class PageBreakAnnotationRequest
{
    [Required]
    public int ImageId { get; set; }
    
    [Required]
    public int PageIndex { get; set; }  // 0-based
    
    [Required]
    public string Text { get; set; } = string.Empty;
    
    [Required]
    public int ImageDocumentTypeId { get; set; }
    
    public DateTime? DocumentDate { get; set; }
    
    public string? Comments { get; set; }
}

public class SavePageDeletionRequest
{
    [Required]
    public int DocumentId { get; set; }
    
    [Required]
    public List<PageDeletionAnnotationRequest> PageDeletions { get; set; } = new();
}

public class PageDeletionAnnotationRequest
{
    [Required]
    public int ImageId { get; set; }
    
    [Required]
    public int PageIndex { get; set; }  // 0-based
    
    [Required]
    public int CreatedBy { get; set; }
}

public class ProcessManipulationsRequest
{
    [Required]
    public int DocumentId { get; set; }
    
    public string? SessionId { get; set; }
}

public class ProcessManipulationsResponse
{
    public string SessionId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int DocumentId { get; set; }
}

public class ProcessingProgressUpdate
{
    [Required]
    public string SessionId { get; set; } = string.Empty;
    
    [Required]
    public int ImageId { get; set; }
    
    [Required]
    public string Status { get; set; } = string.Empty;  // starting, processing, completed, error
    
    [Required]
    public int Progress { get; set; }  // 0-100
    
    [Required]
    public string Message { get; set; } = string.Empty;
    
    public object? Data { get; set; }
    
    public string? Error { get; set; }
}

public class ProcessingStatus
{
    public int DocumentId { get; set; }
    public string Status { get; set; } = string.Empty;
    public int Progress { get; set; }
    public string? CurrentOperation { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime LastUpdate { get; set; }
    public object? Result { get; set; }
    public string? Error { get; set; }
}

public class DocumentManipulationState
{
    public int DocumentId { get; set; }
    public int PageCount { get; set; }
    public List<RedactionAnnotation> Redactions { get; set; } = new();
    public List<RotationAnnotation> Rotations { get; set; } = new();
    public List<PageBreakAnnotation> PageBreaks { get; set; } = new();
    public List<PageDeletionAnnotation> PageDeletions { get; set; } = new();
    public bool HasUnsavedChanges { get; set; }
    public string ProcessingStatus { get; set; } = "idle";
    public DateTime LastModified { get; set; }
    public int ModifiedBy { get; set; }
}

public class RedactionAnnotation
{
    public int Id { get; set; }
    public int ImageId { get; set; }
    public int PageNumber { get; set; }
    public float PageX { get; set; }
    public float PageY { get; set; }
    public float PageWidth { get; set; }
    public float PageHeight { get; set; }
    public string Guid { get; set; } = string.Empty;
    public string? Text { get; set; }
    public bool Applied { get; set; }
    public int DrawOrientation { get; set; }
    public int CreatedBy { get; set; }
    public DateTime DateCreated { get; set; }
    public bool Deleted { get; set; }
}

public class RotationAnnotation
{
    public int Id { get; set; }
    public int ImageId { get; set; }
    public int PageIndex { get; set; }
    public int Rotate { get; set; }
}

public class PageBreakAnnotation
{
    public int Id { get; set; }
    public int ImageId { get; set; }
    public int PageIndex { get; set; }
    public string Text { get; set; } = string.Empty;
    public int ImageDocumentTypeId { get; set; }
    public int? ResultImageId { get; set; }
    public bool IsGeneric { get; set; }
    public string DisplayText { get; set; } = string.Empty;
    public bool Deleted { get; set; }
    public DateTime? DocumentDate { get; set; }
    public string? Comments { get; set; }
}

public class PageDeletionAnnotation
{
    public int Id { get; set; }
    public int ImageId { get; set; }
    public int PageIndex { get; set; }
    public int CreatedBy { get; set; }
    public DateTime DateCreated { get; set; }
}

public class ChangeSummary
{
    public int PendingRedactions { get; set; }
    public int PendingRotations { get; set; }
    public int PendingPageBreaks { get; set; }
    public int PendingDeletions { get; set; }
    public int TotalChanges { get; set; }
}

public class ManipulationValidationResult
{
    public bool Valid { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}
