using TestingCore;

namespace TestingCore.Tests;

public sealed class TestCaseSyncTests
{
    [Fact]
    public async Task Existing_case_tag_updates_case()
    {
        var port = new FakeManagement();
        var context = new TestContext();
        await context.StepAsync("GET /health", "200", () => Task.CompletedTask);

        var result = await new TestCaseSync(port).SyncAsync("@C123 Health responds", context);

        Assert.Equal("123", result.ExternalId);
        Assert.Equal("Health responds", port.Definition?.Title);
        Assert.Equal("GET /health", Assert.Single(port.Definition!.Steps).Action);
        Assert.Equal("123", port.UpdatedId);
    }

    [Fact]
    public async Task Missing_case_tag_creates_case()
    {
        var port = new FakeManagement();
        var result = await new TestCaseSync(port).SyncAsync("Health responds", new TestContext());

        Assert.Equal("456", result.ExternalId);
        Assert.Null(port.UpdatedId);
    }

    private sealed class FakeManagement : ITestManagementPort
    {
        public TestCaseDefinition? Definition { get; private set; }
        public string? UpdatedId { get; private set; }
        public Task<TestCaseReference?> GetCaseAsync(string id, CancellationToken token = default) =>
            Task.FromResult<TestCaseReference?>(null);
        public Task<TestCaseReference> CreateCaseAsync(TestCaseDefinition testCase, CancellationToken token = default)
        {
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