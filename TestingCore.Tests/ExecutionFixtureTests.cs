using TestingCore.Application;
using TestingCore.Domain;
using TestingCore.Ports;
using TestingCore.Xunit;

namespace TestingCore.Tests;

public sealed class ExecutionFixtureTests
{
    [Fact]
    public async Task Collection_fixture_runs_suites_inside_one_Azure_execution()
    {
        var summary = new TestRunSummaryService(new MemoryStore());
        var azure = new FakeExecution();
        var fixture = new DemoExecution(summary, new TestRunPublisher(azure));

        await fixture.InitializeAsync();
        await fixture.Suite.RunAsync("@C123 accepts command", context =>
        {
            context.Metadata.TestManagement.CaseId = "123";
            return context.StepAsync("execute command", "accepted", () => Task.CompletedTask);
        });
        await fixture.DisposeAsync();

        var completed = await summary.GetAsync();
        Assert.Equal(1, completed.Statistics.Passed);
        Assert.Single(completed.RecordedSuiteIds);
        Assert.Equal("7", completed.AzureRunId);
        Assert.NotNull(completed.AzureRunClosedAt);
        Assert.NotNull(completed.FinishedAt);
        Assert.Equal(1, azure.Created);
        Assert.Equal(1, azure.Published);
        Assert.Equal(1, azure.Closed);
        Assert.Equal("22", Assert.Single(fixture.Suite.Suite.Tests).Metadata.TestManagement.ResultId);
    }

    [Fact]
    public async Task Failed_suite_setup_still_records_failure_and_completes_execution()
    {
        var summary = new TestRunSummaryService(new MemoryStore());
        var fixture = new DemoExecution(summary, hooks: new SuiteHooks(
            BeforeAll: () => throw new InvalidOperationException("Setup failed")));

        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.InitializeAsync());
        await fixture.DisposeAsync();

        var completed = await summary.GetAsync();
        Assert.NotNull(completed.FinishedAt);
        Assert.Single(completed.SuiteFailures);
        Assert.Equal(0, completed.Statistics.Total);
    }

    [Fact]
    public async Task Failed_Azure_close_can_be_retried_without_disposing_suites_twice()
    {
        var summary = new TestRunSummaryService(new MemoryStore());
        var azure = new FakeExecution { FailClose = true };
        var fixture = new DemoExecution(summary, new TestRunPublisher(azure));
        await fixture.InitializeAsync();
        await fixture.Suite.RunAsync("passes", _ => Task.CompletedTask);

        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.DisposeAsync());
        Assert.Null((await summary.GetAsync()).AzureRunClosedAt);
        azure.FailClose = false;
        await fixture.DisposeAsync();

        Assert.NotNull((await summary.GetAsync()).AzureRunClosedAt);
        Assert.Single((await summary.GetAsync()).RecordedSuiteIds);
        Assert.Equal(2, azure.Closed);
    }

    private sealed class DemoExecution : ExecutionFixture
    {
        public DemoSuite Suite { get; }

        public DemoExecution(
            TestRunSummaryService summary, TestRunPublisher? publisher = null,
            SuiteHooks? hooks = null)
            : base(summary, "run-1", azureRun: publisher is null
                ? null : new TestRunDefinition("Backend", "run-1"), publisher: publisher)
        {
            Suite = RegisterSuite(new DemoSuite(Workflow, hooks));
        }
    }

    private sealed class DemoSuite(TestResultWorkflow workflow, SuiteHooks? hooks) : SuiteFixture(
        "commands", hooks, workflow.OnTestFinishingAsync,
        workflow.OnSuiteFinishedAsync, workflow.OnTestFinishedAsync);

    private sealed class FakeExecution : ITestExecutionPort
    {
        public int Created { get; private set; }
        public int Published { get; private set; }
        public int Closed { get; private set; }
        public bool FailClose { get; set; }

        public Task<TestRunReference> CreateRunAsync(TestRunDefinition run, CancellationToken token = default)
        {
            Created++;
            return Task.FromResult(new TestRunReference("7", run.Name, "https://azure/run/7"));
        }

        public Task<TestResultReference> AddResultAsync(
            string runId, TestResultDefinition result, CancellationToken token = default)
        {
            Published++;
            return Task.FromResult(new TestResultReference("22", "https://azure/result/22"));
        }

        public Task CompleteRunAsync(string runId, CancellationToken token = default)
        {
            Closed++;
            if (FailClose) throw new InvalidOperationException("Azure close failed");
            return Task.CompletedTask;
        }
    }

    private sealed class MemoryStore : ITestRunSummaryStore
    {
        private TestRunSummary? _summary;

        public Task<TestRunSummary?> ReadAsync(CancellationToken token = default) =>
            Task.FromResult(_summary);

        public Task<TestRunSummary> UpdateAsync(
            Func<TestRunSummary?, TestRunSummary> update, CancellationToken token = default)
        {
            _summary = update(_summary);
            return Task.FromResult(_summary);
        }
    }
}