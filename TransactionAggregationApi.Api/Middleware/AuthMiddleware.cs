using System.Text.Json;

namespace TransactionAggregationApi.Api.Middleware;

/// <summary>
/// Middleware for API key authentication.
/// Validates the X-Api-Key header against the configured API key.
/// TODO: Migrate to JWT/OAuth2 for production environments.
/// TODO: Implement rate limiting per API key.
/// TODO: Support multiple API keys with different permission levels.
/// </summary>
public class AuthMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthMiddleware> _logger;

    /// <summary>
    /// Header name for API key authentication.
    /// </summary>
    private const string ApiKeyHeaderName = "X-Api-Key";

    public AuthMiddleware(RequestDelegate next, IConfiguration configuration, ILogger<AuthMiddleware> logger)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Invokes the middleware to validate API key authentication.
    /// Excludes /swagger and /health endpoints from authentication.
    /// </summary>
    public async Task InvokeAsync(HttpContext context)
    {
        // Skip authentication for Swagger and health check endpoints
        if (context.Request.Path.StartsWithSegments("/swagger") || 
            context.Request.Path.StartsWithSegments("/health"))
        {
            _logger.LogDebug("Skipping authentication for excluded path: {Path}", context.Request.Path);
            await _next(context);
            return;
        }

        // Extract API key from request header
        if (!context.Request.Headers.TryGetValue(ApiKeyHeaderName, out var extractedApiKey))
        {
            _logger.LogWarning(
                "Authentication failed: Missing API key header. Path: {Path}, IP: {IP}",
                context.Request.Path,
                context.Connection.RemoteIpAddress);

            await WriteUnauthorizedResponse(context, "Invalid API key");
            return;
        }

        // Get configured API key from appsettings.json or environment variable
        var configuredApiKey = _configuration["ApiKey"];

        if (string.IsNullOrWhiteSpace(configuredApiKey))
        {
            _logger.LogError("API key not configured in appsettings.json or environment variables");
            await WriteUnauthorizedResponse(context, "Invalid API key");
            return;
        }

        // Validate API key
        if (!configuredApiKey.Equals(extractedApiKey.ToString(), StringComparison.Ordinal))
        {
            _logger.LogWarning(
                "Authentication failed: Invalid API key provided. Path: {Path}, IP: {IP}",
                context.Request.Path,
                context.Connection.RemoteIpAddress);

            await WriteUnauthorizedResponse(context, "Invalid API key");
            return;
        }

        // Authentication successful
        _logger.LogDebug(
            "Authentication successful. Path: {Path}, Method: {Method}",
            context.Request.Path,
            context.Request.Method);

        // Call next middleware in the pipeline
        await _next(context);
    }

    /// <summary>
    /// Writes a standardized 401 Unauthorized JSON response.
    /// </summary>
    private static async Task WriteUnauthorizedResponse(HttpContext context, string message)
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        context.Response.ContentType = "application/json";

        var errorResponse = new { error = message };
        var json = JsonSerializer.Serialize(errorResponse);

        await context.Response.WriteAsync(json);
    }
}

/// <summary>
/// Extension methods for registering the AuthMiddleware.
/// </summary>
public static class AuthMiddlewareExtensions
{
    /// <summary>
    /// Adds API key authentication middleware to the application pipeline.
    /// Should be called before UseAuthorization() and before endpoint mapping.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <returns>The application builder for chaining.</returns>
    public static IApplicationBuilder UseApiKeyAuth(this IApplicationBuilder app)
    {
        return app.UseMiddleware<AuthMiddleware>();
    }
}
