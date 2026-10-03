using System.Net;
using TestingCore.Domain;
using TestingCore.Infrastructure;
using TestingCore.Ports;

namespace TestingCore.Tests;

public sealed class HttpArtifactEvidenceTests
{
    [Fact]
    public async Task Default_policy_keeps_wire_and_return_values_but_omits_sensitive_step_evidence()
    {
        var handler = new RecordingHandler();
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://backend.test/") };
        var context = new TestContext();
        var client = new HttpTestClient(http, context);
        ApiArtifact? raw = null;

        await context.StepAsync("submit request", "response is available", async () =>
            raw = await client.SendAsync(HttpMethod.Post, "/private?token=query-secret",
                new { password = "client-secret" }));

        var recorded = Assert.IsType<ApiArtifact>(Assert.Single(Assert.Single(context.Steps).Artifacts));
        Assert.Contains("client-secret", handler.RequestBody);
        Assert.Contains("query-secret", handler.RequestUrl);
        Assert.Contains("client-secret", raw!.RequestBody);
        Assert.Contains("server-secret", raw.ResponseBody);
        Assert.Contains("query-secret", raw.Url);
        Assert.Equal("[omitted]", recorded.RequestBody);
        Assert.Equal("[omitted]", recorded.ResponseBody);
        Assert.Equal("https://backend.test/private", recorded.Url);
    }

    [Fact]
    public void Default_policy_removes_url_credentials_query_and_fragment()
    {
        var raw = new ApiArtifact("GET", "https://name:password@backend.test/private?token=secret#details",
            200, null, "", TimeSpan.Zero, DateTimeOffset.UtcNow);

        var recorded = new MinimalHttpArtifactEvidencePolicy().Prepare(raw);

        Assert.Equal("https://backend.test/private", recorded.Url);
    }
    [Fact]
    public async Task Http_client_can_be_used_during_setup_without_an_active_step()
    {
        using var http = new HttpClient(new RecordingHandler())
        {
            BaseAddress = new Uri("https://backend.test/")
        };
        var context = new TestContext();

        var result = await new HttpTestClient(http, context).SendAsync(HttpMethod.Get, "/private");

        Assert.Equal(200, result.Status);
        Assert.Empty(context.Steps);
    }

    [Fact]
    public async Task Consumer_can_replace_the_evidence_policy_without_changing_the_response()
    {
        using var http = new HttpClient(new RecordingHandler())
        {
            BaseAddress = new Uri("https://backend.test/")
        };
        var context = new TestContext();
        var client = new HttpTestClient(http, context, new FixedEvidencePolicy());
        ApiArtifact? raw = null;

        await context.StepAsync("request", "response", async () =>
            raw = await client.SendAsync(HttpMethod.Get, "/private"));

        var recorded = Assert.IsType<ApiArtifact>(Assert.Single(Assert.Single(context.Steps).Artifacts));
        Assert.Contains("server-secret", raw!.ResponseBody);
        Assert.Equal("[custom evidence]", recorded.ResponseBody);
    }

    private sealed class FixedEvidencePolicy : IHttpArtifactEvidencePolicy
    {
        public ApiArtifact Prepare(ApiArtifact raw) => raw with { ResponseBody = "[custom evidence]" };
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public string? RequestBody { get; private set; }
        public string? RequestUrl { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestUrl = request.RequestUri?.ToString();
            RequestBody = request.Content is null
                ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"token":"server-secret","status":"ok"}""")
            };
        }
    }
}