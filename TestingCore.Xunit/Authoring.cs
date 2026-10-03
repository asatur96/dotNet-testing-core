using TestingCore.Domain;

namespace TestingCore.Xunit;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class SuiteAttribute(string name) : Attribute
{
    public string Name { get; } = name;
}

public static class RunWith
{
    public static IEnumerable<object[]> Cases(RunOptions? options = null) =>
        RunMatrix.Expand(options).Select(run => new object[] { run });
}

public sealed class TestStep(TestContext context)
{
    public Task Run(string action, string expected, Func<Task> body) =>
        context.StepAsync(action, expected, body);
}