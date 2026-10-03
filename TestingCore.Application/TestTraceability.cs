using TestingCore.Domain;
using TestingCore.Ports;

namespace TestingCore.Application;

public sealed class TestTraceability(ITraceabilityPort traceability)
{
    public Task LinkRequirementAsync(
        int requirementId, TestContext context, CancellationToken cancellationToken = default) =>
        traceability.LinkRequirementToCaseAsync(
            requirementId, RequireCaseId(context), cancellationToken);

    public Task LinkBugAsync(
        int bugId, TestContext context, CancellationToken cancellationToken = default) =>
        traceability.LinkBugToCaseAsync(
            bugId, RequireCaseId(context), cancellationToken);

    private static int RequireCaseId(TestContext context)
    {
        if (!int.TryParse(context.Metadata.TestManagement.CaseId, out var caseId) || caseId <= 0)
            throw new InvalidOperationException("An Azure Test Case ID is required before linking work items");
        return caseId;
    }
}