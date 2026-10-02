using TestingCore.Domain;
using TestingCore.Ports;
using System.Text.RegularExpressions;

namespace TestingCore.Application;

public sealed partial class TestCaseSync(ITestManagementPort management)
{
    [GeneratedRegex(@"@[Cc]_?(\d+)")]
    private static partial Regex CaseIdPattern();

    public async Task<TestCaseReference> SyncAsync(string title, TestContext context, CancellationToken cancellationToken = default)
    {
        var match = CaseIdPattern().Match(title);
        var cleanTitle = CaseIdPattern().Replace(title, "").Trim();
        if (string.IsNullOrWhiteSpace(cleanTitle)) throw new ArgumentException("Test title is required", nameof(title));

        var definition = new TestCaseDefinition(cleanTitle, $"Automated test {context.Id}", context.Steps);
        if (match.Success)
        {
            var id = match.Groups[1].Value;
            await management.UpdateCaseAsync(id, definition, cancellationToken);
            return new TestCaseReference(id, cleanTitle);
        }

        return await management.CreateCaseAsync(definition, cancellationToken);
    }
}