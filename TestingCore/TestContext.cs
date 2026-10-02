namespace TestingCore.Domain;

public enum StepStatus { Passed, Failed }

public abstract record StepArtifact(DateTimeOffset Timestamp);
public sealed record ApiArtifact(
    string Method, string Url, int Status, string? RequestBody, string ResponseBody,
    TimeSpan Duration, DateTimeOffset Timestamp) : StepArtifact(Timestamp);
public sealed record ValidationArtifact(
    string Name, string Expected, string Actual, bool Success, DateTimeOffset Timestamp) : StepArtifact(Timestamp);
public sealed record StepResult(
    string Action, string Expected, StepStatus Status, string? Error,
    IReadOnlyList<StepArtifact> Artifacts);

public sealed class TestContext
{
    private readonly List<StepResult> _steps = [];
    private StepContext? _active;

    public Guid Id { get; } = Generators.Guid();
    public DateTimeOffset StartedAt { get; } = DateTimeOffset.UtcNow;
    public Dictionary<string, string> Metadata { get; } = [];
    public IReadOnlyList<StepResult> Steps => _steps;
    public bool HasFailures => _steps.Any(s => s.Status == StepStatus.Failed);

    public StepContext StartStep(string action, string expected)
    {
        if (_active is not null) throw new InvalidOperationException("Previous step not completed");
        return _active = new StepContext(this, action, expected);
    }

    public async Task StepAsync(string action, string expected, Func<Task> body)
    {
        using var step = StartStep(action, expected);
        try
        {
            await body();
            step.Pass();
        }
        catch (Exception error)
        {
            step.Fail(error);
            throw;
        }
    }

    public void AddArtifact(StepArtifact artifact) =>
        (_active ?? throw new InvalidOperationException("No active step")).AddArtifact(artifact);

    internal void Complete(StepContext scope, StepStatus status, string? error, IReadOnlyList<StepArtifact> artifacts)
    {
        if (!ReferenceEquals(_active, scope)) throw new InvalidOperationException("Step is not active");
        _steps.Add(new StepResult(scope.Action, scope.Expected, status, error, artifacts));
        _active = null;
    }
}