using TestingCore.Domain;
using TestingCore.Xunit;

namespace TestingCore.Tests;

[Suite("Backend health")]
public sealed class AuthoringTests(BackendFixture fixture) : IClassFixture<BackendFixture>
{
    public static IEnumerable<object[]> Cases => RunWith.Cases(new RunOptions(
        Languages: [TestLanguage.EN, TestLanguage.HY],
        Platforms: [TestPlatform.Api],
        IsAuthorized: false));

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Health_check(RunCase run)
    {
        TestContext? completed = null;
        await fixture.RunAsync($"Health {run}", async context =>
        {
            completed = context;
            context.Metadata.Extensions["language"] = run.Language.ToString();
            context.Metadata.Extensions["platform"] = run.Platform.ToString();
            context.Metadata.Extensions["authorized"] = run.IsAuthorized.ToString();
            var scope = fixture.CreateScope(context);

            await new TestStep(context).Run("GET /health", "200 and healthy", async () =>
            {
                var response = await scope.Api.SendAsync(HttpMethod.Get, "/health");
                scope.Expect.ShouldHaveStatus(response, 200)
                    .ShouldHaveJsonValue(response, "status", "healthy");
            });
        });

        Assert.Equal(TestStatus.Passed, completed!.Status);
        Assert.Equal(StepStatus.Passed, Assert.Single(completed.Steps).Status);
    }
}