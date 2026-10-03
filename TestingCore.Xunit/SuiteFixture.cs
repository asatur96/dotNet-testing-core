using System.Runtime.ExceptionServices;
using TestingCore.Domain;
using Xunit;

namespace TestingCore.Xunit;

public sealed record SuiteHooks(
    Func<Task>? BeforeAll = null,
    Func<Task>? AfterAll = null,
    Func<Task>? BeforeEach = null,
    Func<Task>? AfterEach = null);

public abstract class SuiteFixture(string name, SuiteHooks? hooks = null) : IAsyncLifetime
{
    private readonly SuiteHooks _hooks = hooks ?? new();
    public SuiteContext Suite { get; } = new(name);

    public virtual async Task InitializeAsync()
    {
        if (_hooks.BeforeAll is not null) await _hooks.BeforeAll();
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
            context.Finish(failure is null ? TestStatus.Passed : TestStatus.Failed, failure);
            Suite.AddTest(context);
        }

        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }

    public virtual async Task DisposeAsync()
    {
        try
        {
            if (_hooks.AfterAll is not null) await _hooks.AfterAll();
        }
        finally
        {
            Suite.FinalizeSuite();
        }
    }
}