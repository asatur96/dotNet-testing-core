using System.Diagnostics;
using System.Text;
using System.Text.Json;
using TestingCore.Domain;
using TestingCore.Ports;

namespace TestingCore.Infrastructure;

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