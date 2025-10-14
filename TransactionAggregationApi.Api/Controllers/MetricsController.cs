using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using System.Diagnostics;

namespace TransactionAggregationApi.Api.Controllers;

/// <summary>
/// Controller for exposing application metrics and monitoring information.
/// Provides observability data for monitoring systems and operations teams.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class MetricsController : ControllerBase
{
    private readonly ILogger<MetricsController> _logger;
    private readonly IMemoryCache _cache;
    private static readonly DateTime _startTime = DateTime.UtcNow;
    private static long _requestCount = 0;

    /// <summary>
    /// Initializes a new instance of the MetricsController.
    /// </summary>
    /// <param name="logger">Logger for diagnostic information</param>
    /// <param name="cache">Memory cache for metrics collection</param>
    public MetricsController(ILogger<MetricsController> logger, IMemoryCache cache)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
    }

    /// <summary>
    /// Retrieves current application metrics and health indicators.
    /// </summary>
    /// <remarks>
    /// This endpoint provides basic application metrics for monitoring and observability.
    /// 
    /// **Metrics Include:**
    /// - Application uptime (seconds)
    /// - Current memory usage (MB)
    /// - Cache statistics (entry count)
    /// - Process information (thread count, handles)
    /// - Total requests processed
    /// - Current timestamp
    /// 
    /// **Production Integration:**
    /// - For production environments, integrate with Prometheus, Application Insights, or similar
    /// - Add custom business metrics (transaction counts, error rates, latency percentiles)
    /// - Implement metrics aggregation and time-series storage
    /// 
    /// Sample response:
    /// 
    ///     {
    ///       "uptime": 3600,
    ///       "uptimeFormatted": "1h 0m 0s",
    ///       "memory": {
    ///         "workingSetMB": 145.5,
    ///         "gcTotalMemoryMB": 42.3
    ///       },
    ///       "cache": {
    ///         "entryCount": 125,
    ///         "status": "healthy"
    ///       },
    ///       "process": {
    ///         "threadCount": 24,
    ///         "handleCount": 512
    ///       },
    ///       "requests": {
    ///         "total": 1543,
    ///         "ratePerSecond": 0.43
    ///       },
    ///       "timestamp": "2025-10-14T10:30:00Z"
    ///     }
    /// 
    /// </remarks>
    /// <returns>Current application metrics</returns>
    /// <response code="200">Returns the current metrics</response>
    [HttpGet]
    [ProducesResponseType(typeof(MetricsResponse), StatusCodes.Status200OK)]
    public IActionResult Get()
    {
        try
        {
            Interlocked.Increment(ref _requestCount);

            var currentProcess = Process.GetCurrentProcess();
            var uptime = (DateTime.UtcNow - _startTime).TotalSeconds;
            var workingSetMB = currentProcess.WorkingSet64 / 1024.0 / 1024.0;
            var gcMemoryMB = GC.GetTotalMemory(false) / 1024.0 / 1024.0;

            // Get cache entry count (MemoryCache specific)
            int? cacheEntryCount = null;
            if (_cache is MemoryCache memoryCache)
            {
                cacheEntryCount = memoryCache.Count;
            }

            var metrics = new
            {
                uptime = (long)uptime,
                uptimeFormatted = FormatUptime(uptime),
                memory = new
                {
                    workingSetMB = Math.Round(workingSetMB, 2),
                    gcTotalMemoryMB = Math.Round(gcMemoryMB, 2),
                    gen0Collections = GC.CollectionCount(0),
                    gen1Collections = GC.CollectionCount(1),
                    gen2Collections = GC.CollectionCount(2)
                },
                cache = new
                {
                    entryCount = cacheEntryCount,
                    status = cacheEntryCount.HasValue ? "healthy" : "unknown"
                },
                process = new
                {
                    threadCount = currentProcess.Threads.Count,
                    handleCount = currentProcess.HandleCount,
                    processId = currentProcess.Id,
                    processorTime = Math.Round(currentProcess.TotalProcessorTime.TotalSeconds, 2)
                },
                requests = new
                {
                    total = _requestCount,
                    ratePerSecond = Math.Round(_requestCount / uptime, 2)
                },
                environment = new
                {
                    machineName = Environment.MachineName,
                    osVersion = Environment.OSVersion.ToString(),
                    dotnetVersion = Environment.Version.ToString(),
                    processorCount = Environment.ProcessorCount
                },
                timestamp = DateTime.UtcNow
            };

            _logger.LogDebug("Metrics retrieved successfully. Uptime: {Uptime}s, Cache entries: {CacheCount}",
                (long)uptime, cacheEntryCount);

            return Ok(metrics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving metrics");
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                error = "Failed to retrieve metrics",
                timestamp = DateTime.UtcNow
            });
        }
    }

    /// <summary>
    /// Formats uptime in seconds to a human-readable string.
    /// </summary>
    private static string FormatUptime(double seconds)
    {
        var timespan = TimeSpan.FromSeconds(seconds);
        
        if (timespan.TotalDays >= 1)
            return $"{(int)timespan.TotalDays}d {timespan.Hours}h {timespan.Minutes}m {timespan.Seconds}s";
        
        if (timespan.TotalHours >= 1)
            return $"{(int)timespan.TotalHours}h {timespan.Minutes}m {timespan.Seconds}s";
        
        if (timespan.TotalMinutes >= 1)
            return $"{(int)timespan.TotalMinutes}m {timespan.Seconds}s";
        
        return $"{(int)timespan.TotalSeconds}s";
    }
}

