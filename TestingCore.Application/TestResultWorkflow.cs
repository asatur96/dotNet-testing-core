using System.Runtime.ExceptionServices;
using TestingCore.Domain;

namespace TestingCore.Application;

public sealed class TestResultWorkflow(
    TestCaseSync? caseSync = null,
    TestRunPublisher? runPublisher = null,
    TestRunSummaryService? summary = null,
    string? azureRunId = null)
{
    public async Task OnTestFinishingAsync(TestContext context)
    {
        if (caseSync is null) return;
        var title = context.Metadata.Title ?? throw new InvalidOperationException("Test title is required for case sync");
        await caseSync.SyncAsync(title, context);
    }

    public Task OnSuiteFinishedAsync(SuiteContext suite) =>
        summary?.RecordSuiteAsync(suite) ?? Task.CompletedTask;
    public async Task OnTestFinishedAsync(TestContext context)
    {
        if (context.FinishedAt is null)
            throw new InvalidOperationException("Test must be finished before result publication");
        Exception? publicationError = null;
        var currentRunId = azureRunId;
        if (runPublisher is not null && string.IsNullOrWhiteSpace(currentRunId) && summary is not null)
            currentRunId = (await summary.GetAsync()).AzureRunId;
        if (runPublisher is not null && !string.IsNullOrWhiteSpace(currentRunId))
        {
            try
            {
                await runPublisher.PublishResultAsync(currentRunId, context);
            }
            catch (Exception error)
            {
                publicationError = error;
            }
        }

        try
        {
            if (summary is not null) await summary.RecordAsync(context);
        }
        catch (Exception error)
        {
            if (publicationError is not null) throw new AggregateException(publicationError, error);
            throw;
        }

        if (publicationError is not null) ExceptionDispatchInfo.Capture(publicationError).Throw();
    }
}