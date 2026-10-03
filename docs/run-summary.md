# Run summary

The TypeScript framework aggregates test outcomes across workers for teardown reporting. The C# equivalent keeps the summary model in TestingCore.Domain, lifecycle logic in TestingCore.Application, and JSON persistence in TestingCore.Infrastructure.

Initialize a unique summary path once before suites run:

    var summary = new TestRunSummaryService(
        new FileTestRunSummaryStore("TestResults/run-123.json"));
    await summary.InitializeAsync("run-123",
        new TestRunMetadata("test", "main", "commit-sha", "CI"));

For a local-only summary, pass a callback to a SuiteFixture subclass so its finalized tests and suite hook failures are recorded:

    public sealed class PaymentFixture(TestRunSummaryService summary) : SuiteFixture(
        "payments", onSuiteFinished: suite => summary.RecordSuiteAsync(suite));

When publishing Azure results, use TestResultWorkflow with onTestFinishing, onTestFinished, and onSuiteFinished instead; it records each completed test after Azure publication and still records it if publication fails. After every suite fixture has disposed, call FinalizeAsync and GetAsync. Publish the JSON file as a pipeline artifact alongside the xUnit TRX file. An Azure dashboard or incident task can link the artifact URL; it is separate from an Azure Test Run. For the suite-level publisher, publish the Azure run before recording the suite summary so result IDs are included.

FileTestRunSummaryStore locks a sidecar .lock file for each read/update and replaces JSON while holding the lock. Parallel fixtures and processes using the same path cannot overwrite each other's counts. Each test and suite ID is recorded once. A missing summary, duplicate initialization, incomplete test, or write after finalization fails explicitly.

The artifact contains environment, branch, commit, executor, status counts, failed test title/suite/error, optional worker ID, Test Case ID, Azure run/result IDs and URL, confirmed Bug IDs, and suite hook failures. Keep credentials out of titles, metadata, and exception messages. Do not put a secret variable into TestRunMetadata.