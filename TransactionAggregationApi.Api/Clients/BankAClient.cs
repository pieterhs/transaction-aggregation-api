using TransactionAggregationApi.Api.Models;

namespace TransactionAggregationApi.Api.Clients;

/// <summary>
/// Mock implementation of Bank A client.
/// Simulates integration with Bank A's transaction API.
/// </summary>
/// <remarks>
/// TODO: Replace with actual Bank A API integration
/// - API Endpoint: https://api.banka.com/v1/transactions
/// - Authentication: OAuth 2.0 with client credentials
/// - Rate Limits: 1000 requests per hour
/// - Documentation: https://developer.banka.com/docs
/// </remarks>
public class BankAClient : IBankClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<BankAClient> _logger;
    private readonly Random _random = new();
    private static int _requestCount = 0;

    public string Name => "BankA";

    public BankAClient(HttpClient httpClient, ILogger<BankAClient> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Fetches transactions from Bank A's mock API.
    /// Includes artificial latency and occasional failures for testing resilience.
    /// </summary>
    public async Task<IReadOnlyList<TransactionDto>> GetTransactionsAsync(
        DateTime from, 
        DateTime to, 
        string? category = null)
    {
        var requestId = Guid.NewGuid().ToString("N")[..8];
        _requestCount++;

        _logger.LogInformation(
            "[{BankName}] Request #{RequestCount} ({RequestId}): Fetching transactions from {From} to {To}, Category: {Category}",
            Name, _requestCount, requestId, from.ToShortDateString(), to.ToShortDateString(), category ?? "all");

        try
        {
            // Simulate network latency (100-800ms)
            var delayMs = _random.Next(100, 800);
            _logger.LogDebug("[{BankName}] ({RequestId}) Simulating network delay: {DelayMs}ms", Name, requestId, delayMs);
            await Task.Delay(delayMs);

            // Simulate occasional transient failures (10% chance)
            // This tests Polly retry and circuit breaker policies
            if (_random.Next(100) < 10)
            {
                _logger.LogWarning("[{BankName}] ({RequestId}) Simulating transient failure", Name, requestId);
                throw new HttpRequestException($"[{Name}] Simulated transient HTTP failure - Service temporarily unavailable");
            }

            // TODO: Replace with actual API call
            // var response = await _httpClient.GetAsync($"/transactions?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}");
            // response.EnsureSuccessStatusCode();
            // var content = await response.Content.ReadAsStringAsync();
            // var apiTransactions = JsonSerializer.Deserialize<BankATransactionResponse>(content);
            
            // Generate mock transaction data
            var transactions = GenerateMockTransactions(from, to, category);

            _logger.LogInformation(
                "[{BankName}] ({RequestId}) Successfully fetched {Count} transactions",
                Name, requestId, transactions.Count);

            return transactions;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "[{BankName}] ({RequestId}) HTTP request failed", Name, requestId);
            throw; // Re-throw to allow Polly to handle retry
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{BankName}] ({RequestId}) Unexpected error", Name, requestId);
            throw;
        }
    }

    /// <summary>
    /// Generates mock transaction data for Bank A.
    /// Bank A specializes in retail transactions (USD).
    /// </summary>
    private IReadOnlyList<TransactionDto> GenerateMockTransactions(
        DateTime from, 
        DateTime to, 
        string? category)
    {
        var transactions = new List<TransactionDto>();

        // Bank A mock data: US-based retail transactions
        var mockData = new[]
        {
            new { Amount = 150.50m, Category = "Groceries", DaysAgo = 1 },
            new { Amount = 75.25m, Category = "Entertainment", DaysAgo = 2 },
            new { Amount = 42.99m, Category = "Dining", DaysAgo = 3 },
            new { Amount = 199.99m, Category = "Shopping", DaysAgo = 5 },
            new { Amount = 25.00m, Category = "Transportation", DaysAgo = 7 },
            new { Amount = 89.50m, Category = "Groceries", DaysAgo = 10 },
            new { Amount = 120.00m, Category = "Utilities", DaysAgo = 15 },
            new { Amount = 55.75m, Category = "Entertainment", DaysAgo = 20 },
        };

        foreach (var item in mockData)
        {
            var transactionDate = DateTime.UtcNow.AddDays(-item.DaysAgo);
            
            // Filter by date range
            if (transactionDate < from || transactionDate > to)
                continue;

            // Filter by category if specified
            if (!string.IsNullOrWhiteSpace(category) && 
                !item.Category.Equals(category, StringComparison.OrdinalIgnoreCase))
                continue;

            transactions.Add(new TransactionDto
            {
                Id = $"BANKA-{Guid.NewGuid()}",
                Date = transactionDate,
                Amount = item.Amount,
                Currency = "USD",
                Category = item.Category,
                Source = Name
            });
        }

        return transactions.AsReadOnly();
    }
}
