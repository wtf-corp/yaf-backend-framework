# 15 — Testing (`YAF.Testing`, `YAF.Testing.Integration` and the repository's test suites)

## 1. Approach

- **Test-first.** Write the test, run it, see it fail for the expected reason, then write the code. A test that passes immediately proves little: break the code once to see it fail.
- Tests live next to what they test: every `src/<Name>` has `tests/<Name>.Tests`; framework behaviour is never tested only through the sample.
- Real dependencies over simulations: PostgreSQL and brokers run in containers. No in-memory database provider.
- Hand-written fakes over mocking libraries for ports the project owns; a mocking library only for third-party interfaces.

Stack (decided in ADR-0018): xUnit v3 on Microsoft.Testing.Platform (`global.json` selects the runner), AwesomeAssertions for assertions, NSubstitute for third-party interfaces only, ArchUnitNET for architecture rules, Testcontainers, `Microsoft.Extensions.Diagnostics.Testing` (fake logger, metric collector), `Microsoft.Extensions.TimeProvider.Testing`.

Naming: `Method_Scenario_Expected` or `Should_Expected_When_Scenario`; one behaviour per test; Arrange / Act / Assert separated by blank lines.

## 2. Levels

| Level | Scope | Needs | Speed target |
|---|---|---|---|
| Domain | Aggregates, value objects, rules, state machines, memento round trips | nothing | whole project < 5 s |
| Application | Handlers, validators, middleware, process managers with fakes | nothing | < 10 s |
| Component | One package against real PostgreSQL or a real broker | Docker | minutes |
| API integration | A service over HTTP, in-process (`YafWebApplicationFactory`) on PostgreSQL + in-memory transport | Docker | minutes |
| End-to-end | The sample through its Aspire AppHost, real broker | Docker | nightly + before release |

## 3. The two test packages

Test support is split so that a domain or application test project does not pull in ASP.NET Core, EF Core and container tooling.

| Package | Depends on | For |
|---|---|---|
| `YAF.Testing` | `YAF.Application` (and so Domain and Core), `Microsoft.Extensions.Diagnostics.Testing`, `Microsoft.Extensions.TimeProvider.Testing`, ArchUnitNET | Domain and application tests, architecture tests. No ASP.NET Core, no EF Core, no Docker |
| `YAF.Testing.Integration` | `YAF.Testing`, `YAF.Http`, `YAF.Persistence.EfCore.PostgreSql`, xUnit v3, Testcontainers, the ASP.NET Core test host | Component, API integration and security tests; the conformance suites |

The helpers of `YAF.Testing` throw their own `YafAssertionException`, so they do not depend on a test framework or an assertion library; the abstract suites and fixtures of `YAF.Testing.Integration` are xUnit v3 types, which ties a project that uses them to xUnit v3.

> **Amended 2026-11-25**, by the owner's decisions of 2026-11-25 (`specs/0009-domain/plan.md`, decision log Q2): `YAF.Testing` depends on `YAF.Domain` (and so Core) until M3, and on `YAF.Application` from M3 on, when the first ports it fakes exist.

### 3.1 `YAF.Testing` (pure helpers)

| Area | Types |
|---|---|
| Fakes | `FakeExecutionContext` (builders for a user actor, and for a system actor with an initiator), `FakeUnitOfWork` (runs the delegate, records commit/rollback and the save/dispatch rounds), `RecordingIntegrationEventPublisher`, `RecordingJobScheduler`, `InMemoryRepository<TAggregate,TId>`, `FakeObjectStore`, `FakeKeyProvider`, `FakeFieldProtector`, `FakeMarkupSanitizer` |
| Time | `TestClock` over `FakeTimeProvider` |
| Domain helpers | `MementoAssert.RoundTrips<T>()` (snapshot → generated data class → restore → snapshot is equal, for every member of the memento), `MementoAssert.RoundTrips(entity, mementoFactory)` (the same over a memento the factory creates), `DomainEventAssert.Raised<T>(aggregate)`, `AggregateAssert.InvariantsHold(aggregate)`, `StateMachineAssert.IsWellFormed(machine, initial, finals)` (`02-domain.md` §4 and §10), `ResultAssert` (`ShouldSucceed`, `ShouldFailWith(descriptor)`) — assert on **codes**, never messages |
| Logs | `LogAssert.ContainsNo(value)` over `FakeLogCollector` |
| Approvals | `Approvals.Verify(actualText, name, [CallerFilePath])`: compares with `<name>.approved.txt` next to the test, writes `<name>.received.txt` and fails listing added/removed lines; no third-party dependency. Used for `errors.approved.txt` (which also proves codes are unique across all catalogues of a service), `fields.approved.txt` (the stable field ids of protected members), contracts and inventories |
| Architecture | `YafArchitectureRules.For(assemblies)` → ready-made rule set (§4) |
| Controls | `[Control("C-AC-1")]`, `[Control("S1")]` trait attribute, read by the compliance tool |
| Builders | `TestIds` (deterministic UUIDv7 sequences), synthetic personal data generators that are obviously fake (`attendee-0001@example.test`) |

