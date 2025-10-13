using Microsoft.Extensions.Caching.Memory;

namespace TransactionAggregationApi.Api.Infrastructure;

public class TransactionCache : ITransactionCache
{
    private readonly IMemoryCache _cache;
    private readonly ILogger<TransactionCache> _logger;

    public TransactionCache(IMemoryCache cache, ILogger<TransactionCache> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public virtual async Task<T?> GetOrCreateAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiration = null)
    {
        if (_cache.TryGetValue(key, out T? cachedValue))
        {
            _logger.LogInformation("Cache hit for key: {Key}", key);
            return cachedValue;
        }

        _logger.LogInformation("Cache miss for key: {Key}", key);
        var value = await factory();

        var cacheEntryOptions = new MemoryCacheEntryOptions()
            .SetAbsoluteExpiration(expiration ?? TimeSpan.FromMinutes(5));

        _cache.Set(key, value, cacheEntryOptions);

        return value;
    }

    public virtual void Remove(string key)
    {
        _cache.Remove(key);
        _logger.LogInformation("Cache entry removed for key: {Key}", key);
    }

    public virtual void Clear()
    {
        if (_cache is MemoryCache memoryCache)
        {
            memoryCache.Compact(1.0);
            _logger.LogInformation("Cache cleared");
        }
    }
}
