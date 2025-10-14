using Polly;
using Polly.Timeout;
using TransactionAggregationApi.Api.Clients;
using TransactionAggregationApi.Api.Infrastructure;
using TransactionAggregationApi.Api.Models;

namespace TransactionAggregationApi.Api.Services;

/// <summary>
/// Service for aggregating customer transactions from multiple banking systems.
/// Implements caching, resilience patterns, and concurrent data fetching.
/// </summary>
public class TransactionService : ITransactionService
{
    private readonly IEnumerable<IBankClient> _bankClients;
    private readonly ITransactionCache _cache;
    private readonly ILogger<TransactionService> _logger;
    private readonly IAsyncPolicy _resiliencePolicy;

    // TODO: Add metrics collection for monitoring
    // - Cache hit/miss rate
    // - Latency per bank client
    // - Success/failure rates
    // - Transaction volume trends

    public TransactionService(
        IEnumerable<IBankClient> bankClients,
        ITransactionCache cache,
        ILogger<TransactionService> logger)
    {
        _bankClients = bankClients ?? throw new ArgumentNullException(nameof(bankClients));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Build composite resilience policy
        _resiliencePolicy = BuildResiliencePolicy();
    }

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
    public async Task<PagedResultDto<TransactionDto>> GetTransactionsAsync(
        DateTime from,
        DateTime to,
        string? category = null,
        int page = 1,
        int pageSize = 50,
        string? userId = null)
    {
        try
        {
            // Validate input parameters
            ValidateInputParameters(from, to, page, pageSize);

            // Generate cache key including all query parameters
            var cacheKey = GenerateCacheKey(userId, from, to, category, page, pageSize);

            _logger.LogInformation(
                "Processing transaction request: From={From}, To={To}, Category={Category}, Page={Page}, PageSize={PageSize}, CacheKey={CacheKey}",
                from, to, category ?? "all", page, pageSize, cacheKey);

            // Try to get from cache first
            var allTransactions = await _cache.GetAsync(cacheKey);

            // Cache miss - fetch from banks and cache the result
            if (allTransactions == null)
            {
                _logger.LogDebug("Cache miss - fetching from banks");
                var fetchedTransactions = await FetchTransactionsFromBanksAsync(from, to);
                
                if (fetchedTransactions != null && fetchedTransactions.Any())
                {
                    // Convert to read-only list and cache
                    var transactionList = fetchedTransactions.ToList().AsReadOnly();
                    await _cache.SetAsync(cacheKey, transactionList, TransactionCache.GetDefaultTtl());
                    allTransactions = transactionList;
                }
            }

            if (allTransactions == null || !allTransactions.Any())
            {
                _logger.LogWarning("No transactions found for the specified criteria");
                return CreateEmptyPagedResult(page, pageSize);
            }

            // Apply filters and pagination
            var pagedResult = ApplyFiltersAndPagination(allTransactions, category, page, pageSize);

            _logger.LogInformation(
                "Successfully retrieved {Count} transactions (Total: {Total}, Page: {Page}/{TotalPages})",
                pagedResult.Transactions.Count(),
                pagedResult.Total,
                pagedResult.Page,
                pagedResult.TotalPages);

            return pagedResult;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in GetTransactionsAsync");
            // Return empty result gracefully on failures
            return CreateEmptyPagedResult(page, pageSize);
        }
    }

    /// <summary>
    /// Fetches transactions concurrently from all bank clients with resilience policies.
    /// </summary>
    private async Task<IEnumerable<TransactionDto>> FetchTransactionsFromBanksAsync(DateTime from, DateTime to)
    {
        _logger.LogInformation("Initiating concurrent fetch from {BankCount} banks", _bankClients.Count());

        var startTime = DateTimeOffset.UtcNow;

        try
        {
            // Execute all bank client requests concurrently
            var fetchTasks = _bankClients.Select(client => 
                FetchFromBankWithResilienceAsync(client, from, to));
            
            var results = await Task.WhenAll(fetchTasks);

            // Aggregate all transactions from all banks
            var allTransactions = results
                .Where(r => r != null)
                .SelectMany(r => r!)
                .ToList();

            var elapsedMs = (DateTimeOffset.UtcNow - startTime).TotalMilliseconds;

            _logger.LogInformation(
                "Successfully aggregated {Count} transactions from {BankCount} banks in {ElapsedMs}ms",
                allTransactions.Count,
                _bankClients.Count(),
                elapsedMs);

            // TODO: Track latency metrics here
            // MetricsCollector.RecordAggregationLatency(elapsedMs);

            return allTransactions;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Critical error during concurrent bank fetch operations");
            // Return empty collection rather than throwing to handle partial failures gracefully
            return Enumerable.Empty<TransactionDto>();
        }
    }

