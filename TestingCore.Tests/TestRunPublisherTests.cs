using TestingCore.Application;
using TestingCore.Domain;
using TestingCore.Ports;
using TestingCore.Xunit;

namespace TestingCore.Tests;

public sealed class TestRunPublisherTests
{
    [Fact]
    public async Task Finalized_suite_publishes_case_result_and_confirmed_bug()
    {
        var execution = new FakeExecution();
        var fixture = new PublishingFixture(new TestRunPublisher(execution));
        await fixture.InitializeAsync();
        await fixture.RunAsync("@C123 service returns a result", context =>
            context.StepAsync("handle command", "result is valid", () => Task.CompletedTask));
        await fixture.DisposeAsync();

        var published = Assert.Single(execution.Results);
        var test = Assert.Single(fixture.Suite.Tests);
        Assert.Equal("123", published.CaseId);
        Assert.Equal(TestStatus.Passed, published.Status);
        Assert.Equal([67], published.BugIds);
        Assert.Equal(9, published.TestPointId);
        Assert.Contains("handle command: Passed", published.Comment);
        Assert.Equal("7", test.Metadata.TestManagement.RunId);
        Assert.Equal("22", test.Metadata.TestManagement.ResultId);
        Assert.Equal("https://azure/result/22", test.Metadata.TestManagement.ResultUrl);
        Assert.Equal(["create", "result", "complete"], execution.Events);
    }

    [Fact]
    public async Task Skipped_or_untagged_test_does_not_publish_a_result()
    {
        var execution = new FakeExecution();
        var publisher = new TestRunPublisher(execution);
        var context = new TestContext();
        context.Finish(TestStatus.Skipped);

        Assert.Null(await publisher.PublishResultAsync("7", context));
        context.Metadata.TestManagement.CaseId = "123";
        context.Metadata.TestManagement.Skip = true;
        Assert.Null(await publisher.PublishResultAsync("7", context));
        Assert.Empty(execution.Results);
    }

    [Fact]
    public async Task Incomplete_test_is_rejected_before_Azure_write()
    {
        var execution = new FakeExecution();
        var context = new TestContext();
        context.Metadata.TestManagement.CaseId = "123";

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new TestRunPublisher(execution).PublishResultAsync("7", context));

        Assert.Empty(execution.Results);
    }

    private sealed class PublishingFixture(TestRunPublisher publisher) : SuiteFixture(
        "service suite",
        onTestFinishing: context =>
        {
            context.Metadata.TestManagement.CaseId = "123";
            context.Metadata.TestManagement.PointId = 9;
            context.Metadata.TestManagement.BugIds.Add(67);
            return Task.CompletedTask;
        },
        onSuiteFinished: suite => publisher.PublishSuiteAsync(
            suite, new TestRunDefinition("service suite", "execution-1")));

    private sealed class FakeExecution : ITestExecutionPort
    {
        public List<string> Events { get; } = [];
        public List<TestResultDefinition> Results { get; } = [];

        public Task<TestRunReference> CreateRunAsync(
            TestRunDefinition run, CancellationToken cancellationToken = default)
        {
            Events.Add("create");
            return Task.FromResult(new TestRunReference("7", run.Name, "https://azure/run/7"));
        }

        public Task<TestResultReference> AddResultAsync(
            string runId, TestResultDefinition result, CancellationToken cancellationToken = default)
        {
            Events.Add("result");
            Results.Add(result);
            return Task.FromResult(new TestResultReference("22", "https://azure/result/22"));
        }

        public Task CompleteRunAsync(string runId, CancellationToken cancellationToken = default)
        {
            Events.Add("complete");
            return Task.CompletedTask;
        }
    }
}