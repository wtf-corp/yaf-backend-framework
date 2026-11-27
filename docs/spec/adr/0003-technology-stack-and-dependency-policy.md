# ADR-0003 — Technology stack and dependency policy

> **Status** Accepted; row YAF.Testing superseded by ADR-0033
> **Date** 2026-11
> **Deciders** Framework maintainers

## Context

A framework passes every dependency it takes on to every app built on it: the licence, the vulnerabilities, the breaking changes. In recent years several libraries that were de facto standards in .NET backends (a mediator, a message bus framework, an assertion library) moved from permissive to commercial licences between major versions. The prior design referenced such libraries directly from application code, so a licence change became every service's problem.

## Drivers

- Apps must be able to use YAF without buying a third-party licence.
- The contracts apps code against must not change because a vendor changed.
- A small supply-chain surface in the packages every app loads.
- Use what the platform already provides before adding anything.
- Teams must still be able to plug in the engine they prefer.

## Options considered

| Option | For | Against |
|---|---|---|
| Reference popular libraries directly | Familiar; least code | Vendor contracts leak into app code; licence and breaking-change exposure |
| Own contracts, vendor engines mandatory behind them | Stable contracts | Still requires the vendor to run |
| **Own contracts and own default implementations; vendors as optional adapters where the engine adds something** | Runs with no third-party engine; brokers, stores, key stores and validators pluggable | More framework code to write and test |
| The same, plus adapters for third-party mediators | Teams keep a familiar engine | Once YAF owns the request contracts and the pipeline, a mediator only locates a handler: its own features go unused, and each adapter adds a bridge, a conformance suite and, for one engine, a commercial licence in CI |
| Own everything, no adapters | Smallest surface | Ignores teams' existing investments |
| Own cron expression parser | No dependency | Time zones and daylight-saving transitions are where own parsers fail; a small, permissively licensed library without dependencies exists |

## Decision

### Platform

