using TestingCore.Domain;
using TestingCore.Ports;
using TestingCore.Application;
using TestingCore.Infrastructure;

namespace TestingCore.Tests;

public sealed class BackendTests(BackendFixture fixture) : IClassFixture<BackendFixture>
{
    [Theory]
    [InlineData("/health", 200)]
    [InlineData("/missing", 404)]
    public async Task Get_endpoint_records_request_and_validations(string path, int status)
    {
        var scope = fixture.CreateScope();
        await scope.Context.StepAsync($"GET {path}", $"HTTP {status}", async () =>
        {
            var response = await scope.Api.SendAsync(HttpMethod.Get, path);
            scope.Expect.ShouldHaveStatus(response, status)
                .ShouldHaveNonEmptyBody(response);
        });

        var step = Assert.Single(scope.Context.Steps);
        Assert.Equal(StepStatus.Passed, step.Status);
        Assert.Contains(step.Artifacts, a => a is ApiArtifact);
        Assert.Contains(step.Artifacts, a => a is ValidationArtifact);
    }

    [Fact]
    public async Task Failed_assertion_records_failed_step()
    {
        var scope = fixture.CreateScope();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            scope.Context.StepAsync("GET /health", "HTTP 500", async () =>
            {
                var response = await scope.Api.SendAsync(HttpMethod.Get, "/health");
                scope.Expect.ShouldHaveStatus(response, 500);
            }));

        Assert.True(scope.Context.HasFailures);
        Assert.Equal(StepStatus.Failed, Assert.Single(scope.Context.Steps).Status);
    }
}