using TransactionAggregationApi.Api.Models;

namespace TransactionAggregationApi.Api.Clients;

public interface IBankClient
{
    Task<IEnumerable<TransactionDto>> GetTransactionsAsync(DateTime from, DateTime to);
    string BankName { get; }
}
