using TestingCore.Application;
using TestingCore.Domain;
using TestingCore.Infrastructure;
using TestingCore.Xunit;

namespace TestingCore.Tests;

public sealed class TestRunSummaryTests
{
    [Fact]
    public void Summary_counts_statuses_and_deduplicates_a_suite()
    {
        var summary = TestRunSummary.Start("run-1", new TestRunMetadata("test", "main", "abc", "CI"));
        var suite = new SuiteContext("payments");
        foreach (var status in new[]
                 { TestStatus.Passed, TestStatus.Failed, TestStatus.Skipped, TestStatus.TimedOut, TestStatus.Interrupted })
        {
            var test = new TestContext();
            test.Metadata.Title = status.ToString();
            test.Metadata.SuitePath.Add(suite.Name);
            if (status == TestStatus.Failed)
            {
                test.Metadata.TestManagement.CaseId = "123";
                test.Metadata.ExecutionThreadId = "worker-2";
            }
            test.Finish(status, status == TestStatus.Failed ? new InvalidOperationException("failed") : null);
            suite.AddTest(test);
        }
        suite.FinalizeSuite();

        summary.RecordSuite(suite);
        summary.RecordSuite(suite);

        Assert.Equal(5, summary.Statistics.Total);
        Assert.Equal(1, summary.Statistics.Passed);
        Assert.Equal(1, summary.Statistics.Failed);
        Assert.Equal(1, summary.Statistics.Skipped);
        Assert.Equal(1, summary.Statistics.TimedOut);
        Assert.Equal(1, summary.Statistics.Interrupted);
        Assert.Equal("123", Assert.Single(summary.Failures).CaseId);
        Assert.Equal("worker-2", Assert.Single(summary.Failures).WorkerId);
        Assert.True(summary.HasFailures);
        Assert.Equal("main", summary.Metadata.Branch);
    }

    [Fact]
    public async Task File_store_keeps_all_concurrent_updates()
    {
        var directory = Path.Combine(Path.GetTempPath(), "testing-core-summary-" + Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "run.json");
        try
        {
            var service = new TestRunSummaryService(new FileTestRunSummaryStore(path));
            await service.InitializeAsync("run-concurrent");
            var tests = Enumerable.Range(0, 24).Select(_ =>
            {
                var context = new TestContext();
                context.Finish(TestStatus.Passed);
                return context;
            }).ToArray();

            await Task.WhenAll(tests.Select(async context =>
                await new TestRunSummaryService(new FileTestRunSummaryStore(path)).RecordAsync(context)));
            await service.FinalizeAsync();

            var summary = await service.GetAsync();
            Assert.Equal(24, summary.Statistics.Total);
            Assert.Equal(24, summary.Statistics.Passed);
            Assert.Equal(24, summary.RecordedTestIds.Count);
            Assert.NotNull(summary.FinishedAt);
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.RecordAsync(tests[0]));
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task Fixture_records_suite_teardown_failure_in_summary()
    {
        var directory = Path.Combine(Path.GetTempPath(), "testing-core-summary-" + Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "run.json");
        try
        {
            var service = new TestRunSummaryService(new FileTestRunSummaryStore(path));
            await service.InitializeAsync("run-suite");
            var fixture = new SummaryFixture(service);
            await fixture.InitializeAsync();
            await fixture.RunAsync("passes", _ => Task.CompletedTask);

            await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.DisposeAsync());

            var summary = await service.GetAsync();
            Assert.Equal(1, summary.Statistics.Passed);
            Assert.Equal(1, summary.Statistics.Total);
            Assert.True(summary.HasFailures);
            Assert.Contains("teardown failed", Assert.Single(summary.SuiteFailures).Error);
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    private sealed class SummaryFixture(TestRunSummaryService summary) : SuiteFixture(
        "summary suite",
        hooks: new SuiteHooks(AfterAll: () => throw new InvalidOperationException("teardown failed")),
        onSuiteFinished: suite => summary.RecordSuiteAsync(suite));
}