using TransactionAggregationApi.Api.Models;

namespace TransactionAggregationApi.Api.Services;

/// <summary>
/// Interface for transaction aggregation service.
/// Defines operations for retrieving and aggregating transactions from multiple banks.
/// </summary>
public interface ITransactionService
{
    /// <summary>
    /// Retrieves paginated transactions from multiple banks with filtering and caching.
    /// </summary>
    /// <param name="from">Start date for transaction filter</param>
    /// <param name="to">End date for transaction filter</param>
    /// <param name="category">Optional category filter</param>
    /// <param name="page">Page number (1-based)</param>
    /// <param name="pageSize">Number of items per page</param>
    /// <param name="userId">Optional user ID for cache key (for multi-tenancy)</param>
    /// <returns>Paged result containing transactions and metadata</returns>
    Task<PagedResultDto<TransactionDto>> GetTransactionsAsync(
        DateTime from,
        DateTime to,
        string? category = null,
        int page = 1,
        int pageSize = 50,
        string? userId = null);
}
