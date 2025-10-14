using TransactionAggregationApi.Api.Models;

namespace TransactionAggregationApi.Api.Clients;

/// <summary>
/// Interface for bank client implementations that fetch transaction data.
/// Supports integration with multiple banking systems.
/// </summary>
public interface IBankClient
{
    /// <summary>
    /// Retrieves transactions for a given date range and optional category filter.
    /// </summary>
    /// <param name="from">Start date for transaction filter</param>
    /// <param name="to">End date for transaction filter</param>
    /// <param name="category">Optional category filter</param>
    /// <returns>Read-only list of normalized transactions</returns>
    Task<IReadOnlyList<TransactionDto>> GetTransactionsAsync(DateTime from, DateTime to, string? category = null);

    /// <summary>
    /// Gets the unique name of the bank/financial institution.
    /// </summary>
    string Name { get; }
}
