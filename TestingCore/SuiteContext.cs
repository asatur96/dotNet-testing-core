namespace TestingCore.Domain;

public sealed class SuiteContext(string name)
{
    private readonly List<TestContext> _tests = [];
    public Guid Id { get; } = Generators.Guid();
    public string Name { get; } = name;
    public DateTimeOffset StartedAt { get; } = DateTimeOffset.UtcNow;
    public IReadOnlyList<TestContext> Tests => _tests;
    public bool HasFailures => _tests.Any(test => test.HasFailures);
    public void AddTest(TestContext test) => _tests.Add(test);
}