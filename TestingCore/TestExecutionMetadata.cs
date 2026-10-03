namespace TestingCore.Domain;

public sealed class TestExecutionMetadata
{
    public string? ExecutionId { get; set; }
    public string? ExecutionThreadId { get; set; }
    public List<string> SuitePath { get; } = [];
    public string? Title { get; set; }
    public TestStatus Status { get; internal set; } = TestStatus.Unknown;
    public DateTimeOffset? FinishedAt { get; internal set; }
    public TimeSpan? Duration { get; internal set; }
    public string? Environment { get; set; }
    public string? Project { get; set; }
    public List<string> Tags { get; } = [];
    public TestManagementMetadata TestManagement { get; } = new();
    public Dictionary<string, object?> Integrations { get; } = [];
    public Dictionary<string, object?> Extensions { get; } = [];
    public string? Error { get; internal set; }
}