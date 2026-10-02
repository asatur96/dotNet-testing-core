namespace TestingCore;

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
    private StepScope? _active;

    public Guid Id { get; } = Guid.NewGuid();
    public DateTimeOffset StartedAt { get; } = DateTimeOffset.UtcNow;
    public Dictionary<string, string> Metadata { get; } = [];
    public IReadOnlyList<StepResult> Steps => _steps;
    public bool HasFailures => _steps.Any(s => s.Status == StepStatus.Failed);

    public StepScope StartStep(string action, string expected)
    {
        if (_active is not null) throw new InvalidOperationException("Previous step not completed");
        return _active = new StepScope(this, action, expected);
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

    internal void Complete(StepScope scope, StepStatus status, string? error, IReadOnlyList<StepArtifact> artifacts)
    {
        if (!ReferenceEquals(_active, scope)) throw new InvalidOperationException("Step is not active");
        _steps.Add(new StepResult(scope.Action, scope.Expected, status, error, artifacts));
        _active = null;
    }
}

public sealed class StepScope(TestContext context, string action, string expected) : IDisposable
{
    private readonly List<StepArtifact> _artifacts = [];
    private bool _completed;
    public string Action { get; } = action;
    public string Expected { get; } = expected;
    public void AddArtifact(StepArtifact artifact) => _artifacts.Add(artifact);
    public void Pass() => Complete(StepStatus.Passed, null);
    public void Fail(Exception error) => Complete(StepStatus.Failed, error.ToString());
    private void Complete(StepStatus status, string? error)
    {
        if (_completed) throw new InvalidOperationException("Step already completed");
        context.Complete(this, status, error, _artifacts.ToArray());
        _completed = true;
    }
    public void Dispose()
    {
        if (!_completed) Complete(StepStatus.Failed, "Step disposed without an outcome");
    }
}