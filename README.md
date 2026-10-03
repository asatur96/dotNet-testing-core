# dotNet-testing-core

C# backend testing core modeled on the TypeScript testing-core project. It has no Playwright dependency.

## Architecture

- TestingCore: domain contexts, artifacts, Suite/RunWith model, and generators. This project has no infrastructure references.
- TestingCore.Ports: contracts for HTTP testing, configuration, credentials, Test Cases, Bugs, traceability, and reporting.
- TestingCore.Application: assertions, Test Case synchronization, traceability, and run summary lifecycle.
- TestingCore.Infrastructure: HttpClient, Azure DevOps, process environment, credentials, integration registry, and atomic JSON run summary store.
- TestingCore.Xunit: runner bridge for Suite, RunWith, TestStep, hooks, per-test lifecycle, and per-test .NET DI scopes.
- TestingCore.Tests: executable examples and contract-focused tests.
- TestingCore.Impact.Cli: change-impact report from a component map and Git diff.

The dependency direction is Infrastructure -> Ports/Application -> Domain; Xunit -> Domain. The test project is the composition root for examples. When copied under the backend monolith, reference the backend source project directly from TestingCore.Tests and construct its services/controllers through your production DI container or WebApplicationFactory. The HTTP adapter is only for tests that cross an HTTP boundary. ScopedSuiteFixture gives each direct service test a fresh .NET DI scope. GenericAssertions records structural, containment, and predicate checks as step validation artifacts for direct service tests.

## Authoring and integrations

AuthoringTests shows Suite, RunWith, and TestStep. RunWith defaults to API platform and the EN/HY/RU language matrix; override these per suite. Each test gets a fresh TestContext. TestCaseSync understands @C123 markers, records the external ID in TestManagement metadata, and can be attached to SuiteFixture through its onTestFinishing callback. This is opt-in to avoid unexpected Azure writes. TestTraceability uses that ID to link requirements and confirmed Bugs.

Set AZDO_ENABLED=true, AZDO_ORGANIZATION, and AZDO_PROJECT for Azure composition. AzureCredentialProvider uses SYSTEM_ACCESSTOKEN in Pipelines or AZDO_PAT locally. The credential is resolved only when a request is made. Add an integration by registering an IntegrationDefinition in EnvConfigAdapter and an IIntegrationFactory<TServices> in IntegrationRegistry. The factory receives non-secret settings and an ISecretPort; resolve credentials only when the integration makes a call. See docs/integrations.md.

Run locally: dotnet test dotNet-testing-core.sln

See docs/azure-quality-workflow.md for Test Case/Bug links, release readiness, RCA, and Azure access guidance. See docs/change-impact.md for component coupling and affected-test selection. See docs/http-evidence.md for the default HTTP evidence policy and custom policy port. See docs/run-summary.md for aggregate evidence across suites and docs/white-box-scopes.md for direct source testing through DI. See docs/typescript-parity.md for the TypeScript-to-C# mapping and current gaps. The live Azure connection requires your organization and project.