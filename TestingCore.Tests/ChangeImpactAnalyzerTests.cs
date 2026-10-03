using TestingCore.Application;

namespace TestingCore.Tests;

public sealed class ChangeImpactAnalyzerTests
{
    private static readonly ImpactManifest Manifest = new(
    [
        new ImpactComponent("Accounts", ["src/Accounts/**"], [], "Component=Accounts"),
        new ImpactComponent("Payments", ["src/Payments/**"], ["Accounts"], "Component=Payments"),
        new ImpactComponent("Checkout", ["src/Checkout/**"], ["Payments"], "Component=Checkout")
    ]);

    [Fact]
    public void Change_reaches_transitive_dependents_and_test_filters()
    {
        var report = new ChangeImpactAnalyzer(Manifest).Analyze(["src\\Accounts\\Ledger.cs"]);

        Assert.Equal("targeted", report.Selection);
        Assert.Empty(report.UnmappedFiles);
        Assert.Equal(["Accounts", "Checkout", "Payments"],
            report.AffectedComponents.Select(item => item.Name));
        Assert.Equal("(Component=Accounts)|(Component=Checkout)|(Component=Payments)",
            report.RecommendedFilter);
        Assert.Equal("depends on: Accounts",
            report.AffectedComponents.Single(item => item.Name == "Payments").Reason);
    }

    [Fact]
    public void Unknown_change_requires_full_suite()
    {
        var report = new ChangeImpactAnalyzer(Manifest).Analyze(
            ["src/Accounts/Ledger.cs", "Directory.Build.props"]);

        Assert.Equal("full", report.Selection);
        Assert.Null(report.RecommendedFilter);
        Assert.Equal(["Directory.Build.props"], report.UnmappedFiles);
    }

    [Fact]
    public void Invalid_dependency_is_rejected()
    {
        var manifest = new ImpactManifest(
            [new ImpactComponent("Payments", ["src/Payments/**"], ["Missing"], "Component=Payments")]);

        Assert.Throws<ArgumentException>(() => new ChangeImpactAnalyzer(manifest));
    }

    [Fact]
    public void Dot_prefixed_file_remains_identifiable_when_unmapped()
    {
        var report = new ChangeImpactAnalyzer(Manifest).Analyze([".github/workflows/test.yml"]);

        Assert.Equal([".github/workflows/test.yml"], report.UnmappedFiles);
    }
}