namespace TestingCore.Domain;

public sealed class StepContext(TestContext context, string action, string expected) : IDisposable
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