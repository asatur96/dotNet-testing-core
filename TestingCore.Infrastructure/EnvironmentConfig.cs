using System.Net.Http.Headers;
using System.Text;
using TestingCore.Ports;

namespace TestingCore.Infrastructure;

public sealed class ProcessEnvAdapter : IEnvPort, ISecretPort
{
    public string GetRequired(string name) =>
        GetOptional(name) ?? throw new InvalidOperationException($"Missing environment variable: {name}");
    public string? GetOptional(string name) =>
        Environment.GetEnvironmentVariable(name) is { Length: > 0 } value ? value : null;
    public bool GetBoolean(string name) => string.Equals(GetOptional(name), "true", StringComparison.OrdinalIgnoreCase);
    public string? GetSecret(string name) => GetOptional(name);
}

public sealed record IntegrationDefinition(
    string Name, string EnvironmentPrefix, IReadOnlyDictionary<string, string> RequiredSettings);

public sealed class EnvConfigAdapter : IConfigPort
{
    private readonly IEnvPort _env;
    private readonly Dictionary<string, IntegrationDefinition> _definitions = new(StringComparer.OrdinalIgnoreCase);

    public EnvConfigAdapter(IEnvPort env)
    {
        _env = env;
        Register(new IntegrationDefinition("azureDevOps", "AZDO", new Dictionary<string, string>
        {
            ["organization"] = "ORGANIZATION",
            ["project"] = "PROJECT"
        }));
    }

    public EnvConfigAdapter Register(IntegrationDefinition definition)
    {
        if (!_definitions.TryAdd(definition.Name, definition))
            throw new InvalidOperationException($"Integration config already registered: {definition.Name}");
        return this;
    }

    public IntegrationOptions GetIntegration(string name)
    {
        if (!_definitions.TryGetValue(name, out var definition))
            throw new KeyNotFoundException($"Integration config not registered: {name}");
        if (!_env.GetBoolean($"{definition.EnvironmentPrefix}_ENABLED"))
            return new IntegrationOptions(false, new Dictionary<string, string>());

        var settings = definition.RequiredSettings.ToDictionary(
            pair => pair.Key,
            pair => _env.GetRequired($"{definition.EnvironmentPrefix}_{pair.Value}"));
        return new IntegrationOptions(true, settings);
    }
}
public sealed class AzureCredentialProvider(ISecretPort secrets) : ICredentialProvider
{
    public ValueTask<Credential> GetCredentialAsync(CancellationToken cancellationToken = default)
    {
        if (secrets.GetSecret("SYSTEM_ACCESSTOKEN") is { } jobToken)
            return ValueTask.FromResult(new Credential("Bearer", jobToken));
        if (secrets.GetSecret("AZDO_PAT") is { } pat)
            return ValueTask.FromResult(new Credential("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes(":" + pat))));
        throw new InvalidOperationException("Azure credential missing: set SYSTEM_ACCESSTOKEN or AZDO_PAT");
    }
}