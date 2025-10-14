using Microsoft.AspNetCore.Mvc;
using TransactionAggregationApi.Api.Models;
using TransactionAggregationApi.Api.Services;

namespace TransactionAggregationApi.Api.Controllers;

/// <summary>
/// Controller for managing transaction aggregation from multiple banking systems.
/// Provides endpoints to retrieve, filter, and paginate transactions.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class TransactionsController : ControllerBase
{
    private readonly ITransactionService _transactionService;
    private readonly ILogger<TransactionsController> _logger;

    /// <summary>
    /// Initializes a new instance of the TransactionsController.
    /// </summary>
    /// <param name="transactionService">Service for transaction aggregation operations</param>
    /// <param name="logger">Logger for diagnostic information</param>
    public TransactionsController(
        ITransactionService transactionService,
        ILogger<TransactionsController> logger)
    {
        _transactionService = transactionService ?? throw new ArgumentNullException(nameof(transactionService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Retrieves aggregated transactions from multiple banks with filtering and pagination.
    /// </summary>
    /// <remarks>
    /// Sample request:
    /// 
    ///     GET /api/transactions?from=2025-10-01&amp;to=2025-10-10&amp;category=Food&amp;page=1&amp;pageSize=20
    /// 
    /// This endpoint aggregates transactions from all connected bank APIs, applies filters,
    /// and returns paginated results. Results are cached for improved performance.
    /// 
    /// **Date Format:** ISO 8601 (YYYY-MM-DD) or full DateTime (YYYY-MM-DDTHH:mm:ss)
    /// 
    /// **Categories:** Groceries, Entertainment, Dining, Shopping, Transportation, Utilities, etc.
    /// </remarks>
    /// <param name="from">Start date for transaction filter (inclusive). Required.</param>
    /// <param name="to">End date for transaction filter (inclusive). Required.</param>
    /// <param name="category">Optional category filter (e.g., "Groceries", "Entertainment"). Case-insensitive.</param>
    /// <param name="page">Page number for pagination (minimum: 1, default: 1).</param>
    /// <param name="pageSize">Number of items per page (range: 1-100, default: 50).</param>
    /// <returns>Paginated list of transactions with metadata.</returns>
    /// <response code="200">Returns the paginated list of transactions.</response>
    /// <response code="400">Invalid request parameters (e.g., from &gt; to, invalid page/pageSize).</response>
    /// <response code="401">Missing or invalid API key.</response>
    /// <response code="500">Unexpected server error during transaction retrieval.</response>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResultDto<TransactionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetTransactions(
        [FromQuery] DateTime from,
        [FromQuery] DateTime to,
        [FromQuery] string? category = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        try
        {
            // Validate input parameters
            var validationError = ValidateParameters(from, to, page, pageSize);
            if (validationError != null)
            {
                _logger.LogWarning(
                    "Invalid request parameters: {Error}. From={From}, To={To}, Page={Page}, PageSize={PageSize}",
                    validationError, from, to, page, pageSize);
                return BadRequest(new { error = validationError });
            }

            _logger.LogInformation(
                "Retrieving transactions: From={From}, To={To}, Category={Category}, Page={Page}, PageSize={PageSize}",
                from, to, category ?? "all", page, pageSize);

            // Call service to get paginated transactions
            var result = await _transactionService.GetTransactionsAsync(
                from, to, category, page, pageSize);

            // Add pagination headers for client convenience
            Response.Headers["X-Total-Count"] = result.Total.ToString();
            Response.Headers["X-Page"] = result.Page.ToString();
            Response.Headers["X-PageSize"] = result.PageSize.ToString();
            Response.Headers["X-Total-Pages"] = result.TotalPages.ToString();

            _logger.LogInformation(
                "Successfully retrieved {Count} transactions (Total: {Total}, Page: {Page}/{TotalPages})",
                result.Transactions.Count(),
                result.Total,
                result.Page,
                result.TotalPages);

            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid argument in GetTransactions");
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error retrieving transactions");
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new ErrorResponse { Error = "Unexpected error occurred while retrieving transactions" });
        }
    }

    /// <summary>
    /// Retrieves pagination metadata for transactions without returning the full data.
    /// Useful for clients that need to know the total count before fetching data.
    /// </summary>
    /// <remarks>
    /// Sample request:
    /// 
    ///     HEAD /api/transactions?from=2025-10-01&amp;to=2025-10-10&amp;category=Food
    /// 
    /// Returns only HTTP headers with pagination information:
    /// - X-Total-Count: Total number of matching transactions
    /// - X-Page: Current page number
    /// - X-PageSize: Number of items per page
    /// - X-Total-Pages: Total number of pages
    /// 
    /// This is more efficient than GET when you only need the count.
    /// </remarks>
    /// <param name="from">Start date for transaction filter (inclusive). Required.</param>
    /// <param name="to">End date for transaction filter (inclusive). Required.</param>
    /// <param name="category">Optional category filter. Case-insensitive.</param>
    /// <param name="page">Page number (minimum: 1, default: 1).</param>
    /// <param name="pageSize">Items per page (range: 1-100, default: 50).</param>
    /// <returns>No content, only headers with pagination metadata.</returns>
    /// <response code="200">Success. Check response headers for pagination information.</response>
    /// <response code="400">Invalid request parameters.</response>
    /// <response code="401">Missing or invalid API key.</response>
    /// <response code="500">Unexpected server error.</response>
    [HttpHead]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetTransactionsMetadata(
        [FromQuery] DateTime from,
        [FromQuery] DateTime to,
        [FromQuery] string? category = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        try
        {
            // Validate input parameters
            var validationError = ValidateParameters(from, to, page, pageSize);
            if (validationError != null)
            {
                _logger.LogWarning(
                    "Invalid request parameters for HEAD: {Error}. From={From}, To={To}, Page={Page}, PageSize={PageSize}",
                    validationError, from, to, page, pageSize);
                return BadRequest();
            }

            _logger.LogDebug(
                "HEAD request for transactions metadata: From={From}, To={To}, Category={Category}, Page={Page}, PageSize={PageSize}",
                from, to, category ?? "all", page, pageSize);

            // Get pagination info (reuses the same service call)
            var result = await _transactionService.GetTransactionsAsync(
                from, to, category, page, pageSize);

            // Return only headers, no body
            Response.Headers["X-Total-Count"] = result.Total.ToString();
            Response.Headers["X-Page"] = result.Page.ToString();
            Response.Headers["X-PageSize"] = result.PageSize.ToString();
            Response.Headers["X-Total-Pages"] = result.TotalPages.ToString();

            return Ok();
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid argument in GetTransactionsMetadata");
            return BadRequest();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error retrieving transaction metadata");
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Validates request parameters for transaction queries.
    /// </summary>
    /// <returns>Error message if validation fails, null if valid.</returns>
    private static string? ValidateParameters(DateTime from, DateTime to, int page, int pageSize)
    {
        // Validate date range
        if (from > to)
        {
            return "Parameter 'from' must be less than or equal to 'to'";
        }

        // Validate date is not too far in the past or future
        var maxDateRange = DateTime.UtcNow.AddYears(1);
        var minDateRange = DateTime.UtcNow.AddYears(-10);

        if (from < minDateRange)
        {
            return $"Parameter 'from' cannot be more than 10 years in the past";
        }

        if (to > maxDateRange)
        {
            return $"Parameter 'to' cannot be more than 1 year in the future";
        }

        // Validate page number
        if (page < 1)
        {
            return "Parameter 'page' must be greater than or equal to 1";
        }

        // Validate page size
        if (pageSize < 1)
        {
            return "Parameter 'pageSize' must be greater than or equal to 1";
        }

        if (pageSize > 100)
        {
            return "Parameter 'pageSize' must be less than or equal to 100";
        }

        return null;
    }
}

/// <summary>
/// Error response model for API errors.
/// </summary>
public class ErrorResponse
{
    /// <summary>
    /// Error message describing what went wrong.
    /// </summary>
    public string Error { get; set; } = string.Empty;
}
