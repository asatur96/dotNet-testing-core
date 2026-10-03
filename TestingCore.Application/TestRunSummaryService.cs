using TestingCore.Domain;
using TestingCore.Ports;

namespace TestingCore.Application;

public sealed class TestRunSummaryService(ITestRunSummaryStore store)
{
    public Task<TestRunSummary> InitializeAsync(
        string runId, TestRunMetadata? metadata = null, CancellationToken cancellationToken = default) =>
        store.UpdateAsync(existing =>
        {
            if (existing is not null) throw new InvalidOperationException("Run summary already initialized");
            return TestRunSummary.Start(runId, metadata);
        }, cancellationToken);

    public Task<TestRunSummary> AttachAzureRunAsync(
        TestRunReference run, CancellationToken cancellationToken = default) =>
        store.UpdateAsync(existing =>
        {
            var summary = existing ?? throw new InvalidOperationException("Run summary not initialized");
            summary.AttachAzureRun(run.Id, run.Url);
            return summary;
        }, cancellationToken);
    public Task<TestRunSummary> MarkAzureRunClosedAsync(CancellationToken cancellationToken = default) =>
        store.UpdateAsync(existing =>
        {
            var summary = existing ?? throw new InvalidOperationException("Run summary not initialized");
            summary.MarkAzureRunClosed(DateTimeOffset.UtcNow);
            return summary;
        }, cancellationToken);
    public Task<TestRunSummary> RecordAsync(TestContext context, CancellationToken cancellationToken = default) =>
        store.UpdateAsync(existing =>
        {
            var summary = existing ?? throw new InvalidOperationException("Run summary not initialized");
            summary.Record(context);
            return summary;
        }, cancellationToken);

    public async Task RecordSuiteAsync(SuiteContext suite, CancellationToken cancellationToken = default)
    {
        await store.UpdateAsync(existing =>
        {
            var summary = existing ?? throw new InvalidOperationException("Run summary not initialized");
            summary.RecordSuite(suite);
            return summary;
        }, cancellationToken);
    }

    public Task<TestRunSummary> FinalizeAsync(CancellationToken cancellationToken = default) =>
        store.UpdateAsync(existing =>
        {
            var summary = existing ?? throw new InvalidOperationException("Run summary not initialized");
            summary.FinalizeRun();
            return summary;
        }, cancellationToken);

    public async Task<TestRunSummary> GetAsync(CancellationToken cancellationToken = default) =>
        await store.ReadAsync(cancellationToken)
            ?? throw new InvalidOperationException("Run summary not initialized");
}