using TestingCore.Domain;
using TestingCore.Ports;
using TestingCore.Application;
using TestingCore.Infrastructure;

namespace TestingCore.Tests;

public sealed class IntegrationConfigurationTests
{
    [Fact]
    public void Disabled_integration_does_not_read_secrets()
    {
        var secrets = new FakeSecrets();
        var registry = new IntegrationRegistry(
            new FakeConfig(false), secrets, new HttpClient())
            .Register(new AzureDevOpsIntegrationFactory());

        Assert.Null(registry.Create("azureDevOps"));
        Assert.Equal(0, secrets.Reads);
    }

    [Fact]
    public void Enabled_integration_registers_its_ports_without_reading_secrets()
    {
        var secrets = new FakeSecrets();
        var registry = new IntegrationRegistry(
            new FakeConfig(true), secrets, new HttpClient())
            .Register(new AzureDevOpsIntegrationFactory());

        var services = Assert.IsType<IntegrationServices>(registry.Create("azureDevOps"));
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
    private sealed class FakeConfig(bool enabled) : IConfigPort
    {
        public IntegrationOptions GetIntegration(string name) =>
            new(enabled, new Dictionary<string, string>
            {
                ["organization"] = "org",
                ["project"] = "project"
            });
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
}