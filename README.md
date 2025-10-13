# 🏗️ Architecture Overview

The **Transaction Aggregation API** consolidates customer transactions from multiple mock banking systems into a unified JSON response.  
It supports **filtering, pagination, authentication, caching, and resilience** — designed for production-grade scalability.

---

## 🧠 High-Level Design

| Layer | Responsibility | Example Components |
|-------|----------------|--------------------|
| **API Layer** | REST endpoints for `/api/transactions` with authentication and Swagger | `TransactionsController`, `AuthMiddleware` |
| **Application Layer** | Business logic for aggregation, filtering, pagination, caching | `TransactionService`, `TransactionCache` |
| **Integration Layer** | Parallel queries to multiple mock bank APIs | `BankAClient`, `BankBClient`, `BankCClient` |
| **Infrastructure** | Observability, caching, resilience, configuration | `Polly Resilience Layer`, `MetricsService` |

---

## 🗺️ Architectural Diagram

![Transaction Aggregation API Architecture](./docs/transaction_aggregation_api_architecture_enhanced.png)

**Key Features**
- **Authentication:** API key middleware to restrict access.  
- **Caching:** In-memory or Redis cache (TTL ≈ 10 min) to reduce latency.  
- **Resilience:** Polly policies for retry, rate-limit, and circuit-breaker logic.  
- **Parallel Aggregation:** `Task.WhenAll()` to fetch from all banks concurrently.  
- **Observability:** Health and metrics endpoints expose cache hit rate, latency, and request counts.

---

## 🔄 Sequence Flow

![Request Lifecycle](./docs/transaction_aggregation_api_sequence_detailed.png)

1. **Client Request:** Authenticated `GET /api/transactions?...`  
2. **Cache Check:** Immediate return if cached.  
3. **Resilience & Aggregation:** On miss, Polly guards external calls; parallel tasks fetch transactions.  
4. **Normalization & Caching:** Responses merged, normalized, cached.  
5. **Response:** Paginated, consistent JSON schema.  
6. **Metrics Logged:** Cache statistics and API timing recorded for monitoring.

---

## 🔌 Component Interaction

![Component Interaction Diagram](./docs/transaction_aggregation_api_component_interaction.png)

**Flow Summary**
1. The **Client** sends a request to the **API**.  
2. **TransactionService** orchestrates filtering, caching, and aggregation.  
3. **TransactionCache** returns or stores data.  
4. **Polly Resilience Layer** applies retry and rate-limit safeguards.  
5. Multiple **Mock Bank APIs** are queried in parallel.  
6. **Metrics & Observability** collect health, latency, and cache stats.  
7. **Unified JSON Response** is returned to the client.

---

## 🧩 Data Model (simplified)

```json
{
  "id": "uuid",
  "date": "2025-10-10",
  "amount": 120.50,
  "currency": "ZAR",
  "category": "Food",
  "source": "BankA"
}
```

Paginated Response:
```json
{
  "total": 123,
  "page": 1,
  "pageSize": 50,
  "transactions": [ ... ]
}
```

---

## ⚙️ Optional Enhancements
- **ETag / Conditional GETs** — reduces redundant data transfer.  
- **Rate Limiting** — protect upstream mock APIs.  
- **Circuit Breakers** — ensure degraded mode resilience.  
- **Prometheus Metrics** — for cache and latency insights.  
- **Redis Cache** — replace in-memory cache for distributed deployments.

---

## ✅ Summary

This architecture demonstrates **SE3-level design maturity**:
- Scalable and resilient structure.  
- Clear separation of concerns.  
- Production-ready operational visibility.  
- Extensible foundation for integrating real banking APIs or third-party data sources.

---

## 💡 Design Rationale

This section explains the key design decisions behind the **Transaction Aggregation API** and how they align with production-grade, SE3-level engineering principles.

---

### **1. Modular, Layered Architecture**
The project is intentionally structured into clear layers — API, Application, Integration, and Infrastructure — to ensure high cohesion and low coupling.  
Each layer serves a single purpose:
- **API Layer:** Handles routing, validation, authentication, and consistent response formatting.
- **Application Layer:** Implements aggregation, filtering, pagination, and caching logic.
- **Integration Layer:** Encapsulates communication with external mock bank APIs.
- **Infrastructure Layer:** Provides cross-cutting concerns like caching, resilience, and observability.

This separation makes the system maintainable, testable, and easy to extend with new data sources or caching strategies.

---

### **2. Caching Strategy**
Caching was introduced to improve performance and resilience:
- **Motivation:** Transaction data doesn’t change frequently, but external APIs are slow or rate-limited.
- **Implementation:** A `TransactionCache` abstraction allows swapping between in-memory and distributed (Redis) caching without touching the service logic.
- **TTL:** A configurable time-to-live (5–15 minutes) balances freshness with performance.
- **Key Design:** Composite cache keys (`userId:dateRange:category:page`) prevent collisions and enable fine-grained cache reuse.

This approach provides fast responses for repeated queries and shields upstream systems from redundant requests.

---

### **3. Resilience with Polly**
External APIs are simulated as unreliable. To handle transient failures gracefully:
- **Retry Policy:** Retries transient errors with exponential backoff.
- **Circuit Breaker:** Temporarily halts requests when a bank API repeatedly fails.
- **Rate Limiting:** Prevents overload on slower mock systems.

Using **Polly** introduces fault tolerance and aligns with real-world distributed system resilience patterns.

---

### **4. Parallel Aggregation for Performance**
The aggregation layer fetches data from all mock bank systems concurrently using `Task.WhenAll()`.  
Benefits:
- Minimizes total response time compared to sequential calls.
- Allows future scalability (e.g., dozens of providers).
- Keeps the service responsive under load.

Each bank client implements a shared interface so new integrations can be added with minimal code changes.

---

### **5. Consistent JSON Schema**
Different bank APIs may return varied formats.  
To ensure consistency:
- All external data is normalized into a unified **Transaction DTO**.
- A consistent response schema simplifies client-side parsing and reduces integration friction.
- Pagination and filtering logic are standardized across all sources.

This ensures predictability and makes the API easy to consume by other systems or front-end teams.

---

### **6. Observability & Metrics**
The service tracks:
- Cache hit/miss ratios
- Request latency
- Retry counts
- Error rates

Metrics can be exposed through `/api/admin/metrics` or integrated with Prometheus.  
This makes the service transparent and easier to monitor in production environments.

---

### **7. Authentication Simplicity**
A lightweight **API key middleware** protects endpoints without over-engineering OAuth2 or JWT.  
This meets the brief’s requirement for simple authentication while keeping the door open for future extension.

---

### **8. Extensibility**
Key areas are designed for easy evolution:
- Swap `MemoryCache` → Redis without changing business logic.
- Add new bank integrations by implementing a new client class.
- Extend observability by plugging in third-party metrics exporters.

This future-proofs the solution for real-world adoption.

---

### ✅ **Summary**
Every design choice — from caching and Polly policies to normalization and metrics — was made to balance **performance**, **resilience**, and **maintainability**.  
The result is a system that behaves predictably under load, degrades gracefully under failure, and can scale horizontally with minimal friction.

