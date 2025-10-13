using TransactionAggregationApi.Api.Models;

namespace TransactionAggregationApi.Api.Clients;

public class BankBClient : IBankClient
{
    private readonly HttpClient _httpClient;

    public string BankName => "BankB";

    public BankBClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<IEnumerable<TransactionDto>> GetTransactionsAsync(DateTime from, DateTime to)
    {
        // Mock implementation - In production, this would call the actual Bank B API
        await Task.Delay(150); // Simulate network delay

        return new List<TransactionDto>
        {
            new TransactionDto
            {
                Id = $"BANKB-{Guid.NewGuid()}",
                Date = DateTime.UtcNow.AddDays(-1),
                Amount = 250.00m,
                Currency = "USD",
                Category = "Utilities",
                Source = BankName
            },
            new TransactionDto
            {
                Id = $"BANKB-{Guid.NewGuid()}",
                Date = DateTime.UtcNow.AddDays(-3),
                Amount = 45.99m,
                Currency = "USD",
                Category = "Dining",
                Source = BankName
            }
        };
    }
}
