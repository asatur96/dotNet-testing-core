using System.Diagnostics;
using System.Text.Json;
using TestingCore.Application;

namespace TestingCore.Infrastructure;

public static class ImpactManifestLoader
{
    public static ImpactManifest Load(string path)
    {
        var manifest = JsonSerializer.Deserialize<ImpactManifest>(
            File.ReadAllText(path), new JsonSerializerOptions(JsonSerializerDefaults.Web));
        return manifest ?? throw new InvalidDataException("Impact manifest is empty");
    }
}

public static class GitChangedFiles
{
    public static async Task<IReadOnlyList<string>> SinceAsync(
        string repositoryRoot, string baseRevision, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(baseRevision) || baseRevision.StartsWith('-'))
            throw new ArgumentException("A Git base revision is required", nameof(baseRevision));

        var start = new ProcessStartInfo("git")
        {
            WorkingDirectory = Path.GetFullPath(repositoryRoot),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        foreach (var argument in new[]
                 { "diff", "--name-only", "--diff-filter=ACDMR", "-z", baseRevision + "...HEAD", "--" })
            start.ArgumentList.Add(argument);

        using var process = Process.Start(start)
            ?? throw new InvalidOperationException("Git could not be started");
        var output = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var error = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        if (process.ExitCode != 0)
            throw new InvalidOperationException("Git diff failed: " + await error);
        return (await output).Split('\0', StringSplitOptions.RemoveEmptyEntries);
    }
}