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

namespace EsizzleAPI.Tests.Controllers;

/// <summary>
/// Tests for DocumentController that validate WinForms document management workflow
/// Based on analysis of WinForms eStacker LoadDocuments() and SelectImage() functionality
/// </summary>
[TestClass]
public class DocumentControllerTests
{
    private DocumentController _controller;
    private Mock<IDocumentRepository> _mockDocumentRepository;
    private Mock<ISecurityRepository> _mockSecurityRepository;
    private Mock<ILogger<DocumentController>> _mockLogger;

    [TestInitialize]
    public void Setup()
    {
        _mockDocumentRepository = new Mock<IDocumentRepository>();
        _mockSecurityRepository = new Mock<ISecurityRepository>();
        _mockLogger = new Mock<ILogger<DocumentController>>();
        
        _controller = new DocumentController(
            _mockLogger.Object,
            _mockDocumentRepository.Object,
            _mockSecurityRepository.Object);
    }

    #region GetDocumentsByLoan Tests (WinForms LoadDocuments equivalent)

    [TestMethod]
    public async Task GetDocumentsByLoan_ValidLoanWithAccess_ReturnsOrderedDocuments()
    {
        // Arrange - Test WinForms LoadDocuments() with OrderImagesByOptions logic
        var loanId = 12345;
        var mockUser = TestDataBuilder.CreateMockUser();
        var testDocuments = TestDataBuilder.CreateTestDocuments(loanId);
        
        // Convert to DocumentSummaryModel for ordering validation
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

        _mockSecurityRepository.Setup(x => x.HasLoanAccessAsync(mockUser.Id, loanId))
            .ReturnsAsync(true);
        _mockDocumentRepository.Setup(x => x.GetByLoanIdAsync(loanId))
            .ReturnsAsync(documentSummaries);

        SetupMockUser(mockUser);

        // Act
        var result = await _controller.GetDocumentsByLoan(loanId);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var documents = ((OkObjectResult)result).Value as List<DocumentSummaryModel>;
        documents.Should().NotBeNull();
        documents!.Should().HaveCount(testDocuments.Count);

        // Verify WinForms OrderImagesByOptions logic is replicated
        var isCorrectOrder = WinFormsLogicValidators.ValidateDocumentOrdering(documents);
        isCorrectOrder.Should().BeTrue("Documents should be ordered by DocType then OriginalName");

        // Verify repository calls
        _mockSecurityRepository.Verify(x => x.HasLoanAccessAsync(mockUser.Id, loanId), Times.Once);
        _mockDocumentRepository.Verify(x => x.GetByLoanIdAsync(loanId), Times.Once);
    }

    [TestMethod]
    public async Task GetDocumentsByLoan_NoLoanAccess_Returns403Forbidden()
    {
        // Arrange - Test WinForms security check failure
        var loanId = 12345;
        var mockUser = TestDataBuilder.CreateMockUser();

        _mockSecurityRepository.Setup(x => x.HasLoanAccessAsync(mockUser.Id, loanId))
            .ReturnsAsync(false);

        SetupMockUser(mockUser);

        // Act
        var result = await _controller.GetDocumentsByLoan(loanId);

        // Assert
        result.Should().BeOfType<ObjectResult>();
        var objectResult = (ObjectResult)result;
        objectResult.StatusCode.Should().Be(403);

        // Verify document repository was never called
        _mockDocumentRepository.Verify(x => x.GetByLoanIdAsync(It.IsAny<int>()), Times.Never);
    }

    #endregion

    #region GetDocument Tests (WinForms SelectImage equivalent)

