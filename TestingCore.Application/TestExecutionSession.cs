using System.Runtime.ExceptionServices;
using TestingCore.Domain;
using TestingCore.Ports;

namespace TestingCore.Application;

public sealed class TestExecutionSession(
    TestRunSummaryService summary, TestRunPublisher? publisher = null)
{
    public async Task<TestRunReference?> StartAsync(
        string executionId, TestRunMetadata? metadata = null,
        TestRunDefinition? azureRun = null, CancellationToken cancellationToken = default)
    {
        if (azureRun is not null && publisher is null)
            throw new InvalidOperationException("An Azure publisher is required to create a run");
        if (azureRun is not null && azureRun.ExecutionId != executionId)
            throw new ArgumentException("Azure and local execution IDs must match", nameof(azureRun));

        await summary.InitializeAsync(executionId, metadata, cancellationToken);
        if (azureRun is null) return null;
        var run = await publisher!.CreateAsync(azureRun, cancellationToken);
        await summary.AttachAzureRunAsync(run, cancellationToken);
        return run;
    }

    public async Task<TestRunSummary> CompleteAsync(CancellationToken cancellationToken = default)
    {
        var current = await summary.GetAsync(cancellationToken);
        if (current.FinishedAt is not null &&
            (current.AzureRunId is null || current.AzureRunClosedAt is not null)) return current;
        if (current.AzureRunId is not null && publisher is null)
            throw new InvalidOperationException("An Azure publisher is required to complete the attached run");

        Exception? publicationError = null;
        if (current.AzureRunId is not null && current.AzureRunClosedAt is null)
        {
            try
            {
                await publisher!.CompleteAsync(current.AzureRunId, cancellationToken);
                await summary.MarkAzureRunClosedAsync(cancellationToken);
            }
            catch (Exception error)
            {
                publicationError = error;
            }
        }

        TestRunSummary finalized;
        try
        {
            finalized = current.FinishedAt is null
                ? await summary.FinalizeAsync(cancellationToken)
                : await summary.GetAsync(cancellationToken);
        }
        catch (Exception error)
        {
            if (publicationError is not null) throw new AggregateException(publicationError, error);
            throw;
        }

        if (publicationError is not null) ExceptionDispatchInfo.Capture(publicationError).Throw();
        return finalized;
    }
}