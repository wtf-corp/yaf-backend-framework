---
name: yaf-testing
description: How to test YAF and services built on it - choosing the test level, test-first workflow, hand-written fakes over mocks, shared PostgreSQL container fixtures with per-test tenants, approval files, conformance suites, the security test pack, [Control] tags, testing background work (messages, jobs, cron, time) and naming. Load BEFORE writing or changing any test, test fixture or approved file; before claiming a change is done; when a test is flaky or an approval test fails; and before adding a mocking library or an in-memory database.
license: MIT
metadata:
  owner: yaf
  version: "1"
---

# YAF testing

Test first. Real dependencies in containers. Fakes you can read. Evidence before "done".

## When to load

- Starting any change (the first step is a failing test).
- Choosing where a test belongs or how to isolate it.
- Testing something asynchronous: outbox, consumers, jobs, cron, timeouts.
- An `*.approved.*` file differs.
- About to add a mock framework call, `Thread.Sleep`, or database clean-up code.

## Which level?

First match wins.

| What you are proving | Level | Project | Needs |
|---|---|---|---|
| An aggregate, value object, rule, state machine, memento round trip | Domain | `tests/<Svc>/Domain.Tests` | nothing |
| A handler, validator, middleware, process manager step | Application | `tests/<Svc>/Application.Tests` | fakes |
| A framework package against real PostgreSQL or a broker | Component | `tests/<Package>.Tests` | Docker |
| An endpoint, authorization, tenancy, persistence, the whole pipeline | API integration | `tests/<Svc>/Api.IntegrationTests` | Docker |
| Services talking through a real broker | End-to-end | `samples/conference/tests/EndToEnd.Tests` | Docker, nightly |
| A rule about structure (layers, visibility, markers) | Architecture | `tests/Architecture.Tests` / the service's | nothing |
| An analyzer or generator | Analyzer | `tests/Analyzers.Tests` | in-memory compilation |
| A transport or HTTP style adapter | Conformance | derive from the abstract suite in `YAF.Testing.Integration` | Docker for transports |

Two test packages: `YAF.Testing` holds the pure helpers (memento and result assertions, fakes, test clock, approval helper, architecture rules) and is all a Domain or Application test project references. `YAF.Testing.Integration` holds the web factory, the container fixtures, test auth with its signing key, the security test pack and the conformance suites; only component, API integration and end-to-end projects reference it. Assertions are AwesomeAssertions; architecture rules use ArchUnitNET; NSubstitute is for third-party interfaces only.

## Rules

1. **REQUIRED** Write the test first, run it, see it fail for the expected reason, then write the code. If it passes immediately, break the code once to prove the test can fail.
2. **REQUIRED** Tests live with what they test. Framework behaviour is never tested only through the sample.
3. **REQUIRED** No in-memory database provider and no SQLite: persistence tests run on PostgreSQL in a container.
4. **REQUIRED** Ports the project owns are faked by hand (`InMemoryRepository<,>`, `RecordingJobScheduler`, `FakeObjectStore`, …). A mocking library is only for third-party interfaces.
5. **REQUIRED** Assert on error **codes** (`ResultAssert.ShouldFailWith(descriptor)`), never on messages.
6. **REQUIRED** One container and one host per xUnit collection. Tests isolate with their own tenant (`factory.NewTenant()`), not by cleaning tables.
7. **REQUIRED** No `Thread.Sleep` / `Task.Delay` waits. Use `factory.Messaging.WaitForIdleAsync()`, `factory.Jobs.RunDueAsync()`, `factory.Cron.TriggerAsync(name)`, `factory.Time.Advance(...)` or `Eventually.Async(assert, timeout)`.
8. **REQUIRED** Time comes from the fake `TimeProvider`; ids from `TestIds`; personal data from the obviously fake generators (`attendee-0001@example.test`). Never real data, never an app's real ids. Test callers are authenticated with tokens signed by the test key (`factory.Auth`); no test relies on an unsigned token or on switching validation off.
9. **REQUIRED** Approved files (`errors`, `fields`, `pipeline`, authorization inventory, contract schemas, OpenAPI, public API) change only on purpose: read the `received` diff, then replace. Never delete or regenerate blindly.
10. **REQUIRED** Every new use case that writes has a rollback test; every new endpoint has a permission test and is covered by the cross-tenant suite; every new personal-data path has a log-leak assertion; every aggregate with children has a test in which two concurrent commands each change only a child and one gets `Conflict`; every read model with a protected column has a test that it returns the clear value.
11. **REQUIRED** A test for a security or compliance control carries `[Control("…")]` (for example `[Control("C-AC-2")]`, `[Control("S1")]`).
12. **REQUIRED** Never skip, weaken or delete a failing test to get green. Fix the cause or stop and report.
13. **REQUIRED** "Done" means you ran the commands in *Verify* and can quote the output.
14. **RECOMMENDED** Names: `Method_Scenario_Expected` or `Should_Expected_When_Scenario`; one behaviour per test; Arrange / Act / Assert separated by blank lines; no logic (loops, ifs) in tests.
15. **RECOMMENDED** Test builders ("mothers") per aggregate in the test project; no shared mutable fixtures.
16. **RECOMMENDED** Put slow suites behind traits (`Docker`, `Broker:<name>`, `Templates`, `E2E`) so the default run needs no Docker. Service tests use the in-memory transport; the RabbitMQ conformance suite runs in a container; the SNS/SQS suite runs only when the LocalStack token is present and reports itself as skipped otherwise.

