namespace TestingCore.Domain;

public enum StepStatus { Passed, Failed }
public enum TestStatus { Unknown, Passed, Failed, Skipped, TimedOut, Interrupted }

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
    public DateTimeOffset? FinishedAt { get; private set; }
    public TestStatus Status { get; private set; } = TestStatus.Unknown;
    public string? Error { get; private set; }
    public TestExecutionMetadata Metadata { get; } = new();
    public IReadOnlyList<StepResult> Steps => _steps;
    public bool HasFailures => Status == TestStatus.Failed || _steps.Any(s => s.Status == StepStatus.Failed);

    public StepContext StartStep(string action, string expected)
    {
        if (_active is not null) throw new InvalidOperationException("Previous step not completed");
        if (FinishedAt is not null) throw new InvalidOperationException("Test already finished");
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

    public void Finish(TestStatus status, Exception? error = null)
    {
        if (FinishedAt is not null) throw new InvalidOperationException("Test already finished");
        if (_active is not null) throw new InvalidOperationException("Active step not completed");
        Status = HasFailures ? TestStatus.Failed : status;
        Error = error?.ToString();
        FinishedAt = DateTimeOffset.UtcNow;
        Metadata.Status = Status;
        Metadata.FinishedAt = FinishedAt;
        Metadata.Duration = FinishedAt.Value - StartedAt;
        Metadata.Error = Error;
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