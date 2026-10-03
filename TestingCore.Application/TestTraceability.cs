using TestingCore.Domain;
using TestingCore.Ports;

namespace TestingCore.Application;

public sealed class TestTraceability(ITraceabilityPort traceability)
{
    public Task LinkRequirementAsync(
        int requirementId, TestContext context, CancellationToken cancellationToken = default) =>
        traceability.LinkRequirementToCaseAsync(
            requirementId, RequireCaseId(context), cancellationToken);

    public async Task LinkBugAsync(
        int bugId, TestContext context, CancellationToken cancellationToken = default)
    {
        if (bugId <= 0) throw new ArgumentOutOfRangeException(nameof(bugId));
        var caseId = RequireCaseId(context);
        if (context.Metadata.TestManagement.BugIds.Contains(bugId)) return;
        await traceability.LinkBugToCaseAsync(bugId, caseId, cancellationToken);
        context.Metadata.TestManagement.BugIds.Add(bugId);
    }

    private static int RequireCaseId(TestContext context)
    {
        if (!int.TryParse(context.Metadata.TestManagement.CaseId, out var caseId) || caseId <= 0)
            throw new InvalidOperationException("An Azure Test Case ID is required before linking work items");
        return caseId;
    }
}