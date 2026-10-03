using System.Runtime.ExceptionServices;
using TestingCore.Domain;
using Xunit;

namespace TestingCore.Xunit;

public sealed record SuiteHooks(
    Func<Task>? BeforeAll = null,
    Func<Task>? AfterAll = null,
    Func<Task>? BeforeEach = null,
    Func<Task>? AfterEach = null);

public abstract class SuiteFixture(
    string name, SuiteHooks? hooks = null,
    Func<TestContext, Task>? onTestFinishing = null,
    Func<SuiteContext, Task>? onSuiteFinished = null,
    Func<TestContext, Task>? onTestFinished = null) : IAsyncLifetime
{
    private readonly SuiteHooks _hooks = hooks ?? new();
    private readonly Func<TestContext, Task>? _onTestFinishing = onTestFinishing;
    private readonly Func<SuiteContext, Task>? _onSuiteFinished = onSuiteFinished;
    private readonly Func<TestContext, Task>? _onTestFinished = onTestFinished;
    public SuiteContext Suite { get; } = new(name);

    public virtual async Task InitializeAsync()
    {
        try
        {
            if (_hooks.BeforeAll is not null) await _hooks.BeforeAll();
        }
        catch (Exception error)
        {
            Suite.RecordFailure(error);
            throw;
        }
    }

    public async Task RunAsync(string title, Func<TestContext, Task> body)
    {
        var context = new TestContext();
        context.Metadata.Title = title;
        context.Metadata.SuitePath.Add(Suite.Name);
        context.Metadata.Tags.AddRange(System.Text.RegularExpressions.Regex.Matches(title, @"@(\w+)")
            .Select(match => match.Groups[1].Value));
        Exception? failure = null;
        try
        {
            if (_hooks.BeforeEach is not null) await _hooks.BeforeEach();
            await body(context);
        }
        catch (Exception error)
        {
            failure = error;
        }
        finally
        {
            try
            {
                if (_hooks.AfterEach is not null) await _hooks.AfterEach();
            }
            catch (Exception error)
            {
                failure = failure is null ? error : new AggregateException(failure, error);
            }
            try
            {
                if (_onTestFinishing is not null) await _onTestFinishing(context);
            }
            catch (Exception error)
            {
                failure = failure is null ? error : new AggregateException(failure, error);
            }
            context.Finish(failure is null ? TestStatus.Passed : TestStatus.Failed, failure);
            Suite.AddTest(context);
            try
            {
                if (_onTestFinished is not null) await _onTestFinished(context);
            }
            catch (Exception error)
            {
                Suite.RecordFailure(error);
                failure = failure is null ? error : new AggregateException(failure, error);
            }
        }

        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }

    public virtual async Task DisposeAsync()
    {
        Exception? failure = null;
        try
        {
            if (_hooks.AfterAll is not null) await _hooks.AfterAll();
        }
        catch (Exception error)
        {
            Suite.RecordFailure(error);
            failure = error;
        }

        Suite.FinalizeSuite();
        try
        {
            if (_onSuiteFinished is not null) await _onSuiteFinished(Suite);
        }
        catch (Exception error)
        {
            failure = failure is null ? error : new AggregateException(failure, error);
        }

        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }
}