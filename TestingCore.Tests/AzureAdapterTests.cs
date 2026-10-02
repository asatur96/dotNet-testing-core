using System.Net;
using System.Text.Json;
using TestingCore;

namespace TestingCore.Tests;

public sealed class AzureAdapterTests
{
    [Fact]
    public async Task Create_case_sends_steps_to_Azure_work_item_API()
    {
        var handler = new CaptureHandler("""{"id":42}""");
        using var http = new HttpClient(handler);
        var azure = new AzureDevOpsAdapter(http, "org", "project", _ => Task.FromResult("token"));
        var result = await azure.CreateCaseAsync(new TestCaseDefinition("Health", "API health",
            [new StepResult("GET /health", "200", StepStatus.Passed, null, [])]));

        Assert.Equal("42", result.ExternalId);
        Assert.Contains("/$Test%20Case?api-version=7.1", handler.Path);
        Assert.Contains("Microsoft.VSTS.TCM.Steps", handler.Body);
        Assert.Contains("GET /health", handler.Body);
        Assert.Equal("Bearer token", handler.Authorization);
    }

    [Fact]
    public async Task Create_bug_sends_reproduction_to_Azure_Boards()
    {
        var handler = new CaptureHandler("""{"id":81}""");
        using var http = new HttpClient(handler);
        var azure = new AzureDevOpsAdapter(http, "org", "project", _ => Task.FromResult("token"));
        var id = await azure.CreateBugAsync(new Incident("Failure", "Repro steps", "2 - High", "https://run"));

        Assert.Equal("81", id);
        Assert.Contains("/$Bug?api-version=7.1", handler.Path);
        Assert.Contains("Microsoft.VSTS.TCM.ReproSteps", handler.Body);
        Assert.Contains("Repro steps", handler.Body);
    }

    private sealed class CaptureHandler(string responseJson) : HttpMessageHandler
    {
        public string Path { get; private set; } = "";
        public string Body { get; private set; } = "";
        public string Authorization { get; private set; } = "";

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Path = request.RequestUri?.PathAndQuery ?? "";
            Body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);
            Authorization = request.Headers.Authorization?.ToString() ?? "";
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(responseJson) };
        }
    }
}