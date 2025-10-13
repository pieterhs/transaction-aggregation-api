# Transaction Aggregation API

A .NET 8 Web API that aggregates financial transactions from multiple bank APIs with caching, resilience patterns, and API key authentication.

## Features

- **Multi-Bank Aggregation**: Fetches and aggregates transactions from multiple bank sources (Bank A, Bank B, Bank C)
- **Caching**: In-memory caching to reduce external API calls and improve performance
- **Resilience**: Polly-based retry policies for handling transient failures
- **Authentication**: API key-based authentication middleware
- **Pagination**: Support for paginated results
- **Category Filtering**: Filter transactions by category
- **Docker Support**: Containerized deployment with Docker and docker-compose
- **Swagger Documentation**: Interactive API documentation
- **Health Checks**: Built-in health check endpoint

## Architecture

The API follows clean architecture principles with the following structure:

```
TransactionAggregationApi/
├── TransactionAggregationApi.Api/
│   ├── Controllers/          # API endpoints
│   ├── Models/               # DTOs and data models
│   ├── Services/             # Business logic
│   ├── Clients/              # External bank API clients
│   ├── Infrastructure/       # Cross-cutting concerns (caching, etc.)
│   └── Middleware/           # Custom middleware (auth, etc.)
└── TransactionAggregationApi.Tests/  # Unit and integration tests
```

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Docker](https://www.docker.com/get-started) (optional, for containerized deployment)
- [Visual Studio 2022](https://visualstudio.microsoft.com/) or [VS Code](https://code.visualstudio.com/) (recommended)

## Getting Started

### 1. Clone the Repository

```bash
git clone https://github.com/pieterhs/transaction-aggregation-api.git
cd transaction-aggregation-api
```

### 2. Restore Dependencies

```bash
dotnet restore
```

### 3. Configure API Key

Update the `appsettings.json` file with your API key:

```json
{
  "ApiKey": "your-secure-api-key-here"
}
```

Or set it as an environment variable:

```bash
export ApiKey="your-secure-api-key-here"
```

### 4. Run the Application

#### Option A: Using .NET CLI

```bash
cd TransactionAggregationApi.Api
dotnet run
```

The API will be available at:
- HTTP: `http://localhost:5000`
- HTTPS: `https://localhost:5001`
- Swagger UI: `https://localhost:5001/swagger`

#### Option B: Using Docker

```bash
# Build and run with docker-compose
docker-compose up --build

# Or build and run manually
docker build -t transaction-api .
docker run -p 8080:8080 -e ApiKey="your-api-key" transaction-api
```

The API will be available at `http://localhost:8080`

## API Endpoints

### Get Transactions

**GET** `/api/transactions`

Retrieves aggregated transactions from multiple banks.

**Headers:**
```
X-API-Key: your-api-key-here
```

**Query Parameters:**
- `from` (optional): Start date in YYYY-MM-DD format (default: 30 days ago)
- `to` (optional): End date in YYYY-MM-DD format (default: today)
- `category` (optional): Filter by category (e.g., "Groceries", "Entertainment")
- `page` (optional): Page number (default: 1)
- `pageSize` (optional): Results per page (default: 50, max: 100)

**Example Request:**
```bash
curl -X GET "https://localhost:5001/api/transactions?from=2025-09-01&to=2025-10-13&category=Groceries&page=1&pageSize=20" \
  -H "X-API-Key: your-api-key-here"
```

**Example Response:**
```json
[
  {
    "id": "BANKA-123e4567-e89b-12d3-a456-426614174000",
    "date": "2025-10-12T00:00:00Z",
    "amount": 150.50,
    "currency": "USD",
    "category": "Groceries",
    "source": "BankA"
  },
  {
    "id": "BANKB-987fcdeb-51d3-45a6-b789-123456789abc",
    "date": "2025-10-11T00:00:00Z",
    "amount": 250.00,
    "currency": "USD",
    "category": "Utilities",
    "source": "BankB"
  }
]
```

### Health Check

**GET** `/health`

Returns the health status of the API (no authentication required).

**Example Request:**
```bash
curl -X GET "https://localhost:5001/health"
```

**Example Response:**
```json
{
  "status": "healthy",
  "timestamp": "2025-10-13T12:00:00Z",
  "service": "Transaction Aggregation API"
}
```

## Configuration

### appsettings.json

```json
{
  "ApiKey": "your-secure-api-key",
  "BankA": {
    "BaseUrl": "https://banka-api.example.com"
  },
  "BankB": {
    "BaseUrl": "https://bankb-api.example.com"
  },
  "BankC": {
    "BaseUrl": "https://bankc-api.example.com"
  }
}
```

### Environment Variables

The following environment variables can be used to override configuration:

- `ApiKey`: API key for authentication
- `BankA__BaseUrl`: Base URL for Bank A API
- `BankB__BaseUrl`: Base URL for Bank B API
- `BankC__BaseUrl`: Base URL for Bank C API

## Testing

### Run Unit Tests

```bash
dotnet test
```

### Run Specific Test

```bash
dotnet test --filter "FullyQualifiedName~TransactionServiceTests"
```

## Development

### Project Structure

- **Controllers**: Handle HTTP requests and responses
- **Services**: Contain business logic and orchestrate data aggregation
- **Clients**: Interface with external bank APIs
- **Models**: Define data transfer objects (DTOs)
- **Infrastructure**: Provide cross-cutting concerns like caching
- **Middleware**: Custom middleware for authentication, logging, etc.

### Adding a New Bank Client

1. Create a new client class implementing `IBankClient`:

```csharp
public class BankDClient : IBankClient
{
    private readonly HttpClient _httpClient;
    public string BankName => "BankD";

    public BankDClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<IEnumerable<TransactionDto>> GetTransactionsAsync(DateTime from, DateTime to)
    {
        // Implementation
    }
}
```

2. Register it in `Program.cs`:

```csharp
builder.Services.AddHttpClient<IBankClient, BankDClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["BankD:BaseUrl"]);
    client.Timeout = TimeSpan.FromSeconds(30);
})
.AddPolicyHandler(retryPolicy);
```

## Deployment

### Docker Deployment

```bash
# Build the image
docker build -t transaction-api:latest .

# Run the container
docker run -d \
  -p 8080:8080 \
  -e ApiKey="production-api-key" \
  -e ASPNETCORE_ENVIRONMENT=Production \
  --name transaction-api \
  transaction-api:latest
```

### Docker Compose

```bash
# Start services
docker-compose up -d

# View logs
docker-compose logs -f

# Stop services
docker-compose down
```

## Performance Considerations

- **Caching**: Transactions are cached for 10 minutes by default
- **Parallel Requests**: Bank API calls are made in parallel for better performance
- **Retry Policy**: Exponential backoff with 3 retries for transient failures
- **Connection Pooling**: HttpClient is registered as a service for efficient connection reuse

## Security

- **API Key Authentication**: All endpoints (except `/health` and `/swagger`) require a valid API key
- **HTTPS**: The API should be deployed behind HTTPS in production
- **Rate Limiting**: Consider adding rate limiting middleware for production use
- **Input Validation**: All user inputs are validated

## Monitoring and Logging

The API uses structured logging with different log levels:

- **Information**: General application flow
- **Warning**: Abnormal or unexpected events
- **Error**: Errors and exceptions

Example logs:
```
info: TransactionAggregationApi.Api.Services.TransactionService[0]
      Fetching transactions from 3 banks
info: TransactionAggregationApi.Api.Services.TransactionService[0]
      Successfully fetched 6 transactions from all banks
```

## Troubleshooting

### Common Issues

1. **401 Unauthorized**: Check that the `X-API-Key` header is set correctly
2. **Connection Refused**: Ensure the API is running on the expected port
3. **Cache Issues**: Clear the cache by restarting the application

## Contributing

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

## License

This project is licensed under the MIT License.

## Contact

Pieter - [@pieterhs](https://github.com/pieterhs)

Project Link: [https://github.com/pieterhs/transaction-aggregation-api](https://github.com/pieterhs/transaction-aggregation-api)
