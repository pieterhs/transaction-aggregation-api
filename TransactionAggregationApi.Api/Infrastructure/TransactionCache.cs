using Microsoft.Extensions.Caching.Memory;
using TransactionAggregationApi.Api.Models;

namespace TransactionAggregationApi.Api.Infrastructure;

/// <summary>
/// In-memory implementation of transaction cache.
/// Provides thread-safe caching using IMemoryCache.
/// TODO: Migrate to IDistributedCache for Redis support in distributed/production environments.
/// TODO: Add cache statistics (hit/miss rate, entry count) for monitoring.
/// </summary>
public class TransactionCache : ITransactionCache
{
    private readonly IMemoryCache _cache;
    private readonly ILogger<TransactionCache> _logger;

    // Default TTL constant - can be overridden via appsettings.json
    private const int DefaultTtlMinutes = 10;

    /// <summary>
    /// Initializes a new instance of TransactionCache.
    /// </summary>
    /// <param name="cache">IMemoryCache instance (thread-safe by default)</param>
    /// <param name="logger">Logger for cache operations</param>
    public TransactionCache(IMemoryCache cache, ILogger<TransactionCache> logger)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Retrieves cached transactions by key.
    /// Key pattern: transactions:{userId}:{from:yyyyMMdd}-{to:yyyyMMdd}:{category}:{page}:{pageSize}
    /// Example: "transactions:user123:20250901-20251014:Groceries:1:50"
    /// </summary>
    public virtual Task<IReadOnlyList<TransactionDto>?> GetAsync(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Cache key cannot be null or empty", nameof(key));
        }

        if (_cache.TryGetValue(key, out IReadOnlyList<TransactionDto>? cachedValue))
        {
            _logger.LogInformation(
                "Cache HIT for key: {Key}, returned {Count} transactions",
                key,
                cachedValue?.Count ?? 0);
            return Task.FromResult(cachedValue);
        }

        _logger.LogInformation("Cache MISS for key: {Key}", key);
        return Task.FromResult<IReadOnlyList<TransactionDto>?>(null);
    }

    /// <summary>
    /// Stores transactions in cache with specified TTL.
    /// IMemoryCache provides thread-safe operations.
    /// </summary>
    public virtual Task SetAsync(string key, IReadOnlyList<TransactionDto> value, TimeSpan ttl)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Cache key cannot be null or empty", nameof(key));
        }

        if (value == null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        if (ttl <= TimeSpan.Zero)
        {
            throw new ArgumentException("TTL must be greater than zero", nameof(ttl));
        }

        var cacheEntryOptions = new MemoryCacheEntryOptions()
            .SetAbsoluteExpiration(ttl)
            .SetSize(value.Count); // Size for eviction policy if configured

        _cache.Set(key, value, cacheEntryOptions);

        _logger.LogInformation(
            "Cache SET for key: {Key}, stored {Count} transactions, TTL: {Ttl} minutes",
            key,
            value.Count,
            ttl.TotalMinutes);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Removes a specific cache entry.
    /// </summary>
    public virtual void Remove(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Cache key cannot be null or empty", nameof(key));
        }

        _cache.Remove(key);
        _logger.LogInformation("Cache entry REMOVED for key: {Key}", key);
    }

    /// <summary>
    /// Clears all cache entries by compacting the memory cache.
    /// Note: This is a best-effort operation for MemoryCache.
    /// TODO: When migrating to Redis, implement proper cache flush.
    /// </summary>
    public virtual void Clear()
    {
        if (_cache is MemoryCache memoryCache)
        {
            memoryCache.Compact(1.0); // Remove 100% of entries
            _logger.LogWarning("Cache CLEARED - all entries removed via compaction");
        }
        else
        {
            _logger.LogWarning("Cache CLEAR attempted but cache type does not support compaction");
        }
    }

    /// <summary>
    /// Helper method to generate cache keys consistently.
    /// Pattern: transactions:{userId}:{from:yyyyMMdd}-{to:yyyyMMdd}:{category}:{page}:{pageSize}
    /// </summary>
    /// <example>
    /// BuildCacheKey("user123", new DateTime(2025, 9, 1), new DateTime(2025, 10, 14), "Groceries", 1, 50)
    /// Returns: "transactions:user123:20250901-20251014:Groceries:1:50"
    /// </example>
    public static string BuildCacheKey(
        string? userId,
        DateTime from,
        DateTime to,
        string? category,
        int page,
        int pageSize)
    {
        var userPart = string.IsNullOrEmpty(userId) ? "anonymous" : userId;
        var categoryPart = string.IsNullOrEmpty(category) ? "all" : category;
        var dateRange = $"{from:yyyyMMdd}-{to:yyyyMMdd}";

        return $"transactions:{userPart}:{dateRange}:{categoryPart}:{page}:{pageSize}";
    }

    /// <summary>
    /// Gets the default TTL for cache entries.
    /// TODO: Make this configurable via IConfiguration/appsettings.json
    /// </summary>
    public static TimeSpan GetDefaultTtl() => TimeSpan.FromMinutes(DefaultTtlMinutes);
}
