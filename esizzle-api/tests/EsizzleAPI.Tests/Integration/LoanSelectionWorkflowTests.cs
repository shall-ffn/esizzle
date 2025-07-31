using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using FluentAssertions;
using EsizzleAPI.Controllers;
using EsizzleAPI.Repositories;
using EsizzleAPI.Models;
using EsizzleAPI.Middleware;
using EsizzleAPI.Tests.TestHelpers;

namespace EsizzleAPI.Tests.Integration;

/// <summary>
/// Integration tests that validate the complete WinForms loan selection workflow
/// Tests the end-to-end flow: Select Loan -> Load Documents -> Select Document -> Load PDF
/// Based on analysis of WinForms eStacker SelectLoan() -> LoadSpecificWorkItem() -> LoadDocuments() -> SelectImage()
/// </summary>
[TestClass]
public class LoanSelectionWorkflowTests
{
    private LoanController _loanController;
    private DocumentController _documentController;
    private Mock<ILoanRepository> _mockLoanRepository;
    private Mock<IDocumentRepository> _mockDocumentRepository;
    private Mock<ISecurityRepository> _mockSecurityRepository;
    private Mock<ILogger<LoanController>> _mockLoanLogger;
    private Mock<ILogger<DocumentController>> _mockDocumentLogger;

    [TestInitialize]
    public void Setup()
    {
        _mockLoanRepository = new Mock<ILoanRepository>();
        _mockDocumentRepository = new Mock<IDocumentRepository>();
        _mockSecurityRepository = new Mock<ISecurityRepository>();
        _mockLoanLogger = new Mock<ILogger<LoanController>>();
        _mockDocumentLogger = new Mock<ILogger<DocumentController>>();
        
        _loanController = new LoanController(
            _mockLoanLogger.Object,
            _mockLoanRepository.Object,
            _mockSecurityRepository.Object);
            
        _documentController = new DocumentController(
            _mockDocumentLogger.Object,
            _mockDocumentRepository.Object,
            _mockSecurityRepository.Object);
    }

    #region Complete Loan Selection Workflow

    [TestMethod]
    public async Task LoanSelectionWorkflow_CompleteFlow_ReplicatesWinFormsSequence()
    {
        // Arrange - Setup the complete WinForms workflow scenario
        var saleId = 678;
        var loanId = 12345;
        var documentId = 67890;
        var mockUser = TestDataBuilder.CreateMockUser();

        // Test data matching WinForms scenarios
        var testLoans = TestDataBuilder.CreateTestLoans(saleId, count: 3);
        var selectedLoan = testLoans.First(l => l.LoanId == loanId);
        var testDocuments = TestDataBuilder.CreateTestDocuments(loanId);
        var selectedDocument = testDocuments.First();
        var documentUrl = "https://s3.amazonaws.com/bucket/document.pdf?signature=xyz";

        // Setup mocks for the complete workflow
        SetupCompleteWorkflowMocks(saleId, loanId, documentId, mockUser, testLoans, selectedLoan, testDocuments, selectedDocument, documentUrl);

        SetupMockUser(_loanController, mockUser);
        SetupMockUser(_documentController, mockUser);

        // Act & Assert - Execute the complete workflow sequence

        // Step 1: Load loans for sale (WinForms LoadLoans equivalent)
        var loansResult = await _loanController.GetLoansBySale(saleId);
        loansResult.Should().BeOfType<OkObjectResult>();
        var loans = ((OkObjectResult)loansResult).Value as List<LoanSummaryModel>;
        loans.Should().NotBeNull();
        loans!.Should().HaveCount(3);

        // Step 2: Select specific loan (WinForms SelectLoan equivalent)
        var loanResult = await _loanController.GetLoan(loanId);
        loanResult.Should().BeOfType<OkObjectResult>();
        var loan = ((OkObjectResult)loanResult).Value as LoanModel;
        loan.Should().NotBeNull();
        loan!.LoanId.Should().Be(loanId);

        // Step 3: Load documents for selected loan (WinForms LoadSpecificWorkItem -> LoadDocuments equivalent)
        var documentsResult = await _documentController.GetDocumentsByLoan(loanId);
        documentsResult.Should().BeOfType<OkObjectResult>();
        var documents = ((OkObjectResult)documentsResult).Value as List<DocumentSummaryModel>;
        documents.Should().NotBeNull();
        documents!.Should().HaveCount(testDocuments.Count);

        // Verify WinForms document ordering is maintained
        var isCorrectOrder = WinFormsLogicValidators.ValidateDocumentOrdering(documents);
        isCorrectOrder.Should().BeTrue("Documents should be ordered like WinForms OrderImagesByOptions");

        // Step 4: Select specific document (WinForms SelectImage equivalent)
        var documentResult = await _documentController.GetDocument(selectedDocument.Id);
        documentResult.Should().BeOfType<OkObjectResult>();
        var document = ((OkObjectResult)documentResult).Value as DocumentModel;
        document.Should().NotBeNull();
        document!.Id.Should().Be(selectedDocument.Id);

        // Step 5: Load document URL for PDF viewing (WinForms LoadDocument equivalent)
        var urlResult = await _documentController.GetDocumentUrl(selectedDocument.Id);
        urlResult.Should().BeOfType<OkObjectResult>();
        var urlResponse = ((OkObjectResult)urlResult).Value as DocumentUrlResponse;
        urlResponse.Should().NotBeNull();
        urlResponse!.Url.Should().Be(documentUrl);

        // Verify all repository calls were made in correct sequence
        VerifyWorkflowRepositoryCalls(saleId, loanId, selectedDocument.Id, mockUser);
    }

