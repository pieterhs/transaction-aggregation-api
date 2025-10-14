using TransactionAggregationApi.Api.Models;

namespace TransactionAggregationApi.Api.Clients;

/// <summary>
/// Mock implementation of Bank B client.
/// Simulates integration with Bank B's transaction API.
/// </summary>
/// <remarks>
/// TODO: Replace with actual Bank B API integration
/// - API Endpoint: https://api.bankb.eu/transactions
/// - Authentication: API Key in X-API-Key header
/// - Rate Limits: 500 requests per hour
/// - Supports EUR and GBP currencies
/// - Documentation: https://docs.bankb.eu/api
/// </remarks>
public class BankBClient : IBankClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<BankBClient> _logger;
    private readonly Random _random = new();
    private static int _requestCount = 0;

    public string Name => "BankB";

    public BankBClient(HttpClient httpClient, ILogger<BankBClient> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Fetches transactions from Bank B's mock API.
    /// Bank B has slightly higher latency and different failure patterns.
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
            // Bank B has slightly higher latency (150-900ms)
            var delayMs = _random.Next(150, 900);
            _logger.LogDebug("[{BankName}] ({RequestId}) Simulating network delay: {DelayMs}ms", Name, requestId, delayMs);
            await Task.Delay(delayMs);

            // Simulate occasional transient failures (8% chance - slightly more reliable than Bank A)
            if (_random.Next(100) < 8)
            {
                _logger.LogWarning("[{BankName}] ({RequestId}) Simulating transient failure", Name, requestId);
                throw new HttpRequestException($"[{Name}] Simulated API gateway timeout - 504 Gateway Timeout");
            }

            // TODO: Replace with actual API call
            // var request = new HttpRequestMessage(HttpMethod.Get, 
            //     $"/transactions?startDate={from:yyyy-MM-dd}&endDate={to:yyyy-MM-dd}&category={category}");
            // request.Headers.Add("X-API-Key", _apiKey);
            // var response = await _httpClient.SendAsync(request);
            // var apiData = await response.Content.ReadFromJsonAsync<BankBTransactionList>();
            
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
    /// Generates mock transaction data for Bank B.
    /// Bank B is European-based with EUR transactions and business focus.
    /// </summary>
    private IReadOnlyList<TransactionDto> GenerateMockTransactions(
        DateTime from, 
        DateTime to, 
        string? category)
    {
        var transactions = new List<TransactionDto>();

        // Bank B mock data: European business/corporate transactions in EUR
        var mockData = new[]
        {
            new { Amount = 350.00m, Category = "Utilities", DaysAgo = 1, Currency = "EUR" },
            new { Amount = 125.50m, Category = "Dining", DaysAgo = 3, Currency = "EUR" },
            new { Amount = 890.00m, Category = "Business Services", DaysAgo = 4, Currency = "EUR" },
            new { Amount = 67.99m, Category = "Entertainment", DaysAgo = 6, Currency = "EUR" },
            new { Amount = 450.00m, Category = "Transportation", DaysAgo = 8, Currency = "EUR" },
            new { Amount = 1200.00m, Category = "Business Services", DaysAgo = 12, Currency = "EUR" },
            new { Amount = 85.00m, Category = "Dining", DaysAgo = 14, Currency = "EUR" },
            new { Amount = 220.50m, Category = "Shopping", DaysAgo = 18, Currency = "EUR" },
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
                Id = $"BANKB-{Guid.NewGuid()}",
                Date = transactionDate,
                Amount = item.Amount,
                Currency = item.Currency,
                Category = item.Category,
                Source = Name
            });
        }

        return transactions.AsReadOnly();
    }
}
