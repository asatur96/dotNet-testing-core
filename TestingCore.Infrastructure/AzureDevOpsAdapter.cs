using TestingCore.Domain;
using TestingCore.Ports;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;

namespace TestingCore.Infrastructure;

public sealed class AzureDevOpsAdapter(
    HttpClient http, string organization, string project, ICredentialProvider credentials)
    : ITestManagementPort, IIssueTrackingPort, ITraceabilityPort
{
    private string Root => $"https://dev.azure.com/{Uri.EscapeDataString(organization)}/{Uri.EscapeDataString(project)}/_apis/wit/workitems";

    public async Task<TestCaseReference?> GetCaseAsync(string id, CancellationToken cancellationToken = default)
    {
        if (!int.TryParse(id, out var caseId)) throw new ArgumentException("Azure case ID must be numeric", nameof(id));
        using var response = await SendAsync(HttpMethod.Get, $"{Root}/{caseId}?api-version=7.1", null, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var fields = json.RootElement.GetProperty("fields");
        if (fields.GetProperty("System.WorkItemType").GetString() != "Test Case")
            throw new InvalidOperationException($"Work item {id} is not a Test Case");
        return new TestCaseReference(id, fields.GetProperty("System.Title").GetString() ?? "");
    }

    public async Task<TestCaseReference> CreateCaseAsync(TestCaseDefinition testCase, CancellationToken cancellationToken = default)
    {
        var patch = CasePatch(testCase);
        using var response = await SendAsync(HttpMethod.Post, $"{Root}/$Test%20Case?api-version=7.1", patch, cancellationToken);
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        return new TestCaseReference(json.RootElement.GetProperty("id").GetInt32().ToString(), testCase.Title);
    }

    public async Task UpdateCaseAsync(string id, TestCaseDefinition testCase, CancellationToken cancellationToken = default)
    {
        if (!int.TryParse(id, out var caseId)) throw new ArgumentException("Azure case ID must be numeric", nameof(id));
        using var response = await SendAsync(HttpMethod.Patch, $"{Root}/{caseId}?api-version=7.1", CasePatch(testCase), cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<string> CreateBugAsync(Incident incident, CancellationToken cancellationToken = default)
    {
        var patch = new object[]
        {
            Field("System.Title", incident.Title),
            Field("Microsoft.VSTS.TCM.ReproSteps", incident.Description),
            Field("Microsoft.VSTS.Common.Severity", incident.Severity),
            Field("System.Description", $"Automated run: {incident.RunUrl}")
        };
        using var response = await SendAsync(HttpMethod.Post, $"{Root}/$Bug?api-version=7.1", patch, cancellationToken);
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        return json.RootElement.GetProperty("id").GetInt32().ToString();
    }

    public Task LinkRequirementToCaseAsync(int requirementId, int caseId, CancellationToken cancellationToken = default) =>
        AddLinkAsync(requirementId, caseId, "Microsoft.VSTS.Common.TestedBy-Forward", cancellationToken);

    public Task LinkBugToCaseAsync(int bugId, int caseId, CancellationToken cancellationToken = default) =>
        AddLinkAsync(bugId, caseId, "System.LinkTypes.Related", cancellationToken);

    private async Task AddLinkAsync(int sourceId, int targetId, string relation, CancellationToken cancellationToken)
    {
        if (sourceId <= 0 || targetId <= 0) throw new ArgumentOutOfRangeException(nameof(sourceId));
        var targetUrl = $"{Root}/{targetId}";
        var patch = new object[]
        {
            new { op = "add", path = "/relations/-", value = new { rel = relation, url = targetUrl } }
        };
        using var response = await SendAsync(HttpMethod.Patch, $"{Root}/{sourceId}?api-version=7.1", patch, cancellationToken);
        response.EnsureSuccessStatusCode();
    }
    private static object[] CasePatch(TestCaseDefinition testCase) =>
    [
        Field("System.Title", testCase.Title),
        Field("System.Description", testCase.Description),
        Field("Microsoft.VSTS.TCM.Steps", StepsXml(testCase.Steps))
    ];

    private static object Field(string path, string value) => new { op = "add", path = $"/fields/{path}", value };

    private static string StepsXml(IReadOnlyList<StepResult> steps) =>
        new XElement("steps", new XAttribute("id", "0"), new XAttribute("last", steps.Count),
            steps.Select((step, index) => new XElement("step",
                new XAttribute("id", index + 1), new XAttribute("type", "ActionStep"),
                new XElement("parameterizedString", new XAttribute("isformatted", "true"), step.Action),
                new XElement("parameterizedString", new XAttribute("isformatted", "true"), step.Expected),
                new XElement("description")))).ToString(SaveOptions.DisableFormatting);

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string url, object? patch, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, url);
        var credential = await credentials.GetCredentialAsync(cancellationToken);
        request.Headers.Authorization = new AuthenticationHeaderValue(credential.Scheme, credential.Value);
        if (patch is not null)
            request.Content = new StringContent(JsonSerializer.Serialize(patch), Encoding.UTF8, "application/json-patch+json");
        return await http.SendAsync(request, cancellationToken);
    }
}