    [TestMethod]
    public async Task GetDocument_ValidDocumentWithAccess_ReturnsDocumentDetails()
    {
        // Arrange - Test WinForms SelectImage(imageID) success
        var documentId = 67890;
        var mockUser = TestDataBuilder.CreateMockUser();
        var expectedDocument = TestDataBuilder.CreateTestDocuments(12345, includeVariousTypes: false).First();

        _mockSecurityRepository.Setup(x => x.HasDocumentAccessAsync(mockUser.Id, documentId))
            .ReturnsAsync(true);
        _mockDocumentRepository.Setup(x => x.GetByIdAsync(documentId))
            .ReturnsAsync(expectedDocument);

        SetupMockUser(mockUser);

        // Act
        var result = await _controller.GetDocument(documentId);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var document = ((OkObjectResult)result).Value as DocumentModel;
        document.Should().NotBeNull();
        document!.Id.Should().Be(expectedDocument.Id);
        document.OriginalName.Should().Be(expectedDocument.OriginalName);

        // Verify repository calls match WinForms workflow
        _mockSecurityRepository.Verify(x => x.HasDocumentAccessAsync(mockUser.Id, documentId), Times.Once);
        _mockDocumentRepository.Verify(x => x.GetByIdAsync(documentId), Times.Once);
    }

    [TestMethod]
    public async Task GetDocument_NoDocumentAccess_Returns403Forbidden()
    {
        // Arrange - Test WinForms document access control
        var documentId = 67890;
        var mockUser = TestDataBuilder.CreateMockUser();

        _mockSecurityRepository.Setup(x => x.HasDocumentAccessAsync(mockUser.Id, documentId))
            .ReturnsAsync(false);

        SetupMockUser(mockUser);

        // Act
        var result = await _controller.GetDocument(documentId);

        // Assert
        result.Should().BeOfType<ObjectResult>();
        var objectResult = (ObjectResult)result;
        objectResult.StatusCode.Should().Be(403);
    }

    [TestMethod]
    public async Task GetDocument_NonExistentDocument_Returns404NotFound()
    {
        // Arrange
        var documentId = 99999;
        var mockUser = TestDataBuilder.CreateMockUser();

        _mockSecurityRepository.Setup(x => x.HasDocumentAccessAsync(mockUser.Id, documentId))
            .ReturnsAsync(true);
        _mockDocumentRepository.Setup(x => x.GetByIdAsync(documentId))
            .ReturnsAsync((DocumentModel?)null);

        SetupMockUser(mockUser);

        // Act
        var result = await _controller.GetDocument(documentId);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
        var notFoundResult = (NotFoundObjectResult)result;
        notFoundResult.Value.Should().Be($"Document {documentId} not found");
    }

    #endregion

    #region GetDocumentUrl Tests (WinForms PDF loading functionality)

    [TestMethod]
    public async Task GetDocumentUrl_ValidDocument_ReturnsPresignedUrl()
    {
        // Arrange - Test WinForms PDF loading (equivalent to LoadDocument method)
        var documentId = 67890;
        var mockUser = TestDataBuilder.CreateMockUser();
        var expectedUrl = "https://s3.amazonaws.com/bucket/document.pdf?signature=xyz";

        _mockSecurityRepository.Setup(x => x.HasDocumentAccessAsync(mockUser.Id, documentId))
            .ReturnsAsync(true);
        _mockDocumentRepository.Setup(x => x.GenerateDocumentUrlAsync(documentId))
            .ReturnsAsync(expectedUrl);

        SetupMockUser(mockUser);

        // Act
        var result = await _controller.GetDocumentUrl(documentId);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var response = ((OkObjectResult)result).Value as DocumentUrlResponse;
        response.Should().NotBeNull();
        response!.Url.Should().Be(expectedUrl);
        response.ContentType.Should().Be("application/pdf");
        response.ExpiresAt.Should().BeAfter(DateTime.UtcNow);
    }

