using TestingCore.Ports;
using TestingCore.Infrastructure;

namespace TestingCore.Tests;

public sealed class IntegrationConfigurationTests
{
    [Fact]
    public void Disabled_integration_does_not_read_secrets()
    {
        var secrets = new FakeSecrets();
        using var http = new HttpClient();
        var registry = new IntegrationRegistry(new FakeConfig(false), secrets)
            .Register(new AzureDevOpsIntegrationFactory(http));

        Assert.Null(registry.Create<IntegrationServices>("azureDevOps"));
        Assert.Equal(0, secrets.Reads);
    }

    [Fact]
    public void Enabled_integration_registers_its_ports_without_reading_secrets()
    {
        var secrets = new FakeSecrets();
        using var http = new HttpClient();
        var registry = new IntegrationRegistry(new FakeConfig(true), secrets)
            .Register(new AzureDevOpsIntegrationFactory(http));

        var services = Assert.IsType<IntegrationServices>(registry.Create<IntegrationServices>("azureDevOps"));
        Assert.NotNull(services.TestManagement);
        Assert.NotNull(services.IssueTracking);
        Assert.NotNull(services.Traceability);
        Assert.Equal(0, secrets.Reads);
    }

    [Fact]
    public async Task Pipeline_token_takes_precedence_over_local_pat()
    {
        var secrets = new FakeSecrets(new Dictionary<string, string>
        {
            ["SYSTEM_ACCESSTOKEN"] = "job-token",
            ["AZDO_PAT"] = "local-pat"
        });
        var credential = await new AzureCredentialProvider(secrets).GetCredentialAsync();
        Assert.Equal("Bearer", credential.Scheme);
        Assert.Equal("job-token", credential.Value);
    }

    [Fact]
    public void Third_party_definition_reads_only_registered_nonsecret_settings()
    {
        var env = new FakeEnv(new Dictionary<string, string>
        {
            ["EMAIL_ENABLED"] = "true",
            ["EMAIL_HOST"] = "mail.example",
            ["EMAIL_PASSWORD"] = "secret"
        });
        var config = new EnvConfigAdapter(env).Register(new IntegrationDefinition(
            "email", "EMAIL", new Dictionary<string, string> { ["host"] = "HOST" }));

        var options = config.GetIntegration("email");

        Assert.True(options.Enabled);
        Assert.Equal("mail.example", options.Settings["host"]);
        Assert.DoesNotContain("password", options.Settings.Keys);
        Assert.DoesNotContain("EMAIL_PASSWORD", env.ReadKeys);
    }

    [Fact]
    public void Custom_integration_can_be_added_without_changing_shared_services()
    {
        var secrets = new FakeSecrets(new Dictionary<string, string> { ["AUDIT_TOKEN"] = "audit-secret" });
        var registry = new IntegrationRegistry(new FakeConfig(true), secrets)
            .Register(new AuditFactory());

        var audit = Assert.IsAssignableFrom<IAuditService>(registry.Create<IAuditService>("audit"));
        Assert.Equal(0, secrets.Reads);
        Assert.Equal("project:audit-secret", audit.Identify());
        Assert.Equal(1, secrets.Reads);
    }

    [Fact]
    public void Wrong_service_type_fails_before_loading_config_or_secrets()
    {
        var secrets = new FakeSecrets();
        var config = new FakeConfig(true);
        var registry = new IntegrationRegistry(config, secrets).Register(new AuditFactory());

        Assert.Throws<InvalidOperationException>(() => registry.Create<IntegrationServices>("audit"));
        Assert.Equal(0, config.Reads);
        Assert.Equal(0, secrets.Reads);
    }

    private sealed class FakeConfig(bool enabled) : IConfigPort
    {
        public int Reads { get; private set; }
        public IntegrationOptions GetIntegration(string name)
        {
            Reads++;
            return new(enabled, new Dictionary<string, string>
            {
                ["organization"] = "org",
                ["project"] = "project"
            });
        }
    }

    private sealed class FakeEnv(Dictionary<string, string> values) : IEnvPort
    {
        public List<string> ReadKeys { get; } = [];
        public string GetRequired(string name) =>
            GetOptional(name) ?? throw new InvalidOperationException(name);
        public string? GetOptional(string name)
        {
            ReadKeys.Add(name);
            return values.TryGetValue(name, out var value) ? value : null;
        }
        public bool GetBoolean(string name) => GetOptional(name) == "true";
    }

    private sealed class FakeSecrets(Dictionary<string, string>? values = null) : ISecretPort
    {
        public int Reads { get; private set; }
        public string? GetSecret(string name)
        {
            Reads++;
            return values is not null && values.TryGetValue(name, out var value) ? value : null;
        }
    }

    private interface IAuditService
    {
        string Identify();
    }

    private sealed class AuditService(string project, ISecretPort secrets) : IAuditService
    {
        public string Identify() => $"{project}:{secrets.GetSecret("AUDIT_TOKEN")}";
    }

    private sealed class AuditFactory : IIntegrationFactory<IAuditService>
    {
        public string Name => "audit";
        public IAuditService Create(IntegrationOptions options, ISecretPort secrets) =>
            new AuditService(options.Settings["project"], secrets);
    }
}