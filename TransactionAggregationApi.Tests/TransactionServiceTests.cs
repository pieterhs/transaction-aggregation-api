using Moq;
using Microsoft.Extensions.Logging;
using TransactionAggregationApi.Api.Clients;
using TransactionAggregationApi.Api.Infrastructure;
using TransactionAggregationApi.Api.Models;
using TransactionAggregationApi.Api.Services;

namespace TransactionAggregationApi.Tests;

public class TransactionServiceTests
{
    private readonly Mock<IBankClient> _mockBankAClient;
    private readonly Mock<IBankClient> _mockBankBClient;
    private readonly Mock<TransactionCache> _mockCache;
    private readonly Mock<ILogger<TransactionService>> _mockLogger;
    private readonly TransactionService _transactionService;

    public TransactionServiceTests()
    {
        _mockBankAClient = new Mock<IBankClient>();
        _mockBankBClient = new Mock<IBankClient>();
        _mockCache = new Mock<TransactionCache>(MockBehavior.Loose, null!, null!);
        _mockLogger = new Mock<ILogger<TransactionService>>();

        _mockBankAClient.Setup(x => x.BankName).Returns("BankA");
        _mockBankBClient.Setup(x => x.BankName).Returns("BankB");

        var bankClients = new List<IBankClient> { _mockBankAClient.Object, _mockBankBClient.Object };
        
        _transactionService = new TransactionService(
            bankClients,
            _mockCache.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task GetTransactionsAsync_ShouldReturnTransactionsFromMultipleBanks()
    {
        // Arrange
        var from = DateTime.UtcNow.AddDays(-7);
        var to = DateTime.UtcNow;

        var bankATransactions = new List<TransactionDto>
        {
            new TransactionDto
            {
                Id = "BANKA-1",
                Date = DateTime.UtcNow.AddDays(-1),
                Amount = 100.00m,
                Currency = "USD",
                Category = "Groceries",
                Source = "BankA"
            }
        };

        var bankBTransactions = new List<TransactionDto>
        {
            new TransactionDto
            {
                Id = "BANKB-1",
                Date = DateTime.UtcNow.AddDays(-2),
                Amount = 200.00m,
                Currency = "USD",
                Category = "Entertainment",
                Source = "BankB"
            }
        };

        _mockBankAClient
            .Setup(x => x.GetTransactionsAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(bankATransactions);

        _mockBankBClient
            .Setup(x => x.GetTransactionsAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(bankBTransactions);

        // Mock cache to return null (cache miss)
        _mockCache
            .Setup(x => x.GetOrCreateAsync(
                It.IsAny<string>(),
                It.IsAny<Func<Task<IEnumerable<TransactionDto>>>>(),
                It.IsAny<TimeSpan?>()))
            .Returns(async (string key, Func<Task<IEnumerable<TransactionDto>>> factory, TimeSpan? expiration) => 
                await factory());

        // Act
        var result = await _transactionService.GetTransactionsAsync(from, to);

        // Assert
        Assert.NotNull(result);
        var transactions = result.ToList();
        Assert.Equal(2, transactions.Count);
        Assert.Contains(transactions, t => t.Source == "BankA");
        Assert.Contains(transactions, t => t.Source == "BankB");
    }

    [Fact]
    public async Task GetTransactionsAsync_ShouldFilterByCategory()
    {
        // Arrange
        var from = DateTime.UtcNow.AddDays(-7);
        var to = DateTime.UtcNow;
        var category = "Groceries";

        var allTransactions = new List<TransactionDto>
        {
            new TransactionDto
            {
                Id = "1",
                Date = DateTime.UtcNow.AddDays(-1),
                Amount = 100.00m,
                Currency = "USD",
                Category = "Groceries",
                Source = "BankA"
            },
            new TransactionDto
            {
                Id = "2",
                Date = DateTime.UtcNow.AddDays(-2),
                Amount = 200.00m,
                Currency = "USD",
                Category = "Entertainment",
                Source = "BankB"
            }
        };

        _mockBankAClient
            .Setup(x => x.GetTransactionsAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(allTransactions);

        _mockBankBClient
            .Setup(x => x.GetTransactionsAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(new List<TransactionDto>());

        _mockCache
            .Setup(x => x.GetOrCreateAsync(
                It.IsAny<string>(),
                It.IsAny<Func<Task<IEnumerable<TransactionDto>>>>(),
                It.IsAny<TimeSpan?>()))
            .Returns(async (string key, Func<Task<IEnumerable<TransactionDto>>> factory, TimeSpan? expiration) => 
                await factory());

        // Act
        var result = await _transactionService.GetTransactionsAsync(from, to, category);

        // Assert
        Assert.NotNull(result);
        var transactions = result.ToList();
        Assert.Single(transactions);
        Assert.Equal("Groceries", transactions[0].Category);
    }

    [Fact]
    public async Task GetTransactionsAsync_ShouldApplyPagination()
    {
        // Arrange
        var from = DateTime.UtcNow.AddDays(-7);
        var to = DateTime.UtcNow;

        var allTransactions = Enumerable.Range(1, 10).Select(i => new TransactionDto
        {
            Id = $"TRANS-{i}",
            Date = DateTime.UtcNow.AddDays(-i),
            Amount = i * 10.00m,
            Currency = "USD",
            Category = "Test",
            Source = "BankA"
        }).ToList();

        _mockBankAClient
            .Setup(x => x.GetTransactionsAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(allTransactions);

        _mockBankBClient
            .Setup(x => x.GetTransactionsAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(new List<TransactionDto>());

        _mockCache
            .Setup(x => x.GetOrCreateAsync(
                It.IsAny<string>(),
                It.IsAny<Func<Task<IEnumerable<TransactionDto>>>>(),
                It.IsAny<TimeSpan?>()))
            .Returns(async (string key, Func<Task<IEnumerable<TransactionDto>>> factory, TimeSpan? expiration) => 
                await factory());

        // Act
        var result = await _transactionService.GetTransactionsAsync(from, to, null, page: 2, pageSize: 3);

        // Assert
        Assert.NotNull(result);
        var transactions = result.ToList();
        Assert.Equal(3, transactions.Count);
    }
}
