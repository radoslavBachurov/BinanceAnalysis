# Engineering Notes

Senior Backend Engineer Technical Assessment — VSG Bulgaria

## 1. Approach

The solution separates external Binance communication, persistence, business services, HTTP endpoints and the Console client. The API infrastructure owns Binance-specific protocol handling. Services work with typed events and persisted data rather than raw WebSocket payloads.

Engineering priorities were reliable ingestion, runtime symbol configuration, explicit time-window semantics, database-enforced concurrency, administrative security and meaningful automated tests.

## 2. Project responsibilities

- `BinanceAnalysis.API`: ASP.NET Core endpoints, authentication, middleware, dependency injection, startup migration and seeding.
- `BinanceAnalysis.API.Infrastructure`: Binance WebSocket/REST clients and transport DTOs.
- `BinanceAnalysis.Data`: EF Core `ApplicationDbContext`, configurations and migrations.
- `BinanceAnalysis.Data.Models`: persistence entities.
- `BinanceAnalysis.Services.Data`: trade persistence, monitored-symbol management and price analysis.
- `BinanceAnalysis.Console`: HTTP client for the API.
- `BinanceAnalysis.Tests`: unit and integration tests.

## 3. Binance ingestion

The WebSocket client connects to `wss://stream.binance.com:9443/stream`, receives combined-stream messages and deserializes trade events. Subscription responses are handled separately from trade payloads.

The ingestion service uses a bounded channel to separate receiving from database writes. This limits queued memory and applies backpressure when persistence is slower than ingestion. The consumer persists batches according to batch-size and flush-interval settings. Active symbols are refreshed from the database periodically.

The client compares desired subscriptions with its local subscription set and sends `SUBSCRIBE`/`UNSUBSCRIBE` commands. A `SemaphoreSlim(1, 1)` serializes outgoing sends because the same `ClientWebSocket` is shared by the subscription manager and receive workflow. This is a narrowly scoped transport lock, not a general application lock.

Reconnects use bounded exponential backoff. Cancellation is propagated through receive and subscription workflows.


Subscription commands wait for their matching Binance acknowledgement, with a 10-second timeout. Rejected or unexpected responses fail the current WebSocket session so that the ingestion service can reconnect and rebuild subscriptions from the active database configuration. Pending acknowledgement tasks are also completed when the receive loop terminates.




### Deployment consideration

The hosted ingestion service is intended to have one active owner of Binance subscriptions. Running multiple API replicas with the ingestion worker enabled can create duplicate network subscriptions and event traffic. A scaled deployment should separate the ingestion worker or use distributed lease/leader ownership. The database unique index protects stored uniqueness but does not eliminate duplicated ingestion traffic.

Shutdown draining should be verified under realistic cancellation and database-failure conditions before claiming a strict no-loss guarantee. A normal Ctrl+C shutdown was manually verified to complete persistence of the remaining queued batch before process exit. This does not establish a strict durability guarantee under forced termination or database failure.

## 4. Persistence and data integrity

`MarketPrice` stores symbol, Binance trade ID, decimal price, observed timestamp and creation timestamp.

- Unique `(Symbol, TradeId)` index prevents duplicate storage of a Binance trade.
- `(Symbol, ObservedAtUtc)` index supports time-window queries.
- Price precision is configured in the EF Core model.
- Binance string prices are parsed using invariant culture.
- Binance Unix-millisecond timestamps are converted to UTC.

Batch-level duplicate filtering reduces unnecessary work; the database unique index is the authoritative protection across independent writers. The persistence service handles SQL Server duplicate-key errors. EF Core SQL Server transient retry handling is configured.

`MonitoredSymbol` stores a unique symbol, active flag and creation timestamp.

## 5. Symbol administration and concurrency

### Add

The add operation trims and uppercases the symbol, checks for an existing row, validates the symbol against Binance exchange information and inserts it. Binance validation requires the symbol to be in `TRADING` status.

The initial existence check is only a fast path. Two requests can both observe that the symbol is absent. The unique database index resolves this race; SQL Server duplicate-key errors `2601` and `2627` are mapped to `AlreadyExists`. The failed entity is detached from the EF change tracker before returning.

### Activate/deactivate and delete

`SetActiveStatusAsync` uses `ExecuteUpdateAsync`; `DeleteAsync` uses `ExecuteDeleteAsync`. These are single database-side operations and return the number of affected rows. This avoids a separate read followed by a tracked save.

Concurrent status changes are resolved by SQL Server execution order (last executed update wins). In a delete/update race, the update affects one row if it executes first and zero rows if the delete has already removed the row. With two deletes, only one can affect the row.

### Why no semaphore or rowversion?

A semaphore scoped to the service instance would not coordinate separate HTTP request scopes. A static/process-wide semaphore would not coordinate multiple application instances and would add unnecessary serialization. Atomic SQL statements and unique constraints provide the relevant guarantees across processes.

A `rowversion` column would be appropriate if stale updates needed to be detected and reported as conflicts. The current behavior is last-database-write-wins, so rowversion is not required.

