using TestingCore.Domain;
using TestingCore.Ports;

namespace TestingCore.Application;

public sealed class TestRunPublisher(ITestExecutionPort execution)
{
    public Task<TestRunReference> CreateAsync(
        TestRunDefinition run, CancellationToken cancellationToken = default) =>
        execution.CreateRunAsync(run, cancellationToken);

    public async Task<TestResultReference?> PublishResultAsync(
        string runId, TestContext context, int? testPointId = null,
        CancellationToken cancellationToken = default)
    {
        if (context.FinishedAt is null || context.Status == TestStatus.Unknown)
            throw new InvalidOperationException("Only completed tests can be published");
        var metadata = context.Metadata.TestManagement;
        if (metadata.Skip || string.IsNullOrWhiteSpace(metadata.CaseId)) return null;
        if (!int.TryParse(metadata.CaseId, out var caseId) || caseId <= 0)
            throw new InvalidOperationException("Azure Test Case ID must be positive and numeric");
        if (metadata.RunId is not null && metadata.RunId != runId)
            throw new InvalidOperationException("Test result was already published to another run");
        if (metadata.ResultId is not null)
            return new TestResultReference(metadata.ResultId, metadata.ResultUrl ?? "");

        var title = context.Metadata.Title ?? "Unnamed test";
        var automatedName = string.Join(".", context.Metadata.SuitePath.Append(title));
        var comment = string.Join("\n", context.Steps.Select(step => $"{step.Action}: {step.Status}"));
        if (comment.Length > 1000) comment = comment[..1000];
        var definition = new TestResultDefinition(caseId.ToString(), title, automatedName,
            context.Status, comment, context.StartedAt, context.FinishedAt.Value,
            metadata.BugIds.ToArray(), testPointId ?? metadata.PointId);
        var result = await execution.AddResultAsync(runId, definition, cancellationToken);
        metadata.RunId = runId;
        metadata.ResultId = result.Id;
        metadata.ResultUrl = result.Url;
        return result;
    }

    public Task CompleteAsync(string runId, CancellationToken cancellationToken = default) =>
        execution.CompleteRunAsync(runId, cancellationToken);

    public async Task<TestRunReference> PublishSuiteAsync(
        SuiteContext suite, TestRunDefinition definition,
        CancellationToken cancellationToken = default)
    {
        if (suite.Metadata.FinishedAt is null)
            throw new InvalidOperationException("Only a finalized suite can be published");
        var run = await CreateAsync(definition, cancellationToken);
        foreach (var test in suite.Tests)
            await PublishResultAsync(run.Id, test, cancellationToken: cancellationToken);
        await CompleteAsync(run.Id, cancellationToken);
        return run;
    }
}