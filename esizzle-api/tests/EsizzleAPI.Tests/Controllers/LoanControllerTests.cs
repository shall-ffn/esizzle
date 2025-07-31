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
/// Tests for LoanController that validate WinForms loan selection workflow
/// Based on analysis of WinForms eStacker SelectLoan() functionality
/// </summary>
[TestClass]
public class LoanControllerTests
{
    private LoanController _controller;
    private Mock<ILoanRepository> _mockLoanRepository;
    private Mock<ISecurityRepository> _mockSecurityRepository;
    private Mock<ILogger<LoanController>> _mockLogger;

    [TestInitialize]
    public void Setup()
    {
        _mockLoanRepository = new Mock<ILoanRepository>();
        _mockSecurityRepository = new Mock<ISecurityRepository>();
        _mockLogger = new Mock<ILogger<LoanController>>();
        
        _controller = new LoanController(
            _mockLogger.Object,
            _mockLoanRepository.Object,
            _mockSecurityRepository.Object);
    }

    #region GetLoan Tests (WinForms SelectLoan equivalent)

    [TestMethod]
    public async Task GetLoan_ValidLoanWithAccess_ReturnsLoanDetails()
    {
        // Arrange - Test scenario matching WinForms SelectLoan(loanID) success
        var loanId = 12345;
        var expectedLoan = TestDataBuilder.CreateTestLoan(loanId);
        var mockUser = TestDataBuilder.CreateMockUser();

        _mockSecurityRepository.Setup(x => x.HasLoanAccessAsync(mockUser.Id, loanId))
            .ReturnsAsync(true);
        _mockLoanRepository.Setup(x => x.GetByIdAsync(loanId))
            .ReturnsAsync(expectedLoan);

        SetupMockUser(mockUser);

        // Act
        var result = await _controller.GetLoan(loanId);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var loan = ((OkObjectResult)result).Value as LoanModel;
        loan.Should().NotBeNull();
        loan!.LoanId.Should().Be(loanId);
        loan.AssetName.Should().Be(expectedLoan.AssetName);

        // Verify repository calls match WinForms workflow
        _mockSecurityRepository.Verify(x => x.HasLoanAccessAsync(mockUser.Id, loanId), Times.Once);
        _mockLoanRepository.Verify(x => x.GetByIdAsync(loanId), Times.Once);
    }

    [TestMethod]
    public async Task GetLoan_NoAccess_Returns403Forbidden()
    {
        // Arrange - Test WinForms security check failure
        var loanId = 12345;
        var mockUser = TestDataBuilder.CreateMockUser();

        _mockSecurityRepository.Setup(x => x.HasLoanAccessAsync(mockUser.Id, loanId))
            .ReturnsAsync(false);

        SetupMockUser(mockUser);

        // Act
        var result = await _controller.GetLoan(loanId);

        // Assert
        result.Should().BeOfType<ObjectResult>();
        var objectResult = (ObjectResult)result;
        objectResult.StatusCode.Should().Be(403);

        // Verify loan repository was never called (security check failed)
        _mockLoanRepository.Verify(x => x.GetByIdAsync(It.IsAny<int>()), Times.Never);
    }

    [TestMethod]
    public async Task GetLoan_NonExistentLoan_Returns404NotFound()
    {
        // Arrange - Test loan not found scenario
        var loanId = 99999;
        var mockUser = TestDataBuilder.CreateMockUser();

        _mockSecurityRepository.Setup(x => x.HasLoanAccessAsync(mockUser.Id, loanId))
            .ReturnsAsync(true);
        _mockLoanRepository.Setup(x => x.GetByIdAsync(loanId))
            .ReturnsAsync((LoanModel?)null);

        SetupMockUser(mockUser);

        // Act
        var result = await _controller.GetLoan(loanId);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
        var notFoundResult = (NotFoundObjectResult)result;
        notFoundResult.Value.Should().Be($"Loan {loanId} not found");
    }

    #endregion

    #region GetLoansBySale Tests (WinForms LoadLoans equivalent)

