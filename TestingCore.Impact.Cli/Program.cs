using System.Text.Json;
using TestingCore.Application;
using TestingCore.Infrastructure;

if (args.Length != 3)
{
    Console.Error.WriteLine("Usage: impact <repository-root> <base-revision> <manifest.json>");
    return 2;
}

try
{
    var root = Path.GetFullPath(args[0]);
    var manifestPath = Path.IsPathRooted(args[2]) ? args[2] : Path.Combine(root, args[2]);
    var manifest = ImpactManifestLoader.Load(manifestPath);
    var files = await GitChangedFiles.SinceAsync(root, args[1]);
    var report = new ChangeImpactAnalyzer(manifest).Analyze(files);
    Console.WriteLine(JsonSerializer.Serialize(report, new JsonSerializerOptions(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    }));
    return 0;
}
catch (Exception error)
{
    Console.Error.WriteLine(error.Message);
    return 2;
}