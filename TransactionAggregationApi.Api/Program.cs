using Polly;
using Polly.Extensions.Http;
using TransactionAggregationApi.Api.Clients;
using TransactionAggregationApi.Api.Infrastructure;
using TransactionAggregationApi.Api.Middleware;
using TransactionAggregationApi.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { 
        Title = "Transaction Aggregation API", 
        Version = "v1",
        Description = "API for aggregating transactions from multiple banks"
    });
});

// Add memory cache (thread-safe, singleton by default)
builder.Services.AddMemoryCache();

// Register TransactionCache as scoped
// TODO: When migrating to Redis, use AddStackExchangeRedisCache() or AddDistributedMemoryCache()
builder.Services.AddScoped<ITransactionCache, TransactionCache>();

// Register TransactionService
builder.Services.AddScoped<TransactionService>();

// Configure Polly policies for HttpClient
// Retry policy: 3 retries with exponential backoff
var retryPolicy = HttpPolicyExtensions
    .HandleTransientHttpError()
    .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));

// Circuit breaker: Open after 5 consecutive failures, reset after 30s
var circuitBreakerPolicy = HttpPolicyExtensions
    .HandleTransientHttpError()
    .CircuitBreakerAsync(
        handledEventsAllowedBeforeBreaking: 5,
        durationOfBreak: TimeSpan.FromSeconds(30));

// Register Bank Clients with HttpClient and Polly resilience policies
builder.Services.AddHttpClient<IBankClient, BankAClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration.GetValue<string>("BankA:BaseUrl") ?? "https://api.banka.com");
    client.Timeout = TimeSpan.FromSeconds(30);
    client.DefaultRequestHeaders.Add("User-Agent", "TransactionAggregationAPI/1.0");
})
.AddPolicyHandler(retryPolicy)
.AddPolicyHandler(circuitBreakerPolicy);

builder.Services.AddHttpClient<IBankClient, BankBClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration.GetValue<string>("BankB:BaseUrl") ?? "https://api.bankb.eu");
    client.Timeout = TimeSpan.FromSeconds(30);
    client.DefaultRequestHeaders.Add("User-Agent", "TransactionAggregationAPI/1.0");
})
.AddPolicyHandler(retryPolicy)
.AddPolicyHandler(circuitBreakerPolicy);

builder.Services.AddHttpClient<IBankClient, BankCClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration.GetValue<string>("BankC:BaseUrl") ?? "https://api.bankc.asia");
    client.Timeout = TimeSpan.FromSeconds(30);
    client.DefaultRequestHeaders.Add("User-Agent", "TransactionAggregationAPI/1.0");
})
.AddPolicyHandler(retryPolicy)
.AddPolicyHandler(circuitBreakerPolicy);

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Transaction Aggregation API v1");
    });
}

// Add API key authentication middleware (before UseAuthorization)
app.UseApiKeyAuth();

app.UseHttpsRedirection();
app.UseAuthorization();

// Map controllers
app.MapControllers();

// Health check endpoint
app.MapGet("/health", () => Results.Ok(new
{
    status = "healthy",
    timestamp = DateTime.UtcNow,
    service = "Transaction Aggregation API"
}))
.WithName("HealthCheck")
.WithOpenApi();

app.Run();
