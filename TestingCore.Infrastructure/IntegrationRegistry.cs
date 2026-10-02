using TestingCore.Ports;

namespace TestingCore.Infrastructure;

public sealed class IntegrationRegistry(IConfigPort config, ISecretPort secrets, HttpClient http)
{
    private readonly Dictionary<string, IIntegrationFactory> _factories = new(StringComparer.OrdinalIgnoreCase);

    public IntegrationRegistry Register(IIntegrationFactory factory)
    {
        if (!_factories.TryAdd(factory.Name, factory))
            throw new InvalidOperationException($"Integration already registered: {factory.Name}");
        return this;
    }

    public IntegrationServices? Create(string name)
    {
        if (!_factories.TryGetValue(name, out var factory))
            throw new KeyNotFoundException($"Integration not registered: {name}");
        var options = config.GetIntegration(name);
        return options.Enabled ? factory.Create(options, http, secrets) : null;
    }
}

public sealed class AzureDevOpsIntegrationFactory : IIntegrationFactory
{
    public string Name => "azureDevOps";

    public IntegrationServices Create(IntegrationOptions options, HttpClient http, ISecretPort secrets)
    {
        var adapter = new AzureDevOpsAdapter(http,
            options.Settings["organization"], options.Settings["project"],
            new AzureCredentialProvider(secrets));
        return new IntegrationServices(adapter, adapter, adapter);
    }
}