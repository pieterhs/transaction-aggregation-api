using Microsoft.AspNetCore.Mvc;
using TransactionAggregationApi.Api.Models;
using TransactionAggregationApi.Api.Services;

namespace TransactionAggregationApi.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TransactionsController : ControllerBase
{
    private readonly TransactionService _transactionService;
    private readonly ILogger<TransactionsController> _logger;

    public TransactionsController(
        TransactionService transactionService,
        ILogger<TransactionsController> logger)
    {
        _transactionService = transactionService;
        _logger = logger;
    }

    /// <summary>
    /// Get aggregated transactions from multiple banks
    /// </summary>
    /// <param name="from">Start date for transactions (YYYY-MM-DD)</param>
    /// <param name="to">End date for transactions (YYYY-MM-DD)</param>
    /// <param name="category">Optional category filter</param>
    /// <param name="page">Page number (default: 1)</param>
    /// <param name="pageSize">Page size (default: 50, max: 100)</param>
    /// <returns>List of transactions</returns>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<TransactionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetTransactions(
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] string? category = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        try
        {
            // Default to last 30 days if not specified
            var fromDate = from ?? DateTime.UtcNow.AddDays(-30);
            var toDate = to ?? DateTime.UtcNow;

            // Validate dates
            if (fromDate > toDate)
            {
                return BadRequest("'from' date must be before 'to' date");
            }

            // Validate pagination
            if (page < 1)
            {
                return BadRequest("Page must be greater than 0");
            }

            if (pageSize < 1 || pageSize > 100)
            {
                return BadRequest("Page size must be between 1 and 100");
            }

            _logger.LogInformation(
                "Getting transactions from {From} to {To}, category: {Category}, page: {Page}, pageSize: {PageSize}",
                fromDate, toDate, category ?? "all", page, pageSize);

            var transactions = await _transactionService.GetTransactionsAsync(
                fromDate, toDate, category, page, pageSize);

            return Ok(transactions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving transactions");
            return StatusCode(500, "An error occurred while retrieving transactions");
        }
    }
}
