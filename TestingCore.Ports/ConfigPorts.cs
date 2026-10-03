namespace TestingCore.Ports;

public sealed record IntegrationOptions(bool Enabled, IReadOnlyDictionary<string, string> Settings);

public interface IEnvPort
{
    string GetRequired(string name);
    string? GetOptional(string name);
    bool GetBoolean(string name);
}

public interface ISecretPort
{
    string? GetSecret(string name);
}

public interface IConfigPort
{
    IntegrationOptions GetIntegration(string name);
}

public sealed record Credential(string Scheme, string Value);

public interface ICredentialProvider
{
    ValueTask<Credential> GetCredentialAsync(CancellationToken cancellationToken = default);
}

public sealed record IntegrationServices(ITestManagementPort? TestManagement = null, IIssueTrackingPort? IssueTracking = null, ITraceabilityPort? Traceability = null);

public interface IIntegrationFactory<out TServices> where TServices : class
{
    string Name { get; }
    TServices Create(IntegrationOptions options, ISecretPort secrets);
}