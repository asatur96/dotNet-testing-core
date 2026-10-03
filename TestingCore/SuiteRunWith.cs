namespace TestingCore.Domain;

public enum TestPlatform { Api, Web, Mobile }
public enum TestLanguage { EN, HY, RU }

public sealed record RunOptions(
    IReadOnlyList<TestLanguage>? Languages = null,
    IReadOnlyList<TestPlatform>? Platforms = null,
    bool IsAuthorized = true,
    string? UserQuery = null);

public sealed record RunCase(TestLanguage Language, bool IsAuthorized, TestPlatform Platform, string? UserQuery)
{
    public override string ToString() => $"{Language}/{Platform}/authorized={IsAuthorized}";
}

public static class RunMatrix
{
    public static IEnumerable<RunCase> Expand(RunOptions? options = null)
    {
        options ??= new RunOptions();
        var languages = options.Languages ?? [TestLanguage.EN, TestLanguage.HY, TestLanguage.RU];
        var platforms = options.Platforms ?? [TestPlatform.Api];
        foreach (var language in languages)
        foreach (var platform in platforms)
            yield return new RunCase(language, options.IsAuthorized, platform, options.UserQuery);
    }
}