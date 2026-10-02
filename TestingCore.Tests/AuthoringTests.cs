using TestingCore;

namespace TestingCore.Tests;

[Suite("Backend health")]
public sealed class AuthoringTests(BackendFixture fixture) : IClassFixture<BackendFixture>
{
    public static IEnumerable<object[]> Cases => RunWith.Cases(new RunOptions(
        Languages: [TestLanguage.EN, TestLanguage.HY],
        Platforms: [TestPlatform.Web, TestPlatform.Mobile],
        IsAuthorized: false));

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Health_check(RunCase run)
    {
        var scope = fixture.CreateScope();
        scope.Context.Metadata["suite"] = "Backend health";
        scope.Context.Metadata["language"] = run.Language.ToString();
        scope.Context.Metadata["platform"] = run.Platform.ToString();
        scope.Context.Metadata["authorized"] = run.IsAuthorized.ToString();

        await new TestStep(scope.Context).Run("GET /health", "200 and healthy", async () =>
        {
            var response = await scope.Api.SendAsync(HttpMethod.Get, "/health");
            scope.Expect.ShouldHaveStatus(response, 200)
                .ShouldHaveJsonValue(response, "status", "healthy");
        });

        Assert.Equal(StepStatus.Passed, Assert.Single(scope.Context.Steps).Status);
    }
}