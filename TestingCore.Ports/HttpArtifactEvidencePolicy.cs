using TestingCore.Domain;

namespace TestingCore.Ports;

public interface IHttpArtifactEvidencePolicy
{
    ApiArtifact Prepare(ApiArtifact raw);
}