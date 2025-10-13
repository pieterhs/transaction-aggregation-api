namespace TransactionAggregationApi.Api.Infrastructure;

public interface ITransactionCache
{
    Task<T?> GetOrCreateAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiration = null);
    void Remove(string key);
    void Clear();
}
