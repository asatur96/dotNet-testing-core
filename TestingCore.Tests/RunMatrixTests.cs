using TestingCore.Domain;

namespace TestingCore.Tests;

public sealed class RunMatrixTests
{
    [Fact]
    public void RunMatrix_expands_each_language_and_platform()
    {
        var cases = RunMatrix.Expand(new RunOptions(
            Languages: [TestLanguage.EN, TestLanguage.HY],
            Platforms: [TestPlatform.Api, TestPlatform.Mobile],
            IsAuthorized: false,
            UserQuery: "editor")).ToArray();

        Assert.Equal(4, cases.Length);
        Assert.All(cases, run =>
        {
            Assert.False(run.IsAuthorized);
            Assert.Equal("editor", run.UserQuery);
        });
        Assert.Equal((TestLanguage.EN, TestPlatform.Api), (cases[0].Language, cases[0].Platform));
        Assert.Equal((TestLanguage.HY, TestPlatform.Mobile), (cases[3].Language, cases[3].Platform));
    }

    [Fact]
    public void Failed_step_overrides_passed_runner_outcome()
    {
        var context = new TestContext();
        using (var step = context.StartStep("act", "expected"))
            step.Fail(new InvalidOperationException("broken"));
        context.Finish(TestStatus.Passed);
        Assert.Equal(TestStatus.Failed, context.Status);
        Assert.Equal(TestStatus.Failed, context.Metadata.Status);
    }
}