A test project that calls `Approvals.Verify` sets `DeterministicSourcePaths` to `false`: in a CI build the compiler maps the repository root to `/_/` in `[CallerFilePath]`, and the helper fails with a message that says so when the directory of the calling file does not exist.

> **Amended 2026-11-25**, by the owner's decisions of 2026-11-25 (`specs/0009-domain/plan.md`, decision log Q1, Q2 and Q5; deviation 7, confirmed by the owner on 2026-11-25): `Approvals.Verify` takes the name of the approved file, which has no other source, and the note on `DeterministicSourcePaths` is added. The domain helpers list `AggregateAssert` and `StateMachineAssert`, which `02-domain.md` names, and the overload of `MementoAssert.RoundTrips` with a memento factory; `RoundTrips<T>()` over the generated data class comes with the `[MementoData]` generator in M3. Each fake comes with the milestone of its port; `YafArchitectureRules`, `TestIds` and the generators come with M3, `[Control]` with M4, `LogAssert` with M5 (`18-build-plan.md` §2).

### 3.2 `YAF.Testing.Integration`

| Area | Types |
|---|---|
| Host | `YafWebApplicationFactory<TEntryPoint, TDbContext>`: PostgreSQL container (shared per collection), migrations applied through the service's migrator code with the three database roles (migrator, runtime, maintenance) created as in production, in-memory transport, rate limits lifted, fixed test keys for every key ring, fake time provider, fake log collector and in-memory telemetry exporters exposed as `Logs`, `Spans`, `Metrics` |
| Identity | `TestAuth`: a **test issuer** with a signing key generated per test run, registered as the host's JWT provider, and `TestPrivilegeSource` configured in code (`factory.Auth.User(alice).InTenant(t1).With(Capabilities.Sessions.All)`); `client.As(alice, t1)` attaches a token signed by that issuer. The host validates it through the same JWT bearer path as production; there is no test authentication scheme that bypasses validation and no switch that accepts unsigned tokens |
| Data and messages | `factory.Seed(async scope => …)` (runs under a given tenant and actor), `factory.Messaging.Published<T>()`, `factory.Messaging.Redeliver(messageId)`, `factory.Messaging.DeadLetters` |
| Background work | `await factory.Messaging.WaitForIdleAsync()`, `factory.Jobs.RunDueAsync()`, `factory.Cron.TriggerAsync("name")`, `factory.Audit.SealAsync()` (runs the chain sealer once), `factory.Time.Advance(…)`; polling helper `Eventually.Async(assert, timeout)` |
| Conformance | `TransportConformance`, `HttpParity` abstract suites |
| Security pack | namespace `YAF.Testing.Integration.Security` (§5) |

`YAF.Testing.Integration` throws when the host environment is Production.

Shared fixtures: one container and one host per xUnit collection; tests isolate by **their own tenant** (`factory.NewTenant()`), not by cleaning the database. Tests of behaviour that is not tenant-scoped (cross-tenant elevation, cron fan-out, the relay and runners, the sealer, retention and partition drops) run in a collection of their own with its own database (`[Collection(YafCollections.Global)]`), so they cannot disturb, or be disturbed by, tenant-isolated tests.

## 4. Architecture rules (shipped, and applied to YAF itself, the sample and the templates)

1. Layer dependencies: Domain → Core only; Application → Domain; Infrastructure/Persistence → Application; Api → Application + composition; nothing → Api.
2. Domain assemblies reference no package outside `YAF.Core` / `YAF.Domain`.
3. Aggregates have no public constructors and no public setters; events are records ending in a past-tense verb and carry no aggregate type.
4. Handlers, validators, repositories and middleware are `internal sealed`.
5. Every request type has exactly one authorization marker; every command with input has a validator.
6. Every memento interface member is classified; every data-subject-owned type has retention, exporter and eraser; every `[StoredInClear]` member has a reason and appears in the data inventory.
7. Every `ITenantOwnedMemento` table has a row-level security policy (database test).
8. Vendor SDK namespaces appear only in their adapter assembly.
9. No reference to `YAF.Testing` or `YAF.Testing.Integration` from non-test assemblies; no reference to `YAF.Testing.Integration`, ASP.NET Core or EF Core from a domain test project.
10. No type in a non-test assembly named `*Fake*`, `*Stub*`, `*Dev*` unless registered behind an environment check (the dev stand-ins are annotated `[DevelopmentOnly]`, and a startup check enforces it).

