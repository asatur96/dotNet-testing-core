using System.Net;
using System.Text.Json;
using TestingCore.Domain;
using TestingCore.Infrastructure;
using TestingCore.Ports;

namespace TestingCore.Tests;

public sealed class AzureTestRunAdapterTests
{
    [Fact]
    public async Task Creates_automated_run_adds_case_and_bug_result_then_completes_run()
    {
        var handler = new CaptureHandler();
        using var http = new HttpClient(handler);
        var adapter = new AzureDevOpsAdapter(http, "org", "project", new StaticCredentials());
        var run = await adapter.CreateRunAsync(new TestRunDefinition("Service tests", "execution-1", 3, [9]));
        var result = await adapter.AddResultAsync(run.Id,
            new TestResultDefinition("123", "handles command", "Suite.handles command",
                TestStatus.Failed, "validation failed", DateTimeOffset.UtcNow.AddSeconds(-1),
                DateTimeOffset.UtcNow, [67], 9));
        await adapter.CompleteRunAsync(run.Id);

        Assert.Equal("7", run.Id);
        Assert.Equal("22", result.Id);
        Assert.Equal(3, handler.Requests.Count);
        var create = handler.Requests[0];
        Assert.Equal("POST", create.Method);
        Assert.Contains("/_apis/test/runs?api-version=7.1", create.Path);
        Assert.Equal("application/json", create.MediaType);
        Assert.Equal("Bearer token", create.Authorization);
        using (var json = JsonDocument.Parse(create.Body))
        {
            Assert.True(json.RootElement.GetProperty("automated").GetBoolean());
            Assert.Equal("InProgress", json.RootElement.GetProperty("state").GetString());
            Assert.Equal("3", json.RootElement.GetProperty("plan").GetProperty("id").GetString());
            Assert.Equal(9, json.RootElement.GetProperty("pointIds")[0].GetInt32());
        }

        var added = handler.Requests[1];
        Assert.Equal("POST", added.Method);
        Assert.Contains("/runs/7/results?api-version=7.1", added.Path);
        using (var json = JsonDocument.Parse(added.Body))
        {
            var item = json.RootElement[0];
            Assert.Equal("123", item.GetProperty("testCase").GetProperty("id").GetString());
            Assert.Equal("Failed", item.GetProperty("outcome").GetString());
            Assert.Equal("67", item.GetProperty("associatedBugs")[0].GetProperty("id").GetString());
            Assert.Equal("9", item.GetProperty("testPoint").GetProperty("id").GetString());
        }

        var complete = handler.Requests[2];
        Assert.Equal("PATCH", complete.Method);
        Assert.Contains("/runs/7?api-version=7.1", complete.Path);
        using var completed = JsonDocument.Parse(complete.Body);
        Assert.Equal("Completed", completed.RootElement.GetProperty("state").GetString());
    }

    [Fact]
    public async Task Invalid_case_id_fails_before_the_result_request()
    {
        var handler = new CaptureHandler();
        using var http = new HttpClient(handler);
        var adapter = new AzureDevOpsAdapter(http, "org", "project", new StaticCredentials());
        var now = DateTimeOffset.UtcNow;

        await Assert.ThrowsAsync<ArgumentException>(() => adapter.AddResultAsync("7",
            new TestResultDefinition("invalid", "title", "name", TestStatus.Passed,
                "", now, now.AddSeconds(1), [])));

        Assert.Empty(handler.Requests);
    }

    private sealed class StaticCredentials : ICredentialProvider
    {
        public ValueTask<Credential> GetCredentialAsync(CancellationToken token = default) =>
            ValueTask.FromResult(new Credential("Bearer", "token"));
    }

    private sealed record CapturedRequest(string Method, string Path, string MediaType,
        string Authorization, string Body);

    private sealed class CaptureHandler : HttpMessageHandler
    {
        public List<CapturedRequest> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri?.PathAndQuery ?? "";
            Requests.Add(new CapturedRequest(request.Method.Method, path,
                request.Content?.Headers.ContentType?.MediaType ?? "",
                request.Headers.Authorization?.ToString() ?? "",
                request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken)));
            var response = request.Method == HttpMethod.Post && path.EndsWith("/results?api-version=7.1")
                ? """{"value":[{"id":22,"url":"https://azure/result/22"}]}"""
                : """{"id":7,"webAccessUrl":"https://azure/run/7"}""";
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(response) };
        }
    }
}