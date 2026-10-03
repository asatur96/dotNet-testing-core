using TestingCore.Ports;

namespace TestingCore.Infrastructure;

public sealed class IntegrationRegistry(IConfigPort config, ISecretPort secrets)
{
    private readonly Dictionary<string, object> _factories = new(StringComparer.OrdinalIgnoreCase);

    public IntegrationRegistry Register<TServices>(IIntegrationFactory<TServices> factory)
        where TServices : class
    {
        if (!_factories.TryAdd(factory.Name, factory))
            throw new InvalidOperationException($"Integration already registered: {factory.Name}");
        return this;
    }

    public TServices? Create<TServices>(string name) where TServices : class
    {
        if (!_factories.TryGetValue(name, out var registration))
            throw new KeyNotFoundException($"Integration not registered: {name}");
        if (registration is not IIntegrationFactory<TServices> factory)
            throw new InvalidOperationException($"Integration {name} does not provide {typeof(TServices).Name}");

        var options = config.GetIntegration(name);
        return options.Enabled ? factory.Create(options, secrets) : null;
    }
}

public sealed class AzureDevOpsIntegrationFactory(HttpClient http) : IIntegrationFactory<IntegrationServices>
{
    public string Name => "azureDevOps";

    public IntegrationServices Create(IntegrationOptions options, ISecretPort secrets)
    {
        var adapter = new AzureDevOpsAdapter(http,
            options.Settings["organization"], options.Settings["project"],
            new AzureCredentialProvider(secrets));
        return new IntegrationServices(adapter, adapter, adapter);
    }
}