using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using TransactionAggregationApi.Api.Clients;
using TransactionAggregationApi.Api.Controllers;
using TransactionAggregationApi.Api.Infrastructure;
using TransactionAggregationApi.Api.Models;
using TransactionAggregationApi.Api.Services;

namespace TransactionAggregationApi.Tests;

/// <summary>
/// Unit tests for TransactionsController.
/// Validates endpoint behavior, parameter validation, error handling, and responses.
/// </summary>
public class TransactionsControllerTests
{
    private readonly Mock<ITransactionService> _mockTransactionService;
    private readonly Mock<ILogger<TransactionsController>> _mockLogger;
    private readonly TransactionsController _controller;

    public TransactionsControllerTests()
    {
        _mockTransactionService = new Mock<ITransactionService>();
        _mockLogger = new Mock<ILogger<TransactionsController>>();

        _controller = new TransactionsController(
            _mockTransactionService.Object,
            _mockLogger.Object);

        // Setup HttpContext for Response.Headers
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
    }

    [Fact]
    public async Task GetTransactions_WithValidParameters_ReturnsOkResult()
    {
        // Arrange
        var from = new DateTime(2025, 10, 1);
        var to = new DateTime(2025, 10, 14);
        var expectedResult = CreateMockPagedResult(25);

        _mockTransactionService
            .Setup(s => s.GetTransactionsAsync(from, to, null, 1, 50, null))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _controller.GetTransactions(from, to, null, 1, 50);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var pagedResult = Assert.IsType<PagedResultDto<TransactionDto>>(okResult.Value);
        Assert.Equal(25, pagedResult.Total);
        Assert.Equal(1, pagedResult.Page);
    }

    [Fact]
    public async Task GetTransactions_WithValidParameters_SetsPaginationHeaders()
    {
        // Arrange
        var from = new DateTime(2025, 10, 1);
        var to = new DateTime(2025, 10, 14);
        var expectedResult = CreateMockPagedResult(100, page: 2, pageSize: 20);

        _mockTransactionService
            .Setup(s => s.GetTransactionsAsync(from, to, null, 2, 20, null))
            .ReturnsAsync(expectedResult);

        // Act
        await _controller.GetTransactions(from, to, null, 2, 20);

        // Assert
        Assert.Equal("100", _controller.Response.Headers["X-Total-Count"]);
        Assert.Equal("2", _controller.Response.Headers["X-Page"]);
        Assert.Equal("20", _controller.Response.Headers["X-PageSize"]);
        Assert.Equal("5", _controller.Response.Headers["X-Total-Pages"]);
    }

    [Fact]
    public async Task GetTransactions_WhenFromIsAfterTo_ReturnsBadRequest()
    {
        // Arrange
        var from = new DateTime(2025, 10, 14);
        var to = new DateTime(2025, 10, 1); // Invalid: to < from

        // Act
        var result = await _controller.GetTransactions(from, to);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        var errorResponse = badRequestResult.Value;
        Assert.NotNull(errorResponse);
    }

    [Fact]
    public async Task GetTransactions_WhenPageIsZero_ReturnsBadRequest()
    {
        // Arrange
        var from = new DateTime(2025, 10, 1);
        var to = new DateTime(2025, 10, 14);

        // Act
        var result = await _controller.GetTransactions(from, to, null, page: 0);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.NotNull(badRequestResult.Value);
    }

    [Fact]
    public async Task GetTransactions_WhenPageIsNegative_ReturnsBadRequest()
    {
        // Arrange
        var from = new DateTime(2025, 10, 1);
        var to = new DateTime(2025, 10, 14);

        // Act
        var result = await _controller.GetTransactions(from, to, null, page: -1);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.NotNull(badRequestResult.Value);
    }

    [Fact]
    public async Task GetTransactions_WhenPageSizeIsZero_ReturnsBadRequest()
    {
        // Arrange
        var from = new DateTime(2025, 10, 1);
        var to = new DateTime(2025, 10, 14);

        // Act
        var result = await _controller.GetTransactions(from, to, null, 1, pageSize: 0);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.NotNull(badRequestResult.Value);
    }

