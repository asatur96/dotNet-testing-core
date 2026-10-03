using TestingCore.Domain;

namespace TestingCore.Ports;

public interface ITestRunSummaryStore
{
    Task<TestRunSummary?> ReadAsync(CancellationToken cancellationToken = default);
    Task<TestRunSummary> UpdateAsync(
        Func<TestRunSummary?, TestRunSummary> update,
        CancellationToken cancellationToken = default);
}