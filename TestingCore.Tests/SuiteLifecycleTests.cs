using TestingCore.Domain;
using TestingCore.Xunit;

namespace TestingCore.Tests;

public sealed class SuiteLifecycleTests
{
    [Fact]
    public async Task Suite_runs_hooks_and_finalizes_results()
    {
        var events = new List<string>();
        var fixture = new ExampleFixture(new SuiteHooks(
            BeforeAll: () => Record("beforeAll"),
            AfterAll: () => Record("afterAll"),
            BeforeEach: () => Record("beforeEach"),
            AfterEach: () => Record("afterEach")));
        await fixture.InitializeAsync();
        await fixture.RunAsync("pass", _ => Record("body"));
        await fixture.DisposeAsync();

        Assert.Equal(["beforeAll", "beforeEach", "body", "afterEach", "afterAll"], events);
        Assert.Equal(TestStatus.Passed, fixture.Suite.FinalizeSuite().Status);
        Assert.Equal(1, fixture.Suite.FinalizeSuite().TestCount);
        Assert.Equal(TestStatus.Passed, fixture.Suite.Metadata.Status);
        Assert.Equal("pass", Assert.Single(fixture.Suite.Tests).Metadata.Title);

        Task Record(string value) { events.Add(value); return Task.CompletedTask; }
    }

    [Fact]
    public async Task Failure_outside_a_step_is_included_in_suite_outcome()
    {
        var events = new List<string>();
        var fixture = new ExampleFixture(new SuiteHooks(AfterEach: () =>
        {
            events.Add("afterEach");
            return Task.CompletedTask;
        }));
        await fixture.InitializeAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            fixture.RunAsync("failure", _ => throw new InvalidOperationException("broken")));
        await fixture.DisposeAsync();

        Assert.Equal(["afterEach"], events);
        Assert.True(fixture.Suite.HasFailures);
        Assert.Equal(TestStatus.Failed, Assert.Single(fixture.Suite.Tests).Status);
        Assert.Equal(TestStatus.Failed, fixture.Suite.FinalizeSuite().Status);
        Assert.Equal(TestStatus.Failed, fixture.Suite.Metadata.Status);
        Assert.Contains("broken", Assert.Single(fixture.Suite.Tests).Metadata.Error);
    }

    [Fact]
    public async Task Completion_callback_failure_is_recorded_in_test_and_suite()
    {
        var fixture = new CompletionFixture();
        await fixture.InitializeAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            fixture.RunAsync("case sync", _ => Task.CompletedTask));
        await fixture.DisposeAsync();

        var test = Assert.Single(fixture.Suite.Tests);
        Assert.Equal(TestStatus.Failed, test.Status);
        Assert.Contains("sync failed", test.Error);
        Assert.Equal(TestStatus.Failed, fixture.Suite.FinalizeSuite().Status);
    }

    private sealed class CompletionFixture() : SuiteFixture(
        "example", onTestFinishing: _ => throw new InvalidOperationException("sync failed"));

    private sealed class ExampleFixture(SuiteHooks hooks) : SuiteFixture("example", hooks);
}