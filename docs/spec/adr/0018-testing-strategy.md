# ADR-0018 — Testing strategy

> **Status** Accepted · **Date** 2026-11 · **Deciders** maintainers

## Context

The framework's value is that its guarantees hold: isolation, atomicity, redaction, parity between adapters. Those are properties of real databases, real brokers and whole hosts. The framework is also written largely by agents, so tests are the specification an agent verifies itself against, and the artefacts reviewers read.

## Drivers

- Every rule in the spec has an executable check.
- Tests run on the real engines the guarantees depend on.
- Contract changes (errors, API, permissions, wire formats) are visible as diffs.
- Adapters are interchangeable by proof.
- No licence risk in test dependencies.
- A domain test project does not pull in a web server, an ORM and container tooling.
- Evidence for the control catalogue is generated from tests.

## Options considered

| Topic | Chosen | Not chosen, why |
|---|---|---|
| Runner | xUnit v3 on Microsoft.Testing.Platform | VSTest: legacy runner, slower, different switches |
| Assertions | AwesomeAssertions (Apache-2.0) | A commercially licensed assertion library; plain `Assert` (less readable failures) |
| Doubles | Hand-written fakes first, NSubstitute sparingly | Mock-heavy tests that assert calls instead of outcomes |
| Database in tests | PostgreSQL in Testcontainers | In-memory provider or SQLite: different semantics, no row-level security |
| Brokers in tests | Real broker containers and emulators | Mocks of SDK clients |
| Architecture rules | ArchUnitNET | A dormant alternative library |
| Snapshot/approval | A small own approval helper (received/approved text files) | A snapshot framework: more than needed |
| Test support packaging | Two packages: `YAF.Testing` (pure helpers) and `YAF.Testing.Integration` (hosts, containers, suites) | One package depending on `YAF.Http` and the PostgreSQL provider: every domain test project would restore ASP.NET Core, EF Core and Testcontainers |
| Identity in tests | Tokens signed by a test issuer, validated by the production code path | A test authentication scheme or an "accept unsigned tokens" switch: a second code path, and an unsafe option that must then be guarded |

## Decision

**Test-first.** Write the test, see it fail for the expected reason, then write the code.

**Pyramid.**

| Level | Scope | Needs |
|---|---|---|
| Domain unit | Aggregates, value objects, rules, state machines, memento round trips | Nothing |
| Application unit | Handlers, validators, middleware with hand-written fakes of ports | Nothing |
| Integration | Persistence, outbox, inbox, jobs, HTTP hosts on PostgreSQL and real brokers | Docker |
| End-to-end | The reference sample through its Aspire AppHost | Docker |

**Two packages.** `YAF.Testing` holds the pure helpers: memento, result and domain-event assertions, fakes of the framework's ports, the test clock, the approval helper, the architecture rules. It depends on `YAF.Application` and on no test framework (its helpers throw their own assertion exception). `YAF.Testing.Integration` holds the web host factory, the Testcontainers fixtures, test identity with a signing key, the security test pack and the conformance suites; it depends on `YAF.Testing`, `YAF.Http`, `YAF.Persistence.EfCore.PostgreSql` and xUnit v3.

**Libraries.** AwesomeAssertions for assertions, NSubstitute for third-party interfaces only, ArchUnitNET for architecture rules. These are decided here; they are not open points for the implementer.

**Conformance suites** in `YAF.Testing.Integration`, abstract and derived per implementation: transports (`TransportConformance`) and HTTP style parity (`HttpParity`). Each takes the implementation as a parameter, so a later transport or style joins without changing the expectations.

**Approval files**, each a committed text file compared by a test that writes a `.received` file and lists added and removed lines: error catalogue (the same test proves codes are unique across all catalogues of a service), pipeline order, authorization inventory, stable field ids of protected members (`fields.approved.txt`), OpenAPI document, public API (alongside the public API analyzer), integration event schemas.

**Security test pack** (namespace `YAF.Testing.Integration.Security`): token and key attacks (unsigned tokens are refused in every environment), cross-tenant requests generated from the running app's endpoints, immediate revocation of privileged permissions, identity of asynchronous work, the input guard, headers, CORS, rate limits, error-leak checks. A "break it" suite flips each default and asserts the guard fails build, startup or test.

**Control tagging.** Tests carry `[Control("<id>")]`; CI generates `docs/compliance/controls.md` from them. A control without a passing test is a failure.

**Quality gates.** Coverage is reported for all packages; the gate is 85 % line coverage on Core, Domain and Application. Mutation testing with Stryker runs weekly on those three, non-blocking, with results published. Benchmarks exist and do not run in CI.

**Conventions.** Names `Method_Scenario_Expected`; Arrange-Act-Assert; one behaviour per test; assert on error **codes**, not messages; no logic in tests; tests are order-independent; a shared host or container per collection with per-test data (a tenant per test) rather than a clean database, and a separate collection with its own database for tests of behaviour that is not tenant-scoped (elevation, cron fan-out, relay, sealer, retention); poll with a timeout for asynchronous effects, never sleep. Test data is synthetic and the repository's own; no identifiers or data from any application.

**Where tests live.** Next to the code they test: one test project per `src` project, plus `Architecture.Tests` and adapter conformance projects. Framework code is never tested only through a sample.

## Consequences

- CI needs Docker and several minutes for integration suites; domain and application tests stay fast and run on every save.
- Approval files add small, deliberate friction to contract changes.
- LocalStack needs an account token: the SNS/SQS conformance and end-to-end jobs run when the secret is present and are reported as skipped otherwise, and a release requires that they ran.
- Two test packages to version and document instead of one; the integration package ties its users to xUnit v3.
- Tests need a signing key and an issuer set up by the factory, which is slightly more machinery than a fake scheme.
- Fakes must be maintained, but they keep tests about behaviour.

## Enforcement

- CI: build, all test levels, coverage gate, approval drift, conformance matrix per transport.
- Architecture tests: layering, vendor isolation, internal implementations, no production reference to `YAF.Testing` or `YAF.Testing.Integration`, `YAF.Testing` free of ASP.NET Core, EF Core, Testcontainers and test-framework references, every tenant table protected.
- Banned API: the in-memory EF Core provider in test projects.
- Pull request template asks for the failing-first evidence; review (ADR-0027) rejects code without tests.
- `YAF.Testing.Integration` hosts throw when constructed in a Production environment.

## Guidance for agents

- Start every change with a failing test and say which assertion failed before you implement.
- Pick the lowest level that proves the behaviour; use integration tests for anything about transactions, tenancy, encryption, messaging or HTTP mapping.
- Prefer a fake you can read over a mock you configure. Assert outcomes and stored state.
- Reference `YAF.Testing` from domain and application test projects, and `YAF.Testing.Integration` only from projects that start a host or a container.
- Authenticate test clients with `client.As(user, tenant)`; never add a scheme or option that skips token validation.
- Never weaken or delete a failing test to get green; if the test is wrong, say why and fix it in the same change.
- When an approval test fails, read the diff, decide whether it is intended, and only then replace the approved file. Mention it in the pull request.
- Tag tests that prove a catalogue control with `[Control]`.
- Do not use real personal data, production data or another project's ids in tests.
- Report the commands you ran and their results; "should pass" is not evidence.

## Related

0004, 0005, 0008, 0009, 0010, 0012, 0014, 0019, 0023, 0026, 0027, 0030
