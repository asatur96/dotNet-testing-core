# Azure DevOps quality workflow

## Traceability model

Use one Azure Boards User Story or Product Backlog Item per behavior. Link it to a Test Case with the Tested By relation. Keep one stable Test Case ID in the automated test title as @C123. The TestCaseSync use case updates that case from recorded steps when explicitly invoked or attached to SuiteFixture via onTestFinishing. It stores the returned case ID in TestContext.Metadata.TestManagement.CaseId. A test can opt out with TestManagement.Skip. If a case is newly created, add its @C tag to the test title before the next run; otherwise another context will create another case. Link confirmed product defects as Bugs to the Test Case and to the failed pipeline result. Several failures can point to the same Bug when they share one cause.

The TestTraceability application use case reads the recorded case ID and calls the Azure adapter through ITraceabilityPort. The adapter exposes LinkRequirementToCaseAsync and LinkBugToCaseAsync. The former adds Microsoft.VSTS.Common.TestedBy-Forward from the requirement to the case; the latter adds a Related relation from the Bug to the case. Use the pipeline Tests tab to link a specific failed result to the Bug and requirement. The optional run publisher now records run and result IDs so a completed automated result can be identified.

Suggested chain: requirement -> Test Case -> pipeline result -> Bug -> fix -> release. Keep IDs and URLs in Azure work item links, not duplicate spreadsheets. Publish the JSON run summary as a pipeline artifact alongside TRX; it contains case IDs, failure details, and build metadata for triage. When recorded after run publication, failed entries also contain Azure run/result IDs, result URLs, and confirmed Bug IDs. The summary is evidence, while Azure Boards remains the work item system of record.

## Automated run results

TestRunPublisher follows the TypeScript create run -> publish result -> close run lifecycle through ITestExecutionPort. AzureDevOpsAdapter implements the port with the Azure Test 7.1 REST API. The recommended per-test composition is TestResultWorkflow: create one Azure run and initialize one JSON summary before the suites, then supply its methods to the fixture callbacks. SuiteFixture calls onTestFinishing before TestContext.Finish for case sync, then onTestFinished after finalization for Azure publication and summary aggregation. Complete the Azure run and finalize the summary after all suites. This supports one run across multiple suites and matches the TypeScript fixture order.

    var run = await publisher.CreateAsync(
        new TestRunDefinition("Backend regression", executionId));
    await summary.InitializeAsync(executionId);
    var workflow = new TestResultWorkflow(caseSync, publisher, summary, run.Id);
    // In the derived SuiteFixture constructor:
    // onTestFinishing: workflow.OnTestFinishingAsync,
    // onTestFinished: workflow.OnTestFinishedAsync,
    // onSuiteFinished: workflow.OnSuiteFinishedAsync
    // After all suite fixtures dispose:
    await publisher.CompleteAsync(run.Id);
    await summary.FinalizeAsync();

This flow is opt-in. The publisher stores Azure run ID, result ID, and result URL in each TestContext.TestManagement metadata. Tests with Skip=true or no CaseId are omitted from Azure; if no Azure run ID exists, publication is skipped. Every finalized test is still counted in the local summary, and the suite callback captures teardown failures. If Azure publication fails, the local summary is still recorded and the suite reports the integration failure without changing the test's original outcome. PublishSuiteAsync remains available when a separate Azure run per suite is desired. A suite-level publication failure leaves the Azure run open for investigation.

TestTraceability.LinkBugAsync records a confirmed Bug ID after its work item link succeeds. The publisher includes those IDs as associatedBugs on the result; it never creates a Bug for an unexplained failure. It publishes step names and outcomes as a concise comment, without raw exception messages or HTTP bodies. Keep step names free of secrets. A test can set TestManagement.PointId, and the run definition can include PlanId and PointIds when the organization has a managed Test Plan. A Test Case reference alone does not establish the full plan/suite/point context.