    [Fact]
    public async Task GetTransactions_WhenPageSizeExceeds100_ReturnsBadRequest()
    {
        // Arrange
        var from = new DateTime(2025, 10, 1);
        var to = new DateTime(2025, 10, 14);

        // Act
        var result = await _controller.GetTransactions(from, to, null, 1, pageSize: 101);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.NotNull(badRequestResult.Value);
    }

    [Fact]
    public async Task GetTransactions_WhenPageSizeIs100_ReturnsOk()
    {
        // Arrange
        var from = new DateTime(2025, 10, 1);
        var to = new DateTime(2025, 10, 14);
        var expectedResult = CreateMockPagedResult(100, pageSize: 100);

        _mockTransactionService
            .Setup(s => s.GetTransactionsAsync(from, to, null, 1, 100, null))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _controller.GetTransactions(from, to, null, 1, 100);

        // Assert
        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task GetTransactions_WithCategoryFilter_PassesCategoryToService()
    {
        // Arrange
        var from = new DateTime(2025, 10, 1);
        var to = new DateTime(2025, 10, 14);
        var category = "Groceries";
        var expectedResult = CreateMockPagedResult(10);

        _mockTransactionService
            .Setup(s => s.GetTransactionsAsync(from, to, category, 1, 50, null))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _controller.GetTransactions(from, to, category, 1, 50);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        _mockTransactionService.Verify(
            s => s.GetTransactionsAsync(from, to, category, 1, 50, null),
            Times.Once);
    }

    [Fact]
    public async Task GetTransactions_WhenServiceThrowsArgumentException_ReturnsBadRequest()
    {
        // Arrange
        var from = new DateTime(2025, 10, 1);
        var to = new DateTime(2025, 10, 14);

        _mockTransactionService
            .Setup(s => s.GetTransactionsAsync(from, to, null, 1, 50, null))
            .ThrowsAsync(new ArgumentException("Invalid date range"));

        // Act
        var result = await _controller.GetTransactions(from, to);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.NotNull(badRequestResult.Value);
    }

    [Fact]
    public async Task GetTransactions_WhenServiceThrowsException_Returns500()
    {
        // Arrange
        var from = new DateTime(2025, 10, 1);
        var to = new DateTime(2025, 10, 14);

        _mockTransactionService
            .Setup(s => s.GetTransactionsAsync(from, to, null, 1, 50, null))
            .ThrowsAsync(new Exception("Database connection failed"));

        // Act
        var result = await _controller.GetTransactions(from, to);

        // Assert
        var statusCodeResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, statusCodeResult.StatusCode);
        Assert.NotNull(statusCodeResult.Value);
    }

    [Fact]
    public async Task GetTransactions_WhenFromIsTooOld_ReturnsBadRequest()
    {
        // Arrange
        var from = DateTime.UtcNow.AddYears(-11); // More than 10 years in the past
        var to = DateTime.UtcNow;

        // Act
        var result = await _controller.GetTransactions(from, to);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.NotNull(badRequestResult.Value);
    }

    [Fact]
    public async Task GetTransactions_WhenToIsTooFarInFuture_ReturnsBadRequest()
    {
        // Arrange
        var from = DateTime.UtcNow;
        var to = DateTime.UtcNow.AddYears(2); // More than 1 year in the future

        // Act
        var result = await _controller.GetTransactions(from, to);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.NotNull(badRequestResult.Value);
    }

    [Fact]
    public async Task GetTransactionsMetadata_WithValidParameters_ReturnsOkWithHeaders()
    {
        // Arrange
        var from = new DateTime(2025, 10, 1);
        var to = new DateTime(2025, 10, 14);
        var expectedResult = CreateMockPagedResult(150, page: 3, pageSize: 25);

        _mockTransactionService
            .Setup(s => s.GetTransactionsAsync(from, to, null, 3, 25, null))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _controller.GetTransactionsMetadata(from, to, null, 3, 25);

        // Assert
        var okResult = Assert.IsType<OkResult>(result);
        Assert.Equal("150", _controller.Response.Headers["X-Total-Count"]);
        Assert.Equal("3", _controller.Response.Headers["X-Page"]);
        Assert.Equal("25", _controller.Response.Headers["X-PageSize"]);
        Assert.Equal("6", _controller.Response.Headers["X-Total-Pages"]);
    }