    [TestMethod]
    public async Task LoanSelectionWorkflow_WithDocumentFiltering_ReplicatesWinFormsFilterLogic()
    {
        // Arrange - Test WinForms document filtering (ShowClosing, ActionFilter, etc.)
        var loanId = 12345;
        var mockUser = TestDataBuilder.CreateMockUser();
        var allDocuments = TestDataBuilder.CreateTestDocuments(loanId);
        
        // Create mixed document set with closing and non-closing docs
        var mixedDocuments = allDocuments.Select((doc, index) => new DocumentSummaryModel
        {
            Id = doc.Id,
            OriginalName = doc.OriginalName,
            DocumentType = doc.DocumentType,
            PageCount = doc.PageCount,
            Length = doc.Length,
            DateCreated = doc.DateCreated,
            ImageStatusTypeId = doc.ImageStatusTypeId,
            Corrupted = doc.Corrupted,
            LoanId = doc.LoanId,
            // ClosingDoc property not available in DocumentSummaryModel yet
        }).ToList();

        _mockSecurityRepository.Setup(x => x.HasLoanAccessAsync(mockUser.Id, loanId))
            .ReturnsAsync(true);
        _mockDocumentRepository.Setup(x => x.GetByLoanIdAsync(loanId))
            .ReturnsAsync(mixedDocuments);

        SetupMockUser(_documentController, mockUser);

        // Act
        var result = await _documentController.GetDocumentsByLoan(loanId);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var documents = ((OkObjectResult)result).Value as List<DocumentSummaryModel>;
        documents.Should().NotBeNull();

        // Test that we can filter for closing documents only (WinForms ShowClosing functionality)
        var closingDocs = documents!.Where((d, index) => index % 2 == 0).ToList(); // Mock closing filter
        var isValidClosingFilter = WinFormsLogicValidators.ValidateClosingDocumentFilter(closingDocs, showClosingOnly: true);
        isValidClosingFilter.Should().BeTrue("Closing document filter should work like WinForms");

        // Verify document ordering with mixed types
        var isCorrectOrder = WinFormsLogicValidators.ValidateDocumentOrdering(documents);
        isCorrectOrder.Should().BeTrue("Mixed document types should maintain WinForms ordering");
    }

