using TestingCore.Application;
using TestingCore.Domain;
using TestingCore.Ports;
using TestingCore.Xunit;

namespace TestingCore.Tests;

public sealed class TestResultWorkflowTests
{
    [Fact]
    public async Task Fixture_syncs_case_then_publishes_completed_result_then_records_summary()
    {
        var events = new List<string>();
        var store = new MemorySummaryStore(events);
        var summary = new TestRunSummaryService(store);
        await summary.InitializeAsync("local-run");
        events.Clear();
        var workflow = new TestResultWorkflow(
            new TestCaseSync(new FakeCases(events)),
            new TestRunPublisher(new FakeExecution(events)), summary, "7");
        var fixture = new WorkflowFixture(workflow);

        await fixture.InitializeAsync();
        await fixture.RunAsync("@C123 handles command", context =>
            context.StepAsync("handle command", "accepted", () => Task.CompletedTask));
        await fixture.DisposeAsync();

        Assert.Equal(["sync", "publish", "summary", "suiteSummary"], events);
        var test = Assert.Single(fixture.Suite.Tests);
        Assert.Equal(TestStatus.Passed, test.Status);
        Assert.Equal("123", test.Metadata.TestManagement.CaseId);
        Assert.Equal("22", test.Metadata.TestManagement.ResultId);
        Assert.Equal(1, (await summary.GetAsync()).Statistics.Passed);
    }

    [Fact]
    public async Task Missing_azure_run_skips_publication_but_records_local_summary()
    {
        var events = new List<string>();
        var summary = new TestRunSummaryService(new MemorySummaryStore(events));
        await summary.InitializeAsync("local-run");
        events.Clear();
        var workflow = new TestResultWorkflow(
            new TestCaseSync(new FakeCases(events)),
            new TestRunPublisher(new FakeExecution(events)), summary);
        var fixture = new WorkflowFixture(workflow);

        await fixture.InitializeAsync();
        await fixture.RunAsync("@C123 handles command", _ => Task.CompletedTask);
        await fixture.DisposeAsync();

        Assert.Equal(["sync", "summary", "suiteSummary"], events);
        Assert.Equal(1, (await summary.GetAsync()).Statistics.Passed);
    }

    [Fact]
    public async Task Publication_failure_keeps_test_outcome_and_still_records_summary()
    {
        var events = new List<string>();
        var store = new MemorySummaryStore(events);
        var summary = new TestRunSummaryService(store);
        await summary.InitializeAsync("local-run");
        events.Clear();
        var workflow = new TestResultWorkflow(
            new TestCaseSync(new FakeCases(events)),
            new TestRunPublisher(new FakeExecution(events, fail: true)), summary, "7");
        var fixture = new WorkflowFixture(workflow);
        await fixture.InitializeAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            fixture.RunAsync("@C123 handles command", _ => Task.CompletedTask));
        await fixture.DisposeAsync();

        Assert.Equal(["sync", "publish", "summary", "suiteSummary"], events);
        Assert.Equal(TestStatus.Passed, Assert.Single(fixture.Suite.Tests).Status);
        Assert.Equal(TestStatus.Failed, fixture.Suite.FinalizeSuite().Status);
        Assert.Equal(1, (await summary.GetAsync()).Statistics.Passed);
        Assert.Single((await summary.GetAsync()).SuiteFailures);
    }

    private sealed class WorkflowFixture(TestResultWorkflow workflow) : SuiteFixture(
        "workflow", onTestFinishing: workflow.OnTestFinishingAsync,
        onSuiteFinished: workflow.OnSuiteFinishedAsync,
        onTestFinished: workflow.OnTestFinishedAsync);

    private sealed class FakeCases(List<string> events) : ITestManagementPort
    {
        public Task<TestCaseReference?> GetCaseAsync(string id, CancellationToken token = default) =>
            Task.FromResult<TestCaseReference?>(null);
        public Task<TestCaseReference> CreateCaseAsync(TestCaseDefinition testCase, CancellationToken token = default) =>
            Task.FromResult(new TestCaseReference("123", testCase.Title));
        public Task UpdateCaseAsync(string id, TestCaseDefinition testCase, CancellationToken token = default)
        {
            events.Add("sync");
            return Task.CompletedTask;
        }
    }

    private sealed class FakeExecution(List<string> events, bool fail = false) : ITestExecutionPort
    {
        public Task<TestRunReference> CreateRunAsync(TestRunDefinition run, CancellationToken token = default) =>
            Task.FromResult(new TestRunReference("7", run.Name, "https://azure/run/7"));
        public Task<TestResultReference> AddResultAsync(
            string runId, TestResultDefinition result, CancellationToken token = default)
        {
            events.Add("publish");
            if (fail) throw new InvalidOperationException("Azure publication failed");
            return Task.FromResult(new TestResultReference("22", "https://azure/result/22"));
        }
        public Task CompleteRunAsync(string runId, CancellationToken token = default) => Task.CompletedTask;
    }

    private sealed class MemorySummaryStore(List<string> events) : ITestRunSummaryStore
    {
        private TestRunSummary? _summary;
        public Task<TestRunSummary?> ReadAsync(CancellationToken token = default) =>
            Task.FromResult(_summary);
        public Task<TestRunSummary> UpdateAsync(
            Func<TestRunSummary?, TestRunSummary> update, CancellationToken token = default)
        {
            var testsBefore = _summary?.Statistics.Total ?? 0;
            var suitesBefore = _summary?.RecordedSuiteIds.Count ?? 0;
            _summary = update(_summary);
            if (_summary.Statistics.Total > testsBefore) events.Add("summary");
            if (_summary.RecordedSuiteIds.Count > suitesBefore) events.Add("suiteSummary");
            return Task.FromResult(_summary);
        }
    }
}