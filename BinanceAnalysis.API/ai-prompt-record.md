AI-Assisted Development — Prompt Record
Senior Backend Engineer Technical Assessment — VSG Bulgaria
Purpose and development approach
I designed the application architecture, defined the responsibilities of its components, selected the technologies and established the expected behavior of the main features.
During implementation, I used AI as a development assistant to help translate my technical requirements into individual classes, methods and supporting code. Rather than asking AI to design and generate the entire application, I provided detailed instructions describing how particular components should work, their responsibilities, constraints and how they should integrate with the existing solution.
I reviewed the generated suggestions, adapted them where necessary, integrated the code into the solution and verified the implementation through builds, automated tests and manual execution.
The prompts below are representative examples of the instructions used during development. They are illustrative summaries, not verbatim conversation transcripts.
1. Solution architecture and component implementation
Representative prompts
Based on the existing solution structure, implement the service responsible for trade ingestion. It should follow the existing project boundaries and use dependency injection. Keep WebSocket communication separate from database persistence.
Implement the repository and service classes according to the existing entity models and DbContext. Follow the established naming conventions and avoid moving responsibilities between projects.
AI assistance: Helped implement individual classes and methods within the architecture and project structure I had established.
2. Binance WebSocket communication
Representative prompts
Implement a Binance Spot combined-stream WebSocket client using ClientWebSocket. It must connect to the combined stream endpoint, subscribe to the requested symbols using the trade stream, deserialize incoming trade events and support fragmented messages.
Extend the existing client to support runtime SUBSCRIBE and UNSUBSCRIBE commands. Use request IDs, serialize outgoing WebSocket sends with SemaphoreSlim and log subscription responses and errors.
Implement appropriate cancellation and connection lifecycle handling without introducing concurrent receive operations on the same WebSocket.
AI assistance: Helped implement and refine the WebSocket client, message handling, dynamic subscriptions and logging based on the specified behavior.
3. Trade ingestion and database persistence
Representative prompts
Implement a background service that receives trade events from the WebSocket client and passes them through a bounded channel to a separate persistence consumer. The channel must apply backpressure rather than allowing unlimited memory growth.
Implement batch persistence with configurable batch size and flush interval. Handle duplicate trades and respect the unique database constraint on symbol and trade ID.
Add reconnection with bounded exponential backoff and periodically refresh the active monitored symbols so that runtime changes are reflected in Binance subscriptions.
AI assistance: Helped implement the background service, channel-based processing, batching, persistence logic and reconnection behavior.
4. Price analysis services and API endpoints
Representative prompts
Implement a service that calculates the arithmetic average of stored trade prices over the preceding 24 hours for a requested symbol. Use UTC timestamps and return an appropriate result when no data is available.
Implement the Simple Moving Average calculation. Support the periods 1w, 1d, 30m, 5m and 1m. Select the requested number of most recent stored trade observations within the specified time window and handle insufficient data.
Implement the API controller with the required routes, query parameters, validation and JSON/XML response support. Keep calculation logic in the service layer rather than the controller.
AI assistance: Helped implement calculation methods, validation, controller actions and response formatting according to the defined API requirements.
5. Runtime symbol administration
Representative prompts
Implement the monitored-symbol service with methods to add, activate, deactivate and delete symbols. Normalize symbols to uppercase and validate new symbols against Binance exchange information before insertion.
Make activation and deletion operations atomic using EF Core bulk operations. Consider concurrent requests and rely on the database unique constraint as the final protection against duplicate symbols.
Implement the administrator API endpoints and restrict access using the Admin role.
AI assistance: Helped implement the administration service, Binance symbol validation, database operations and protected API endpoints.
6. Authentication and application reliability
Representative prompts
Implement JWT token issuance for the administrator using the configured credentials, issuer, audience, signing key and expiration settings. Validate the configuration at application startup.
Configure JWT bearer authentication and ensure that only authenticated users with the Admin role can access symbol administration endpoints. Keep price-analysis endpoints publicly accessible.
Implement centralized exception handling that returns safe ProblemDetails responses without exposing internal exception details.
AI assistance: Helped implement authentication, authorization, configuration validation and centralized error handling.
7. Automated testing and implementation review
Representative prompts
Review the existing tests for the monitored-symbol service. The production implementation uses EF Core ExecuteUpdateAsync and ExecuteDeleteAsync, which are not supported by the InMemory provider. Suggest how to test these operations against SQL Server without weakening the production implementation.
Write integration tests for concurrent operations on monitored symbols, including simultaneous activation, deactivation, deletion and addition. Use separate DbContext instances to represent concurrent requests.
Review the persistence implementation for duplicate trade handling and identify potential race conditions involving concurrent batches.
AI assistance: Helped identify testing considerations, implement test cases and review edge cases. I executed the builds and tests and used their results to validate the changes.
8. Documentation and deployment preparation
Representative prompts
Prepare README instructions explaining how to clone the repository, configure environment variables, start SQL Server and the API using Docker Compose, and run the Console client.
Review the engineering notes against the actual implementation. Document assumptions, limitations and deployment considerations without claiming functionality that has not been implemented or verified.
AI assistance: Helped prepare and refine technical documentation, setup instructions and deployment proposals.
9. Developer responsibility
The architecture, component boundaries, technology choices, functional requirements and implementation decisions were established by me.
AI was used as an assistant during implementation, primarily to help write individual classes and methods based on detailed technical instructions, review code and suggest improvements.
I reviewed and integrated the generated code, resolved implementation issues and performed builds, automated tests and manual verification.
I take responsibility for the final implementation, its technical decisions, correctness, security and maintainability.

