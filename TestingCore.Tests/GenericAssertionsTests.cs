using TestingCore.Application;
using TestingCore.Domain;

namespace TestingCore.Tests;

public sealed class GenericAssertionsTests
{
    [Fact]
    public async Task Deep_comparison_and_containment_record_service_validations()
    {
        var context = new TestContext();
        var assertions = new GenericAssertions(context);
        var actual = new { Id = 7, Profile = new { Name = "Ada", Roles = new[] { "admin", "reader" } } };

        await context.StepAsync("inspect service result", "profile is complete", () =>
        {
            assertions.Value(actual)
                .ShouldHaveValue(new { Id = 7, Profile = new { Name = "Ada", Roles = new[] { "admin", "reader" } } },
                    ComparisonMode.Deep)
                .ShouldContain(new { Profile = new { Name = "Ada" } })
                .ShouldContain(new Dictionary<string, object> { ["Id"] = 7 })
                .ShouldSatisfy(value => value.Profile.Roles.Length == 2, "two roles");
            return Task.CompletedTask;
        });

        var step = Assert.Single(context.Steps);
        Assert.Equal(StepStatus.Passed, step.Status);
        Assert.Equal(4, step.Artifacts.OfType<ValidationArtifact>().Count());
        Assert.All(step.Artifacts.OfType<ValidationArtifact>(), artifact => Assert.True(artifact.Success));
    }

    [Fact]
    public async Task Failure_records_validation_and_masks_sensitive_values()
    {
        var context = new TestContext();
        var assertions = new GenericAssertions(context);
        var actual = new { Password = "secret-value", Account = "1234567890123456", Count = 2 };

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            context.StepAsync("inspect result", "count is three", () =>
            {
                assertions.ShouldHaveValue(actual,
                    new { Password = "another-secret", Account = "1234567890123456", Count = 3 },
                    ComparisonMode.Deep);
                return Task.CompletedTask;
            }));

        var step = Assert.Single(context.Steps);
        var artifact = Assert.IsType<ValidationArtifact>(Assert.Single(step.Artifacts));
        Assert.Equal(StepStatus.Failed, step.Status);
        Assert.False(artifact.Success);
        Assert.Contains("[redacted]", artifact.Actual);
        Assert.DoesNotContain("secret-value", artifact.Actual);
        Assert.DoesNotContain("1234567890123456", artifact.Actual);
        Assert.DoesNotContain("secret-value", error.Message);
        Assert.DoesNotContain("secret-value", step.Error);
    }

    [Fact]
    public async Task Api_json_value_can_be_explicitly_hidden_from_validation_evidence()
    {
        var context = new TestContext();
        var response = new ApiArtifact("GET", "/private", 200, null,
            """{"code":"one-time-value"}""", TimeSpan.Zero, DateTimeOffset.UtcNow);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            context.StepAsync("check code", "code matches", () =>
            {
                new ApiAssertions(context).ShouldHaveJsonValue(response, "code", "wrong", sensitive: true);
                return Task.CompletedTask;
            }));

        var artifact = Assert.IsType<ValidationArtifact>(Assert.Single(Assert.Single(context.Steps).Artifacts));
        Assert.Equal("[redacted]", artifact.Actual);
        Assert.DoesNotContain("one-time-value", error.Message);
    }
    [Fact]
    public async Task Api_body_validation_does_not_copy_the_body_into_validation_evidence()
    {
        var context = new TestContext();
        var response = new ApiArtifact("GET", "/private", 200, null, "secret-response-body",
            TimeSpan.Zero, DateTimeOffset.UtcNow);

        await context.StepAsync("check body", "body exists", () =>
        {
            new ApiAssertions(context).ShouldHaveNonEmptyBody(response);
            return Task.CompletedTask;
        });

        var artifact = Assert.IsType<ValidationArtifact>(Assert.Single(Assert.Single(context.Steps).Artifacts));
        Assert.Equal("nonempty", artifact.Actual);
        Assert.DoesNotContain("secret-response-body", artifact.Actual);
    }
}