    /// <summary>
    /// Fetches transactions from a single bank with resilience policies applied.
    /// </summary>
    private async Task<IEnumerable<TransactionDto>> FetchFromBankWithResilienceAsync(
        IBankClient client, 
        DateTime from, 
        DateTime to)
    {
        var bankName = client.Name;
        var startTime = DateTimeOffset.UtcNow;

        try
        {
            _logger.LogDebug("Fetching transactions from {BankName}", bankName);

            // Execute with resilience policy (retry, circuit breaker, timeout)
            var transactions = await _resiliencePolicy.ExecuteAsync(async () =>
            {
                return await client.GetTransactionsAsync(from, to, category: null);
            });

            var elapsedMs = (DateTimeOffset.UtcNow - startTime).TotalMilliseconds;

            _logger.LogInformation(
                "Successfully fetched {Count} transactions from {BankName} in {ElapsedMs}ms",
                transactions.Count,
                bankName,
                elapsedMs);

            // TODO: Track per-bank latency metrics
            // MetricsCollector.RecordBankLatency(bankName, elapsedMs);

            return transactions;
        }
        catch (TimeoutRejectedException ex)
        {
            _logger.LogWarning(ex, 
                "Timeout occurred while fetching from {BankName} after {ElapsedMs}ms", 
                bankName, 
                (DateTimeOffset.UtcNow - startTime).TotalMilliseconds);
            
            // TODO: Track timeout metrics
            // MetricsCollector.RecordBankTimeout(bankName);
            
            return Enumerable.Empty<TransactionDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "Error fetching transactions from {BankName} after {ElapsedMs}ms", 
                bankName, 
                (DateTimeOffset.UtcNow - startTime).TotalMilliseconds);
            
            // TODO: Track failure metrics
            // MetricsCollector.RecordBankFailure(bankName, ex.GetType().Name);
            
            // Return empty collection to handle partial failures gracefully
            return Enumerable.Empty<TransactionDto>();
        }
    }

    /// <summary>
    /// Builds a composite resilience policy combining retry, circuit breaker, and timeout.
    /// </summary>
    private IAsyncPolicy BuildResiliencePolicy()
    {
        // Retry policy: 3 retries with exponential backoff
        var retryPolicy = Policy
            .Handle<HttpRequestException>()
            .Or<TimeoutException>()
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                onRetry: (exception, timeSpan, retryCount, context) =>
                {
                    _logger.LogWarning(
                        exception,
                        "Retry {RetryCount} after {Delay}s due to {ExceptionType}",
                        retryCount,
                        timeSpan.TotalSeconds,
                        exception.GetType().Name);
                });

        // Circuit breaker: Open after 5 consecutive failures, reset after 30s
        var circuitBreakerPolicy = Policy
            .Handle<HttpRequestException>()
            .Or<TimeoutException>()
            .CircuitBreakerAsync(
                exceptionsAllowedBeforeBreaking: 5,
                durationOfBreak: TimeSpan.FromSeconds(30),
                onBreak: (exception, duration) =>
                {
                    _logger.LogError(
                        exception,
                        "Circuit breaker opened for {Duration}s due to {ExceptionType}",
                        duration.TotalSeconds,
                        exception.GetType().Name);
                },
                onReset: () =>
                {
                    _logger.LogInformation("Circuit breaker reset - resuming normal operations");
                },
                onHalfOpen: () =>
                {
                    _logger.LogInformation("Circuit breaker half-open - testing with next request");
                });

        // Timeout policy: 5 seconds per bank API call
        var timeoutPolicy = Policy
            .TimeoutAsync(
                timeout: TimeSpan.FromSeconds(5),
                onTimeoutAsync: async (context, timespan, task) =>
                {
                    _logger.LogWarning("Operation timed out after {Timeout}s", timespan.TotalSeconds);
                    await Task.CompletedTask;
                });

        // Wrap policies: Timeout -> Retry -> Circuit Breaker (innermost to outermost)
        return Policy.WrapAsync(circuitBreakerPolicy, retryPolicy, timeoutPolicy);
    }

    /// <summary>
    /// Applies category filter, sorting, and pagination to transaction collection.
    /// </summary>
    private PagedResultDto<TransactionDto> ApplyFiltersAndPagination(
        IEnumerable<TransactionDto> transactions,
        string? category,
        int page,
        int pageSize)
    {
        // Filter by category if specified
        var filteredTransactions = string.IsNullOrWhiteSpace(category)
            ? transactions
            : transactions.Where(t => t.Category.Equals(category, StringComparison.OrdinalIgnoreCase));

        // Sort by date descending (most recent first)
        var sortedTransactions = filteredTransactions
            .OrderByDescending(t => t.Date)
            .ToList();

        var total = sortedTransactions.Count;
        var totalPages = (int)Math.Ceiling(total / (double)pageSize);

        // Apply pagination
        var pagedTransactions = sortedTransactions
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return new PagedResultDto<TransactionDto>
        {
            Total = total,
            Page = page,
            PageSize = pageSize,
            TotalPages = totalPages,
            Transactions = pagedTransactions
        };
    }

    /// <summary>
    /// Generates a cache key based on query parameters.
    /// Uses TransactionCache.BuildCacheKey for consistency.
    /// Pattern: transactions:{userId}:{from:yyyyMMdd}-{to:yyyyMMdd}:{category}:{page}:{pageSize}
    /// </summary>
    private static string GenerateCacheKey(
        string? userId,
        DateTime from,
        DateTime to,
        string? category,
        int page,
        int pageSize)
    {
        return TransactionCache.BuildCacheKey(userId, from, to, category, page, pageSize);
    }

    /// <summary>
    /// Validates input parameters for GetTransactionsAsync.
    /// </summary>
    private static void ValidateInputParameters(DateTime from, DateTime to, int page, int pageSize)
    {
        if (from > to)
        {
            throw new ArgumentException("'from' date must be before or equal to 'to' date");
        }

        if (page < 1)
        {
            throw new ArgumentException("Page number must be greater than 0", nameof(page));
        }

        if (pageSize < 1 || pageSize > 100)
        {
            throw new ArgumentException("Page size must be between 1 and 100", nameof(pageSize));
        }
    }

    /// <summary>
    /// Creates an empty paged result for error scenarios.
    /// </summary>
    private static PagedResultDto<TransactionDto> CreateEmptyPagedResult(int page, int pageSize)
    {
        return new PagedResultDto<TransactionDto>
        {
            Total = 0,
            Page = page,
            PageSize = pageSize,
            TotalPages = 0,
            Transactions = Enumerable.Empty<TransactionDto>()
        };
    }
}
