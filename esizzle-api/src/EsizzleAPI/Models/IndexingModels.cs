using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EsizzleAPI.Models
{
    // === LEGACY DATABASE MODELS ===

    /// <summary>
    /// Main document table matching legacy Images schema
    /// </summary>
    [Table("Image")]
    public class Image
    {
        [Key]
        public int ID { get; set; }

        public int? DocTypeManualID { get; set; }  // FK to ImageDocTypeMasterLists (User-assigned)
        public int? DocTypeAutoID { get; set; }    // FK to ImageDocTypeMasterLists (AI-suggested)
        
        [Required]
        public int LoanID { get; set; }            // FK to Loans

        public DateTime? DocumentDate { get; set; }
        public string? Comments { get; set; }
        public string? ParsedName { get; set; }
        
        [Required]
        [MaxLength(255)]
        public string OriginalName { get; set; } = string.Empty;
        
        [Required]
        public int PageCount { get; set; } = 1;
        
        [Required]
        [MaxLength(500)]
        public string FilePath { get; set; } = string.Empty;
        
        [Required]
        public int CreatedBy { get; set; }         // FK to Users
        
        [Required]
        public DateTime DateCreated { get; set; } = DateTime.UtcNow;
        
        [Required]
        public DateTime LastModified { get; set; } = DateTime.UtcNow;
        
        [Required]
        public bool IsDeleted { get; set; } = false;

        // Navigation properties
        public virtual Loan? Loan { get; set; }
        public virtual ImageDocTypeMasterList? DocTypeManual { get; set; }
        public virtual ImageDocTypeMasterList? DocTypeAuto { get; set; }
        public virtual ICollection<ImageBookmark> Bookmarks { get; set; } = new List<ImageBookmark>();
    }

    /// <summary>
    /// Document bookmarks/breaks table matching legacy schema
    /// </summary>
    [Table("ImageBookmark")]
    public class ImageBookmark
    {
        [Key]
        public int ID { get; set; }

        [Required]
        public int ImageID { get; set; }           // FK to Images (Source document)

        [Required]
        public int PageIndex { get; set; }         // 0-based page number

        [Required]
        public string Text { get; set; } = string.Empty;  // Pipe-delimited format

        [Required]
        public int ImageDocumentTypeID { get; set; } // FK to ImageDocTypeMasterLists

        public int? ResultImageID { get; set; }    // FK to Images (Created split document)

        [Required]
        public int CreatedBy { get; set; }         // FK to Users

        [Required]
        public DateTime DateCreated { get; set; } = DateTime.UtcNow;

        [Required]
        public bool IsDeleted { get; set; } = false;

        // Navigation properties
        public virtual Image SourceImage { get; set; } = null!;
        public virtual Image? ResultImage { get; set; }
        public virtual ImageDocTypeMasterList DocumentType { get; set; } = null!;
    }

    /// <summary>
    /// Master document types table matching legacy schema
    /// </summary>
    [Table("ImageDocTypeMasterList")]
    public class ImageDocTypeMasterList
    {
        [Key]
        public int ID { get; set; }

        [Required]
        [MaxLength(255)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string Code { get; set; } = string.Empty;  // IndexCode: "ffn", "UPB", etc.

        [Required]
        public DateTime DateCreated { get; set; } = DateTime.UtcNow;

        [Required]
        public bool IsUsed { get; set; } = true;           // Active/inactive flag

        [Required]
        public bool IsGeneric { get; set; } = false;       // Orange vs green display

        // Navigation properties
        public virtual ICollection<ImageBookmark> Bookmarks { get; set; } = new List<ImageBookmark>();
        public virtual ICollection<Image> ManuallyTypedImages { get; set; } = new List<Image>();
        public virtual ICollection<Image> AutoTypedImages { get; set; } = new List<Image>();
    }

    /// <summary>
    /// Project offerings table matching legacy schema
    /// </summary>
    [Table("Offerings")]
    public class Offering
    {
        [Key]
        public int OfferingID { get; set; }

        [Required]
        [MaxLength(50)]
        public string IndexCode { get; set; } = string.Empty;  // Controls available doc types

        [Required]
        [MaxLength(255)]
        public string OfferingName { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? OfferingDescription { get; set; }

        [Required]
        public bool IsActive { get; set; } = true;

        // Navigation properties
        public virtual ICollection<Sale> Sales { get; set; } = new List<Sale>();
    }

    /// <summary>
    /// Sales table matching legacy schema
    /// </summary>
    [Table("Sales")]
    public class Sale
    {
        [Key]
        public int SaleID { get; set; }

        [Required]
        [MaxLength(255)]
        public string SaleDesc { get; set; } = string.Empty;

        [Required]
        public int OfferingID { get; set; }        // FK to Offerings

        [Required]
        public int LoansCount { get; set; } = 0;

        // Navigation properties
        public virtual Offering Offering { get; set; } = null!;
        public virtual ICollection<Loan> Loans { get; set; } = new List<Loan>();
    }

    /// <summary>
    /// Loans table matching legacy schema
    /// </summary>
    [Table("Loans")]
    public class Loan
    {
        [Key]
        public int ID { get; set; }

        [Required]
        [MaxLength(255)]
        public string AssetName { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string AssetNo { get; set; } = string.Empty;

        [MaxLength(255)]
        public string? AssetName2 { get; set; }

        [Column(TypeName = "decimal(15,2)")]
        public decimal? BookBalance { get; set; }

        [Required]
        public DateTime LoadedOn { get; set; }

        [Required]
        public int SaleID { get; set; }            // FK to Sales

        // Navigation properties
        public virtual Sale Sale { get; set; } = null!;
        public virtual ICollection<Image> Images { get; set; } = new List<Image>();
    }

    // === PROCESSING SESSION TRACKING ===

    /// <summary>
    /// Processing sessions for tracking async operations
    /// </summary>
    [Table("ProcessingSessions")]
    public class ProcessingSession
    {
        [Key]
        public int ID { get; set; }

        [Required]
        [MaxLength(50)]
        public string SessionId { get; set; } = string.Empty;

        [Required]
        public int ImageID { get; set; }

        [Required]
        [MaxLength(50)]
        public string ProcessingType { get; set; } = string.Empty;  // "DocumentSplitting", "SimpleIndexing"

        [Required]
        [MaxLength(20)]
        public string Status { get; set; } = "Queued";  // "Queued", "Processing", "Completed", "Failed"

        public string? ErrorMessage { get; set; }

        [Required]
        public int CreatedBy { get; set; }

        [Required]
        public DateTime DateCreated { get; set; } = DateTime.UtcNow;

        public DateTime? CompletedDate { get; set; }

        // Navigation properties
        public virtual Image Image { get; set; } = null!;
    }

    // === PAGE THUMBNAILS ===

    /// <summary>
    /// Page thumbnails for UI display
    /// </summary>
    [Table("PageThumbnails")]
    public class PageThumbnail
    {
        [Key]
        public int ID { get; set; }

        [Required]
        public int ImageID { get; set; }

        [Required]
        public int PageNumber { get; set; }  // 1-based page number

        [Required]
        [MaxLength(500)]
        public string ThumbnailUrl { get; set; } = string.Empty;

        [Required]
        public int Width { get; set; }

        [Required]
        public int Height { get; set; }

        [Required]
        public DateTime DateCreated { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public virtual Image Image { get; set; } = null!;
    }
}