| Area | Choice |
|---|---|
| Runtime | .NET 11 and later. The first release has a single target, `net11.0`, for every project. The `Analyzers` project targets `netstandard2.0`, as Roslyn requires |
| Language | C# 15; current language features are used freely |
| Later runtimes | **Multi-target.** When a new .NET ships, a minor release adds it as a further target (`net11.0;net12.0`). A target is dropped in the next major after Microsoft ends support for it; dropping a target is a breaking change (ADR-0020). There is one line of maintenance: an app upgrades its runtime and its YAF version independently. Conditional compilation on the target framework is allowed only where a newer API is used, in files named `*.NetNN.cs`. CI builds and tests every target. Rejected: a new YAF major per runtime with limited maintenance of the previous one (no conditional code, but two branches to maintain and every runtime upgrade forces a YAF major upgrade on apps); also targeting `net10.0` from the first release (the design uses .NET 11 and C# 15 features freely and would need fallbacks from day one) |
| Native AOT and trimming | `IsAotCompatible` and `IsTrimmable` on `YAF.Core` only. A non-goal for the other packages while persistence depends on EF Core; specifications and validators compile expression trees |
| Web | ASP.NET Core; `Microsoft.AspNetCore.OpenApi` for documents |
| Store | PostgreSQL through EF Core and Npgsql; the first-class and, in the first release, only store |
| Telemetry | OpenTelemetry (logs, metrics, traces) through `Microsoft.Extensions.*` abstractions |
| Development host | Aspire (ADR-0019) |
| Tests | xUnit v3 on Microsoft.Testing.Platform, Testcontainers, a permissively licensed assertion library (ADR-0018) |

Package versions are not recorded here. `Directory.Packages.props` is the reference; the implementer takes current stable versions from the feed.

### Own the contracts, plug the engines

Application code depends on YAF interfaces only. Validator, broker, object store, key store, markup sanitizer, API versioning and HTTP style are adapters in separate packages (ADR-0006). The dispatcher has no vendor adapters (ADR-0009): `IDispatcher` and the request contracts are the seam, and an app may put its own engine behind them.

The first release ships one adapter of each kind where several are possible (RabbitMQ and AWS SNS/SQS for messaging, AWS for object and key storage, minimal APIs and controllers for HTTP). Azure Service Bus, Azure Blob, Azure Key Vault and FastEndpoints adapters are planned for later releases; the contracts and conformance suites are written so that they are additions.

### Own implementation first

Where a capability is small, central to the contracts, or exposed to licence risk, YAF implements it itself:

| Capability | Own implementation | Optional adapters |
|---|---|---|
| Result and errors | `Result`, `Result<T>`, `Error` (ADR-0008) | none |
| Request dispatch | Source-generated built-in dispatcher (ADR-0009) | none |
| Validation rules | `Validator<T>` rule builder | FluentValidation |
| In-process transport | In-memory transport implementing the full contract | RabbitMQ, SQS/SNS; Azure Service Bus later |
| Outbox, inbox, jobs, cron trigger | Framework engines | none |
| Approval/snapshot checks | Own helper in `YAF.Testing` | none |
| Mapping | Manual, explicit code; memento data classes are generated | none (mapper libraries are not used) |

An own implementation must be justified by this table or by a later ADR. Cryptographic primitives, JSON, HTTP, the ORM, the database driver and the parsing of cron expressions are never reimplemented: cron expressions are parsed and evaluated by Cronos (MIT, no dependencies), hidden behind YAF's schedule type.

### Allow-list for core packages

| Package(s) | Allowed third-party dependencies |
|---|---|
| `YAF.Core` | `Microsoft.Extensions.Compliance.Abstractions` |
| `YAF.Domain` | none beyond Core |
| `YAF.Application`, `YAF.Infrastructure`, `YAF.Privacy` | `Microsoft.Extensions.*` (DI, options, logging, hosting, diagnostics, resilience, feature abstractions), `Microsoft.Extensions.Compliance.*`, `Microsoft.Extensions.Telemetry.Abstractions` |
| `YAF.Infrastructure` in addition | Cronos |
| `YAF.Persistence.EfCore`, `.PostgreSql`, `YAF.Privacy.EfCore` | The above (without Cronos) plus EF Core and Npgsql |
| `YAF.Http` and the Controllers/MinimalApis packages | The ASP.NET Core shared framework, `Microsoft.AspNetCore.OpenApi`. No HTML sanitizer and no `Asp.Versioning`: those live in `YAF.Markup.HtmlSanitizer` and `YAF.Http.Versioning` |
| `Analyzers` project (packed into `YAF.Core`) | Roslyn (`Microsoft.CodeAnalysis.CSharp`), private assets |
| `YAF.Testing` | Nothing beyond `YAF.Application`'s row; its helpers do not depend on a test framework |
| `YAF.Testing.Integration` | Test framework, Testcontainers, ASP.NET Core test host |

Everything else lives in an adapter package named after the vendor. Adding to this table requires a superseding ADR.

### Licence policy

- Core packages depend only on permissively licensed software (MIT, Apache-2.0, BSD, PostgreSQL licence).
- An engine under a commercial or source-available licence may appear only as an optional adapter, is never required by any core package, sample default or template default, and its adapter's README states the licence obligation. Such an adapter fails at startup outside Development when the vendor's licence key is required and missing.
- YAF itself is published under a permissive licence.

## Consequences

**Positive**

- An app can run on the framework with Microsoft, EF Core and Npgsql packages only.
- A vendor's licence or API change touches one adapter, not application code.
- Fewer transitive packages to audit.

**Negative / costs**

- Requiring .NET 11 excludes apps on an older runtime, including the .NET 10 long-term support release. Both leave Microsoft's support at about the same time, so the exclusion concerns apps that cannot move runtime at all.
- Multi-targeting widens the CI matrix with each supported runtime and brings occasional conditional code where a newer API is used.
- The framework owns a dispatcher, a validator and a transport that must be tested to the standard of the libraries they replace (ADR-0018).
- A team that wants a third-party mediator writes its own `IDispatcher`; the framework gives it no adapter.
- Adapters must be kept working against moving vendor versions.
- Developers used to a vendor's API learn YAF's contracts.
- One third-party library (Cronos) is loaded by every app that uses `YAF.Infrastructure`.

## Enforcement

| Mechanism | What it checks |
|---|---|
| `tests/Architecture.Tests` dependency allow-list | Each built core assembly references only assemblies from its row above |
| Vendor isolation test | A vendor SDK namespace appears only in its adapter assembly |
| CI licence check | Licences of all restored packages against the permitted list; adapters are listed exceptions |
| NuGet audit, lock files, package source mapping (ADR-0020) | Vulnerabilities and unexpected sources |
| Conformance suites | The in-memory transport satisfies the same contract as the broker adapters |
| CI target matrix | Build and tests for every target framework in `Directory.Build.props`; a check that `#if NET…` appears only in `*.NetNN.cs` files |
| Banned API list | Mapper libraries and direct clock/id APIs |

## Guidance for agents

- Target what `Directory.Build.props` sets (currently `net11.0`). Adding or dropping a target framework is a maintainer's decision under the policy above, in a spec of its own; never do it inside another change. Do not add a polyfill package. Put a `#if NET…` branch only in a `*.NetNN.cs` file, and only to use a newer API.
- Do not add a mediator library or an adapter for one. Do not write a cron parser; use the schedule type.
- Before adding a `PackageReference`, check the allow-list row of the target project. If the package is not on it, stop: it needs an adapter package or an ADR.
- Never reference a vendor SDK from a core package, even "just for one type".
- Never make a sample or template depend on a commercially licensed engine by default.
- Prefer a BCL or `Microsoft.Extensions` type over a new dependency; prefer an existing YAF type over both.
- Do not write your own cryptography, JSON parser or SQL driver. "Own implementation first" covers only the table in this ADR.
- Take versions from `Directory.Packages.props`; add new ones there, never in a project file.

## Related

- ADR-0006 — packaging; ADR-0008 — results; ADR-0009 — dispatcher; ADR-0012 — transports
- ADR-0018 — testing; ADR-0020 — release and supply chain
- `spec/01-repository-and-packages.md`
