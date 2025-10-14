using TransactionAggregationApi.Api.Models;

namespace TransactionAggregationApi.Api.Infrastructure;

/// <summary>
/// Abstraction for caching aggregated transaction data.
/// TODO: Migrate to IDistributedCache for Redis support in distributed environments.
/// </summary>
public interface ITransactionCache
{
    /// <summary>
    /// Retrieves cached transactions by key.
    /// </summary>
    /// <param name="key">Cache key (e.g., "transactions:user123:20250901-20251014:Groceries:1:50")</param>
    /// <returns>Cached transactions or null if not found</returns>
    Task<IReadOnlyList<TransactionDto>?> GetAsync(string key);

    /// <summary>
    /// Stores transactions in cache with specified TTL.
    /// </summary>
    /// <param name="key">Cache key</param>
    /// <param name="value">Transactions to cache</param>
    /// <param name="ttl">Time to live for cache entry</param>
    Task SetAsync(string key, IReadOnlyList<TransactionDto> value, TimeSpan ttl);

    /// <summary>
    /// Removes a specific cache entry.
    /// </summary>
    /// <param name="key">Cache key to remove</param>
    void Remove(string key);

    /// <summary>
    /// Clears all cache entries.
    /// </summary>
    void Clear();
}
