using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text.Json;
using TransactionAggregationApi.Api.Middleware;

namespace TransactionAggregationApi.Tests;

/// <summary>
/// Unit tests for AuthMiddleware.
/// Validates API key authentication logic, exclusion paths, and error responses.
/// </summary>
public class AuthMiddlewareTests
{
    private readonly Mock<RequestDelegate> _mockNext;
    private readonly Mock<IConfiguration> _mockConfiguration;
    private readonly Mock<ILogger<AuthMiddleware>> _mockLogger;
    private readonly AuthMiddleware _middleware;

    public AuthMiddlewareTests()
    {
        _mockNext = new Mock<RequestDelegate>();
        _mockConfiguration = new Mock<IConfiguration>();
        _mockLogger = new Mock<ILogger<AuthMiddleware>>();

        // Set default API key configuration
        _mockConfiguration.Setup(c => c["ApiKey"]).Returns("test-api-key-12345");

        _middleware = new AuthMiddleware(
            _mockNext.Object,
            _mockConfiguration.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task InvokeAsync_ShouldCallNextMiddleware_WhenValidApiKeyProvided()
    {
        // Arrange
        var context = CreateHttpContext("/api/transactions");
        context.Request.Headers["X-Api-Key"] = "test-api-key-12345";

        // Act
        await _middleware.InvokeAsync(context);

        // Assert
        _mockNext.Verify(next => next(context), Times.Once);
        Assert.Equal(200, context.Response.StatusCode); // Default status code
    }

    [Fact]
    public async Task InvokeAsync_ShouldReturn401_WhenApiKeyHeaderIsMissing()
    {
        // Arrange
        var context = CreateHttpContext("/api/transactions");
        // No X-Api-Key header set

        // Act
        await _middleware.InvokeAsync(context);

        // Assert
        _mockNext.Verify(next => next(context), Times.Never);
        Assert.Equal(401, context.Response.StatusCode);

        // Verify JSON response
        var responseBody = await GetResponseBody(context);
        var errorResponse = JsonSerializer.Deserialize<Dictionary<string, string>>(responseBody);
        Assert.NotNull(errorResponse);
        Assert.Equal("Invalid API key", errorResponse["error"]);
    }

    [Fact]
    public async Task InvokeAsync_ShouldReturn401_WhenApiKeyIsInvalid()
    {
        // Arrange
        var context = CreateHttpContext("/api/transactions");
        context.Request.Headers["X-Api-Key"] = "invalid-api-key";

        // Act
        await _middleware.InvokeAsync(context);

        // Assert
        _mockNext.Verify(next => next(context), Times.Never);
        Assert.Equal(401, context.Response.StatusCode);

        // Verify JSON response
        var responseBody = await GetResponseBody(context);
        var errorResponse = JsonSerializer.Deserialize<Dictionary<string, string>>(responseBody);
        Assert.NotNull(errorResponse);
        Assert.Equal("Invalid API key", errorResponse["error"]);
    }

    [Fact]
    public async Task InvokeAsync_ShouldReturn401_WhenApiKeyIsEmpty()
    {
        // Arrange
        var context = CreateHttpContext("/api/transactions");
        context.Request.Headers["X-Api-Key"] = "";

        // Act
        await _middleware.InvokeAsync(context);

        // Assert
        _mockNext.Verify(next => next(context), Times.Never);
        Assert.Equal(401, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_ShouldSkipAuth_ForSwaggerEndpoint()
    {
        // Arrange
        var context = CreateHttpContext("/swagger/index.html");
        // No X-Api-Key header set

        // Act
        await _middleware.InvokeAsync(context);

        // Assert - Next middleware should be called without authentication
        _mockNext.Verify(next => next(context), Times.Once);
        Assert.Equal(200, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_ShouldSkipAuth_ForSwaggerJsonEndpoint()
    {
        // Arrange
        var context = CreateHttpContext("/swagger/v1/swagger.json");
        // No X-Api-Key header set

        // Act
        await _middleware.InvokeAsync(context);

        // Assert
        _mockNext.Verify(next => next(context), Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_ShouldSkipAuth_ForHealthEndpoint()
    {
        // Arrange
        var context = CreateHttpContext("/health");
        // No X-Api-Key header set

        // Act
        await _middleware.InvokeAsync(context);

        // Assert
        _mockNext.Verify(next => next(context), Times.Once);
        Assert.Equal(200, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_ShouldEnforceAuth_ForProtectedEndpoints()
    {
        // Arrange
        var protectedPaths = new[]
        {
            "/api/transactions",
            "/api/transactions/summary",
            "/api/users",
            "/"
        };

        foreach (var path in protectedPaths)
        {
            var context = CreateHttpContext(path);
            // No X-Api-Key header set

            // Act
            await _middleware.InvokeAsync(context);

            // Assert
            Assert.Equal(401, context.Response.StatusCode);
        }
    }

    [Fact]
    public async Task InvokeAsync_ShouldReturn401_WhenConfiguredApiKeyIsNull()
    {
        // Arrange
        _mockConfiguration.Setup(c => c["ApiKey"]).Returns((string?)null);
        var middleware = new AuthMiddleware(
            _mockNext.Object,
            _mockConfiguration.Object,
            _mockLogger.Object);

        var context = CreateHttpContext("/api/transactions");
        context.Request.Headers["X-Api-Key"] = "some-key";

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        _mockNext.Verify(next => next(context), Times.Never);
        Assert.Equal(401, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_ShouldReturn401_WhenConfiguredApiKeyIsEmpty()
    {
        // Arrange
        _mockConfiguration.Setup(c => c["ApiKey"]).Returns("");
        var middleware = new AuthMiddleware(
            _mockNext.Object,
            _mockConfiguration.Object,
            _mockLogger.Object);

        var context = CreateHttpContext("/api/transactions");
        context.Request.Headers["X-Api-Key"] = "some-key";

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        _mockNext.Verify(next => next(context), Times.Never);
        Assert.Equal(401, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_ShouldBeCase​Sensitive_ForApiKey()
    {
        // Arrange
        var context = CreateHttpContext("/api/transactions");
        context.Request.Headers["X-Api-Key"] = "TEST-API-KEY-12345"; // Different case

        // Act
        await _middleware.InvokeAsync(context);

        // Assert
        _mockNext.Verify(next => next(context), Times.Never);
        Assert.Equal(401, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_ShouldLogDebug_WhenAuthenticationSucceeds()
    {
        // Arrange
        var context = CreateHttpContext("/api/transactions");
        context.Request.Headers["X-Api-Key"] = "test-api-key-12345";

        // Act
        await _middleware.InvokeAsync(context);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Authentication successful")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_ShouldLogWarning_WhenAuthenticationFails()
    {
        // Arrange
        var context = CreateHttpContext("/api/transactions");
        context.Request.Headers["X-Api-Key"] = "invalid-key";

        // Act
        await _middleware.InvokeAsync(context);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Authentication failed")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_ShouldLogWarning_WhenApiKeyIsMissing()
    {
        // Arrange
        var context = CreateHttpContext("/api/transactions");
        // No X-Api-Key header

        // Act
        await _middleware.InvokeAsync(context);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Missing API key")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_ShouldSetContentType_ToJson_OnUnauthorized()
    {
        // Arrange
        var context = CreateHttpContext("/api/transactions");
        context.Request.Headers["X-Api-Key"] = "invalid-key";

        // Act
        await _middleware.InvokeAsync(context);

        // Assert
        Assert.Equal("application/json", context.Response.ContentType);
    }

    [Fact]
    public void UseApiKeyAuth_ShouldRegisterMiddleware()
    {
        // Arrange
        var mockAppBuilder = new Mock<IApplicationBuilder>();
        mockAppBuilder.Setup(x => x.Use(It.IsAny<Func<RequestDelegate, RequestDelegate>>()))
            .Returns(mockAppBuilder.Object);

        // Act
        var result = mockAppBuilder.Object.UseApiKeyAuth();

        // Assert
        Assert.NotNull(result);
    }

    /// <summary>
    /// Helper method to create an HttpContext with a mock response stream.
    /// </summary>
    private static DefaultHttpContext CreateHttpContext(string path)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        context.Response.Body = new MemoryStream();
        return context;
    }

    /// <summary>
    /// Helper method to read the response body as a string.
    /// </summary>
    private static async Task<string> GetResponseBody(HttpContext context)
    {
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(context.Response.Body);
        return await reader.ReadToEndAsync();
    }
}