## 5. Security test pack

Run against any `YafWebApplicationFactory`; the service template includes it.

| Suite | What it does |
|---|---|
| `AuthorizationInventory` | Lists every endpoint with access level, permissions and the request marker they were derived from; compares with the approved file; fails on an endpoint that neither dispatches a request nor declares its access explicitly, and on any endpoint whose edge metadata differs from its request's marker (possible only where the attribute was written by hand) |
| `CrossTenantRequests` | For every tenant-scoped endpoint discovered from the running app: no tenant, malformed tenant, foreign tenant, route/header tenant mismatch → refused before the handler |
| `TokenAttacks` | `alg=none` and unsigned tokens (refused in every environment, Development included), a token signed with a key that is not the issuer's, HS/RS confusion, wrong issuer, wrong audience, expired, not-yet-valid, missing subject, oversized token |
| `ApiKeyAttacks` | Malformed, unknown, expired, revoked-within-cache-window, throttling after repeated failures |
| `PrivilegeRevocation` | A `privileged` permission revoked in the privilege source is refused on the next request, with a warm cache; an ordinary one is refused after the cache window or at once after `IPrivilegeCacheInvalidator` is called |
| `AsyncIdentity` | A message or job carrying another user's id as initiator runs as the service's system principal and gains none of that user's permissions; a `[UserScoped]` request dispatched from a consumer or job is refused |
| `InputGuard` | Control characters and invalid Unicode in any command string are refused with `Input.InvalidCharacters`; text with `<`, `>`, `&` is stored and returned unchanged; a `[Markup]` member is sanitized by the registered sanitizer |
| `TransportSecurity` | Security headers, CORS preflight from an unlisted origin, HSTS outside Development |
| `ErrorLeaks` | Forces a 500 and a database error; asserts no stack trace, SQL, type name or connection detail in any response body |
| `LogLeaks` | Sends requests with marked synthetic personal data and secrets; asserts none of the markers appear in any log record, span attribute or metric tag |
| `Limits` | Oversized body, deep JSON, unknown member, huge page size |
| `StartupGuards` | Starts the host in Production with each unsafe option and with each required extension point missing; asserts it refuses |

## 6. Suites in the repository

| Project | Content |
|---|---|
| `tests/<Package>.Tests` | Unit and component tests of the package |
| `tests/Architecture.Tests` | Rules over all `src` assemblies; dependency allow-list; public API files present |
| `tests/Analyzers.Tests` | Each analyzer and generator: compile snippets in memory, assert diagnostics and generated source |
| `tests/Conformance.<Transport>.Tests` | Transport conformance per transport (`InMemory`, `RabbitMq`, `AwsSqs`) |
| `tests/Http.Parity.Tests` | Same expectations against the two HTTP styles |
| `tests/BreakIt.Tests` | Flips each safety default and asserts the guard trips (`09-security.md` §12) |
| `tests/Templates.Tests` | Instantiates templates, builds and tests the output (category `Templates`, slower) |
| `tests/Benchmarks` | BenchmarkDotNet, informational |
| `samples/conference/tests/**` | Domain, application, API integration and end-to-end tests of the sample (`Aspire.Hosting.Testing`) |

Categories (traits) select what runs where: default (no Docker), `Docker`, `Broker:<name>`, `Templates`, `E2E`.

## 7. Quality gates

- Pull request: build, all non-nightly tests, architecture rules, approved files in sync, line coverage reported per package with a gate of 85 % on `YAF.Core`, `YAF.Domain`, `YAF.Application` (generated code excluded).
- Weekly: mutation testing on `YAF.Core`, `YAF.Domain`, `YAF.Application`; results published, not blocking; a falling score opens an issue.
- Nightly and pre-release: end-to-end, every broker conformance suite, template matrix. Suites that need the LocalStack token (`Broker:AwsSqs`) run when the secret is present and are reported as skipped otherwise; a release requires that they ran.

## 8. Acceptance criteria

- `dotnet test --solution YAF.slnx` passes on a machine with Docker; the default category passes without Docker.
- Each helper in §3 has tests of its own (the approval helper, `Eventually`, fakes honouring their port contracts via the same conformance tests where one exists).
- An architecture test proves `YAF.Testing` references neither ASP.NET Core, EF Core, Testcontainers nor a test framework, and that the sample's and the templates' domain and application test projects reference only `YAF.Testing`.
- The generated control matrix lists at least one test per control and per tenet.
