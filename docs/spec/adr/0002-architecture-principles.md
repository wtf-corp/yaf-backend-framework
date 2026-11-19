# ADR-0002 — Architecture principles

> **Status** Accepted
> **Date** 2026-11
> **Deciders** Framework maintainers

## Context

YAF is a set of libraries for building backend services. Its base types assume a particular architecture: a service that follows it gets consistency, isolation and auditability for free; one that fights it gets none of them. The assumptions must be stated once, so that every other ADR and every base type can refer to them.

## Drivers

- Rich business domains whose rules must be testable without infrastructure.
- An application that starts as one deployable and may be split later without rewriting business code.
- Consistency guarantees that are simple to reason about.
- Rules that hold because a machine checks them, since much of the code is written by agents.

## Options considered

| Option | For | Against |
|---|---|---|
| Layered bounded contexts (DDD + clean/hexagonal), modular monolith first | Clear dependency direction; domain testable alone; extraction path | More projects and ceremony per service |
| Vertical slices over a shared data context | Little ceremony; fast for CRUD | Domain rules leak into handlers; persistence types everywhere; weak isolation |
| Microservices from the start | Independent deployment | Operational cost before boundaries are understood |
| Transaction script per endpoint | Simplest | No home for invariants; does not scale with domain complexity |

## Decision

1. **Bounded contexts.** A service is one bounded context with its own ubiquitous language, its own database schema and its own contracts. Contexts never share tables or domain types. They communicate through integration events and, where a synchronous answer is needed, through published APIs.

2. **Domain-driven design.** Business rules live in aggregates, entities, value objects, rules, policies and domain services (ADR-0007). Handlers orchestrate; they do not decide.

3. **Clean, hexagonal layering.** Domain ← Application ← Infrastructure / Adapters. References point inwards only.

   | Layer | Holds | May reference |
   |---|---|---|
   | Domain | Aggregates, mementos interfaces, events, rules | `YAF.Domain`, `YAF.Core` |
   | Application | Use cases, ports, validators, capability catalogue | Domain, `YAF.Application` |
   | Infrastructure | Data model, repositories, store and broker wiring | Application, YAF infrastructure and adapter packages |
   | Adapters | HTTP hosts, workers, migrator | Application, Infrastructure (composition only) |

   Ports are interfaces owned by the application; adapters implement them. The framework's own packages follow the same direction (ADR-0006).

4. **Modular monolith first.** Several contexts may run in one host. Because they share nothing but contracts, extracting one into its own deployable changes composition and configuration, not domain or application code.

5. **Transactional inside, eventual across.** One command is one transaction, including its domain event handlers and its outbox writes (ADR-0009, ADR-0010). Everything that crosses a context goes through the outbox and is consumed idempotently through the inbox (ADR-0012). No distributed transactions, no synchronous cross-context calls inside a command.

6. **Results, not exceptions**, for business outcomes (ADR-0008).

7. **Own the contracts, plug the engines.** Application code depends on YAF interfaces; brokers, object stores, key stores and HTTP styles are adapters (ADR-0003). Request dispatch is YAF's own and has no third-party engine behind it (ADR-0009).

8. **No hidden state.** No static providers, no service locator in domain or application code, time through `TimeProvider`, ids generated explicitly.

9. **Mechanical enforcement.** A rule that matters has an analyzer, an architecture test, a startup check or a CI gate. Review is for judgment.

10. **Simplicity first.** No abstraction without two real implementations or a pluggability requirement stated in the spec. Manual mapping. Small packages.

11. **Secure and compliant by design** (ADR-0004, ADR-0005), and **docs and samples are part of the product** (ADR-0021).

Detail: `spec/00-overview.md` §2.

## Consequences

**Positive**

- Domain and application code can be unit tested without a database, a broker or a web host.
- One consistency model to learn; failure modes are few and known.
- Boundaries are real, so splitting a deployable is a deployment decision.

**Negative / costs**

- A small service still has several projects and the memento translation step. The data classes are generated (ADR-0007); `Snapshot` and `Restore` are still written by hand.
- Eventual consistency across contexts must be designed for: idempotent consumers, compensation, lag monitoring.
- Some simple CRUD use cases carry more structure than they strictly need. Accepted, in exchange for one way of doing things.

## Enforcement

| Mechanism | What it checks |
|---|---|
| `tests/Architecture.Tests` (framework) | Package dependency graph; no vendor SDK outside its adapter; implementations internal |
| `YAF.Testing.Architecture` rules (run in every service from the template) | Layer references point inwards; domain references only `YAF.Domain`/`YAF.Core`; aggregates have no public setters or constructors; handlers are `internal sealed`; no domain type in an HTTP contract |
| Analyzer `YAF4002` | A request handler or domain event handler using `IDispatcher` |
| Analyzer `YAF6001` | Direct use of a message transport outside infrastructure |
| Analyzer `YAF8001` | A repository, `DbContext` or domain type injected into an endpoint |
| Banned API list | Clock, `Guid.NewGuid()`, console, environment access (hidden state) |

## Guidance for agents

- Decide the layer before writing a type: a rule goes in the domain, orchestration in a handler, I/O behind a port.
- Never add a project reference that points outwards (Domain → Application, Application → Infrastructure). If you need something from an outer layer, declare a port in the inner one.
- Never call another context's code or tables. Publish or consume an integration event, or call its API from a job.
- Never perform an external call (HTTP, broker, file store) inside a command's transaction; schedule a job.
- Do not introduce an abstraction for a single implementation unless the spec asks for pluggability.
- When an architecture test fails, fix the code. Changing or suppressing the test needs an ADR.

## Related

- ADR-0003 — technology stack and dependency policy
- ADR-0006 — repository structure and packaging
- ADR-0007 to ADR-0013 — the patterns per layer
- `spec/00-overview.md`, `spec/01-repository-and-packages.md`
