using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;

namespace TestingCore;

public sealed class AzureDevOpsAdapter(
    HttpClient http, string organization, string project, Func<CancellationToken, Task<string>> accessToken)
    : ITestManagementPort, IIssueTrackingPort
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
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await accessToken(cancellationToken));
        if (patch is not null)
            request.Content = new StringContent(JsonSerializer.Serialize(patch), Encoding.UTF8, "application/json-patch+json");
        return await http.SendAsync(request, cancellationToken);
    }
}