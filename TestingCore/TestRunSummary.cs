namespace TestingCore.Domain;

public sealed class TestRunStatistics
{
    public int Total { get; set; }
    public int Passed { get; set; }
    public int Failed { get; set; }
    public int Skipped { get; set; }
    public int TimedOut { get; set; }
    public int Interrupted { get; set; }
}

public sealed record TestRunFailure(
    Guid TestId, string Title, string SuiteName, string? Error,
    string? CaseId, string? WorkerId);

public sealed record TestRunSuiteFailure(Guid SuiteId, string SuiteName, string Error);

public sealed record TestRunMetadata(
    string? Environment = null, string? Branch = null,
    string? Commit = null, string? Executor = null);

public sealed class TestRunSummary
{
    public string RunId { get; set; } = "";
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
    public TestRunStatistics Statistics { get; set; } = new();
    public List<TestRunFailure> Failures { get; set; } = [];
    public TestRunMetadata Metadata { get; set; } = new();
    public List<TestRunSuiteFailure> SuiteFailures { get; set; } = [];
    public HashSet<Guid> RecordedSuiteIds { get; set; } = [];
    public bool HasFailures => Statistics.Failed > 0 || SuiteFailures.Count > 0;
    public HashSet<Guid> RecordedTestIds { get; set; } = [];

    public static TestRunSummary Start(string runId, TestRunMetadata? metadata = null)
    {
        if (string.IsNullOrWhiteSpace(runId)) throw new ArgumentException("Run ID is required", nameof(runId));
        return new TestRunSummary
        {
            RunId = runId,
            StartedAt = DateTimeOffset.UtcNow,
            Metadata = metadata ?? new()
        };
    }

    public void Record(TestContext context)
    {
        if (FinishedAt is not null) throw new InvalidOperationException("Run already finished");
        if (context.FinishedAt is null || context.Status == TestStatus.Unknown)
            throw new InvalidOperationException("Only completed tests can be recorded");
        if (!RecordedTestIds.Add(context.Id)) return;

        Statistics.Total++;
        switch (context.Status)
        {
            case TestStatus.Passed:
                Statistics.Passed++;
                break;
            case TestStatus.Failed:
                Statistics.Failed++;
                var error = context.Error ?? context.Steps.LastOrDefault(step => step.Status == StepStatus.Failed)?.Error;
                Failures.Add(new TestRunFailure(
                    context.Id,
                    context.Metadata.Title ?? "Unnamed test",
                    context.Metadata.SuitePath.Count == 0 ? "Unknown suite" : string.Join(" > ", context.Metadata.SuitePath),
                    error is { Length: > 2000 } ? error[..2000] : error,
                    context.Metadata.TestManagement.CaseId,
                    context.Metadata.ExecutionThreadId));
                break;
            case TestStatus.Skipped:
                Statistics.Skipped++;
                break;
            case TestStatus.TimedOut:
                Statistics.TimedOut++;
                break;
            case TestStatus.Interrupted:
                Statistics.Interrupted++;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(context));
        }
    }

    public void RecordSuite(SuiteContext suite)
    {
        if (FinishedAt is not null) throw new InvalidOperationException("Run already finished");
        if (suite.Metadata.FinishedAt is null)
            throw new InvalidOperationException("Only finalized suites can be recorded");
        if (!RecordedSuiteIds.Add(suite.Id)) return;

        foreach (var test in suite.Tests) Record(test);
        if (suite.Metadata.Error is { } error)
            SuiteFailures.Add(new TestRunSuiteFailure(suite.Id, suite.Name, error));
    }

    public void FinalizeRun() => FinishedAt ??= DateTimeOffset.UtcNow;
}