    [TestMethod]
    public async Task GetDocumentUrl_CannotGenerateUrl_Returns404NotFound()
    {
        // Arrange
        var documentId = 67890;
        var mockUser = TestDataBuilder.CreateMockUser();

        _mockSecurityRepository.Setup(x => x.HasDocumentAccessAsync(mockUser.Id, documentId))
            .ReturnsAsync(true);
        _mockDocumentRepository.Setup(x => x.GenerateDocumentUrlAsync(documentId))
            .ReturnsAsync((string?)null);

        SetupMockUser(mockUser);

        // Act
        var result = await _controller.GetDocumentUrl(documentId);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    #endregion

    #region UpdateDocumentType Tests (WinForms indexing functionality)

    [TestMethod]
    public async Task UpdateDocumentType_ValidDocumentAndType_ReturnsSuccess()
    {
        // Arrange - Test WinForms document indexing/classification
        var documentId = 67890;
        var newDocumentType = "Mortgage Application";
        var mockUser = TestDataBuilder.CreateMockUser();

        _mockSecurityRepository.Setup(x => x.HasDocumentAccessAsync(mockUser.Id, documentId))
            .ReturnsAsync(true);
        _mockDocumentRepository.Setup(x => x.UpdateDocumentTypeAsync(documentId, newDocumentType, mockUser.Id))
            .ReturnsAsync(true);

        SetupMockUser(mockUser);

        // Act
        var result = await _controller.UpdateDocumentType(documentId, newDocumentType);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var response = ((OkObjectResult)result).Value;
        response.Should().NotBeNull();

        // Verify the update was called with correct parameters
        _mockDocumentRepository.Verify(
            x => x.UpdateDocumentTypeAsync(documentId, newDocumentType, mockUser.Id), 
            Times.Once);
    }

    [TestMethod]
    public async Task UpdateDocumentType_EmptyDocumentType_ReturnsBadRequest()
    {
        // Arrange - Test validation logic
        var documentId = 67890;
        var emptyDocumentType = "";
        var mockUser = TestDataBuilder.CreateMockUser();

        SetupMockUser(mockUser);

        // Act
        var result = await _controller.UpdateDocumentType(documentId, emptyDocumentType);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
        var badRequestResult = (BadRequestObjectResult)result;
        badRequestResult.Value.Should().Be("Document type cannot be empty");

        // Verify repository was never called
        _mockDocumentRepository.Verify(
            x => x.UpdateDocumentTypeAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<int>()), 
            Times.Never);
    }

    #endregion

    #region RotateDocument Tests (WinForms rotation functionality)

    [TestMethod]
    public async Task RotateDocument_ValidRotation_ReturnsSuccess()
    {
        // Arrange - Test WinForms page rotation functionality
        var documentId = 67890;
        var rotationAngle = 90;
        var mockUser = TestDataBuilder.CreateMockUser();
        var rotateRequest = new RotateDocumentRequest { Angle = rotationAngle };

        _mockSecurityRepository.Setup(x => x.HasDocumentAccessAsync(mockUser.Id, documentId))
            .ReturnsAsync(true);

        SetupMockUser(mockUser);

        // Act
        var result = await _controller.RotateDocument(documentId, rotateRequest);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var response = ((OkObjectResult)result).Value;
        response.Should().NotBeNull();
    }

    [TestMethod]
    public async Task RotateDocument_InvalidAngle_ReturnsBadRequest()
    {
        // Arrange - Test WinForms rotation validation (only 90, 180, 270 allowed)
        var documentId = 67890;
        var invalidAngle = 45; // Not a valid rotation angle
        var mockUser = TestDataBuilder.CreateMockUser();
        var rotateRequest = new RotateDocumentRequest { Angle = invalidAngle };

        SetupMockUser(mockUser);

        // Act
        var result = await _controller.RotateDocument(documentId, rotateRequest);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
        var badRequestResult = (BadRequestObjectResult)result;
        badRequestResult.Value.Should().Be("Rotation angle must be 90, 180, or 270 degrees");
    }

    #endregion

    #region RedactDocument Tests (WinForms redaction functionality)

