using System.Diagnostics;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace TestingCore;

public interface IHttpTestClient
{
    Task<ApiArtifact> SendAsync(HttpMethod method, string path, object? body = null, CancellationToken cancellationToken = default);
}

public sealed class HttpTestClient(HttpClient http, TestContext context) : IHttpTestClient
{
    public async Task<ApiArtifact> SendAsync(HttpMethod method, string path, object? body = null, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(method, path);
        string? requestBody = null;
        if (body is not null)
        {
            requestBody = JsonSerializer.Serialize(body);
            request.Content = new StringContent(requestBody, Encoding.UTF8, "application/json");
        }

        var watch = Stopwatch.StartNew();
        using var response = await http.SendAsync(request, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        watch.Stop();
        var artifact = new ApiArtifact(method.Method, request.RequestUri?.ToString() ?? path,
            (int)response.StatusCode, requestBody, responseBody, watch.Elapsed, DateTimeOffset.UtcNow);
        context.AddArtifact(artifact);
        return artifact;
    }
}

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