The implementation follows Microsoft's [create run](https://learn.microsoft.com/en-us/rest/api/azure/devops/test/runs/create?view=azure-devops-rest-7.1), [add results](https://learn.microsoft.com/en-us/rest/api/azure/devops/test/results/add?view=azure-devops-rest-7.1), and [update run](https://learn.microsoft.com/en-us/rest/api/azure/devops/test/runs/update?view=azure-devops-rest-7.1) contracts. Local contract tests verify requests and mapping; this repository has no Azure organization credentials, so live permissions and behavior remain unverified.

## Free-plan experiment

The first five Basic users are free. Basic + Test Plans is paid, with a 30-day trial that must be enabled and assigned to users. Basic access can execute tests, while full Test Plans authoring and management requires Basic + Test Plans or a qualifying Visual Studio subscription. Start with Boards Test Case and Bug work items plus pipeline TRX results under Basic; use the trial to check plan/suite/point workflows before paying. The REST run publication in this repository still needs the target project's Create test runs permission and should be verified against that organization. See Microsoft's [access guidance](https://learn.microsoft.com/en-us/azure/devops/test/manual-test-permissions?view=azure-devops), [paid access guidance](https://learn.microsoft.com/en-us/azure/devops/organizations/billing/buy-basic-access-add-users?view=azure-devops), and [trial steps](https://learn.microsoft.com/en-us/azure/devops/organizations/billing/try-additional-features-vs?view=azure-devops).
## Release readiness in Boards

Create one Task titled Release readiness: version/environment. Give it an owner, date, build and pipeline run link. Add child Tasks for the gates below, each with an owner and evidence URL. Block the production environment with an approval/check until gates are reviewed.

- Build from approved branch and reviewed changes.
- Required unit and integration tests pass; explain quarantined/flaky tests. Attach the change-impact report and run the full suite for release sign-off.
- Open critical/high Bugs are fixed or have a recorded risk acceptance.
- Deployment and rollback procedure has been exercised.
- Database migrations and compatibility have been reviewed.
- Monitoring, alerts, and on-call owner are ready.
- Product owner signs off on scope and known issues.

Use Boards queries for open high severity Bugs, Bugs without an owner, and readiness Tasks not Done. Pin query charts and the Requirements quality/test result widgets to a dashboard. Environment approvals and branch control are the enforcement layer; a text checklist alone is evidence, not a gate.

## Incident and RCA

Create a Bug for a confirmed defect. Record severity, impact, first/last occurrence, affected build and environment, reproduction, expected/actual result, run link, owner, and mitigation. Link repeat failures to the same Bug. For a material production incident, create a linked RCA Task with timeline, trigger, root cause, detection gap, customer impact, containment, permanent fix, and prevention actions. Make follow-up actions child Tasks with due dates and owners. Close the RCA after actions are verified, not merely documented.

Separate product defects from flaky tests and environment outages during triage. Use tags for component and failure class, then query age, recurrence, time to resolution, escaped defects, and reopen rate. Avoid automatically creating Bugs for every failed test.

## Access and secrets

Local runs: supply AZDO_ORGANIZATION, AZDO_PROJECT, AZDO_ENABLED=true, and AZDO_PAT through process environment or a user-level secret store. Do not commit a .env file. The ProcessEnvAdapter reads them at runtime; the config adapter keeps only non-secret organization/project values. AzureCredentialProvider resolves the credential only when an Azure call is made.

Azure Pipelines: prefer the job-scoped SYSTEM_ACCESSTOKEN and grant its build service identity only the required project permissions. Map the token into a dedicated opt-in task's environment; ordinary test jobs do not need it. For other third-party integrations, register an IntegrationDefinition in EnvConfigAdapter and an IIntegrationFactory<TServices> in IntegrationRegistry, then read secrets through ISecretPort at call time. Use a secret variable group or Azure Key Vault for any additional secret values.

The repo has no Azure organization or backend monolith checkout. Live permissions and work item process fields must be checked in the target organization before enabling writes.