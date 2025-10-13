using TransactionAggregationApi.Api.Clients;
using TransactionAggregationApi.Api.Infrastructure;
using TransactionAggregationApi.Api.Models;

namespace TransactionAggregationApi.Api.Services;

public class TransactionService
{
    private readonly IEnumerable<IBankClient> _bankClients;
    private readonly TransactionCache _cache;
    private readonly ILogger<TransactionService> _logger;

    public TransactionService(
        IEnumerable<IBankClient> bankClients,
        TransactionCache cache,
        ILogger<TransactionService> logger)
    {
        _bankClients = bankClients;
        _cache = cache;
        _logger = logger;
    }

    public async Task<IEnumerable<TransactionDto>> GetTransactionsAsync(
        DateTime from,
        DateTime to,
        string? category = null,
        int page = 1,
        int pageSize = 50)
    {
        var cacheKey = $"transactions_{from:yyyyMMdd}_{to:yyyyMMdd}_{category}_{page}_{pageSize}";

        var transactions = await _cache.GetOrCreateAsync(
            cacheKey,
            async () => await FetchTransactionsFromBanksAsync(from, to),
            TimeSpan.FromMinutes(10)
        );

        if (transactions == null)
        {
            return Enumerable.Empty<TransactionDto>();
        }

        // Filter by category if provided
        if (!string.IsNullOrEmpty(category))
        {
            transactions = transactions.Where(t =>
                t.Category.Equals(category, StringComparison.OrdinalIgnoreCase));
        }

        // Sort by date descending
        transactions = transactions.OrderByDescending(t => t.Date);

        // Apply pagination
        var pagedTransactions = transactions
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        _logger.LogInformation(
            "Retrieved {Count} transactions for period {From} to {To}",
            pagedTransactions.Count,
            from,
            to);

        return pagedTransactions;
    }

    private async Task<IEnumerable<TransactionDto>> FetchTransactionsFromBanksAsync(DateTime from, DateTime to)
    {
        _logger.LogInformation("Fetching transactions from {BankCount} banks", _bankClients.Count());

        var tasks = _bankClients.Select(client => FetchFromBankAsync(client, from, to));
        var results = await Task.WhenAll(tasks);

        var allTransactions = results.SelectMany(t => t).ToList();

        _logger.LogInformation("Fetched total of {Count} transactions from all banks", allTransactions.Count);

        return allTransactions;
    }

    private async Task<IEnumerable<TransactionDto>> FetchFromBankAsync(IBankClient client, DateTime from, DateTime to)
    {
        try
        {
            _logger.LogInformation("Fetching transactions from {BankName}", client.BankName);
            var transactions = await client.GetTransactionsAsync(from, to);
            _logger.LogInformation("Successfully fetched {Count} transactions from {BankName}", 
                transactions.Count(), client.BankName);
            return transactions;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching transactions from {BankName}", client.BankName);
            return Enumerable.Empty<TransactionDto>();
        }
    }
}