    [Fact]
    public async Task GetTransactionsMetadata_WhenFromIsAfterTo_ReturnsBadRequest()
    {
        // Arrange
        var from = new DateTime(2025, 10, 14);
        var to = new DateTime(2025, 10, 1);

        // Act
        var result = await _controller.GetTransactionsMetadata(from, to);

        // Assert
        Assert.IsType<BadRequestResult>(result);
    }

    [Fact]
    public async Task GetTransactionsMetadata_WhenServiceThrowsException_Returns500()
    {
        // Arrange
        var from = new DateTime(2025, 10, 1);
        var to = new DateTime(2025, 10, 14);

        _mockTransactionService
            .Setup(s => s.GetTransactionsAsync(from, to, null, 1, 50, null))
            .ThrowsAsync(new Exception("Service error"));

        // Act
        var result = await _controller.GetTransactionsMetadata(from, to);

        // Assert
        var statusCodeResult = Assert.IsType<StatusCodeResult>(result);
        Assert.Equal(500, statusCodeResult.StatusCode);
    }

    [Fact]
    public async Task GetTransactions_WithMultipleCalls_UsesCorrectParameters()
    {
        // Arrange
        var from1 = new DateTime(2025, 9, 1);
        var to1 = new DateTime(2025, 9, 30);
        var category1 = "Food";

        var from2 = new DateTime(2025, 10, 1);
        var to2 = new DateTime(2025, 10, 31);
        var category2 = "Entertainment";

        var result1 = CreateMockPagedResult(15);
        var result2 = CreateMockPagedResult(8);

        _mockTransactionService
            .Setup(s => s.GetTransactionsAsync(from1, to1, category1, 1, 50, null))
            .ReturnsAsync(result1);

        _mockTransactionService
            .Setup(s => s.GetTransactionsAsync(from2, to2, category2, 2, 25, null))
            .ReturnsAsync(result2);

        // Act
        var response1 = await _controller.GetTransactions(from1, to1, category1, 1, 50);
        var response2 = await _controller.GetTransactions(from2, to2, category2, 2, 25);

        // Assert
        var okResult1 = Assert.IsType<OkObjectResult>(response1);
        var pagedResult1 = Assert.IsType<PagedResultDto<TransactionDto>>(okResult1.Value);
        Assert.Equal(15, pagedResult1.Total);

        var okResult2 = Assert.IsType<OkObjectResult>(response2);
        var pagedResult2 = Assert.IsType<PagedResultDto<TransactionDto>>(okResult2.Value);
        Assert.Equal(8, pagedResult2.Total);
    }

    [Fact]
    public async Task GetTransactions_WithDefaultPageAndPageSize_UsesDefaults()
    {
        // Arrange
        var from = new DateTime(2025, 10, 1);
        var to = new DateTime(2025, 10, 14);
        var expectedResult = CreateMockPagedResult(50);

        _mockTransactionService
            .Setup(s => s.GetTransactionsAsync(from, to, null, 1, 50, null))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _controller.GetTransactions(from, to);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        _mockTransactionService.Verify(
            s => s.GetTransactionsAsync(from, to, null, 1, 50, null),
            Times.Once);
    }

    /// <summary>
    /// Helper method to create a mock paged result.
    /// </summary>
    private static PagedResultDto<TransactionDto> CreateMockPagedResult(
        int total,
        int page = 1,
        int pageSize = 50)
    {
        var transactions = Enumerable.Range(1, Math.Min(total, pageSize))
            .Select(i => new TransactionDto
            {
                Id = $"TX-{i}",
                Date = DateTime.UtcNow.AddDays(-i),
                Amount = i * 10.5m,
                Currency = "USD",
                Category = "Test",
                Source = "TestBank"
            })
            .ToList();

        return new PagedResultDto<TransactionDto>
        {
            Total = total,
            Page = page,
            PageSize = pageSize,
            TotalPages = (int)Math.Ceiling(total / (double)pageSize),
            Transactions = transactions
        };
    }
}
