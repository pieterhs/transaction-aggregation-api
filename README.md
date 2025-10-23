# Transaction Aggregation API

[![.NET 8](https://img.shields.io/badge/.NET-8.0-blue)](https://dotnet.microsoft.com/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![Tests](https://img.shields.io/badge/tests-51%20passing-success)](https://github.com/pieterhs/transaction-aggregation-api)

A production-grade REST API that aggregates customer transaction data from multiple mock banking systems and returns a consolidated view with filtering, pagination, caching, and resilience patterns.

---

## 📋 Table of Contents

- [Features](#-features)
- [Architecture](#-architecture)
- [Technology Stack](#-technology-stack)
- [Getting Started](#-getting-started)
  - [Prerequisites](#prerequisites)
  - [Build & Run with Docker](#build--run-with-docker)
  - [Build & Run Locally](#build--run-locally)
- [API Documentation](#-api-documentation)
- [Authentication](#-authentication)
- [Testing](#-testing)
- [Project Structure](#-project-structure)
- [Production Considerations](#-production-considerations)
- [Future Enhancements](#-future-enhancements)

---

## ✨ Features

### Core Requirements (Project Brief)
- ✅ **REST API** for aggregating transaction data from multiple banking systems
- ✅ **Date Range Filtering** - Retrieve transactions by from/to dates
- ✅ **Category Filtering** - Filter by transaction category (Groceries, Entertainment, etc.)
- ✅ **Pagination Support** - Page-based pagination with configurable page size
- ✅ **Simple Authentication** - API Key authentication via `X-Api-Key` header
- ✅ **Consistent JSON Schema** - Standardized response format across all banks
- ✅ **Production-grade Dockerfile** - Multi-stage Alpine-based build (~110MB)
- ✅ **Comprehensive README** - Complete build, run, and test documentation

### Additional Production Features
- 🚀 **Performance**
  - **Dual cache support**: In-memory cache OR Redis distributed cache (configurable)
  - Redis caching with JSON serialization (10 minutes TTL default)
  - Concurrent bank API calls with `Task.WhenAll`
  - Optimized Docker layer caching
  
- 🛡️ **Resilience & Reliability**
  - Polly retry policies (exponential backoff, 3 retries)
  - Circuit breaker pattern (breaks after 5 failures, 30s reset)
  - Timeout policies (30s per request)
  - Comprehensive error handling and logging
  
- 📊 **Observability**
  - Health check endpoint (`/health`)
  - Metrics endpoint (`/api/metrics`) - cache stats, request counts
  - Structured logging with log levels
  - Request/response correlation IDs
  
- 📖 **Developer Experience**
  - Swagger/OpenAPI UI with interactive documentation
  - XML documentation comments
  - Docker Compose for easy local development
  - 51 comprehensive unit tests (100% pass rate)

---

## 🏗️ Architecture

The API aggregates transactions from three mock banking systems (Bank A, B, C) through a clean, layered architecture:

```
┌─────────────┐
│   Client    │
└──────┬──────┘
       │ HTTP + X-Api-Key
       ▼
┌─────────────────────────────────────┐
│     AuthMiddleware (API Key)        │
└──────────────┬──────────────────────┘
               ▼
┌──────────────────────────────────────┐
│   TransactionsController             │
│   - GetTransactions (GET)            │
│   - GetTransactionsMetadata (HEAD)   │
│   - GetById (GET)                    │
│   - GetByIds (POST)                  │
│   - GetCategories (GET)              │
└───────────────┬──────────────────────┘
                ▼
┌───────────────────────────────────────┐
│     TransactionService                │
│     - Aggregation Logic               │
│     - Filtering & Pagination          │
│     - Caching Strategy                │
└────────┬──────────────────────────────┘
         ▼
┌─────────────────────────────────┐
│    TransactionCache             │
│    - In-Memory (Development)    │
│    - Redis (Production)         │
└─────────────────────────────────┘
         │
         ▼ (concurrent)
┌──────────────────────────────────────────────┐
│  Bank Clients (with Polly resilience)        │
│  ┌──────────┐ ┌──────────┐ ┌──────────┐      │
│  │ BankA    │ │ BankB    │ │ BankC    │      │
│  │ Client   │ │ Client   │ │ Client   │      │
│  └──────────┘ └──────────┘ └──────────┘      │
└──────────────────────────────────────────────┘
```

### Key Design Patterns
- **Repository Pattern**: `IBankClient` interface for bank integrations
- **Dependency Injection**: All services registered and resolved via DI
- **Middleware Pattern**: Authentication handled via custom middleware
- **Retry/Circuit Breaker**: Polly policies for transient fault handling
- **Cache-Aside Pattern**: Check cache → miss → fetch → store → return

---

## 🛠️ Technology Stack

- **Framework**: .NET 8.0 (LTS)
- **Language**: C# 12
- **API**: ASP.NET Core Web API
- **Documentation**: Swagger/OpenAPI
- **Testing**: xUnit, Moq
- **Resilience**: Polly (retry, circuit breaker, timeout)
- **Caching**: IMemoryCache + Redis (StackExchange.Redis)
- **Containerization**: Docker, Docker Compose
- **Logging**: Microsoft.Extensions.Logging

---

## 🚀 Getting Started

### Prerequisites

**Option 1: Docker (Recommended)**
- [Docker Desktop](https://www.docker.com/products/docker-desktop) (Windows/Mac/Linux)

**Option 2: Local Development**
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Any IDE (Visual Studio 2022, VS Code, Rider)

---

### Build & Run with Docker

This is the **easiest and recommended** method:

```powershell
# 1. Clone the repository
git clone https://github.com/pieterhs/transaction-aggregation-api.git
cd transaction-aggregation-api

# 2. Build and run with Docker Compose
docker-compose up --build

# 3. Access the API
# Swagger UI:  http://localhost:8080/
# Health:      http://localhost:8080/health
# Metrics:     http://localhost:8080/api/metrics
```

**API Key for Testing**: `dev-api-key-12345` (set in docker-compose.yml)

**Cache Configuration**: Docker Compose uses **Redis** by default (see environment variables in docker-compose.yml)

To stop:
```powershell
docker-compose down
```

---

### Build & Run Locally

For local development without Docker:

```powershell
# 1. Clone the repository
git clone https://github.com/pieterhs/transaction-aggregation-api.git
cd transaction-aggregation-api

# 2. Restore dependencies
dotnet restore

# 3. Build the project
dotnet build

# 4. Run the API
cd TransactionAggregationApi.Api
dotnet run

# 5. Access the API at http://localhost:5000 (or https://localhost:5001)
```

**Default API Key**: `prod-api-key-change-in-production` (set in appsettings.json)

**Cache Configuration**: Local development uses **in-memory cache** by default (see appsettings.Development.json)

**To use Redis locally**:
1. Start Redis: `docker run -d -p 6379:6379 redis:7-alpine`
2. Update `appsettings.Development.json`: Set `"Cache:Provider": "Redis"`
3. Run the API: `dotnet run`

---

## 📖 API Documentation

### Interactive Swagger UI

Once running, visit: **http://localhost:8080/** (Docker) or **http://localhost:5000/** (local)

### Endpoints

| Method | Endpoint | Description | Auth Required |
|--------|----------|-------------|---------------|
| `GET` | `/api/transactions` | Get paginated transactions with filters | ✅ |
| `HEAD` | `/api/transactions` | Get pagination metadata only | ✅ |
| `GET` | `/api/transactions/{id}` | Get transaction by ID | ✅ |
| `POST` | `/api/transactions/batch` | Get transactions by IDs (batch) | ✅ |
| `GET` | `/api/transactions/categories` | Get all available categories | ✅ |
| `GET` | `/api/metrics` | Get API metrics (cache, requests) | ❌ |
| `GET` | `/health` | Health check | ❌ |

### Example Request

```bash
# PowerShell
$headers = @{ "X-Api-Key" = "dev-api-key-12345" }
Invoke-RestMethod -Uri "http://localhost:8080/api/transactions?from=2025-10-01&to=2025-10-15&category=Groceries&page=1&pageSize=20" -Headers $headers
```

```bash
# cURL
curl -H "X-Api-Key: dev-api-key-12345" \
  "http://localhost:8080/api/transactions?from=2025-10-01&to=2025-10-15&category=Groceries&page=1&pageSize=20"
```

### Example Response

```json
{
  "transactions": [
    {
      "id": "TXN-20251005-3782",
      "amount": 89.42,
      "currency": "USD",
      "category": "Groceries",
      "description": "Whole Foods Market",
      "date": "2025-10-05T14:23:00",
      "merchantName": "Whole Foods",
      "bankSource": "BankA"
    }
  ],
  "page": 1,
  "pageSize": 20,
  "total": 47,
  "totalPages": 3
}
```

### Query Parameters

| Parameter | Type | Required | Default | Description |
|-----------|------|----------|---------|-------------|
| `from` | DateTime | ✅ | - | Start date (ISO 8601: YYYY-MM-DD) |
| `to` | DateTime | ✅ | - | End date (ISO 8601: YYYY-MM-DD) |
| `category` | string | ❌ | null | Filter by category (case-insensitive) |
| `page` | int | ❌ | 1 | Page number (minimum: 1) |
| `pageSize` | int | ❌ | 50 | Items per page (range: 1-100) |

### Response Headers

```
X-Total-Count: 47          # Total matching transactions
X-Page: 1                  # Current page
X-PageSize: 20             # Items per page
X-Total-Pages: 3           # Total pages
```

---

## 🔐 Authentication

All endpoints (except `/health`, `/swagger`, `/api/metrics`) require API Key authentication:

### Header Format
```
X-Api-Key: your-api-key-here
```

### Default API Keys

- **Docker**: `dev-api-key-12345` (see `docker-compose.yml`)
- **Local**: `prod-api-key-change-in-production` (see `appsettings.json`)

### Configuration

Set the API key via:
1. **Environment Variable** (recommended for production):
   ```powershell
   $env:ApiKey = "your-secure-key"
   ```

2. **appsettings.json**:
   ```json
   {
     "ApiKey": "your-secure-key"
   }
   ```

3. **Docker Compose**:
   ```yaml
   environment:
     - ApiKey=your-secure-key
   ```

### Example: Unauthorized (401)

```json
{
  "error": "Invalid API key"
}
```

---

## 🧪 Testing

### Run All Tests

```powershell
# From repository root
dotnet test
```

### Test Coverage

```
✅ 51 tests passing (100%)
```

### Test Categories

1. **TransactionsControllerTests** (15 tests)
   - Parameter validation
   - Pagination
   - Error handling
   - Response headers

2. **TransactionServiceTests** (18 tests)
   - Aggregation logic
   - Caching behavior
   - Filtering
   - Concurrent bank calls

3. **AuthMiddlewareTests** (10 tests)
   - API key validation
   - Excluded paths
   - Error responses

4. **TransactionCacheTests** (8 tests)
   - Cache operations
   - TTL behavior
   - Thread safety

### Run Specific Tests

```powershell
# Run only controller tests
dotnet test --filter FullyQualifiedName~TransactionsControllerTests

# Run with detailed output
dotnet test --verbosity detailed
```

---

## 📁 Project Structure

```
transaction-aggregation-api/
├── TransactionAggregationApi.Api/          # Main API project
│   ├── Controllers/                        # API endpoints
│   │   ├── TransactionsController.cs       # Transaction endpoints
│   │   └── MetricsController.cs            # Metrics endpoint
│   ├── Services/                           # Business logic
│   │   ├── ITransactionService.cs
│   │   └── TransactionService.cs           # Aggregation & caching
│   ├── Clients/                            # Bank API clients
│   │   ├── IBankClient.cs
│   │   ├── BankAClient.cs
│   │   ├── BankBClient.cs
│   │   └── BankCClient.cs
│   ├── Infrastructure/                     # Cross-cutting concerns
│   │   ├── ITransactionCache.cs
│   │   ├── TransactionCache.cs             # In-memory cache
│   │   └── RedisTransactionCache.cs        # Redis distributed cache
│   ├── Middleware/                         # Request pipeline
│   │   └── AuthMiddleware.cs               # API key authentication
│   ├── Models/                             # DTOs
│   │   ├── TransactionDto.cs
│   │   └── PagedResultDto.cs
│   ├── Program.cs                          # Application entry point
│   └── appsettings.json                    # Configuration
│
├── TransactionAggregationApi.Tests/        # Unit tests (xUnit)
│   ├── TransactionsControllerTests.cs
│   ├── TransactionServiceTests.cs
│   ├── AuthMiddlewareTests.cs
│   └── TransactionCacheTests.cs
│
├── docs/                                   # Architecture diagrams
│   ├── transaction_aggregation_api_architecture_enhanced.png
│   ├── transaction_aggregation_api_component_interaction.png
│   └── transaction_aggregation_api_sequence_detailed.png
│
├── Dockerfile                              # Multi-stage production build
├── docker-compose.yml                      # Docker orchestration
├── .dockerignore                           # Docker build exclusions
├── TransactionAggregationApi.sln           # Solution file
└── README.md                               # This file
```

---

## 🏭 Production Considerations

This project implements production-grade patterns and practices:

### ✅ Implemented

- **Security**
  - API key authentication with secure header transmission
  - Input validation and sanitization
  - Non-root Docker user (app user)
  - Read-only filesystem support

- **Performance**
  - Multi-stage Docker builds (Alpine, ~110MB)
  - Layer caching optimization
  - **Redis distributed caching** with JSON serialization
  - In-memory caching fallback for development
  - Configurable TTL (default: 10 minutes)
  - Concurrent API calls

- **Resilience**
  - Retry policies with exponential backoff
  - Circuit breaker for failing dependencies
  - Timeout policies
  - Graceful degradation

- **Observability**
  - Structured logging with correlation IDs
  - Health check endpoint
  - Metrics endpoint (cache stats, request counts)
  - Comprehensive error messages

- **Code Quality**
  - 51 unit tests with Moq
  - XML documentation comments
  - Clean architecture (separation of concerns)
  - SOLID principles

### 🔮 Future Enhancements (Production Roadmap)

**High Priority:**
- [ ] Implement **JWT/OAuth2** authentication
- [ ] Add **rate limiting** (per API key)
- [ ] Set up **Application Insights** / OpenTelemetry
- [ ] Add **integration tests** with TestContainers

**Medium Priority:**
- [ ] Add **Redis Sentinel** for high availability
- [ ] Implement **CQRS** pattern for read/write separation
- [ ] Add **background jobs** for cache warming
- [ ] Set up **CI/CD pipeline** (GitHub Actions)
- [ ] Add **API versioning** (/v1, /v2)
- [ ] Implement **request throttling**

**Nice to Have:**
- [ ] GraphQL endpoint for flexible queries
- [ ] WebSocket support for real-time updates
- [ ] Multi-tenancy support
- [ ] Advanced analytics dashboard
- [ ] Internationalization (i18n)

---

## 📝 Design Decisions

### Why Mock Bank Clients?
The brief requires aggregation from "multiple mock banking systems". Each client (`BankAClient`, `BankBClient`, `BankCClient`) simulates realistic scenarios:
- Variable network latency (100-800ms)
- Transient failures (10% chance) to test resilience
- Different transaction patterns per bank

### Cache Strategy: Dual Implementation
The API supports **both in-memory and Redis caching** via configuration:

**In-Memory Cache** (Development):
- Zero dependencies, fast local development
- Configured in `appsettings.Development.json`

**Redis Cache** (Production):
- Distributed caching for horizontal scaling
- Configured via environment variables in `docker-compose.yml`
- Uses `Microsoft.Extensions.Caching.StackExchangeRedis`
- JSON serialization with System.Text.Json
- AOF persistence enabled

**Switching between providers**:
```json
{
  "Cache": {
    "Provider": "Memory",  // or "Redis"
    "DefaultTtlMinutes": 10,
    "Redis": {
      "Configuration": "localhost:6379"
    }
  }
}
```

### Why API Key Authentication?
The brief specifies "simple authentication". API keys are:
- Simple to implement and test
- Suitable for server-to-server communication
- Easy to rotate and revoke
- Production note: Migrate to JWT/OAuth2 for user-facing scenarios

### Why Polly for Resilience?
Polly is the industry-standard .NET library for:
- Transient fault handling
- Retry with exponential backoff
- Circuit breaker pattern
- Production-proven reliability

---