## How to

API integration test with the shared factory:

```csharp
[Collection(CatalogApiCollection.Name)]
public sealed class PublishSessionTests(CatalogApiFactory factory)
{
    [Fact]
    [Control("C-AC-1")]
    public async Task Publish_WithoutPermission_IsForbidden()
    {
        var tenant = factory.NewTenant();
        var viewer = factory.Auth.User(TestIds.User(1)).InTenant(tenant).With(Capabilities.Sessions, "view");
        var sessionId = await factory.Seed.DraftSessionAsync(tenant);

        var response = await factory.CreateClient().As(viewer, tenant)
            .PostAsync($"/api/v1/sessions/{sessionId}/publish", null);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Publish_PublishesIntegrationEvent_AndConsumerSeesTenant()
    {
        var tenant = factory.NewTenant();
        var manager = factory.Auth.User(TestIds.User(2)).InTenant(tenant).With(Capabilities.Sessions.All);
        var sessionId = await factory.Seed.DraftSessionAsync(tenant);

        await factory.CreateClient().As(manager, tenant).PostAsync($"/api/v1/sessions/{sessionId}/publish", null);
        await factory.Messaging.WaitForIdleAsync();

        var published = factory.Messaging.Published<SessionPublishedV1>().Should().ContainSingle().Subject;
        published.Context.TenantId.Should().Be(tenant);
        published.Context.Initiator!.Id.Should().Be(TestIds.User(2));       // the user is carried for audit only
    }
}
```

Handler test with fakes:

```csharp
[Fact]
public async Task Handle_UnknownSession_ReturnsNotFound()
{
    var handler = new PublishSessionHandler(new InMemoryRepository<Session, SessionId>().AsSessionRepository());

    var result = await handler.HandleAsync(new PublishSessionCommand(SessionId.New()), CancellationToken.None);

    result.ShouldFailWith(Session.Errors.NotFound);
}
```

Approval flow: run → test fails and writes `x.received.txt` → read the `+`/`-` lines → if intended, replace `x.approved.txt` and commit it with the change.

## Background work cheat sheet

| To test | Do |
|---|---|
| Outbox → consumer | Act, `WaitForIdleAsync()`, assert the effect |
| Duplicate delivery | `factory.Messaging.Redeliver(message)`; assert state changed once |
| A job | `factory.Jobs.RunDueAsync()`; or assert the scheduled row before running |
| A delayed job / timeout | `factory.Time.Advance(TimeSpan)` then `RunDueAsync()` |
| A cron job | `factory.Cron.TriggerAsync("catalog.close-finished-sessions")` |
| Dead-lettering | Make the handler fail; assert `factory.Messaging.DeadLetters` |
| Logs / spans / metrics | `factory.Logs`, `factory.Spans`, `factory.Metrics` |

## Anti-patterns

| Wrong | Why | Instead |
|---|---|---|
| Writing the code, then a test that mirrors it | Proves nothing | Failing test first |
| `Substitute.For<ISessionRepository>()` with `Received(1)` | Tests the mock script, not behaviour | In-memory fake; assert state |
| `await Task.Delay(3000)` | Slow and flaky | `WaitForIdleAsync` / `Eventually` |
| `TRUNCATE` between tests | Breaks parallelism; hides isolation bugs | New tenant per test |
| `UseInMemoryDatabase` | No RLS, no transactions, different SQL | PostgreSQL container |
| `result.FirstError.Message.Should().Contain("not found")` | Messages are not the contract | Compare codes |
| `[Fact(Skip = "flaky")]` | Hides a real defect | Find the race; fix or report |
| Copying `received` over `approved` without reading | Approves accidental API/authorization changes | Review the diff line by line |
| `DateTimeOffset.UtcNow` in a test | Non-deterministic | Fake `TimeProvider` |
| One giant test covering a whole flow with ten asserts | Fails for unclear reasons | One behaviour per test |

## Verify

```bash
dotnet build YAF.slnx
dotnet test --solution YAF.slnx                         # default category: no Docker needed
dotnet test --project tests/<Package>.Tests             # one project
dotnet test --project samples/conference/tests/Catalog/Api.IntegrationTests   # Docker
```

- No `*.received.*` files left; approved-file diffs are intentional and committed.
- New controls appear in `docs/compliance/controls.md` with a passing test.
- State in your summary which commands you ran and their result; if something could not be run (no Docker), say so.

## References

- `docs/articles/testing.md` — `YAF.Testing` helpers; `YAF.Testing.Integration` fixtures, test auth, security pack, conformance suites.
- `adr/0018-testing-strategy.md` — levels, libraries, gates.
- `samples/conference/tests/` — one example per level; `CatalogApiFactory`, `CatalogApiCollection`.
- `tests/Conformance.*` — how an adapter derives from a conformance suite.
- Skills: every other `yaf-*` skill's *Verify* section names the tests that concern it.
