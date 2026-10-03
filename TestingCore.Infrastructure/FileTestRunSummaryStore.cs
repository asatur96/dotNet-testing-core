using System.Diagnostics;
using System.Text.Json;
using TestingCore.Domain;
using TestingCore.Ports;

namespace TestingCore.Infrastructure;

public sealed class FileTestRunSummaryStore(string path) : ITestRunSummaryStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly string _path = Path.GetFullPath(path);

    public async Task<TestRunSummary?> ReadAsync(CancellationToken cancellationToken = default)
    {
        using var held = await AcquireAsync(cancellationToken);
        return await ReadUnlockedAsync(cancellationToken);
    }

    public async Task<TestRunSummary> UpdateAsync(
        Func<TestRunSummary?, TestRunSummary> update,
        CancellationToken cancellationToken = default)
    {
        using var held = await AcquireAsync(cancellationToken);
        var summary = update(await ReadUnlockedAsync(cancellationToken));
        var temporaryPath = _path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await File.WriteAllTextAsync(
                temporaryPath, JsonSerializer.Serialize(summary, JsonOptions), cancellationToken);
            File.Move(temporaryPath, _path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
        return summary;
    }

    private async Task<TestRunSummary?> ReadUnlockedAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_path)) return null;
        var json = await File.ReadAllTextAsync(_path, cancellationToken);
        return JsonSerializer.Deserialize<TestRunSummary>(json, JsonOptions)
            ?? throw new InvalidDataException("Run summary JSON is empty");
    }

    private async Task<FileStream> AcquireAsync(CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        var watch = Stopwatch.StartNew();
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                return new FileStream(_path + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException) when (watch.Elapsed < TimeSpan.FromSeconds(10))
            {
                await Task.Delay(25, cancellationToken);
            }
        }
    }
}