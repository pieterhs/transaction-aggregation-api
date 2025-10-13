# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution and project files
COPY TransactionAggregationApi.sln .
COPY TransactionAggregationApi.Api/TransactionAggregationApi.Api.csproj TransactionAggregationApi.Api/
COPY TransactionAggregationApi.Tests/TransactionAggregationApi.Tests.csproj TransactionAggregationApi.Tests/

# Restore dependencies
RUN dotnet restore

# Copy the rest of the source code
COPY . .

# Build the project
WORKDIR /src/TransactionAggregationApi.Api
RUN dotnet build -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
EXPOSE 8080

# Copy published files
COPY --from=publish /app/publish .

# Set environment variables
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "TransactionAggregationApi.Api.dll"]