## 6. Price analysis semantics

### 24-hour average

The service calculates the arithmetic average of stored trade observations in the preceding 24 hours using UTC timestamps. If less than 24 hours of observations are available, it averages the available observations in that window. An empty window produces no result.

### Simple moving average

Supported periods are `1w`, `1d`, `30m`, `5m` and `1m`. `n` is validated as positive and capped at 10,000.

- Without `s`, the query window is `[now - p, now]`.
- With `s`, the query window is `[s, s + p]`.
- Observations are ordered by timestamp descending, with ID descending as a tie-breaker.
- The service takes `n` observations and calculates their arithmetic mean only if at least `n` observations exist.

This interpretation deserves explicit attention: the application stores individual trades, so `n` means `n` stored trade observations in the period window. It does not mean `n` evenly spaced samples or `n` OHLC candle closes. If candle-based SMA is expected, an aggregation stage and a clearly defined candle interval/price field would be needed.

## 7. API security and error handling

The API supports JSON/XML content negotiation. Price endpoints are public in the current implementation. Administrative symbol endpoints require a JWT with the `Admin` role. The token endpoint validates configured administrator credentials and issues a signed token.

JWT issuer, audience, signing key and expiry are configuration-driven. Development secrets belong in user secrets; production secrets should be injected from a managed secret store. Integration-test credentials and signing keys are test-only.

Global exception handling returns safe ProblemDetails responses with a trace identifier. Internal exception details should remain in server logs. Swagger includes bearer authentication support.

## 8. Console client

The Console client calls the same HTTP API endpoints as other clients. It does not connect directly to Binance or query SQL Server. This avoids duplicating price-analysis logic. The Console HTTP client has resilience configuration and avoids automatic retries for non-idempotent operations where applicable.

## 9. Testing and validation

### InMemory unit tests

EF Core InMemory is used for provider-independent behavior such as listing symbols and normal add-flow behavior. It is not a relational database and does not support `ExecuteUpdateAsync` or `ExecuteDeleteAsync`; those methods must be tested with a relational provider.

### SQL Server integration tests

`SqlServerIntegrationTestFixture` reads `appsettings.IntegrationTests.json`, verifies that the initial catalog is exactly `BinanceAnalysis_IntegrationTests`, and applies migrations. This guards against accidentally running integration tests against the development database.

Concurrent operations use separate `ApplicationDbContext` instances. Tests cover:

- Repeated, overlapping and concurrent duplicate trade batches.
- Concurrent symbol activate/deactivate.
- Concurrent delete/deactivate.
- Concurrent delete/delete.
- Concurrent activate/activate.
- Concurrent add/add.

The concurrent-add test uses a command interceptor to synchronize the existence checks, making both operations reach the insert race rather than relying only on scheduler timing.

Authentication/authorization integration tests cover token issuance, invalid credentials, anonymous access, invalid bearer tokens, role restrictions and public price endpoint access.

The full solution build and test suite passed after the symbol concurrency changes.

## 10. Issues identified and addressed

### InMemory provider and bulk operations

After adopting `ExecuteUpdateAsync` and `ExecuteDeleteAsync`, existing InMemory tests failed because the provider does not implement those operations. The production implementation was retained and status/delete tests were moved to SQL Server integration tests.

### Concurrent symbol addition

A read-before-insert check is insufficient under concurrent requests. The unique database index plus duplicate-key handling ensures one insert wins and the other request receives `AlreadyExists`.

### Read-then-write status/delete

Read-then-write operations can race across request scopes. Atomic database-side update/delete statements avoid that race and report affected-row counts.

### Scope of application locks

A scoped semaphore does not coordinate separate requests. A process-wide semaphore does not coordinate multiple instances. Database constraints and atomic operations are the appropriate correctness boundary here.

### Public endpoint test expectation

A security integration test initially expected 404 from a public price endpoint, but the integration database contained data and returned a successful response. The assertion was changed to test the intended security property: the endpoint is not rejected with 401 or 403.

## 11. Known limitations and future improvements

2. Ensure a single ingestion owner or distributed lease when deploying multiple API instances.
3. Clarify whether SMA data points mean individual trades or fixed-interval/candle observations.
4. Confirm the optional `s` window interpretation with the assessment reviewer if necessary.
5. Verify queued-event draining and durability during graceful shutdown and prolonged database outages.
6. Add metrics for received/persisted trades, channel depth, batch latency, reconnects and subscription failures.
7. Add readiness/liveness health checks appropriate to the hosting platform.
8. Harden production secret injection, SQL permissions, TLS, backups, rotation and alerting.

## 12. AI-assisted development

AI was used to assist with architecture discussion, code review, concurrency analysis, test planning and documentation. Suggestions were reviewed rather than accepted as proof of correctness. The developer adapted the suggestions to the existing solution and ran the build and tests.

See `ai-prompt-record.md` for representative prompt summaries. They are reconstructed summaries, not a verbatim transcript.

