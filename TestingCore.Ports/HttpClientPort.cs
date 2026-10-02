using TestingCore.Domain;

namespace TestingCore.Ports;

public interface IHttpTestClient
{
    Task<ApiArtifact> SendAsync(HttpMethod method, string path, object? body = null, CancellationToken cancellationToken = default);
}