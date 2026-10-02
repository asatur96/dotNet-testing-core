using System.Collections;

namespace TestingCore;

public enum TestPlatform { Web, Mobile }
public enum TestLanguage { EN, HY, RU }

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class SuiteAttribute(string name) : Attribute
{
    public string Name { get; } = name;
}

public sealed record RunOptions(
    IReadOnlyList<TestLanguage>? Languages = null,
    IReadOnlyList<TestPlatform>? Platforms = null,
    bool IsAuthorized = true,
    string? UserQuery = null);

public sealed record RunCase(TestLanguage Language, bool IsAuthorized, TestPlatform Platform, string? UserQuery)
{
    public override string ToString() => $"{Language}/{Platform}/authorized={IsAuthorized}";
}

public static class RunWith
{
    public static IEnumerable<object[]> Cases(RunOptions? options = null)
    {
        options ??= new RunOptions();
        var languages = options.Languages ?? [TestLanguage.EN, TestLanguage.HY, TestLanguage.RU];
        var platforms = options.Platforms ?? [TestPlatform.Web, TestPlatform.Mobile];
        foreach (var language in languages)
        foreach (var platform in platforms)
            yield return [new RunCase(language, options.IsAuthorized, platform, options.UserQuery)];
    }
}

public sealed class SuiteContext(string name)
{
    private readonly List<TestContext> _tests = [];
    public Guid Id { get; } = Guid.NewGuid();
    public string Name { get; } = name;
    public DateTimeOffset StartedAt { get; } = DateTimeOffset.UtcNow;
    public IReadOnlyList<TestContext> Tests => _tests;
    public bool HasFailures => _tests.Any(test => test.HasFailures);
    public void AddTest(TestContext test) => _tests.Add(test);
}

public sealed class TestStep(TestContext context)
{
    public Task Run(string action, string expected, Func<Task> body) =>
        context.StepAsync(action, expected, body);
}