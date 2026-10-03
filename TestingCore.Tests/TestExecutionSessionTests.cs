using TestingCore.Application;
using TestingCore.Domain;
using TestingCore.Infrastructure;
using TestingCore.Ports;
using TestingCore.Xunit;

namespace TestingCore.Tests;

public sealed class TestExecutionSessionTests
{
    [Fact]
    public async Task Separate_fixtures_share_the_run_id_through_the_summary_file()
    {
        var directory = Path.Combine(Path.GetTempPath(), "testing-core-session-" + Guid.NewGuid().ToString("N"));
        try
        {
            var path = Path.Combine(directory, "run.json");
            var setupSummary = new TestRunSummaryService(new FileTestRunSummaryStore(path));
            var workerSummary = new TestRunSummaryService(new FileTestRunSummaryStore(path));
            var execution = new FakeExecution();
            var publisher = new TestRunPublisher(execution);
            var session = new TestExecutionSession(setupSummary, publisher);
            var run = await session.StartAsync("execution-1", azureRun:
                new TestRunDefinition("Backend", "execution-1"));
            var fixture = new SessionFixture(new TestResultWorkflow(
                runPublisher: publisher, summary: workerSummary));

            await fixture.InitializeAsync();
            await fixture.RunAsync("@C123 handles command", context =>
            {
                context.Metadata.TestManagement.CaseId = "123";
                return Task.CompletedTask;
            });
            await fixture.DisposeAsync();
            var completed = await session.CompleteAsync();

            Assert.Equal("7", run!.Id);
            Assert.Equal(["7"], execution.PublishedRunIds);
            Assert.Equal(["7"], execution.CompletedRunIds);
            Assert.Equal("7", completed.AzureRunId);
            Assert.Equal("https://azure/run/7", completed.AzureRunUrl);
            Assert.Equal(1, completed.Statistics.Passed);
            Assert.NotNull(completed.FinishedAt);
            Assert.NotNull(completed.AzureRunClosedAt);
            Assert.Equal("7", Assert.Single(fixture.Suite.Tests).Metadata.TestManagement.RunId);
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task Local_session_runs_without_Azure_publication()
    {
        var store = new MemoryStore();
        var session = new TestExecutionSession(new TestRunSummaryService(store));

        var run = await session.StartAsync("local-1");
        var completed = await session.CompleteAsync();

        Assert.Null(run);
        Assert.Null(completed.AzureRunId);
        Assert.NotNull(completed.FinishedAt);
    }

    [Fact]
    public async Task Failed_Azure_close_still_finalizes_the_local_summary()
    {
        var store = new MemoryStore();
        var summary = new TestRunSummaryService(store);
        var execution = new FakeExecution { FailClose = true };
        var session = new TestExecutionSession(summary, new TestRunPublisher(execution));
        await session.StartAsync("execution-1", azureRun:
            new TestRunDefinition("Backend", "execution-1"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => session.CompleteAsync());

        Assert.NotNull((await summary.GetAsync()).FinishedAt);
        Assert.Null((await summary.GetAsync()).AzureRunClosedAt);
        execution.FailClose = false;
        var retried = await session.CompleteAsync();
        Assert.NotNull(retried.AzureRunClosedAt);
        Assert.Equal(["7", "7"], execution.CompletedRunIds);
    }

    private sealed class SessionFixture(TestResultWorkflow workflow) : SuiteFixture(
        "session", onTestFinished: workflow.OnTestFinishedAsync,
        onSuiteFinished: workflow.OnSuiteFinishedAsync);

    private sealed class FakeExecution : ITestExecutionPort
    {
        public bool FailClose { get; set; }
        public List<string> PublishedRunIds { get; } = [];
        public List<string> CompletedRunIds { get; } = [];
        public Task<TestRunReference> CreateRunAsync(TestRunDefinition run, CancellationToken token = default) =>
            Task.FromResult(new TestRunReference("7", run.Name, "https://azure/run/7"));
        public Task<TestResultReference> AddResultAsync(
            string runId, TestResultDefinition result, CancellationToken token = default)
        {
            PublishedRunIds.Add(runId);
            return Task.FromResult(new TestResultReference("22", "https://azure/result/22"));
        }
        public Task CompleteRunAsync(string runId, CancellationToken token = default)
        {
            CompletedRunIds.Add(runId);
            if (FailClose) throw new InvalidOperationException("Azure close failed");
            return Task.CompletedTask;
        }
    }

    private sealed class MemoryStore : ITestRunSummaryStore
    {
        private TestRunSummary? _summary;
        public Task<TestRunSummary?> ReadAsync(CancellationToken token = default) => Task.FromResult(_summary);
        public Task<TestRunSummary> UpdateAsync(
            Func<TestRunSummary?, TestRunSummary> update, CancellationToken token = default)
        {
            _summary = update(_summary);
            return Task.FromResult(_summary);
        }
    }
}