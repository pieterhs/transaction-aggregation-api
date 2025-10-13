using TransactionAggregationApi.Api.Models;

namespace TransactionAggregationApi.Api.Clients;

public class BankAClient : IBankClient
{
    private readonly HttpClient _httpClient;

    public string BankName => "BankA";

    public BankAClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<IEnumerable<TransactionDto>> GetTransactionsAsync(DateTime from, DateTime to)
    {
        // Mock implementation - In production, this would call the actual Bank A API
        await Task.Delay(100); // Simulate network delay

        return new List<TransactionDto>
        {
            new TransactionDto
            {
                Id = $"BANKA-{Guid.NewGuid()}",
                Date = DateTime.UtcNow.AddDays(-1),
                Amount = 150.50m,
                Currency = "USD",
                Category = "Groceries",
                Source = BankName
            },
            new TransactionDto
            {
                Id = $"BANKA-{Guid.NewGuid()}",
                Date = DateTime.UtcNow.AddDays(-2),
                Amount = 75.25m,
                Currency = "USD",
                Category = "Entertainment",
                Source = BankName
            }
        };
    }
}
