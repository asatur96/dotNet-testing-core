using TestingCore.Domain;

namespace TestingCore.Ports;

public sealed record TestRunDefinition(
    string Name, string ExecutionId, int? PlanId = null, IReadOnlyList<int>? PointIds = null);
public sealed record TestRunReference(string Id, string Name, string Url);
public sealed record TestResultDefinition(
    string CaseId, string Title, string AutomatedTestName, TestStatus Status,
    string Comment, DateTimeOffset StartedAt, DateTimeOffset FinishedAt,
    IReadOnlyList<int> BugIds, int? TestPointId = null);
public sealed record TestResultReference(string Id, string Url);

public interface ITestExecutionPort
{
    Task<TestRunReference> CreateRunAsync(TestRunDefinition run, CancellationToken cancellationToken = default);
    Task<TestResultReference> AddResultAsync(
        string runId, TestResultDefinition result, CancellationToken cancellationToken = default);
    Task CompleteRunAsync(string runId, CancellationToken cancellationToken = default);
}