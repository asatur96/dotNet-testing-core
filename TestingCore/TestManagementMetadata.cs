namespace TestingCore.Domain;

public sealed class TestManagementMetadata
{
    public bool Skip { get; set; }
    public string? CaseId { get; set; }
    public int? PointId { get; set; }
    public List<int> BugIds { get; } = [];
    public string? RunId { get; set; }
    public string? ResultId { get; set; }
    public string? ResultUrl { get; set; }
}