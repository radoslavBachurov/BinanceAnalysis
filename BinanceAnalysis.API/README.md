Getting Started — Clone and Run
The easiest way to run the application is with Docker Compose. The API and SQL Server run in containers, so you do not need to install SQL Server, Visual Studio or the .NET SDK to start the backend.
1. Prerequisites
• Git
• Docker Desktop with the Linux container engine enabled
• Internet access
Make sure Docker Desktop is running before continuing.
2. Clone the repository
Open PowerShell and run:
git clone https://github.com/radoslavBachurov/BinanceAnalysis.git
cd BinanceAnalysis
3. Configure environment variables
Create your local environment file from the provided example:
Copy-Item .env.example .env
Open .env and configure the following values:
VariableDescriptionSA_PASSWORDStrong password for the SQL Server containerJWT_ISSUERJWT issuer; keep the provided valueJWT_AUDIENCEJWT audience; keep the provided valueJWT_SIGNING_KEYBase64-encoded random key containing at least 32 bytesADMIN_USERNAMEAdministrator usernameADMIN_PASSWORDAdministrator passwordReplace the placeholder signing key and example passwords with your own values. These credentials are used by the local Docker environment. Do not commit the .env file.
4. Build and start the application
From the repository root, execute:
docker compose up --build -d
Docker Compose will:
1. Build the ASP.NET Core API image.
2. Start SQL Server 2022.
3. Wait until SQL Server passes its health check.
4. Start the API container.
5. Allow the API to apply database migrations and initialize the database.
6. Start the Binance trade ingestion background service.
No manual database creation or connection-string changes are required. The database connection string is supplied to the API through Docker Compose environment variables.
5. Verify that the application is running
Check container status:
docker compose ps
Check API logs:
docker compose logs --tail 50 api
The API should be available at:
• API: http://localhost:8080
• Swagger UI: http://localhost:8080/swagger
Swagger is enabled in the Docker Compose Development environment.
6. Test the API
Example JSON request:
curl.exe -i http://localhost:8080/api/BTCUSDT/24hAvgPrice
Example XML request:
curl.exe -i -H "Accept: application/xml" `
  http://localhost:8080/api/BTCUSDT/24hAvgPrice
The initial monitored symbols are BTCUSDT, ADAUSDT and ETHUSDT. Allow some time for Binance trade events to be received and persisted before requesting price calculations.
7. Run the Console client
The Console application communicates with the API over HTTP.
To publish a self-contained Windows executable, install the .NET 10 SDK and run:
dotnet publish .\BinanceAnalysis.Console\BinanceAnalysis.Console.csproj `
  --configuration Release `
  --runtime win-x64 `
  --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  --output .\publish\Console
In publish\Console\appsettings.json, configure the API base URL:
{
  "ApiSettings": {
    "BaseUrl": "http://localhost:8080/"
  }
}
Run the published executable:
.\publish\Console\BinanceAnalysis.Console.exe
Use the administrator username and password configured in .env to authenticate.
The Console supports price analysis and monitored-symbol administration. It also handles access-token acquisition and refresh automatically.
8. Stop the application
To stop the containers:
docker compose down
The SQL Server data volume is preserved, so the database remains available the next time the application is started.
To remove the database volume and its data as well, use docker compose down -v. This permanently deletes the persisted local database data.

# Binance Market Data Collection and Analysis

Senior Backend Engineer Technical Assessment — VSG Bulgaria

## Overview

This .NET 10 solution collects Binance Spot trade events for configured symbols, persists them in SQL Server, and exposes an HTTP API for price analysis. A separate Console application consumes the API over HTTP.

## Solution projects

| Project | Responsibility |
|---|---|
| `BinanceAnalysis.API` | ASP.NET Core API, authentication, middleware and startup |
| `BinanceAnalysis.API.Infrastructure` | Binance WebSocket and REST integrations |
| `BinanceAnalysis.Data` | EF Core DbContext, configurations and migrations |
| `BinanceAnalysis.Data.Models` | Persistence entities |
| `BinanceAnalysis.Services.Data` | Ingestion persistence, symbol management and price analysis |
| `BinanceAnalysis.API.ViewModels` | API models |
| `BinanceAnalysis.Common` | Shared constants |
| `BinanceAnalysis.Console` | HTTP Console client |
| `BinanceAnalysis.Tests` | Unit and integration tests |

Solution file: `BinanceAnalysis.API.slnx`.

## Prerequisites

- .NET 10 SDK
- SQL Server
- Network access to Binance Spot REST and WebSocket endpoints
- Visual Studio 2022 with .NET 10 support, or the .NET CLI

