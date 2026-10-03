using TestingCore.Domain;
using TestingCore.Ports;
using TestingCore.Application;
using TestingCore.Xunit;

namespace TestingCore.Tests;

public sealed class TestCaseSyncTests
{
    [Fact]
    public async Task Existing_case_tag_updates_case_and_records_its_id()
    {
        var port = new FakeManagement();
        var context = new TestContext();
        await context.StepAsync("GET /health", "200", () => Task.CompletedTask);

        var result = await new TestCaseSync(port).SyncAsync("@smoke @C123 Health responds", context);

        Assert.Equal("123", result?.ExternalId);
        Assert.Equal("123", context.Metadata.TestManagement.CaseId);
        Assert.Equal("Health responds", port.Definition?.Title);
        Assert.Equal("GET /health", Assert.Single(port.Definition!.Steps).Action);
        Assert.Equal("123", port.UpdatedId);
    }

    [Fact]
    public async Task Created_case_is_reused_within_the_same_context()
    {
        var port = new FakeManagement();
        var sync = new TestCaseSync(port);
        var context = new TestContext();

        var first = await sync.SyncAsync("Health responds", context);
        var second = await sync.SyncAsync("Health responds", context);

        Assert.Equal("456", first?.ExternalId);
        Assert.Equal("456", second?.ExternalId);
        Assert.Equal("456", context.Metadata.TestManagement.CaseId);
        Assert.Equal(1, port.CreateCount);
        Assert.Equal("456", port.UpdatedId);
    }

    [Fact]
    public async Task Opted_out_test_does_not_write_a_case()
    {
        var port = new FakeManagement();
        var context = new TestContext();
        context.Metadata.TestManagement.Skip = true;

        var result = await new TestCaseSync(port).SyncAsync("Health responds", context);

        Assert.Null(result);
        Assert.Equal(0, port.CreateCount);
        Assert.Null(port.UpdatedId);
    }

    [Fact]
    public async Task Conflicting_case_ids_fail_before_a_write()
    {
        var port = new FakeManagement();
        var context = new TestContext();
        context.Metadata.TestManagement.CaseId = "456";

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new TestCaseSync(port).SyncAsync("@C123 Health responds", context));

        Assert.Equal(0, port.CreateCount);
        Assert.Null(port.UpdatedId);
    }

    [Fact]
    public async Task Fixture_can_sync_recorded_steps_without_azure_dependency()
    {
        var port = new FakeManagement();
        var fixture = new SyncFixture(new TestCaseSync(port));
        await fixture.InitializeAsync();
        await fixture.RunAsync("Health responds", context =>
            context.StepAsync("GET /health", "200", () => Task.CompletedTask));
        await fixture.DisposeAsync();

        var context = Assert.Single(fixture.Suite.Tests);
        Assert.Equal(TestStatus.Passed, context.Status);
        Assert.Equal("456", context.Metadata.TestManagement.CaseId);
        Assert.Equal("GET /health", Assert.Single(port.Definition!.Steps).Action);
        Assert.Equal(1, port.CreateCount);
    }

    private sealed class SyncFixture(TestCaseSync sync) : SuiteFixture(
        "case sync", onTestFinishing: async context =>
        {
            await sync.SyncAsync(context.Metadata.Title!, context);
        });

    private sealed class FakeManagement : ITestManagementPort
    {
        public TestCaseDefinition? Definition { get; private set; }
        public string? UpdatedId { get; private set; }
        public int CreateCount { get; private set; }
        public Task<TestCaseReference?> GetCaseAsync(string id, CancellationToken token = default) =>
            Task.FromResult<TestCaseReference?>(null);
        public Task<TestCaseReference> CreateCaseAsync(TestCaseDefinition testCase, CancellationToken token = default)
        {
            CreateCount++;
            Definition = testCase;
            return Task.FromResult(new TestCaseReference("456", testCase.Title));
        }
        public Task UpdateCaseAsync(string id, TestCaseDefinition testCase, CancellationToken token = default)
        {
            UpdatedId = id;
            Definition = testCase;
            return Task.CompletedTask;
        }
    }
}