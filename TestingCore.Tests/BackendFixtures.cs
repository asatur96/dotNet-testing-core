using System.Net;
using TestingCore.Domain;
using TestingCore.Ports;
using TestingCore.Application;
using TestingCore.Infrastructure;

namespace TestingCore.Tests;

public sealed class BackendFixture : IDisposable
{
    private readonly HttpClient _http = new(new StubHandler())
    {
        BaseAddress = new Uri("https://backend.test/")
    };

    public BackendScope CreateScope()
    {
        var context = new TestContext();
        return new BackendScope(context, new HttpTestClient(_http, context), new ApiAssertions(context));
    }

    public void Dispose() => _http.Dispose();

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