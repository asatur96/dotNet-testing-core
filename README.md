# dotNet-testing-core

Proof of concept for C# white-box tests with xUnit fixtures and Playwright for .NET. TransferPolicy is a sample production class to replace when the actual system under test is known.

## Local run

Requires .NET 10 SDK. Run these commands in order:

    dotnet restore dotNet-testing-core.sln
    dotnet build dotNet-testing-core.sln --configuration Release
    pwsh TestingCore.Tests/bin/Release/net10.0/playwright.ps1 install chromium
    dotnet test dotNet-testing-core.sln --configuration Release

TransferPolicyTests demonstrates xUnit class fixtures and parameterized cases. BrowserSmokeTests uses Playwright PageTest, which supplies a fresh browser context and page per test. The inline page needs no application URL.

## Azure DevOps experiment

1. Create an Azure DevOps project and connect this GitHub repository to a pipeline using azure-pipelines.yml.
2. Run it and confirm results in the pipeline Tests tab.
3. Start the 30-day Basic + Test Plans trial in Organization settings > Billing. Create a plan, suite, and manual case for the transfer policy.
4. Create a sample Boards Bug and link it to a failed result. Add a query for open bugs and a dashboard chart.
5. Decide whether paid Test Plans adds value. Pipeline test results and Boards bugs are the simpler baseline.

Automated code is the source of truth for automated checks. Reserve Test Plans cases for reviewed scenarios needing manual execution, audit, or requirement traceability. Triage failures before creating Bugs.

For each Bug, capture severity, priority, affected build and environment, reproduction steps, expected and actual behavior, evidence and run link, owner, and resolution. Review critical open bugs, age, recurrence, and component trends weekly.

Live Azure setup requires your organization and its GitHub connection.