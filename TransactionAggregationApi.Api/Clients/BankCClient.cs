using TransactionAggregationApi.Api.Models;

namespace TransactionAggregationApi.Api.Clients;

public class BankCClient : IBankClient
{
    private readonly HttpClient _httpClient;

    public string BankName => "BankC";

    public BankCClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<IEnumerable<TransactionDto>> GetTransactionsAsync(DateTime from, DateTime to)
    {
        // Mock implementation - In production, this would call the actual Bank C API
        await Task.Delay(120); // Simulate network delay

        return new List<TransactionDto>
        {
            new TransactionDto
            {
                Id = $"BANKC-{Guid.NewGuid()}",
                Date = DateTime.UtcNow.AddDays(-2),
                Amount = 89.99m,
                Currency = "USD",
                Category = "Shopping",
                Source = BankName
            },
            new TransactionDto
            {
                Id = $"BANKC-{Guid.NewGuid()}",
                Date = DateTime.UtcNow.AddDays(-4),
                Amount = 120.00m,
                Currency = "USD",
                Category = "Transportation",
                Source = BankName
            }
        };
    }
}