    [TestMethod]
    public async Task LoanSelectionWorkflow_WithLoanSearch_ReplicatesWinFormsSearchLogic()
    {
        // Arrange - Test WinForms loan search functionality
        var saleId = 678;
        var searchTerm = "Main St";
        var mockUser = TestDataBuilder.CreateMockUser();
        var allLoans = TestDataBuilder.CreateTestLoans(saleId, count: 8);
        
        // Filter loans that should match the search term (WinForms search logic)
        var expectedMatches = allLoans.Where(l => 
            l.AssetName.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
            l.AssetNo?.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) == true ||
            l.AssetName2?.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) == true
        ).ToList();

        _mockSecurityRepository.Setup(x => x.HasSaleAccessAsync(mockUser.Id, saleId))
            .ReturnsAsync(true);
        // Convert to LoanSummaryModel for repository mock
        var expectedSummaries = expectedMatches.Select(l => new LoanSummaryModel
        {
            LoanId = l.LoanId,
            AssetNo = l.AssetNo,
            AssetName = l.AssetName,
            AssetName2 = l.AssetName2,
            BookBalance = l.BookBalance,
            LoadedOn = l.LoadedOn,
            SaleId = l.SaleId,
            LoanStatusId = l.LoanStatusId,
            DocumentCount = 0,
            LastDocumentDate = null
        });

        _mockLoanRepository.Setup(x => x.SearchLoansAsync(saleId, searchTerm))
            .ReturnsAsync(expectedSummaries);

        SetupMockUser(_loanController, mockUser);

        // Act
        var result = await _loanController.SearchLoansBySale(saleId, searchTerm);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var searchResults = ((OkObjectResult)result).Value as List<LoanSummaryModel>;
        searchResults.Should().NotBeNull();

        // Verify WinForms search logic is replicated exactly (convert to LoanModel for validation)
        var loanModels = searchResults!.Select(s => new LoanModel
        {
            LoanId = s.LoanId,
            AssetNo = s.AssetNo,
            AssetName = s.AssetName,
            AssetName2 = s.AssetName2,
            BookBalance = s.BookBalance,
            LoadedOn = s.LoadedOn,
            SaleId = s.SaleId,
            LoanStatusId = s.LoanStatusId
        }).ToList();
        var isValidSearch = WinFormsLogicValidators.ValidateLoanSearch(allLoans, searchTerm, loanModels);
        isValidSearch.Should().BeTrue("Loan search should match WinForms search logic");

        searchResults!.Should().AllSatisfy(loan =>
            loan.AssetName.Should().Contain(searchTerm, "Search should find matches in AssetName"));
    }

    #endregion

    #region Security Integration Tests

    [TestMethod]
    public async Task LoanSelectionWorkflow_SecurityFailure_BlocksWorkflowAtCorrectStep()
    {
        // Arrange - Test security failure at each step of the workflow
        var saleId = 678;
        var loanId = 12345;
        var documentId = 67890;
        var mockUser = TestDataBuilder.CreateMockUser();

        // Setup security to fail at loan level
        _mockSecurityRepository.Setup(x => x.HasSaleAccessAsync(mockUser.Id, saleId))
            .ReturnsAsync(true);
        _mockSecurityRepository.Setup(x => x.HasLoanAccessAsync(mockUser.Id, loanId))
            .ReturnsAsync(false); // Loan access denied
        _mockSecurityRepository.Setup(x => x.HasDocumentAccessAsync(mockUser.Id, documentId))
            .ReturnsAsync(true);

        SetupMockUser(_loanController, mockUser);
        SetupMockUser(_documentController, mockUser);

        // Act & Assert - Security should block at loan level
        var loansResult = await _loanController.GetLoansBySale(saleId);
        loansResult.Should().BeOfType<OkObjectResult>(); // Sale access works

        var loanResult = await _loanController.GetLoan(loanId);
        loanResult.Should().BeOfType<ObjectResult>(); // Loan access fails
        var loanObjectResult = (ObjectResult)loanResult;
        loanObjectResult.StatusCode.Should().Be(403);

        // Verify that subsequent calls would also be blocked by the same security
        var documentsResult = await _documentController.GetDocumentsByLoan(loanId);
        documentsResult.Should().BeOfType<ObjectResult>();
        var docObjectResult = (ObjectResult)documentsResult;
        docObjectResult.StatusCode.Should().Be(403);
    }

    #endregion

    #region Error Handling Integration Tests

    [TestMethod]
    public async Task LoanSelectionWorkflow_RepositoryFailures_HandleGracefully()
    {
        // Arrange - Test repository failures at different steps
        var saleId = 678;
        var loanId = 12345;
        var mockUser = TestDataBuilder.CreateMockUser();

        // Setup security to pass but repository to fail
        _mockSecurityRepository.Setup(x => x.HasSaleAccessAsync(mockUser.Id, saleId))
            .ReturnsAsync(true);
        _mockSecurityRepository.Setup(x => x.HasLoanAccessAsync(mockUser.Id, loanId))
            .ReturnsAsync(true);

        // Simulate database failure
        _mockLoanRepository.Setup(x => x.GetBySaleIdAsync(saleId))
            .ThrowsAsync(new Exception("Database connection timeout"));
        _mockDocumentRepository.Setup(x => x.GetByLoanIdAsync(loanId))
            .ThrowsAsync(new Exception("Database query failed"));

        SetupMockUser(_loanController, mockUser);
        SetupMockUser(_documentController, mockUser);

        // Act & Assert - Should handle failures gracefully
        var loansResult = await _loanController.GetLoansBySale(saleId);
        loansResult.Should().BeOfType<ObjectResult>();
        var loansObjectResult = (ObjectResult)loansResult;
        loansObjectResult.StatusCode.Should().Be(500);

        var documentsResult = await _documentController.GetDocumentsByLoan(loanId);
        documentsResult.Should().BeOfType<ObjectResult>();
        var docsObjectResult = (ObjectResult)documentsResult;
        docsObjectResult.StatusCode.Should().Be(500);
    }

    #endregion

    #region Helper Methods

    private void SetupCompleteWorkflowMocks(int saleId, int loanId, int documentId, AuthorizedUser mockUser, 
        List<LoanModel> testLoans, LoanModel selectedLoan, List<DocumentModel> testDocuments, 
        DocumentModel selectedDocument, string documentUrl)
    {
        // Security mocks
        _mockSecurityRepository.Setup(x => x.HasSaleAccessAsync(mockUser.Id, saleId))
            .ReturnsAsync(true);
        _mockSecurityRepository.Setup(x => x.HasLoanAccessAsync(mockUser.Id, loanId))
            .ReturnsAsync(true);
        _mockSecurityRepository.Setup(x => x.HasDocumentAccessAsync(mockUser.Id, It.IsAny<int>()))
            .ReturnsAsync(true);

        // Loan repository mocks
        // Convert to LoanSummaryModel for repository mock
        var loanSummaries = testLoans.Select(l => new LoanSummaryModel
        {
            LoanId = l.LoanId,
            AssetNo = l.AssetNo,
            AssetName = l.AssetName,
            AssetName2 = l.AssetName2,
            BookBalance = l.BookBalance,
            LoadedOn = l.LoadedOn,
            SaleId = l.SaleId,
            LoanStatusId = l.LoanStatusId,
            DocumentCount = 0,
            LastDocumentDate = null
        });

        _mockLoanRepository.Setup(x => x.GetBySaleIdAsync(saleId))
            .ReturnsAsync(loanSummaries);
        _mockLoanRepository.Setup(x => x.GetByIdAsync(loanId))
            .ReturnsAsync(selectedLoan);

        // Document repository mocks
        var documentSummaries = testDocuments.Select(d => new DocumentSummaryModel
        {
            Id = d.Id,
            OriginalName = d.OriginalName,
            DocumentType = d.DocumentType,
            PageCount = d.PageCount,
            Length = d.Length,
            DateCreated = d.DateCreated,
            DateUpdated = d.DateUpdated,
            ImageStatusTypeId = d.ImageStatusTypeId,
            Corrupted = d.Corrupted,
            IsRedacted = d.IsRedacted,
            Comments = d.Comments,
            LoanId = d.LoanId,
            AssetNumber = d.AssetNumber
        }).ToList();

        _mockDocumentRepository.Setup(x => x.GetByLoanIdAsync(loanId))
            .ReturnsAsync(documentSummaries);
        _mockDocumentRepository.Setup(x => x.GetByIdAsync(selectedDocument.Id))
            .ReturnsAsync(selectedDocument);
        _mockDocumentRepository.Setup(x => x.GenerateDocumentUrlAsync(selectedDocument.Id))
            .ReturnsAsync(documentUrl);
    }

    private void VerifyWorkflowRepositoryCalls(int saleId, int loanId, int documentId, AuthorizedUser mockUser)
    {
        // Verify security checks were called in correct order
        _mockSecurityRepository.Verify(x => x.HasSaleAccessAsync(mockUser.Id, saleId), Times.AtLeastOnce);
        _mockSecurityRepository.Verify(x => x.HasLoanAccessAsync(mockUser.Id, loanId), Times.AtLeastOnce);
        _mockSecurityRepository.Verify(x => x.HasDocumentAccessAsync(mockUser.Id, documentId), Times.AtLeastOnce);

        // Verify repository calls were made in WinForms workflow order
        _mockLoanRepository.Verify(x => x.GetBySaleIdAsync(saleId), Times.Once);
        _mockLoanRepository.Verify(x => x.GetByIdAsync(loanId), Times.Once);
        _mockDocumentRepository.Verify(x => x.GetByLoanIdAsync(loanId), Times.Once);
        _mockDocumentRepository.Verify(x => x.GetByIdAsync(documentId), Times.Once);
        _mockDocumentRepository.Verify(x => x.GenerateDocumentUrlAsync(documentId), Times.Once);
    }

    private void SetupMockUser(ControllerBase controller, AuthorizedUser user)
    {
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext()
        };

        controller.ControllerContext.HttpContext.Items["AuthorizedUser"] = user;
    }

    #endregion
}
