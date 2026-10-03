using System.Runtime.ExceptionServices;
using TestingCore.Application;
using TestingCore.Domain;
using TestingCore.Ports;
using Xunit;

namespace TestingCore.Xunit;

public abstract class ExecutionFixture : IAsyncLifetime
{
    private readonly TestExecutionSession _session;
    private readonly string _executionId;
    private readonly TestRunMetadata? _metadata;
    private readonly TestRunDefinition? _azureRun;
    private readonly List<SuiteFixture> _suites = [];
    private int _initializedSuites;
    private bool _started;
    private bool _suitesDisposed;
    private bool _completed;

    protected ExecutionFixture(
        TestRunSummaryService summary, string executionId,
        TestRunMetadata? metadata = null, TestRunDefinition? azureRun = null,
        TestCaseSync? caseSync = null, TestRunPublisher? publisher = null)
    {
        ArgumentNullException.ThrowIfNull(summary);
        if (string.IsNullOrWhiteSpace(executionId))
            throw new ArgumentException("Execution ID is required", nameof(executionId));
        Summary = summary;
        Workflow = new TestResultWorkflow(caseSync, publisher, summary);
        _session = new TestExecutionSession(summary, publisher);
        _executionId = executionId;
        _metadata = metadata;
        _azureRun = azureRun;
    }

    public TestRunSummaryService Summary { get; }
    public TestResultWorkflow Workflow { get; }

    protected T RegisterSuite<T>(T suite) where T : SuiteFixture
    {
        ArgumentNullException.ThrowIfNull(suite);
        if (_started || _suitesDisposed)
            throw new InvalidOperationException("Suites must be registered before execution starts");
        _suites.Add(suite);
        return suite;
    }

    public async Task InitializeAsync()
    {
        if (_started || _suitesDisposed)
            throw new InvalidOperationException("Execution fixture already started or disposed");
        await _session.StartAsync(_executionId, _metadata, _azureRun);
        _started = true;
        try
        {
            foreach (var suite in _suites)
            {
                _initializedSuites++;
                await suite.InitializeAsync();
            }
        }
        catch (Exception setupError)
        {
            try { await DisposeAsync(); }
            catch (Exception cleanupError) { throw new AggregateException(setupError, cleanupError); }
            ExceptionDispatchInfo.Capture(setupError).Throw();
        }
    }

    public async Task DisposeAsync()
    {
        if (_completed) return;
        var failures = new List<Exception>();
        if (!_suitesDisposed)
        {
            _suitesDisposed = true;
            for (var index = _initializedSuites - 1; index >= 0; index--)
            {
                try { await _suites[index].DisposeAsync(); }
                catch (Exception error) { failures.Add(error); }
            }
        }
        if (_started)
        {
            try { await _session.CompleteAsync(); }
            catch (Exception error) { failures.Add(error); }
        }
        if (failures.Count == 0) _completed = true;
        if (failures.Count == 1) ExceptionDispatchInfo.Capture(failures[0]).Throw();
        if (failures.Count > 1) throw new AggregateException(failures);
    }
}