    [TestMethod]
    public async Task RedactDocument_ValidRedactionAreas_ReturnsSuccess()
    {
        // Arrange - Test WinForms redaction functionality
        var documentId = 67890;
        var mockUser = TestDataBuilder.CreateMockUser();
        var redactionAreas = new List<RedactionArea>
        {
            new RedactionArea { PageNumber = 1, X = 100, Y = 200, Width = 150, Height = 50 },
            new RedactionArea { PageNumber = 2, X = 50, Y = 300, Width = 200, Height = 75 }
        };
        var redactRequest = new RedactDocumentRequest { Areas = redactionAreas, PermanentRedaction = true };

        _mockSecurityRepository.Setup(x => x.HasDocumentAccessAsync(mockUser.Id, documentId))
            .ReturnsAsync(true);
        _mockDocumentRepository.Setup(x => x.MarkAsRedactedAsync(documentId, mockUser.Id))
            .ReturnsAsync(true);

        SetupMockUser(mockUser);

        // Act
        var result = await _controller.RedactDocument(documentId, redactRequest);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var response = ((OkObjectResult)result).Value;
        response.Should().NotBeNull();

        // Verify the document was marked as redacted
        _mockDocumentRepository.Verify(x => x.MarkAsRedactedAsync(documentId, mockUser.Id), Times.Once);
    }

    [TestMethod]
    public async Task RedactDocument_NoRedactionAreas_ReturnsBadRequest()
    {
        // Arrange - Test validation logic
        var documentId = 67890;
        var mockUser = TestDataBuilder.CreateMockUser();
        var redactRequest = new RedactDocumentRequest { Areas = new List<RedactionArea>() };

        SetupMockUser(mockUser);

        // Act
        var result = await _controller.RedactDocument(documentId, redactRequest);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
        var badRequestResult = (BadRequestObjectResult)result;
        badRequestResult.Value.Should().Be("No redaction areas specified");
    }

    #endregion

    #region GetDocumentTypes Tests

    [TestMethod]
    public async Task GetDocumentTypes_ValidRequest_ReturnsDocumentTypes()
    {
        // Arrange - Test WinForms document type loading for indexing
        var mockUser = TestDataBuilder.CreateMockUser();
        var expectedTypes = new List<string> { "Appraisal", "Credit Report", "Income Documentation", "Title Insurance" };

        _mockDocumentRepository.Setup(x => x.GetDocumentTypesAsync())
            .ReturnsAsync(expectedTypes);

        SetupMockUser(mockUser);

        // Act
        var result = await _controller.GetDocumentTypes();

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var types = ((OkObjectResult)result).Value as List<string>;
        types.Should().NotBeNull();
        types!.Should().BeEquivalentTo(expectedTypes);
    }

    #endregion

    #region Mock Authentication Helper

    private void SetupMockUser(AuthorizedUser user)
    {
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext()
        };

        _controller.ControllerContext.HttpContext.Items["AuthorizedUser"] = user;
    }

    #endregion

    #region Exception Handling Tests

    [TestMethod]
    public async Task GetDocumentsByLoan_RepositoryException_Returns500InternalServerError()
    {
        // Arrange
        var loanId = 12345;
        var mockUser = TestDataBuilder.CreateMockUser();

        _mockSecurityRepository.Setup(x => x.HasLoanAccessAsync(mockUser.Id, loanId))
            .ReturnsAsync(true);
        _mockDocumentRepository.Setup(x => x.GetByLoanIdAsync(loanId))
            .ThrowsAsync(new Exception("Database connection failed"));

        SetupMockUser(mockUser);

        // Act
        var result = await _controller.GetDocumentsByLoan(loanId);

        // Assert
        result.Should().BeOfType<ObjectResult>();
        var objectResult = (ObjectResult)result;
        objectResult.StatusCode.Should().Be(500);
    }

    [TestMethod]
    public async Task GetDocumentUrl_RepositoryException_Returns500InternalServerError()
    {
        // Arrange
        var documentId = 67890;
        var mockUser = TestDataBuilder.CreateMockUser();

        _mockSecurityRepository.Setup(x => x.HasDocumentAccessAsync(mockUser.Id, documentId))
            .ReturnsAsync(true);
        _mockDocumentRepository.Setup(x => x.GenerateDocumentUrlAsync(documentId))
            .ThrowsAsync(new Exception("S3 service unavailable"));

        SetupMockUser(mockUser);

        // Act
        var result = await _controller.GetDocumentUrl(documentId);

        // Assert
        result.Should().BeOfType<ObjectResult>();
        var objectResult = (ObjectResult)result;
        objectResult.StatusCode.Should().Be(500);
    }

    #endregion
}
