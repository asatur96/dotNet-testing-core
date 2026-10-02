# Azure DevOps quality workflow

## Traceability model

Use one Azure Boards User Story or Product Backlog Item per behavior. Link it to a Test Case with the Tested By relation. Keep one stable Test Case ID in the automated test title as @C123. The TestCaseSync use case updates that case from recorded steps when explicitly invoked. Link confirmed product defects as Bugs to the Test Case and to the failed pipeline result. Several failures can point to the same Bug when they share one cause.

The Azure adapter exposes LinkRequirementToCaseAsync and LinkBugToCaseAsync. The former adds Microsoft.VSTS.Common.TestedBy-Forward from the requirement to the case; the latter adds a Related relation from the Bug to the case. Use the pipeline Tests tab to link a specific failed result to the Bug and requirement. Code-level links alone cannot identify a particular result without a run and result ID.

Suggested chain: requirement -> Test Case -> pipeline result -> Bug -> fix -> release. Keep IDs and URLs in Azure work item links, not duplicate spreadsheets.

## Release readiness in Boards

Create one Task titled Release readiness: version/environment. Give it an owner, date, build and pipeline run link. Add child Tasks for the gates below, each with an owner and evidence URL. Block the production environment with an approval/check until gates are reviewed.

- Build from approved branch and reviewed changes.
- Required unit and integration tests pass; explain quarantined/flaky tests.
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

Azure Pipelines: prefer the job-scoped SYSTEM_ACCESSTOKEN and grant its build service identity only the required project permissions. Map the token into a dedicated opt-in task's environment; ordinary test jobs do not need it. For other third-party integrations, register an IntegrationDefinition in EnvConfigAdapter and an IIntegrationFactory in IntegrationRegistry, then read secrets through ISecretPort at call time. Use a secret variable group or Azure Key Vault for any additional secret values.

The repo has no Azure organization or backend monolith checkout. Live permissions, work item process fields, and Test Plans licensing must be checked in the target organization before enabling writes.