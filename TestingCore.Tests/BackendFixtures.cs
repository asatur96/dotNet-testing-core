using System.Net;
using TestingCore.Domain;
using TestingCore.Ports;
using TestingCore.Application;
using TestingCore.Infrastructure;
using TestingCore.Xunit;

namespace TestingCore.Tests;

public sealed class BackendFixture : SuiteFixture
{
    private readonly HttpClient _http = new(new StubHandler())
    {
        BaseAddress = new Uri("https://backend.test/")
    };

    public BackendFixture() : base("Backend health") { }

    public BackendScope CreateScope(TestContext? context = null)
    {
        context ??= new TestContext();
        return new BackendScope(context, new HttpTestClient(_http, context), new ApiAssertions(context));
    }

    public override async Task DisposeAsync()
    {
        try { await base.DisposeAsync(); }
        finally { _http.Dispose(); }
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var status = request.RequestUri?.AbsolutePath == "/health" ? HttpStatusCode.OK : HttpStatusCode.NotFound;
            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(status == HttpStatusCode.OK ? """{"status":"healthy"}""" : """{"error":"not found"}""")
            });
        }
    }
}

public sealed record BackendScope(TestContext Context, IHttpTestClient Api, ApiAssertions Expect);