    [TestMethod]
    public async Task GetLoansBySale_ValidSale_ReturnsFilteredLoans()
    {
        // Arrange - Test WinForms LoadLoans(saleID) functionality
        var saleId = 678;
        var mockUser = TestDataBuilder.CreateMockUser();
        var expectedLoans = TestDataBuilder.CreateTestLoans(saleId, count: 5);

        _mockSecurityRepository.Setup(x => x.HasSaleAccessAsync(mockUser.Id, saleId))
            .ReturnsAsync(true);
        // Convert to LoanSummaryModel for repository mock
        var expectedSummaries = expectedLoans.Select(l => new LoanSummaryModel
        {
            LoanId = l.LoanId,
            AssetNo = l.AssetNo,
            AssetName = l.AssetName,
            AssetName2 = l.AssetName2,
            BookBalance = l.BookBalance,
            LoadedOn = l.LoadedOn,
            SaleId = l.SaleId,
            LoanStatusId = l.LoanStatusId,
            DocumentCount = 0, // Mock value
            LastDocumentDate = null
        });

        _mockLoanRepository.Setup(x => x.GetBySaleIdAsync(saleId))
            .ReturnsAsync(expectedSummaries);

        SetupMockUser(mockUser);

        // Act
        var result = await _controller.GetLoansBySale(saleId);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var loans = ((OkObjectResult)result).Value as List<LoanSummaryModel>;
        loans.Should().NotBeNull();
        loans!.Should().HaveCount(5);
        loans.All(l => l.SaleId == saleId).Should().BeTrue();

        // Verify all loans have required properties (matching WinForms display)
        foreach (var loan in loans)
        {
            loan.AssetName.Should().NotBeNullOrEmpty();
            loan.AssetNo.Should().NotBeNullOrEmpty();
            loan.BookBalance.Should().BeGreaterThan(0);
        }
    }

    [TestMethod]
    public async Task GetLoansBySale_NoSaleAccess_Returns403Forbidden()
    {
        // Arrange - Test WinForms sale access control
        var saleId = 678;
        var mockUser = TestDataBuilder.CreateMockUser();

        _mockSecurityRepository.Setup(x => x.HasSaleAccessAsync(mockUser.Id, saleId))
            .ReturnsAsync(false);

        SetupMockUser(mockUser);

        // Act
        var result = await _controller.GetLoansBySale(saleId);

        // Assert
        result.Should().BeOfType<ObjectResult>();
        var objectResult = (ObjectResult)result;
        objectResult.StatusCode.Should().Be(403);

        _mockLoanRepository.Verify(x => x.GetBySaleIdAsync(It.IsAny<int>()), Times.Never);
    }

    [TestMethod]
    public async Task GetLoansBySale_EmptyResult_ReturnsEmptyList()
    {
        // Arrange - Test sale with no loans
        var saleId = 678;
        var mockUser = TestDataBuilder.CreateMockUser();

        _mockSecurityRepository.Setup(x => x.HasSaleAccessAsync(mockUser.Id, saleId))
            .ReturnsAsync(true);
        _mockLoanRepository.Setup(x => x.GetBySaleIdAsync(saleId))
            .ReturnsAsync(new List<LoanSummaryModel>());

        SetupMockUser(mockUser);

        // Act
        var result = await _controller.GetLoansBySale(saleId);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var loans = ((OkObjectResult)result).Value as List<LoanSummaryModel>;
        loans.Should().NotBeNull();
        loans!.Should().BeEmpty();
    }

    #endregion

    #region SearchLoansBySale Tests (WinForms loan search functionality)

    [TestMethod]
    public async Task SearchLoansBySale_ValidSearchTerm_ReturnsMatchingLoans()
    {
        // Arrange - Test WinForms loan search functionality
        var saleId = 678;
        var searchTerm = "Main St";
        var mockUser = TestDataBuilder.CreateMockUser();
        var allLoans = TestDataBuilder.CreateTestLoans(saleId, count: 5);
        var matchingLoans = allLoans.Where(l => l.AssetName.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)).ToList();

