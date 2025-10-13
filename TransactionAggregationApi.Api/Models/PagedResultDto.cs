namespace TransactionAggregationApi.Api.Models;

public class PagedResultDto<T>
{
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
    public IEnumerable<T> Transactions { get; set; } = Array.Empty<T>();
}
