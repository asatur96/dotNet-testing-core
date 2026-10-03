# TypeScript source-of-truth mapping

Reference project: C:\Users\balya\Downloads\automation-tests

| TypeScript layer | Current C# layer | Status |
| --- | --- | --- |
| testing-core domain contexts, artifacts, generators | TestingCore domain assembly | TestContext, StepContext, SuiteContext, typed test management metadata, run summary, artifacts, and generic generators implemented |
| testing-core ports and application use cases | TestingCore.Ports and TestingCore.Application | HTTP, config, credential, Test Case, Bug, traceability ports; assertions, case sync, work item linking, and run summary lifecycle implemented |
| testing-core integration adapters | TestingCore.Infrastructure | Azure DevOps work items, environment config, credentials, HTTP, typed integration registry and atomic JSON summary store implemented |
| playwright-lib fixture and Suite lifecycle | TestingCore.Xunit | Suite fixture, per-test context, hooks, opt-in case sync and suite summary callbacks, result capture, RunWith theory bridge, TestStep implemented |
| user-management-service | backend monolith source | Pending actual backend identity model |
| services | backend monolith source | Pending actual application services, repositories, data and event contracts |
| ui-api-tests consumer | TestingCore.Tests | Framework examples only; no monolith project reference yet |

Design rule: TestingCore.Domain has no xUnit, Azure, or HTTP dependency. TestingCore.Xunit is the runner bridge, just as playwright-lib is the Playwright bridge. Adding another runner should reuse domain/application/ports and replace this bridge.

Current limits: SuiteAttribute names a suite, while SuiteFixture performs the lifecycle. A consumer must derive a fixture and call RunAsync for tests it wants included in SuiteContext. Azure Test Case writes remain opt-in through TestCaseSync, which can run from the fixture callback before TestContext.Finish. Pipeline TRX publishing is separate. A newly created case ID must be added as an @C tag for stable cross-run traceability. No live Azure credentials, real backend source, user registry, service wrappers, database, or messaging integration is present.

Before adding a feature, inspect the corresponding TypeScript domain type, port, use case, adapter, fixture, and consumer usage. Then place the C# equivalent at the same boundary and add a contract-focused test.