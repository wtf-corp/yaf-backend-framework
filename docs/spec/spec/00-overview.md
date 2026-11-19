# 00 — Overview and principles

## 1. What YAF is

YAF is a set of .NET libraries for building backend services as bounded contexts: DDD building blocks, a CQRS pipeline with its own dispatcher, transactional outbox and inbox over pluggable message brokers, EF Core persistence with tenant isolation and audit, HTTP adapters for controllers and minimal APIs, and the security, privacy and telemetry plumbing every service needs. Root namespace and package prefix: `YAF`.

YAF knows nothing about any business domain. Anything a service must decide (where users and grants live, which broker, which identity provider) is an extension point with a startup check.

Platform: .NET 11 and later. The first release targets `net11.0` only (C# 15). When a new .NET ships, a minor release adds it as a further target; a target is dropped in the next major after Microsoft ends support for it (ADR-0003). PostgreSQL is the first-class store. Aspire is the development host.

The first release (1.0) ships one of each kind: the built-in dispatcher; the in-memory, RabbitMQ and AWS SNS/SQS transports; minimal APIs and controllers; the AWS object-store and key-store adapters; the privacy mechanisms. Azure Service Bus, Azure storage and key adapters, FastEndpoints, the privacy coordinator, the incident register and the AI gateway are planned for later releases; the contracts and conformance suites are designed so that they can be added without a breaking change (`18-build-plan.md` §6).

## 2. Principles

1. **Bounded contexts, clean layers.** Domain ← Application ← Infrastructure / Adapters. References point inwards. A service is a modular-monolith module that can be extracted without code changes in its domain and application layers.
2. **Own the contracts, plug the engines.** Application code depends on YAF interfaces only. Validator, broker, object store, key store and HTTP style are adapters in separate packages. The framework works with none of the third-party engines installed: it ships its own dispatcher, a rule-based validator and an in-memory transport. The dispatcher has no vendor adapters: YAF owns the request contracts and the pipeline, so an engine behind them would only locate a handler; `IDispatcher` remains the seam for an app that wants its own.
3. **Secure by design.** Deny by default, fail closed, fail at startup rather than at the first request. Security properties are enforced by code, analyzers and tests, not by convention. See `09-security.md` and ADR-0004.
4. **Compliant by design.** Controls from ISO/IEC 27001, GDPR, NIS2 and the AI Act that code can enforce are built in and traceable to tests. The framework supplies controls and evidence; it never claims that an app is compliant. See `11-privacy-and-compliance.md` and ADR-0005.
5. **Results, not exceptions.** Business outcomes are `Result` values with declared, stable error codes. Exceptions mean bugs or infrastructure failures.
6. **Transactional inside, eventual across.** One transaction per command, including domain event handlers and outbox writes. Everything that crosses a context goes through the outbox and is consumed idempotently through the inbox.
7. **Mechanical enforcement.** A rule that matters has an analyzer, an architecture test, a startup check or a CI gate. Review is for judgment, not for catching what a machine can catch.
8. **No hidden state.** No static providers, no service locator in domain or application code, time through `TimeProvider`, ids generated explicitly.
9. **Simplicity first.** No abstraction without two real implementations or an explicit pluggability requirement in this spec. Manual mapping over mappers. Small packages over one large one.
10. **Docs and samples are part of the product.** A public API change without XML docs, article and sample updates does not merge.

## 3. What is deliberately better than the prior art this spec was derived from

| Area | Prior approach | YAF |
|---|---|---|
| Result type | Third-party discriminated union, kind chosen at the return site | Own `Result<T>`; kind, code and message declared once in an error catalogue; codes snapshot-tested |
| Mediator | One commercial library referenced directly by application code | Own contracts, pipeline and source-generated dispatcher; no third-party engine |
| Validation | One library in domain and application | Own rule builder and invariants in the domain; adapter for FluentValidation at the application edge |
| Domain events | Aggregates registered with a dispatcher passed into builders and `Restore`; events carried the aggregate | Aggregates only queue events; the unit of work collects them; events are immutable facts |
| Typed ids | Record classes, empty until saved | Source-generated `readonly record struct`, UUIDv7 assigned at creation |
| Mementos | Hand-written data classes with primitive members | Data classes generated from the memento interface; members may be typed ids, enumerations and value objects |
| Protection of stored data | Encryption and sanitization run inside the domain through process-wide providers | Classification attributes on memento interfaces; applied by the persistence mapping layer (repositories and the read-model query helper); per-subject keys for crypto-shredding |
| Input handling | HTML stripped from every string of a command | Input is never rewritten: invalid characters are rejected; only members marked `[Markup]` pass through a sanitizer |
| Messaging | One cloud's topic/queue pair, envelope with type and payload | Transport abstraction with capabilities; CloudEvents envelope; W3C trace context; partition keys; claim check |
| Idempotency | Left to each handler | Framework inbox in the handler's transaction |
| Outbox claiming | Optimistic checkout flags | `FOR UPDATE SKIP LOCKED` batches, ordered per partition key, optional `LISTEN/NOTIFY` wake-up |
| Tenant isolation | Global query filters | Query filters plus PostgreSQL row-level security driven by a per-transaction setting |
| Audit trail | Change rows | Append-only rows written in the business transaction, hash-chained per tenant by a sealer, verified by a job, anchored externally |
| HTTP | Base controller | One result-to-problem mapping used by every endpoint style (controllers and minimal APIs); RFC 9457 everywhere |
| Authorization | Controller attribute | A permission marker on the request type, checked by an application-level authorization middleware so non-HTTP entry points are covered; the edge metadata (`IAuthorizationRequirementData`) is derived from that marker |
| Asynchronous work | Ran as the user named in the message | Runs as the service's system principal; the user who started the operation is carried as the initiator, for audit only |
| Telemetry | Custom correlation and activity headers | W3C `traceparent` only; correlation id is a baggage item; OpenTelemetry messaging conventions |
| Distribution | Git submodule | NuGet packages with package validation, SBOM and provenance; `dotnet new` templates |
| Agent guidance | One tool's instruction file | `AGENTS.md` hierarchy and Agent Skills, tool-neutral |

## 4. Non-goals

- A saga engine with visual modelling, an event-sourcing store, a UI, an identity provider, a workflow engine. Process managers (see `04-application.md` §9) cover multi-step coordination.
- Supporting stores other than PostgreSQL in the first release. The store abstractions allow another provider package; none is specified here.
- Hiding the broker completely. Transports declare capabilities; a feature a broker lacks is either emulated by the framework (delay through the jobs store) or refused at startup.

## 5. How to read this specification

| Folder | Content |
|---|---|
| `spec/` | Normative specification, one file per area. `18-build-plan.md` orders the work |
| `adr/` | The decision records the new repository starts with, including the agentic process ADRs |
| `agents/` | Desired content of `AGENTS.md` (root and nested), the in-repo skills, and the third-party skills to install |

Version numbers of third-party packages quoted in this spec were collected in November 2026 and marked as such; the implementer MUST take the current stable versions from the package feed and record them in `Directory.Packages.props`.
