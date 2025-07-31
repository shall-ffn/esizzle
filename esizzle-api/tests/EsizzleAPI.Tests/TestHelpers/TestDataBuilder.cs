using EsizzleAPI.Models;
using EsizzleAPI.Middleware;

namespace EsizzleAPI.Tests.TestHelpers;

/// <summary>
/// Builder class for creating test data that matches WinForms scenarios
/// </summary>
public static class TestDataBuilder
{
    /// <summary>
    /// Create a test loan matching WinForms loan structure
    /// </summary>
    public static LoanModel CreateTestLoan(int loanId = 12345, int saleId = 678)
    {
        return new LoanModel
        {
            LoanId = loanId,
            AssetName = "123 Main St",
            AssetNo = $"TEST{loanId:000}",
            AssetName2 = "Property Description",
            BookBalance = 250000.00m,
            LoadedOn = DateTime.Now.AddDays(-10),
            SaleId = saleId
        };
    }

    /// <summary>
    /// Create multiple test loans for a sale
    /// </summary>
    public static List<LoanModel> CreateTestLoans(int saleId, int count = 5)
    {
        var loans = new List<LoanModel>();
        for (int i = 1; i <= count; i++)
        {
            // Ensure consistent IDs for integration tests
            var loanId = i == 1 ? 12345 : (saleId * 100 + i); // First loan always has expected test ID
            
            loans.Add(new LoanModel
            {
                LoanId = loanId,
                AssetName = $"{100 + i * 10} Main St {(char)('A' + i - 1)}",
                AssetNo = $"{saleId}{i:000}",
                AssetName2 = $"Property {i}",
                BookBalance = 100000.00m + (i * 50000),
                LoadedOn = DateTime.Now.AddDays(-i),
                SaleId = saleId,
                LoanStatusId = 3 // Published status
            });
        }
        return loans;
    }

    /// <summary>
    /// Create test documents matching WinForms document scenarios
    /// </summary>
    public static List<DocumentModel> CreateTestDocuments(int loanId, bool includeVariousTypes = true)
    {
        var documents = new List<DocumentModel>();

        if (includeVariousTypes)
        {
            // Create documents that test WinForms ordering logic
            documents.AddRange(new[]
            {
                new DocumentModel
                {
                    Id = 1,
                    LoanId = loanId,
                    OriginalName = "Appraisal_Report.pdf",
                    DocumentType = "Appraisal",
                    PageCount = 25,
                    Length = 2048576,
                    DateCreated = new DateTime(2024, 1, 15),
                    DocumentDate = new DateTime(2024, 1, 15),
                    ClosingDoc = true,
                    ImageStatusTypeId = 2,
                    Corrupted = false
                },
                new DocumentModel
                {
                    Id = 2,
                    LoanId = loanId,
                    OriginalName = "Credit_Analysis.pdf",
                    DocumentType = "Credit Report",
                    PageCount = 10,
                    Length = 1024768,
                    DateCreated = new DateTime(2024, 1, 10),
                    DocumentDate = new DateTime(2024, 1, 10),
                    ClosingDoc = false,
                    ImageStatusTypeId = 2,
                    Corrupted = false
                },
                new DocumentModel
                {
                    Id = 3,
                    LoanId = loanId,
                    OriginalName = "Appraisal_Summary.pdf",
                    DocumentType = "Appraisal",
                    PageCount = 8,
                    Length = 512384,
                    DateCreated = new DateTime(2024, 1, 20),
                    DocumentDate = new DateTime(2024, 1, 20),
                    ClosingDoc = true,
                    ImageStatusTypeId = 2,
                    Corrupted = false
                },
                new DocumentModel
                {
                    Id = 4,
                    LoanId = loanId,
                    OriginalName = "Income_Docs.pdf",
                    DocumentType = "Income Documentation",
                    PageCount = 12,
                    Length = 1536000,
                    DateCreated = new DateTime(2024, 1, 12),
                    DocumentDate = new DateTime(2024, 1, 12),
                    ClosingDoc = true,
                    ImageStatusTypeId = 2,
                    Corrupted = false
                },
                new DocumentModel
                {
                    Id = 5,
                    LoanId = loanId,
                    OriginalName = "Unclassified_Doc.pdf",
                    DocumentType = null, // Test unclassified document
                    PageCount = 3,
                    Length = 256000,
                    DateCreated = new DateTime(2024, 1, 8),
                    DocumentDate = null,
                    ClosingDoc = false,
                    ImageStatusTypeId = 1, // Needs work status
                    Corrupted = false
                }
            });
        }
        else
        {
            // Single basic document for simple tests
            documents.Add(new DocumentModel
            {
                Id = 1,
                LoanId = loanId,
                OriginalName = "Test_Document.pdf",
                DocumentType = "Test Document",
                PageCount = 5,
                Length = 512000,
                DateCreated = DateTime.Now,
                ClosingDoc = false,
                ImageStatusTypeId = 2,
                Corrupted = false
            });
        }

        return documents;
    }

