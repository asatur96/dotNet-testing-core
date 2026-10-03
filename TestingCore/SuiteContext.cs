namespace TestingCore.Domain;

public sealed record SuiteResult(
    Guid Id, string Name, DateTimeOffset StartedAt, DateTimeOffset FinishedAt,
    TimeSpan Duration, TestStatus Status, int TestCount, int FailedCount);

public sealed class SuiteContext(string name)
{
    private readonly List<TestContext> _tests = [];
    private SuiteResult? _result;
    public Guid Id { get; } = Generators.Guid();
    public string Name { get; } = name;
    public DateTimeOffset StartedAt { get; } = DateTimeOffset.UtcNow;
    public SuiteExecutionMetadata Metadata { get; } = new() { SuiteName = name, SuiteId = name };
    public IReadOnlyList<TestContext> Tests => _tests;
    public bool HasFailures => _tests.Any(test => test.HasFailures);

    public void AddTest(TestContext test)
    {
        if (_result is not null) throw new InvalidOperationException("Suite already finished");
        if (test.FinishedAt is null) throw new InvalidOperationException("Test has no final status");
        _tests.Add(test);
    }

    public SuiteResult FinalizeSuite()
    {
        if (_result is not null) return _result;
        var finishedAt = DateTimeOffset.UtcNow;
        Metadata.FinishedAt = finishedAt;
        Metadata.Duration = finishedAt - StartedAt;
        Metadata.Status = HasFailures ? TestStatus.Failed : TestStatus.Passed;
        return _result = new SuiteResult(Id, Name, StartedAt, finishedAt,
            finishedAt - StartedAt, HasFailures ? TestStatus.Failed : TestStatus.Passed,
            _tests.Count, _tests.Count(test => test.HasFailures));
    }
}