using Microsoft.Extensions.DependencyInjection;
using TestingCore.Domain;

namespace TestingCore.Xunit;

public abstract class ScopedSuiteFixture(
    string name, IServiceScopeFactory scopeFactory,
    SuiteHooks? hooks = null,
    Func<TestContext, Task>? onTestFinishing = null,
    Func<SuiteContext, Task>? onSuiteFinished = null)
    : SuiteFixture(name, hooks, onTestFinishing, onSuiteFinished)
{
    public async Task RunScopedAsync(
        string title, Func<TestContext, IServiceProvider, Task> body)
    {
        ArgumentNullException.ThrowIfNull(body);
        await using var scope = scopeFactory.CreateAsyncScope();
        await RunAsync(title, context => body(context, scope.ServiceProvider));
    }
}