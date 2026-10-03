# Direct backend service tests

In the TypeScript consumer, Playwright fixtures create ServiceManager, TestDataManager, and user services for each test while longer-lived infrastructure is shared. For a backend monolith, ScopedSuiteFixture applies the same lifetime idea to the application's .NET dependency injection container.

A suite fixture owns a root ServiceProvider built from the backend's actual service registration. Pass its IServiceScopeFactory to ScopedSuiteFixture. Each RunScopedAsync call creates a new async DI scope around the entire test lifecycle, including BeforeEach, AfterEach, and the case-sync callback. Resolve a service from the supplied IServiceProvider inside the test body:

    await fixture.RunScopedAsync("rejects an invalid command", async (context, services) =>
    {
        var handler = services.GetRequiredService<MyCommandHandler>();
        await new TestStep(context).Run("handle command", "validation fails", async () =>
        {
            await Assert.ThrowsAsync<ValidationException>(
                () => handler.Handle(invalidCommand));
        });
    });

For a successful service result, GenericAssertions records each validation as a step artifact and throws on failure:

    await new TestStep(context).Run("handle command", "result has the expected state", async () =>
    {
        var result = await handler.Handle(validCommand);
        new GenericAssertions(context).Value(result)
            .ShouldContain(new { Status = "active" })
            .ShouldSatisfy(value => value.Id != Guid.Empty, "assigned ID");
    });

ShouldHaveValue defaults to C# equality; use ComparisonMode.Deep for structural JSON equality. ShouldContain checks a substring, array member, or partial object. Assertions require an active step and store a ValidationArtifact before throwing. Display values mask long digit sequences and properties named like password or token; pass sensitive: true when a value has another sensitive shape. The comparison still uses the real values. TestStep labels and predicate expectation text should not contain secrets. The raw HTTP return value can contain bodies. The HTTP adapter omits bodies and query values from recorded step evidence by default; see docs/http-evidence.md.

Replace MyCommandHandler, ValidationException, and invalidCommand with types from the backend. Build the root provider from the monolith's production registration method, overriding only the external ports the test must control. A scoped database context, repository, or application service is resolved once per test; the scope is disposed even when the body fails. The fixture owner disposes the root provider in DisposeAsync.

Use direct calls for pure domain rules, application services, and repository behavior. Use the HTTP adapter only when routing, serialization, authentication middleware, or the public API contract is the behavior under test. Keeping those as separate test layers makes a failure easier to locate.

The fixture is in TestingCore.Xunit and references Microsoft.Extensions.DependencyInjection.Abstractions. TestingCore.Domain, Ports, and Application do not depend on xUnit or .NET DI. The standalone repository includes a lifecycle test for scoped instances and async disposal; it cannot wire real backend types until the monolith checkout is provided.