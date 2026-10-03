namespace TestingCore.Domain;

public sealed class SuiteExecutionMetadata
{
    public string? ExecutionId { get; set; }
    public string? SuiteId { get; set; }
    public string? SuiteName { get; set; }
    public DateTimeOffset? FinishedAt { get; internal set; }
    public TimeSpan? Duration { get; internal set; }
    public TestStatus Status { get; internal set; } = TestStatus.Unknown;
    public string? Error { get; internal set; }
}