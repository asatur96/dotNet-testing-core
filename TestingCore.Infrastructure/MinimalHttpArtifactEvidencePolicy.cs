using TestingCore.Domain;
using TestingCore.Ports;

namespace TestingCore.Infrastructure;

public sealed class MinimalHttpArtifactEvidencePolicy : IHttpArtifactEvidencePolicy
{
    public ApiArtifact Prepare(ApiArtifact raw) => raw with
    {
        Url = SafeUrl(raw.Url),
        RequestBody = raw.RequestBody is null ? null : "[omitted]",
        ResponseBody = raw.ResponseBody.Length == 0 ? "" : "[omitted]"
    };

    private static string SafeUrl(string url)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out var parsed))
        {
            var safe = new UriBuilder(parsed)
            {
                UserName = "",
                Password = "",
                Query = "",
                Fragment = ""
            };
            return safe.Uri.ToString();
        }

        var query = url.IndexOfAny(['?', '#']);
        return query < 0 ? url : url[..query];
    }
}