## Configuration

Configure `ConnectionStrings:DefaultConnection` using user secrets or environment variables. Example local development connection string:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=.;Database=BinanceAnalysis;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True;"
  }
}
```

The API also requires JWT settings and administrator credentials. Supply them through user secrets or a secret store; do not commit real credentials or signing keys.

```json
{
  "Jwt": {
    "Issuer": "BinanceAnalysis.API",
    "Audience": "BinanceAnalysis.Client",
    "SigningKey": "<strong-random-base64-signing-key>",
    "ExpirationMinutes": 15
  },
  "AdminCredentials": {
    "Username": "<admin-username>",
    "Password": "<strong-admin-password>"
  }
}
```

Use a randomly generated signing key with at least 32 bytes of key material. Integration-test credentials and signing keys are test-only.

### Trade ingestion settings

The `TradeIngestion` section supports these defaults:

| Setting | Default | Purpose |
|---|---:|---|
| `ChannelCapacity` | 5000 | Bounded queue capacity |
| `BatchSize` | 500 | Maximum batch size |
| `FlushIntervalSeconds` | 1 | Flush partial batches |
| `InitialReconnectDelaySeconds` | 1 | Initial reconnect delay |
| `MaximumReconnectDelaySeconds` | 30 | Maximum reconnect delay |
| `StableConnectionSeconds` | 30 | Stable connection period for backoff reset |
| `SymbolRefreshIntervalSeconds` | 10 | Active-symbol refresh interval |

## Database initialization

The application uses EF Core with SQL Server. On API startup it applies pending migrations and runs the configured seed logic. The initial monitored symbols are `BTCUSDT`, `ADAUSDT` and `ETHUSDT`.

`MarketPrices` stores individual trade observations. A unique composite index on `(Symbol, TradeId)` prevents duplicate trade rows; an index on `(Symbol, ObservedAtUtc)` supports time-window queries.

`MonitoredSymbols` stores configured symbols and active status. Its unique `Symbol` index prevents duplicate configured symbols.

Ensure the configured SQL Server identity can connect and apply migrations.

## Build and run

From the solution root:

```powershell
dotnet restore BinanceAnalysis.API.slnx
dotnet build BinanceAnalysis.API.slnx
dotnet test BinanceAnalysis.API.slnx
dotnet run --project .\BinanceAnalysis.API\BinanceAnalysis.API.csproj
```

The API URL is determined by the launch profile or `ASPNETCORE_URLS`; check startup output or `Properties/launchSettings.json`.

###Console client
The solution includes an interactive Console application that consumes the HTTP API through a typed HttpClient. It does not connect directly to Binance or access the database.
Configure the API base URL in BinanceAnalysis.Console/appsettings.json:
{
  "ApiSettings": {
    "BaseUrl": "https://localhost:7036/"
  }
}
Update the URL to match the API's configured HTTPS or HTTP address.
Run the Console application from the solution root:
dotnet run --project .\BinanceAnalysis.Console\BinanceAnalysis.Console.csproj
The interactive menu provides the following operations:
Price analysis
Get 24-hour average price.
Get Simple Moving Average.
Monitored symbol administration
3. List monitored symbols.
4. Add monitored symbol.
5. Activate monitored symbol.
6. Deactivate monitored symbol.
7. Delete monitored symbol.
Enter 0 to exit.
For SMA calculations, the Console prompts for the trading symbol, number of data points, supported time period and optional UTC start date/time. Leave the date empty to use the default time window.
The Console uses the API's HTTP endpoints for all operations. Its HTTP resilience configuration disables automatic retries for POST, PUT and DELETE requests to avoid automatically repeating operations that modify monitored symbols.

## HTTP API

The API supports JSON and XML content negotiation. Clients can send `Accept: application/json` or `Accept: application/xml`.

### 24-hour average

```http
GET /api/{symbol}/24hAvgPrice
```

Calculates the arithmetic average of stored trade prices in the preceding 24-hour window. If less than 24 hours of observations exist, it averages the observations available in that window. If no observations exist, the API returns `404 Not Found`.

### Simple moving average

```http
GET /api/{symbol}/SimpleMovingAverage?n={numberOfDataPoints}&p={timePeriod}&s={startDateTime}
```

- `symbol`: requested trading symbol.
- `n`: positive number of data points, with a maximum of 10,000.
- `p`: one of `1w`, `1d`, `30m`, `5m`, `1m`.
- `s`: optional starting timestamp.

When `s` is omitted, the service uses the trailing window `[now - p, now]`. When `s` is supplied, it uses `[s, s + p]`. It selects the `n` most recent observations in that window, using the database ID as a tie-breaker. It returns the arithmetic mean only when at least `n` observations are available; otherwise it returns `404 Not Found`.

**Important interpretation:** `n` currently means stored trade observations, not OHLC candles or evenly spaced samples. The application does not synthesize one data point per time bucket. Confirm this interpretation with the assessment reviewer if candle-based SMA was intended.

### Administrative symbol management

All administrative routes require a JWT with the `Admin` role.

| Method | Route | Purpose |
|---|---|---|
| GET | `/api/admin/symbols` | List configured symbols |
| POST | `/api/admin/symbols` | Add a symbol |
| PUT | `/api/admin/symbols/{symbol}` | Activate/deactivate a symbol |
| DELETE | `/api/admin/symbols/{symbol}` | Delete a symbol |

Adding a symbol normalizes its casing, checks for an existing entry and validates it against Binance exchange information (the symbol must be in `TRADING` status). The unique database index remains authoritative under concurrent additions.

Status updates and deletes use atomic EF Core `ExecuteUpdateAsync` and `ExecuteDeleteAsync` operations. Concurrent status updates are resolved by SQL Server execution order. For a delete/update race, the update affects one row if it executes first, or zero rows if deletion has already occurred.

### Authentication

Request an access token:

```http
POST /api/auth/token
Content-Type: application/json
```

```json
{
  "username": "<configured-admin-username>",
  "password": "<configured-admin-password>"
}
```

Send the returned token to administrative endpoints:

```http
Authorization: Bearer <access-token>
```

Never commit real credentials or tokens.

## Reliability

- Binance Spot combined-stream WebSocket trade events are consumed by a background service.
- A bounded channel decouples receiving from persistence and applies backpressure.
- Trades are persisted in batches by size or flush interval.
- Reconnection uses bounded exponential backoff.
- Active symbols are refreshed periodically and subscriptions are updated dynamically.
- Database uniqueness constraints protect against duplicate trades and duplicate monitored symbols.
- EF Core SQL Server transient retry handling is configured.
- Serilog writes structured logs to the configured console and rolling-file sinks. `Logs/` is excluded from Git.
- A `SemaphoreSlim` serializes outgoing sends on the shared WebSocket connection. It is not used as database concurrency control.

## Tests

Run:

```powershell
dotnet test BinanceAnalysis.API.slnx
```

SQL Server integration tests use the separate `BinanceAnalysis_IntegrationTests` database configured in `appsettings.IntegrationTests.json`. The fixture verifies the database name before applying migrations. Ensure this database is available and never point the integration-test connection string at development or production data.

Coverage includes trade persistence duplicates/overlapping batches, monitored-symbol CRUD and concurrency, and authentication/authorization scenarios. InMemory tests are used for provider-independent behavior; SQL Server integration tests exercise bulk update/delete and database concurrency.

## Deployment proposal

A suitable production design would use:

- Azure App Service or Azure Container Apps for the .NET API.
- Azure SQL Database for relational persistence.
- Azure Key Vault for JWT signing material, administrator credentials and database secrets.
- Centralized structured logging, health checks, metrics and alerts for ingestion backlog, WebSocket reconnects, persistence failures and API errors.
- CI to build/test and publish an immutable artifact or container image.
- A controlled migration step before application rollout.

The hosted ingestion worker should initially run as a single intended owner of Binance subscriptions. If the HTTP API is scaled horizontally, separate ingestion into a dedicated worker deployment or introduce distributed lease/leader ownership. A database unique index prevents duplicate persisted rows but does not prevent duplicated network ingestion.

This is a deployment proposal, not a claim that the application is already deployed or containerized. Validate secret injection, health probes, migration ownership, shutdown draining and multi-instance ingestion ownership before production rollout.

## Assumptions and trade-offs

1. Binance `trade` events are persisted as individual observations, not OHLC candles.
2. The 24-hour average is the arithmetic mean of stored trade prices in the preceding 24 hours.
3. SMA uses `n` stored trade observations in the selected period window and requires at least `n` observations.
4. `s` denotes the start of a period-length window; without `s`, the window ends at current UTC time.
5. Symbols are normalized to uppercase and new symbols are checked against Binance `TRADING` status.
6. Administrative endpoints require the `Admin` role.
7. SQL Server is the relational database; integration tests use a separate database.
8. Live deployment is not included.

## AI-assisted development

AI tools were used to support design discussion, code review, test planning, issue identification and documentation. The developer reviewed the implementation and ran the build and tests. See [engineering-notes.md](engineering-notes.md) and [ai-prompt-record.md](ai-prompt-record.md).

