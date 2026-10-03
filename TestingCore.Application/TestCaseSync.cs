using TestingCore.Domain;
using TestingCore.Ports;
using System.Text.RegularExpressions;

namespace TestingCore.Application;

public sealed partial class TestCaseSync(ITestManagementPort management)
{
    [GeneratedRegex(@"@[Cc]_?(\d+)")]
    private static partial Regex CaseIdPattern();

    [GeneratedRegex(@"@(smoke|regression)\b", RegexOptions.IgnoreCase)]
    private static partial Regex ClassificationTagPattern();

    public async Task<TestCaseReference?> SyncAsync(
        string title, TestContext context, CancellationToken cancellationToken = default)
    {
        var metadata = context.Metadata.TestManagement;
        if (metadata.Skip) return null;

        var matches = CaseIdPattern().Matches(title);
        if (matches.Count > 1)
            throw new ArgumentException("A test can reference only one Test Case", nameof(title));

        var taggedId = matches.Count == 1 ? matches[0].Groups[1].Value : null;
        if (taggedId is not null && metadata.CaseId is not null && taggedId != metadata.CaseId)
            throw new InvalidOperationException("Test Case ID conflicts with the title tag");

        var cleanTitle = CaseIdPattern().Replace(title, "");
        cleanTitle = ClassificationTagPattern().Replace(cleanTitle, "");
        cleanTitle = Regex.Replace(cleanTitle, @"\s+", " ").Trim();
        if (cleanTitle.Length == 0) throw new ArgumentException("Test title is required", nameof(title));

        var definition = new TestCaseDefinition(
            cleanTitle, $"Automated test {context.Id}", context.Steps);
        var caseId = taggedId ?? metadata.CaseId;
        TestCaseReference result;
        if (caseId is not null)
        {
            await management.UpdateCaseAsync(caseId, definition, cancellationToken);
            result = new TestCaseReference(caseId, cleanTitle);
        }
        else
        {
            result = await management.CreateCaseAsync(definition, cancellationToken);
        }

        metadata.CaseId = result.ExternalId;
        return result;
    }
}