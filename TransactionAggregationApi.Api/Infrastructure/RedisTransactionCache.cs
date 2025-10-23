using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using TransactionAggregationApi.Api.Models;

namespace TransactionAggregationApi.Api.Infrastructure;

/// <summary>
/// Redis-based implementation of transaction cache using IDistributedCache.
/// Provides distributed caching with JSON serialization, configurable TTL, and comprehensive logging.
/// Suitable for production and distributed environments.
/// </summary>
public class RedisTransactionCache : ITransactionCache
{
    private readonly IDistributedCache _cache;
    private readonly ILogger<RedisTransactionCache> _logger;
    private readonly IConfiguration _configuration;
    private readonly TimeSpan _defaultTtl;

    /// <summary>
    /// Initializes a new instance of RedisTransactionCache.
    /// </summary>
    /// <param name="cache">IDistributedCache instance (Redis implementation)</param>
    /// <param name="logger">Logger for cache operations</param>
    /// <param name="configuration">Configuration for reading cache settings</param>
    public RedisTransactionCache(
        IDistributedCache cache,
        ILogger<RedisTransactionCache> logger,
        IConfiguration configuration)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));

        // Read default TTL from configuration, fallback to 10 minutes
        var ttlMinutes = _configuration.GetValue<int>("Cache:DefaultTtlMinutes", 10);
        _defaultTtl = TimeSpan.FromMinutes(ttlMinutes);

        _logger.LogInformation(
            "RedisTransactionCache initialized with default TTL: {TtlMinutes} minutes",
            ttlMinutes);
    }

    /// <summary>
    /// Retrieves cached transactions by key.
    /// Key pattern: transactions:{userId}:{from:yyyyMMdd}-{to:yyyyMMdd}:{category}:{page}:{pageSize}
    /// Example: "transactions:user123:20250901-20251014:Groceries:1:50"
    /// </summary>
    public async Task<IReadOnlyList<TransactionDto>?> GetAsync(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Cache key cannot be null or empty", nameof(key));
        }

        try
        {
            var cachedBytes = await _cache.GetAsync(key);

            if (cachedBytes != null && cachedBytes.Length > 0)
            {
                var cachedValue = JsonSerializer.Deserialize<List<TransactionDto>>(cachedBytes);

                if (cachedValue != null)
                {
                    _logger.LogInformation(
                        "Cache HIT (redis) for key: {Key}, returned {Count} transactions",
                        key,
                        cachedValue.Count);
                    return cachedValue;
                }

                // Deserialization failed - evict corrupted entry
                _logger.LogWarning(
                    "Cache entry deserialization failed for key: {Key}, evicting corrupted entry",
                    key);
                await _cache.RemoveAsync(key);
                return null;
            }

            _logger.LogInformation("Cache MISS (redis) for key: {Key}", key);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error retrieving cache entry for key: {Key}, returning null",
                key);
            return null;
        }
    }

    /// <summary>
    /// Stores transactions in cache with specified TTL.
    /// Serializes data to JSON before storing in Redis.
    /// </summary>
    public async Task SetAsync(string key, IReadOnlyList<TransactionDto> value, TimeSpan ttl)
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

        try
        {
            var serializedValue = JsonSerializer.SerializeToUtf8Bytes(value);

            var cacheEntryOptions = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = ttl
            };

            await _cache.SetAsync(key, serializedValue, cacheEntryOptions);

            _logger.LogInformation(
                "Cache SET (redis) for key: {Key}, stored {Count} transactions, TTL: {Ttl} minutes",
                key,
                value.Count,
                ttl.TotalMinutes);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error setting cache entry for key: {Key}",
                key);
            throw;
        }
    }

    /// <summary>
    /// Removes a specific cache entry from Redis.
    /// </summary>
    public void Remove(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Cache key cannot be null or empty", nameof(key));
        }

        try
        {
            _cache.Remove(key);
            _logger.LogInformation("Cache entry REMOVED (redis) for key: {Key}", key);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error removing cache entry for key: {Key}",
                key);
            throw;
        }
    }

    /// <summary>
    /// Clears all cache entries.
    /// Note: Redis IDistributedCache does not provide a built-in clear/flush operation.
    /// This implementation logs a warning as clearing requires direct Redis commands.
    /// </summary>
    public void Clear()
    {
        _logger.LogWarning(
            "Cache CLEAR (redis) requested but IDistributedCache does not support flush operations. " +
            "Use Redis CLI 'FLUSHDB' command or implement custom clear logic if needed.");
    }
}
