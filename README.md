# dotNet-testing-core

C# backend testing core based on the structure of the TypeScript testing-core project.

Implemented concepts:
- A per-test TestContext with metadata, named step lifecycle, passed/failed results, and API/validation artifacts.
- A transport port, IHttpTestClient, with an HttpClient adapter that records HTTP request/response details.
- Fluent API assertions that record validation artifacts and fail the step on mismatch.
- Ports for test management, issue tracking, and reporting. These contracts are ready for Azure adapters once the organization and project are known.
- xUnit class fixtures that inject context, HTTP client, and assertions into tests.

BackendTests uses a deterministic in-process HTTP handler. Replace StubHandler with a real backend URL or WebApplicationFactory for integration tests. No browser is required.

Run: dotnet test dotNet-testing-core.sln

Azure Pipelines publishes TRX test results. Test Plans case storage and Boards incidents require an Azure organization connection; this repository does not contain credentials or a guessed project ID.