        _mockSecurityRepository.Setup(x => x.HasSaleAccessAsync(mockUser.Id, saleId))
            .ReturnsAsync(true);
        // Convert to LoanSummaryModel for repository mock
        var matchingSummaries = matchingLoans.Select(l => new LoanSummaryModel
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
            .ReturnsAsync(matchingSummaries);

        SetupMockUser(mockUser);

        // Act
        var result = await _controller.SearchLoansBySale(saleId, searchTerm);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var loans = ((OkObjectResult)result).Value as List<LoanSummaryModel>;
        loans.Should().NotBeNull();
        
        // Verify WinForms search logic is replicated (convert back to LoanModel for validation)
        var loanModels = loans!.Select(s => new LoanModel
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
        isValidSearch.Should().BeTrue();
    }

    [TestMethod]
    public async Task SearchLoansBySale_ShortSearchTerm_ReturnsBadRequest()
    {
        // Arrange - Test WinForms search term validation (minimum 2 characters)
        var saleId = 678;
        var shortSearchTerm = "a"; // Less than 2 characters
        var mockUser = TestDataBuilder.CreateMockUser();

        SetupMockUser(mockUser);

        // Act
        var result = await _controller.SearchLoansBySale(saleId, shortSearchTerm);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
        var badRequestResult = (BadRequestObjectResult)result;
        badRequestResult.Value.Should().Be("Search term must be at least 2 characters");

        // Verify no repository calls were made
        _mockLoanRepository.Verify(x => x.SearchLoansAsync(It.IsAny<int>(), It.IsAny<string>()), Times.Never);
    }

    [TestMethod]
    public async Task SearchLoansBySale_EmptySearchTerm_ReturnsBadRequest()
    {
        // Arrange
        var saleId = 678;
        var emptySearchTerm = "";
        var mockUser = TestDataBuilder.CreateMockUser();

        SetupMockUser(mockUser);

        // Act
        var result = await _controller.SearchLoansBySale(saleId, emptySearchTerm);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    #endregion

    #region Mock Authentication Helper

    private void SetupMockUser(AuthorizedUser user)
    {
        // Setup mock HTTP context with authenticated user
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext()
        };

        _controller.ControllerContext.HttpContext.Items["AuthorizedUser"] = user;
    }

    #endregion

    #region Exception Handling Tests

    [TestMethod]
    public async Task GetLoan_RepositoryException_Returns500InternalServerError()
    {
        // Arrange
        var loanId = 12345;
        var mockUser = TestDataBuilder.CreateMockUser();

        _mockSecurityRepository.Setup(x => x.HasLoanAccessAsync(mockUser.Id, loanId))
            .ReturnsAsync(true);
        _mockLoanRepository.Setup(x => x.GetByIdAsync(loanId))
            .ThrowsAsync(new Exception("Database connection failed"));

        SetupMockUser(mockUser);

        // Act
        var result = await _controller.GetLoan(loanId);

        // Assert
        result.Should().BeOfType<ObjectResult>();
        var objectResult = (ObjectResult)result;
        objectResult.StatusCode.Should().Be(500);
    }

    [TestMethod]
    public async Task GetLoansBySale_RepositoryException_Returns500InternalServerError()
    {
        // Arrange
        var saleId = 678;
        var mockUser = TestDataBuilder.CreateMockUser();

        _mockSecurityRepository.Setup(x => x.HasSaleAccessAsync(mockUser.Id, saleId))
            .ReturnsAsync(true);
        _mockLoanRepository.Setup(x => x.GetBySaleIdAsync(saleId))
            .ThrowsAsync(new Exception("Database timeout"));

        SetupMockUser(mockUser);

        // Act
        var result = await _controller.GetLoansBySale(saleId);

        // Assert
        result.Should().BeOfType<ObjectResult>();
        var objectResult = (ObjectResult)result;
        objectResult.StatusCode.Should().Be(500);
    }

    #endregion
}
