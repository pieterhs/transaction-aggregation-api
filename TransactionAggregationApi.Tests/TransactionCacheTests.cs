using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;
using TransactionAggregationApi.Api.Infrastructure;
using TransactionAggregationApi.Api.Models;

namespace TransactionAggregationApi.Tests;

/// <summary>
/// Unit tests for TransactionCache implementation.
/// Validates cache operations, key generation, and logging.
/// </summary>
public class TransactionCacheTests
{
    private readonly IMemoryCache _memoryCache;
    private readonly Mock<ILogger<TransactionCache>> _mockLogger;
    private readonly TransactionCache _cache;

    public TransactionCacheTests()
    {
        _memoryCache = new MemoryCache(new MemoryCacheOptions());
        _mockLogger = new Mock<ILogger<TransactionCache>>();
        _cache = new TransactionCache(_memoryCache, _mockLogger.Object);
    }

    [Fact]
    public async Task GetAsync_ShouldReturnNull_WhenKeyDoesNotExist()
    {
        // Arrange
        var key = "transactions:user123:20250901-20251014:all:1:50";

        // Act
        var result = await _cache.GetAsync(key);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task SetAsync_ShouldStoreTransactions_AndGetAsync_ShouldRetrieveThem()
    {
        // Arrange
        var key = "transactions:user123:20250901-20251014:Groceries:1:50";
        var transactions = new List<TransactionDto>
        {
            new TransactionDto
            {
                Id = "TX-1",
                Date = DateTime.UtcNow,
                Amount = 100.50m,
                Currency = "USD",
                Category = "Groceries",
                Source = "BankA"
            },
            new TransactionDto
            {
                Id = "TX-2",
                Date = DateTime.UtcNow.AddDays(-1),
                Amount = 250.75m,
                Currency = "EUR",
                Category = "Groceries",
                Source = "BankB"
            }
        }.AsReadOnly();

        var ttl = TimeSpan.FromMinutes(10);

        // Act
        await _cache.SetAsync(key, transactions, ttl);
        var result = await _cache.GetAsync(key);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        Assert.Equal("TX-1", result[0].Id);
        Assert.Equal("TX-2", result[1].Id);
    }

    [Fact]
    public async Task SetAsync_ShouldThrowException_WhenKeyIsNull()
    {
        // Arrange
        var transactions = new List<TransactionDto>().AsReadOnly();
        var ttl = TimeSpan.FromMinutes(10);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(async () => 
            await _cache.SetAsync(null!, transactions, ttl));
    }

    [Fact]
    public async Task SetAsync_ShouldThrowException_WhenValueIsNull()
    {
        // Arrange
        var key = "test-key";
        var ttl = TimeSpan.FromMinutes(10);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(async () => 
            await _cache.SetAsync(key, null!, ttl));
    }

    [Fact]
    public async Task SetAsync_ShouldThrowException_WhenTtlIsZeroOrNegative()
    {
        // Arrange
        var key = "test-key";
        var transactions = new List<TransactionDto>().AsReadOnly();

        // Act & Assert - Zero TTL
        await Assert.ThrowsAsync<ArgumentException>(async () => 
            await _cache.SetAsync(key, transactions, TimeSpan.Zero));

        // Act & Assert - Negative TTL
        await Assert.ThrowsAsync<ArgumentException>(async () => 
            await _cache.SetAsync(key, transactions, TimeSpan.FromMinutes(-1)));
    }

    [Fact]
    public async Task Remove_ShouldRemoveCachedEntry()
    {
        // Arrange
        var key = "transactions:user123:20250901-20251014:all:1:50";
        var transactions = new List<TransactionDto>
        {
            new TransactionDto
            {
                Id = "TX-1",
                Date = DateTime.UtcNow,
                Amount = 100.50m,
                Currency = "USD",
                Category = "Test",
                Source = "BankA"
            }
        }.AsReadOnly();

        await _cache.SetAsync(key, transactions, TimeSpan.FromMinutes(10));

        // Act
        _cache.Remove(key);
        var result = await _cache.GetAsync(key);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task Clear_ShouldRemoveAllCachedEntries()
    {
        // Arrange - Add multiple cache entries
        var key1 = "transactions:user1:20250901-20251014:all:1:50";
        var key2 = "transactions:user2:20250901-20251014:all:1:50";
        
        var transactions = new List<TransactionDto>
        {
            new TransactionDto
            {
                Id = "TX-1",
                Date = DateTime.UtcNow,
                Amount = 100.50m,
                Currency = "USD",
                Category = "Test",
                Source = "BankA"
            }
        }.AsReadOnly();

        await _cache.SetAsync(key1, transactions, TimeSpan.FromMinutes(10));
        await _cache.SetAsync(key2, transactions, TimeSpan.FromMinutes(10));

        // Act
        _cache.Clear();

        // Assert - Verify entries are removed
        var result1 = await _cache.GetAsync(key1);
        var result2 = await _cache.GetAsync(key2);
        
        Assert.Null(result1);
        Assert.Null(result2);
    }

    [Fact]
    public void BuildCacheKey_ShouldGenerateCorrectKeyPattern()
    {
        // Arrange
        var userId = "user123";
        var from = new DateTime(2025, 9, 1);
        var to = new DateTime(2025, 10, 14);
        var category = "Groceries";
        var page = 1;
        var pageSize = 50;

        // Act
        var key = TransactionCache.BuildCacheKey(userId, from, to, category, page, pageSize);

        // Assert
        Assert.Equal("transactions:user123:20250901-20251014:Groceries:1:50", key);
    }

    [Fact]
    public void BuildCacheKey_ShouldHandleNullUserId()
    {
        // Arrange
        var from = new DateTime(2025, 9, 1);
        var to = new DateTime(2025, 10, 14);

        // Act
        var key = TransactionCache.BuildCacheKey(null, from, to, "Test", 1, 50);

        // Assert
        Assert.Contains("anonymous", key);
        Assert.Equal("transactions:anonymous:20250901-20251014:Test:1:50", key);
    }

    [Fact]
    public void BuildCacheKey_ShouldHandleNullCategory()
    {
        // Arrange
        var from = new DateTime(2025, 9, 1);
        var to = new DateTime(2025, 10, 14);

        // Act
        var key = TransactionCache.BuildCacheKey("user123", from, to, null, 1, 50);

        // Assert
        Assert.Contains("all", key);
        Assert.Equal("transactions:user123:20250901-20251014:all:1:50", key);
    }

    [Fact]
    public void BuildCacheKey_ShouldHandleEmptyCategory()
    {
        // Arrange
        var from = new DateTime(2025, 9, 1);
        var to = new DateTime(2025, 10, 14);

        // Act
        var key = TransactionCache.BuildCacheKey("user123", from, to, "", 2, 25);

        // Assert
        Assert.Contains("all", key);
        Assert.Equal("transactions:user123:20250901-20251014:all:2:25", key);
    }

    [Fact]
    public void GetDefaultTtl_ShouldReturn10Minutes()
    {
        // Act
        var ttl = TransactionCache.GetDefaultTtl();

        // Assert
        Assert.Equal(TimeSpan.FromMinutes(10), ttl);
    }

    [Fact]
    public async Task GetAsync_ShouldLogCacheHit_WhenValueExists()
    {
        // Arrange
        var key = "transactions:user123:20250901-20251014:all:1:50";
        var transactions = new List<TransactionDto>
        {
            new TransactionDto
            {
                Id = "TX-1",
                Date = DateTime.UtcNow,
                Amount = 100.50m,
                Currency = "USD",
                Category = "Test",
                Source = "BankA"
            }
        }.AsReadOnly();

        await _cache.SetAsync(key, transactions, TimeSpan.FromMinutes(10));

        // Act
        await _cache.GetAsync(key);

        // Assert - Verify logging was called (cache hit)
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Cache HIT")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task GetAsync_ShouldLogCacheMiss_WhenValueDoesNotExist()
    {
        // Arrange
        var key = "transactions:nonexistent:20250901-20251014:all:1:50";

        // Act
        await _cache.GetAsync(key);

        // Assert - Verify logging was called (cache miss)
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Cache MISS")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}