    /// <summary>
    /// Create mock AuthorizedUser for testing
    /// </summary>
    public static AuthorizedUser CreateMockUser(int userId = 1, string email = "test@example.com", int accessLevel = 2)
    {
        return new AuthorizedUser
        {
            Id = userId,
            Email = email,
            AccessLevel = accessLevel,
            Name = "Test User"
        };
    }

    /// <summary>
    /// Create test sale data
    /// </summary>
    public static SaleModel CreateTestSale(int saleId = 678, int offeringId = 123)
    {
        return new SaleModel
        {
            SaleId = saleId,
            SaleDesc = $"Test Sale {saleId}",
            ClientId = 1
        };
    }

    /// <summary>
    /// Create test offering data
    /// </summary>
    public static OfferingModel CreateTestOffering(int offeringId = 123)
    {
        return new OfferingModel
        {
            OfferingId = offeringId,
            OfferingName = $"Test Offering {offeringId}",
            OfferingDescription = "Test offering for unit tests",
            ClientId = 1
        };
    }

    /// <summary>
    /// Create document actions matching WinForms ImageAction structure
    /// </summary>
    public static List<DocumentAction> CreateTestDocumentActions(int documentId)
    {
        return new List<DocumentAction>
        {
            new DocumentAction
            {
                Id = 1,
                DocumentId = documentId,
                ActionTypeId = 1, // Redact
                ActionName = "Redact Personal Information",
                ActionNote = "Remove SSN and account numbers",
                DateCompleted = null,
                CompletedBy = null,
                CompletedByFullName = null
            },
            new DocumentAction
            {
                Id = 2,
                DocumentId = documentId,
                ActionTypeId = 2, // Index
                ActionName = "Classify Document",
                ActionNote = "Set document type",
                DateCompleted = DateTime.Now.AddDays(-1),
                CompletedBy = 1,
                CompletedByFullName = "Test User"
            },
            new DocumentAction
            {
                Id = 3,
                DocumentId = documentId,
                ActionTypeId = 3, // Validate
                ActionName = "Validate Document Data",
                ActionNote = "Verify extracted information",
                DateCompleted = null,
                CompletedBy = null,
                CompletedByFullName = null
            }
        };
    }
}

/// <summary>
/// Additional model classes needed for testing that match WinForms structures
/// </summary>
public class DocumentAction
{
    public int Id { get; set; }
    public int DocumentId { get; set; }
    public int ActionTypeId { get; set; }
    public string ActionName { get; set; } = string.Empty;
    public string? ActionNote { get; set; }
    public DateTime? DateCompleted { get; set; }
    public int? CompletedBy { get; set; }
    public string? CompletedByFullName { get; set; }
}

/// <summary>
/// Document filter structure matching WinForms LoadDocsFilter
/// </summary>
public class LoadDocsFilter
{
    public int LoanID { get; set; }
    public int OfferingID { get; set; }
    public bool ShowClosing { get; set; }
    public int[]? IncludeOnlyActionIDs { get; set; }
    public string? TextFilter { get; set; }
}

/// <summary>
/// Bookmark data structure matching WinForms BKData
/// </summary>
public class BKData
{
    public int? ImageDocTypeID { get; set; }
    public DateTime? DocDate { get; set; }
    public string? Comments { get; set; }
    public string? ImageDocTypeName { get; set; }
}
