using TestingCore.Application;
using TestingCore.Domain;
using TestingCore.Ports;

namespace TestingCore.Tests;

public sealed class TestTraceabilityTests
{
    [Fact]
    public async Task Synced_case_can_be_linked_to_requirement_and_confirmed_bug()
    {
        var context = new TestContext();
        context.Metadata.TestManagement.CaseId = "123";
        var port = new FakeTraceability();

        var traceability = new TestTraceability(port);
        await traceability.LinkRequirementAsync(45, context);
        await traceability.LinkBugAsync(67, context);
        await traceability.LinkBugAsync(67, context);

        Assert.Equal((45, 123), port.RequirementLink);
        Assert.Equal((67, 123), port.BugLink);
        Assert.Equal([67], context.Metadata.TestManagement.BugIds);
        Assert.Equal(1, port.BugLinkCount);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not-a-number")]
    [InlineData("0")]
    public async Task Missing_or_invalid_case_id_prevents_links(string? caseId)
    {
        var context = new TestContext();
        context.Metadata.TestManagement.CaseId = caseId;
        var port = new FakeTraceability();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new TestTraceability(port).LinkBugAsync(67, context));

        Assert.Null(port.BugLink);
    }

    private sealed class FakeTraceability : ITraceabilityPort
    {
        public (int Requirement, int Case)? RequirementLink { get; private set; }
        public (int Bug, int Case)? BugLink { get; private set; }
        public int BugLinkCount { get; private set; }

        public Task LinkRequirementToCaseAsync(
            int requirementId, int caseId, CancellationToken cancellationToken = default)
        {
            RequirementLink = (requirementId, caseId);
            return Task.CompletedTask;
        }

        public Task LinkBugToCaseAsync(
            int bugId, int caseId, CancellationToken cancellationToken = default)
        {
            BugLink = (bugId, caseId);
            BugLinkCount++;
            return Task.CompletedTask;
        }
    }
}