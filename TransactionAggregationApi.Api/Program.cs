using System.Reflection;
using Microsoft.OpenApi;
using Microsoft.Extensions.Http.Resilience;
using Polly;
using TransactionAggregationApi.Api.Clients;
using TransactionAggregationApi.Api.Infrastructure;
using TransactionAggregationApi.Api.Middleware;
using TransactionAggregationApi.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Configure Swagger/OpenAPI with comprehensive documentation
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Transaction Aggregation API",
        Version = "v1",
        Description = "Aggregates customer transactions from multiple mock banking systems with caching, resilience patterns, and API key authentication.",
        Contact = new OpenApiContact
        {
            Name = "API Support",
            Email = "support@transactionapi.com"
        },
        License = new OpenApiLicense
        {
            Name = "MIT License",
            Url = new Uri("https://opensource.org/licenses/MIT")
        }
    });

    // Include XML comments for enhanced documentation
    var xmlFilename = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFilename);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }

    // Add API Key authentication scheme to Swagger UI
    options.AddSecurityDefinition("ApiKey", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.ApiKey,
        In = ParameterLocation.Header,
        Name = "X-Api-Key",
        Description = "API Key authentication. Enter your API key in the field below."
    });

    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecuritySchemeReference("ApiKey", document),
            new List<string>()
        }
    });
});

// Add Health Checks
builder.Services.AddHealthChecks();

// Configure cache provider based on configuration (Memory or Redis)
var cacheProvider = builder.Configuration.GetValue<string>("Cache:Provider") ?? "Memory";

if (cacheProvider.Equals("Redis", StringComparison.OrdinalIgnoreCase))
{
    // Configure Redis distributed cache
    builder.Services.AddStackExchangeRedisCache(options =>
    {
        options.Configuration = builder.Configuration["Cache:Redis:Configuration"] ?? "localhost:6379";
        options.InstanceName = "txnagg:";
    });
    
    // Register Redis-based transaction cache
    builder.Services.AddScoped<ITransactionCache, RedisTransactionCache>();
    
    builder.Services.AddLogging(logging =>
    {
        logging.AddConsole();
    });
    
    Console.WriteLine($"[Cache] Using Redis distributed cache: {builder.Configuration["Cache:Redis:Configuration"]}");
}
else
{
    // Use in-memory cache (default)
    builder.Services.AddMemoryCache();
    
    // Register in-memory transaction cache
    builder.Services.AddScoped<ITransactionCache, TransactionCache>();
    
    Console.WriteLine("[Cache] Using in-memory cache");
}

// Register TransactionService with interface for better testability
builder.Services.AddScoped<ITransactionService, TransactionService>();

void AddBankResilience(IHttpClientBuilder clientBuilder)
{
    clientBuilder.AddResilienceHandler("bank-resilience", pipeline =>
    {
        pipeline.AddRetry(new HttpRetryStrategyOptions
        {
            MaxRetryAttempts = 3,
            Delay = TimeSpan.FromSeconds(2),
            BackoffType = DelayBackoffType.Exponential,
            UseJitter = false
        });

        pipeline.AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
        {
            FailureRatio = 1.0,
            MinimumThroughput = 5,
            SamplingDuration = TimeSpan.FromSeconds(30),
            BreakDuration = TimeSpan.FromSeconds(30)
        });
    });
}

// Register Bank Clients with HttpClient resilience policies
var bankAClient = builder.Services.AddHttpClient<IBankClient, BankAClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration.GetValue<string>("BankA:BaseUrl") ?? "https://api.banka.com");
    client.Timeout = TimeSpan.FromSeconds(30);
    client.DefaultRequestHeaders.Add("User-Agent", "TransactionAggregationAPI/1.0");
});
AddBankResilience(bankAClient);

var bankBClient = builder.Services.AddHttpClient<IBankClient, BankBClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration.GetValue<string>("BankB:BaseUrl") ?? "https://api.bankb.eu");
    client.Timeout = TimeSpan.FromSeconds(30);
    client.DefaultRequestHeaders.Add("User-Agent", "TransactionAggregationAPI/1.0");
});
AddBankResilience(bankBClient);

var bankCClient = builder.Services.AddHttpClient<IBankClient, BankCClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration.GetValue<string>("BankC:BaseUrl") ?? "https://api.bankc.asia");
    client.Timeout = TimeSpan.FromSeconds(30);
    client.DefaultRequestHeaders.Add("User-Agent", "TransactionAggregationAPI/1.0");
});
AddBankResilience(bankCClient);

var app = builder.Build();

// Configure the HTTP request pipeline
// Enable Swagger in all environments for API documentation
app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "Transaction Aggregation API v1");
    options.RoutePrefix = string.Empty; // Serve Swagger UI at root (http://localhost:8080/)
    options.DocumentTitle = "Transaction Aggregation API - Documentation";
    options.DisplayRequestDuration(); // Show request duration in Swagger UI
    options.EnableDeepLinking(); // Enable deep linking for operations and tags
    options.EnableFilter(); // Enable filtering by tags
    options.ShowExtensions(); // Show vendor extensions
    options.EnableValidator(); // Enable schema validation
});

// Add API key authentication middleware (before UseAuthorization)
// Note: /swagger, /health, and /api/metrics are excluded in AuthMiddleware
app.UseApiKeyAuth();

app.UseHttpsRedirection();
app.UseAuthorization();

// Map controllers (includes MetricsController)
app.MapControllers();

// Health check endpoint with detailed status
// Excluded from authentication for monitoring systems
app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        var response = new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                description = e.Value.Description,
                duration = e.Value.Duration.TotalMilliseconds
            }),
            totalDuration = report.TotalDuration.TotalMilliseconds,
            timestamp = DateTime.UtcNow
        };
        await context.Response.WriteAsJsonAsync(response);
    }
})
.WithName("HealthCheck")
.WithSummary("Health check endpoint")
.WithDescription("Returns the health status of the API and its dependencies. Used by monitoring systems.");

app.Run();
