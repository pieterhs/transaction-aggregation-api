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

// Add memory cache
builder.Services.AddMemoryCache();

// Register TransactionCache
builder.Services.AddSingleton<ITransactionCache, TransactionCache>();

// Register TransactionService
builder.Services.AddScoped<TransactionService>();

// Configure Polly retry policy for HttpClient
var retryPolicy = HttpPolicyExtensions
    .HandleTransientHttpError()
    .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));

// Register Bank Clients with HttpClient and Polly
builder.Services.AddHttpClient<IBankClient, BankAClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration.GetValue<string>("BankA:BaseUrl") ?? "https://banka-api.example.com");
    client.Timeout = TimeSpan.FromSeconds(30);
})
.AddPolicyHandler(retryPolicy);

builder.Services.AddHttpClient<IBankClient, BankBClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration.GetValue<string>("BankB:BaseUrl") ?? "https://bankb-api.example.com");
    client.Timeout = TimeSpan.FromSeconds(30);
})
.AddPolicyHandler(retryPolicy);

builder.Services.AddHttpClient<IBankClient, BankCClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration.GetValue<string>("BankC:BaseUrl") ?? "https://bankc-api.example.com");
    client.Timeout = TimeSpan.FromSeconds(30);
})
.AddPolicyHandler(retryPolicy);

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

// Add custom authentication middleware
app.UseAuthMiddleware();

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
