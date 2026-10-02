# dotNet-testing-core

C# backend testing framework modeled on the TypeScript testing-core and ui-api-tests syntax.

## Authoring concepts

- SuiteAttribute names a test class, matching the role of Suite around a TypeScript describe block.
- RunWith.Cases expands language and platform combinations while carrying authorization and user-query data. Use it with xUnit MemberData.
- TestStep.Run wraps an action, expected result, and async callback. TestContext records pass/fail status and API/validation artifacts.
- BackendFixture shares the HTTP transport; CreateScope creates a fresh context and assertions for every test case.
- AzureDevOpsAdapter reads, creates, and updates Test Case work items and creates Bug work items. It accepts a token provider, so credentials are never stored in source.
- TestCaseSync maps an @C123 case marker to an update; without a marker it creates a case from recorded test steps. Invoke this explicitly after a reviewed test run.
- Azure Pipelines publishes TRX results to the Tests tab. Test Plans cases and Boards bugs use the adapter only when explicitly invoked; failed runs do not automatically create duplicate Bugs.

AuthoringTests shows Suite, RunWith, and TestStep together. BackendTests exercises the context and assertions. The included HTTP handler simulates a backend; replace it with your real backend base URL or WebApplicationFactory.

Run locally: dotnet test dotNet-testing-core.sln

Configure AzureDevOpsAdapter with your organization, project, an HttpClient, and a token provider. The live Azure connection cannot be exercised until those values and permissions are supplied.