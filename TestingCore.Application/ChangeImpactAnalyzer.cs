using System.Text.RegularExpressions;

namespace TestingCore.Application;

public sealed record ImpactComponent(
    string Name, IReadOnlyList<string> Paths,
    IReadOnlyList<string> DependsOn, string TestFilter);

public sealed record ImpactManifest(IReadOnlyList<ImpactComponent> Components);

public sealed record ImpactedComponent(string Name, string Reason, string TestFilter);

public sealed record ChangeImpactReport(
    IReadOnlyList<string> ChangedFiles,
    IReadOnlyList<string> UnmappedFiles,
    IReadOnlyList<ImpactedComponent> AffectedComponents,
    string Selection,
    string? RecommendedFilter);

public sealed class ChangeImpactAnalyzer
{
    private readonly Dictionary<string, ImpactComponent> _components;
    private readonly Dictionary<string, List<string>> _dependents;

    public ChangeImpactAnalyzer(ImpactManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        _components = new Dictionary<string, ImpactComponent>(StringComparer.OrdinalIgnoreCase);
        foreach (var component in manifest.Components)
        {
            if (string.IsNullOrWhiteSpace(component.Name) ||
                component.Paths is null || component.Paths.Count == 0 ||
                component.DependsOn is null || string.IsNullOrWhiteSpace(component.TestFilter))
                throw new ArgumentException("Every component needs a name, paths, dependencies, and test filter");
            if (!_components.TryAdd(component.Name, component))
                throw new ArgumentException($"Duplicate component: {component.Name}");
        }

        _dependents = _components.Keys.ToDictionary(
            name => name, _ => new List<string>(), StringComparer.OrdinalIgnoreCase);
        foreach (var component in _components.Values)
        foreach (var dependency in component.DependsOn)
        {
            if (!_dependents.TryGetValue(dependency, out var dependents))
                throw new ArgumentException($"Unknown dependency {dependency} in {component.Name}");
            if (string.Equals(dependency, component.Name, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException($"Component {component.Name} cannot depend on itself");
            dependents.Add(component.Name);
        }
    }

    public ChangeImpactReport Analyze(IEnumerable<string> changedFiles)
    {
        ArgumentNullException.ThrowIfNull(changedFiles);
        var files = changedFiles.Select(Normalize).Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase).ToArray();
        var unmapped = new List<string>();
        var affected = new Dictionary<string, ImpactedComponent>(StringComparer.OrdinalIgnoreCase);
        var queue = new Queue<string>();

        foreach (var file in files)
        {
            var matches = _components.Values.Where(component =>
                component.Paths.Any(pattern => Matches(pattern, file))).ToArray();
            if (matches.Length == 0)
            {
                unmapped.Add(file);
                continue;
            }

            foreach (var component in matches)
            {
                if (!affected.TryAdd(component.Name,
                        new ImpactedComponent(component.Name, $"changed: {file}", component.TestFilter)))
                    continue;
                queue.Enqueue(component.Name);
            }
        }

        while (queue.Count > 0)
        {
            var dependency = queue.Dequeue();
            foreach (var dependentName in _dependents[dependency])
            {
                var dependent = _components[dependentName];
                if (!affected.TryAdd(dependent.Name,
                        new ImpactedComponent(dependent.Name, $"depends on: {dependency}", dependent.TestFilter)))
                    continue;
                queue.Enqueue(dependent.Name);
            }
        }

        var entries = affected.Values.OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase).ToArray();
        var selection = unmapped.Count > 0 ? "full" : entries.Length == 0 ? "none" : "targeted";
        var filter = selection == "targeted"
            ? string.Join("|", entries.Select(item => item.TestFilter)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Order(StringComparer.OrdinalIgnoreCase)
                .Select(value => $"({value})"))
            : null;
        return new ChangeImpactReport(files, unmapped, entries, selection, filter);
    }

    private static string Normalize(string path)
    {
        var normalized = path.Replace('\\', '/');
        return normalized.StartsWith("./", StringComparison.Ordinal) ? normalized[2..] : normalized;
    }

    private static bool Matches(string pattern, string file)
    {
        var normalized = Normalize(pattern);
        var escaped = Regex.Escape(normalized)
            .Replace(@"\*\*", ".*")
            .Replace(@"\*", "[^/]*")
            .Replace(@"\?", "[^/]");
        return Regex.IsMatch(file, "^" + escaped + "$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }
}