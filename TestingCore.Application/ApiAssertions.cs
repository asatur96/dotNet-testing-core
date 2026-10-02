using System.Text.Json;
using TestingCore.Domain;

namespace TestingCore.Application;

public sealed class ApiAssertions(TestContext context)
{
    public ApiAssertions ShouldHaveStatus(ApiArtifact response, int expected)
    {
        Record("HTTP status", expected.ToString(), response.Status.ToString(), response.Status == expected);
        return this;
    }

    public ApiAssertions ShouldHaveNonEmptyBody(ApiArtifact response)
    {
        Record("Nonempty body", "nonempty", response.ResponseBody, !string.IsNullOrWhiteSpace(response.ResponseBody));
        return this;
    }

    public ApiAssertions ShouldHaveJsonValue<T>(ApiArtifact response, string property, T expected)
    {
        using var json = JsonDocument.Parse(response.ResponseBody);
        var actual = json.RootElement.GetProperty(property).ToString();
        Record(property, expected?.ToString() ?? "null", actual, actual == expected?.ToString());
        return this;
    }

    private void Record(string name, string expected, string actual, bool success)
    {
        context.AddArtifact(new ValidationArtifact(name, expected, actual, success, DateTimeOffset.UtcNow));
        if (!success) throw new InvalidOperationException($"{name}: expected {expected}, got {actual}");
    }
}