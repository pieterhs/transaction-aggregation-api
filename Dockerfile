# ============================================================
# Multi-Stage Dockerfile for Transaction Aggregation API
# ============================================================
# Features:
# - Alpine-based images for minimal size (~110MB vs ~220MB)
# - Layer caching optimization (csproj → restore → source)
# - Security: Non-root user, read-only filesystem ready
# - Health check support
# ============================================================

# ============================================================
# Stage 1: Build
# ============================================================
FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build

# Install necessary build tools
RUN apk add --no-cache bash

WORKDIR /src

# Copy solution and project files first for better layer caching
# This layer will be cached unless project files change
COPY TransactionAggregationApi.sln .
COPY TransactionAggregationApi.Api/TransactionAggregationApi.Api.csproj TransactionAggregationApi.Api/
COPY TransactionAggregationApi.Tests/TransactionAggregationApi.Tests.csproj TransactionAggregationApi.Tests/

# Restore NuGet packages (cached layer)
RUN dotnet restore TransactionAggregationApi.sln

# Copy the rest of the source code
# This layer will be rebuilt when source code changes
COPY TransactionAggregationApi.Api/ TransactionAggregationApi.Api/
COPY TransactionAggregationApi.Tests/ TransactionAggregationApi.Tests/

# Build the project
WORKDIR /src/TransactionAggregationApi.Api
RUN dotnet build -c Release --no-restore

# ============================================================
# Stage 2: Publish
# ============================================================
FROM build AS publish
WORKDIR /src/TransactionAggregationApi.Api

# Publish the application
# - No self-contained deployment (uses runtime image)
# - Optimized for size and startup time
RUN dotnet publish -c Release -o /out --no-restore --no-build /p:UseAppHost=false

# ============================================================
# Stage 3: Runtime
# ============================================================
FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS runtime

# Install necessary packages
# - curl: for health checks
# - icu-libs: for globalization support (.NET requires ICU)
RUN apk add --no-cache \
    curl \
    icu-libs

# Create non-root user for security
RUN addgroup -g 1000 appuser && \
    adduser -u 1000 -G appuser -s /bin/sh -D appuser

WORKDIR /app

# Copy published application from publish stage
COPY --from=publish /out .

# Change ownership to non-root user
RUN chown -R appuser:appuser /app

# Switch to non-root user
USER appuser

# Expose port 8080 (non-privileged port)
EXPOSE 8080

# Set environment variables
ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_RUNNING_IN_CONTAINER=true \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false

# Health check configuration
# Checks /health endpoint every 30 seconds
# Waits 10 seconds before first check
# Times out after 3 seconds
# Considers unhealthy after 3 consecutive failures
HEALTHCHECK --interval=30s --timeout=3s --start-period=10s --retries=3 \
    CMD curl -f http://localhost:8080/health || exit 1

# Entry point
ENTRYPOINT ["dotnet", "TransactionAggregationApi.Api.dll"]
