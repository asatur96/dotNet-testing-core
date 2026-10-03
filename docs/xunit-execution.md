# xUnit execution fixture

The TypeScript playwright-lib globalSetup starts the shared summary and optional remote run before workers start; globalTeardown closes them after workers finish. In xUnit v2, ExecutionFixture provides this boundary for the test classes in one collection. It uses TestExecutionSession for the run and owns the SuiteFixture instances registered during construction. Each suite gets the same TestResultWorkflow, so case synchronization, Azure result publication, and summary recording remain in the existing application use cases.

A consumer composition root can define one execution collection:

    public sealed class BackendRun : ExecutionFixture
    {
        public BackendRun() : base(
            new TestRunSummaryService(new FileTestRunSummaryStore(
                Environment.GetEnvironmentVariable("TEST_RUN_SUMMARY_PATH")
                ?? Path.Combine("TestResults", $"run-{Guid.NewGuid():N}.json"))),
            Environment.GetEnvironmentVariable("BUILD_BUILDID")
                ?? Guid.NewGuid().ToString("N"))
        {
            Commands = RegisterSuite(new CommandsSuite(Workflow));
        }

        public CommandsSuite Commands { get; }
    }

    [CollectionDefinition("Backend run")]
    public sealed class BackendRunCollection : ICollectionFixture<BackendRun> { }

    [Collection("Backend run")]
    public sealed class CommandTests(BackendRun run)
    {
        [Fact]
        public Task Accepts_command() => run.Commands.RunAsync(
            "accepts command", async context =>
            {
                // Resolve the backend service from your per-test DI scope here.
                await context.StepAsync("execute command", "accepted", () => Task.CompletedTask);
            });
    }

    public sealed class CommandsSuite(TestResultWorkflow workflow) : SuiteFixture(
        "Commands",
        onTestFinishing: workflow.OnTestFinishingAsync,
        onTestFinished: workflow.OnTestFinishedAsync,
        onSuiteFinished: workflow.OnSuiteFinishedAsync);

Add all classes that share this run to the same collection. xUnit creates the collection fixture before their tests, initializes registered suites, then disposes suites and completes the run after those tests. A failed suite setup or teardown still finalizes the summary. If Azure run closure fails, a second DisposeAsync call retries closure without disposing suites twice. The fixture exposes Summary for inspection and Workflow for constructing suites. RegisterSuite must be called in the execution fixture constructor, before xUnit initializes it.

The example runs locally without Azure writes. To enable Azure publication, compose TestCaseSync and TestRunPublisher from the Azure adapter and pass them, along with a matching TestRunDefinition, to the ExecutionFixture base constructor. Keep credentials in ISecretPort; do not place them in TestRunMetadata. ScopedSuiteFixture can be registered the same way to give each test a fresh backend DI scope.

xUnit v2 collection fixtures serialize tests within the collection. Separate collections can run in parallel, but each should own a distinct execution ID and summary path. For a run spanning multiple processes or assemblies, initialize and complete TestExecutionSession in an outer runner, and have each suite use TestResultWorkflow with the shared FileTestRunSummaryStore path. The file store supports those concurrent updates; a collection fixture does not provide assembly-wide setup in xUnit v2.