using System.Collections;

namespace TestingCore.Domain;

public enum TestPlatform { Api, Web, Mobile }
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
        var platforms = options.Platforms ?? [TestPlatform.Api];
        foreach (var language in languages)
        foreach (var platform in platforms)
            yield return [new RunCase(language, options.IsAuthorized, platform, options.UserQuery)];
    }
}

public sealed class TestStep(TestContext context)
{
    public Task Run(string action, string expected, Func<Task> body) =>
        context.StepAsync(action, expected, body);
}