using Microsoft.Extensions.DependencyInjection;
using TestingCore.Domain;
using TestingCore.Xunit;

namespace TestingCore.Tests;

public sealed class ScopedSuiteFixtureTests
{
    [Fact]
    public async Task Each_run_gets_a_new_scope_that_survives_hooks_and_disposes_on_failure()
    {
        var probes = new List<ScopedProbe>();
        var fixture = new ProbeFixture(new SuiteHooks(AfterEach: () =>
        {
            Assert.False(probes[^1].Disposed);
            return Task.CompletedTask;
        }));
        await fixture.InitializeAsync();

        await fixture.RunScopedAsync("passes", (_, services) =>
        {
            probes.Add(services.GetRequiredService<ScopedProbe>());
            return Task.CompletedTask;
        });
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            fixture.RunScopedAsync("fails", (_, services) =>
            {
                probes.Add(services.GetRequiredService<ScopedProbe>());
                throw new InvalidOperationException("source failed");
            }));
        await fixture.DisposeAsync();

        Assert.Equal(2, probes.Count);
        Assert.NotSame(probes[0], probes[1]);
        Assert.All(probes, probe => Assert.True(probe.Disposed));
        Assert.Equal(TestStatus.Passed, fixture.Suite.Tests[0].Status);
        Assert.Equal(TestStatus.Failed, fixture.Suite.Tests[1].Status);
    }

    [Fact]
    public async Task Post_finish_callback_runs_before_the_backend_scope_is_disposed()
    {
        ScopedProbe? probe = null;
        var fixture = new ProbeFixture(new SuiteHooks(), context =>
        {
            Assert.Equal(TestStatus.Passed, context.Status);
            Assert.NotNull(probe);
            Assert.False(probe.Disposed);
            return Task.CompletedTask;
        });
        await fixture.InitializeAsync();

        await fixture.RunScopedAsync("scoped result", (_, services) =>
        {
            probe = services.GetRequiredService<ScopedProbe>();
            return Task.CompletedTask;
        });
        await fixture.DisposeAsync();

        Assert.True(probe!.Disposed);
    }
    private sealed class ProbeFixture : ScopedSuiteFixture
    {
        private readonly ServiceProvider _root;

        public ProbeFixture(SuiteHooks hooks, Func<TestContext, Task>? onTestFinished = null) : this(
            new ServiceCollection().AddScoped<ScopedProbe>().BuildServiceProvider(), hooks, onTestFinished) { }

        private ProbeFixture(ServiceProvider root, SuiteHooks hooks, Func<TestContext, Task>? onTestFinished)
            : base("source tests", root.GetRequiredService<IServiceScopeFactory>(), hooks,
                onTestFinished: onTestFinished)
        {
            _root = root;
        }

        public override async Task DisposeAsync()
        {
            try { await base.DisposeAsync(); }
            finally { await _root.DisposeAsync(); }
        }
    }

    private sealed class ScopedProbe : IAsyncDisposable
    {
        public bool Disposed { get; private set; }
        public ValueTask DisposeAsync()
        {
            Disposed = true;
            return ValueTask.CompletedTask;
        }
    }
}