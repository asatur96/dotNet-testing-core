using TestingCore.Domain;

namespace TestingCore.Ports;

public sealed record TestCaseReference(string ExternalId, string Title);
public sealed record TestCaseDefinition(string Title, string Description, IReadOnlyList<StepResult> Steps);
public sealed record Incident(string Title, string Description, string Severity, string RunUrl);

public interface ITestManagementPort
{
    Task<TestCaseReference?> GetCaseAsync(string id, CancellationToken cancellationToken = default);
    Task<TestCaseReference> CreateCaseAsync(TestCaseDefinition testCase, CancellationToken cancellationToken = default);
    Task UpdateCaseAsync(string id, TestCaseDefinition testCase, CancellationToken cancellationToken = default);
}

public interface IIssueTrackingPort
{
    Task<string> CreateBugAsync(Incident incident, CancellationToken cancellationToken = default);
}

public interface IReportingPort
{
    Task OnTestFinishAsync(TestContext context, CancellationToken cancellationToken = default);
}

public interface ITraceabilityPort
{
    Task LinkRequirementToCaseAsync(int requirementId, int caseId, CancellationToken cancellationToken = default);
    Task LinkBugToCaseAsync(int bugId, int caseId, CancellationToken cancellationToken = default);
}