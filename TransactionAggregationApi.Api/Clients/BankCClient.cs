using TransactionAggregationApi.Api.Models;

namespace TransactionAggregationApi.Api.Clients;

/// <summary>
/// Mock implementation of Bank C client.
/// Simulates integration with Bank C's transaction API.
/// </summary>
/// <remarks>
/// TODO: Replace with actual Bank C API integration
/// - API Endpoint: https://api.bankc.asia/v2/transactions
/// - Authentication: JWT Bearer token
/// - Rate Limits: 2000 requests per hour
/// - Supports multiple Asian currencies (JPY, CNY, SGD)
/// - WebSocket support for real-time updates
/// - Documentation: https://developer.bankc.asia
/// </remarks>
public class BankCClient : IBankClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<BankCClient> _logger;
    private readonly Random _random = new();
    private static int _requestCount = 0;

    public string Name => "BankC";

    public BankCClient(HttpClient httpClient, ILogger<BankCClient> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Fetches transactions from Bank C's mock API.
    /// Bank C is the most reliable but serves Asian markets with different currencies.
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
            // Bank C has moderate latency (120-700ms)
            var delayMs = _random.Next(120, 700);
            _logger.LogDebug("[{BankName}] ({RequestId}) Simulating network delay: {DelayMs}ms", Name, requestId, delayMs);
            await Task.Delay(delayMs);

            // Bank C is most reliable - only 5% failure rate
            if (_random.Next(100) < 5)
            {
                _logger.LogWarning("[{BankName}] ({RequestId}) Simulating transient failure", Name, requestId);
                throw new HttpRequestException($"[{Name}] Simulated connection reset - Connection lost");
            }

            // TODO: Replace with actual API call
            // var request = new HttpRequestMessage(HttpMethod.Post, "/v2/transactions/query");
            // request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _jwtToken);
            // request.Content = JsonContent.Create(new {
            //     startDate = from,
            //     endDate = to,
            //     category = category,
            //     includeMetadata = true
            // });
            // var response = await _httpClient.SendAsync(request);
            // var result = await response.Content.ReadFromJsonAsync<BankCApiResponse>();
            
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
    /// Generates mock transaction data for Bank C.
    /// Bank C serves Asian markets with JPY, CNY, and SGD transactions.
    /// </summary>
    private IReadOnlyList<TransactionDto> GenerateMockTransactions(
        DateTime from, 
        DateTime to, 
        string? category)
    {
        var transactions = new List<TransactionDto>();

        // Bank C mock data: Asian market transactions with mixed currencies
        var mockData = new[]
        {
            new { Amount = 12500m, Category = "Shopping", DaysAgo = 2, Currency = "JPY" },
            new { Amount = 850m, Category = "Transportation", DaysAgo = 4, Currency = "CNY" },
            new { Amount = 180.50m, Category = "Dining", DaysAgo = 5, Currency = "SGD" },
            new { Amount = 25000m, Category = "Shopping", DaysAgo = 7, Currency = "JPY" },
            new { Amount = 420.00m, Category = "Entertainment", DaysAgo = 9, Currency = "CNY" },
            new { Amount = 95.75m, Category = "Groceries", DaysAgo = 11, Currency = "SGD" },
            new { Amount = 15750m, Category = "Utilities", DaysAgo = 13, Currency = "JPY" },
            new { Amount = 680.00m, Category = "Transportation", DaysAgo = 16, Currency = "CNY" },
            new { Amount = 145.00m, Category = "Shopping", DaysAgo = 19, Currency = "SGD" },
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
                Id = $"BANKC-{Guid.NewGuid()}",
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