/// <summary>
/// Response model for metrics endpoint.
/// </summary>
public class MetricsResponse
{
    /// <summary>
    /// Application uptime in seconds.
    /// </summary>
    public long Uptime { get; set; }

    /// <summary>
    /// Formatted uptime string (e.g., "1h 30m 45s").
    /// </summary>
    public string UptimeFormatted { get; set; } = string.Empty;

    /// <summary>
    /// Memory usage metrics.
    /// </summary>
    public MemoryMetrics Memory { get; set; } = new();

    /// <summary>
    /// Cache statistics.
    /// </summary>
    public CacheMetrics Cache { get; set; } = new();

    /// <summary>
    /// Process information.
    /// </summary>
    public ProcessMetrics Process { get; set; } = new();

    /// <summary>
    /// Request statistics.
    /// </summary>
    public RequestMetrics Requests { get; set; } = new();

    /// <summary>
    /// Timestamp when metrics were collected.
    /// </summary>
    public DateTime Timestamp { get; set; }
}

/// <summary>
/// Memory usage metrics.
/// </summary>
public class MemoryMetrics
{
    /// <summary>
    /// Working set memory in MB.
    /// </summary>
    public double WorkingSetMB { get; set; }

    /// <summary>
    /// GC total memory in MB.
    /// </summary>
    public double GcTotalMemoryMB { get; set; }
}

/// <summary>
/// Cache metrics.
/// </summary>
public class CacheMetrics
{
    /// <summary>
    /// Number of entries in cache.
    /// </summary>
    public int? EntryCount { get; set; }

    /// <summary>
    /// Cache status.
    /// </summary>
    public string Status { get; set; } = "unknown";
}

/// <summary>
/// Process metrics.
/// </summary>
public class ProcessMetrics
{
    /// <summary>
    /// Number of threads.
    /// </summary>
    public int ThreadCount { get; set; }

    /// <summary>
    /// Number of handles.
    /// </summary>
    public int HandleCount { get; set; }
}

/// <summary>
/// Request metrics.
/// </summary>
public class RequestMetrics
{
    /// <summary>
    /// Total requests processed.
    /// </summary>
    public long Total { get; set; }

    /// <summary>
    /// Requests per second rate.
    /// </summary>
    public double RatePerSecond { get; set; }
}
