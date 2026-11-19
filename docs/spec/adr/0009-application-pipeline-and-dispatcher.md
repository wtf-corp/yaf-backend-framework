# ADR-0009 — Application pipeline and built-in dispatcher

> **Status** Accepted
> **Date** 2026-11
> **Deciders** Framework maintainers

## Context

Use cases need the same cross-cutting behaviour whoever calls them: telemetry, authorization, input checks, validation, a transaction with domain event dispatch, idempotency. The prior design implemented these as behaviours of one mediator library, and every command, query, handler and event in every service implemented that library's interfaces. When the library's licence changed, the whole application layer was affected. Authorization lived only on HTTP controllers, so a job or a message consumer reaching the same use case bypassed it. Request context was held in mutable providers any code could overwrite, and asynchronous work ran as the user named in a message.

Teams have different preferred engines (a reflection-based mediator, a convention-based message bus used as a mediator, source-generated mediators), and their pipeline models differ: wrapper chains, code woven at build time, pre- and post-processors.

## Drivers

- Application code references YAF contracts only.
- Cross-cutting behaviour is identical for every entry point.
- The framework runs with no third-party mediator installed.
- A use case is authorised wherever it is called from, and nothing is authorised on the strength of a message.
- The order of cross-cutting behaviour is explicit and visible in review.
- No framework code whose only purpose is to keep a vendor's engine working.

## Options considered

| Option | For | Against |
|---|---|---|
| One mediator library directly | Least framework code | Vendor contracts throughout app code; licence exposure; semantics tied to it |
| Thin wrapper mapping YAF middleware to each engine's native pipeline | Uses native features | Engines without wrapper chains lose around-semantics; behaviour differs per engine |
| YAF owns contracts and the pipeline; third-party engines as optional adapters that locate and invoke the handler | Teams keep a familiar engine | The engine contributes only the handler lookup; its own behaviours, transactions and outbox must stay off; generated bridges, a conformance suite and a commercial licence key in CI to maintain, for no function the built-in dispatcher lacks |
| **YAF owns contracts, the pipeline and the one dispatcher; `IDispatcher` stays the seam** | Same behaviour everywhere; nothing to keep in step with vendors; no licence on any path | A team that insists on another engine writes and tests its own `IDispatcher` |
| No mediator: inject handlers directly | Simplest | No single place for the pipeline; adapters must know handler types |

## Decision

Contract: `spec/04-application.md`.

**Own contracts.** `IRequest<TResult>` with `ICommand<TResult>` (changes state, runs in a unit of work) and `IQuery<TResult>` (reads only); `IRequestHandler<TRequest, TResult>` returning `ValueTask<TResult>`; `IDispatcher.SendAsync`; `IRequestMiddleware<TRequest, TResult>` with true around-semantics; `IDomainEventHandler<TEvent>`. `TResult` is always `Result` or `Result<T>` (ADR-0008). Handlers are `internal sealed` and registered by a generator, with no runtime assembly scanning.

**One dispatcher, no mediator adapters.** The dispatcher is YAF's own: a source-generated switch from request type to handler, with no reflection per call and no licence. YAF ships no adapter for a third-party mediator. `IDispatcher` remains a public interface and the only thing application code depends on, so an app can register its own implementation; keeping the dispatcher guarantees is then that implementation's job. Guarantees: exactly one handler per request, found missing or duplicated at build time or startup; the pipeline runs once, in order, with around-semantics; handler and middleware share the caller's scope; cancellation flows; streaming queries run through the query pipeline.

**The pipeline belongs to YAF.** Middleware are registered explicitly and in order. The default order, outermost first:

1. Telemetry (span, metrics, names and error codes only)
2. Exception boundary (known infrastructure exceptions to results; everything else logged once and rethrown)
3. Timeout (marked requests)
4. Authorization (request markers against the execution context's actor)
5. Input guard (commands)
6. Validation
7. Unit of work (commands), or a `READ ONLY` transaction (queries)
8. Idempotency (`[Idempotent]` commands), inside the unit of work

The order is data (`IPipelineDescriptor`) and each service snapshots it in an approved file. Middleware must be safe to run twice, because the unit of work may retry.

**Input is checked, never rewritten.** The input guard rejects control characters (other than tab, CR, LF) and invalid Unicode in a command's strings with a validation error, and changes nothing. Only a member marked `[Markup]` is passed through `IMarkupSanitizer`, a port implemented by an adapter package; a `[Markup]` member without a registered sanitizer fails startup. Consumed message and job payloads get the same check. Stripping markup from every string was rejected: it silently changes what users typed, and in a JSON API the control against script injection is output encoding in the client (ADR-0004).

**Undeclared requests are refused.** Every request type carries one of `[RequiresPermission]`, `[UserScoped]`, `[AllowAnonymousRequest]`, `[SystemOnly]`. A request with no marker fails with `core.authorization.undeclared` and raises a security event. The marker is the single declaration: the HTTP edge derives its own check from it (ADR-0014). Permissions flagged `privileged` are never answered from the privilege cache (ADR-0015).

**Queries run in a `READ ONLY` transaction.** One per query request, so the store itself refuses a write in every environment, row-level security has its transaction-local tenant setting, and the handler reads one snapshot. No store command runs outside a transaction: a lookup made before the pipeline has opened one (an asynchronous validation rule) gets a short `READ ONLY` transaction of its own (ADR-0010).

**Unit of work: flush, then dispatch.** `IDomainEventPublisher` is YAF's own in-process, sequential publisher running inside the transaction. After the handler, the unit of work saves, dispatches the domain events collected from tracked aggregates, and repeats save and dispatch until a round raises none (depth limit 10), then commits; any failed result rolls everything back. Event handlers therefore see the changes that raised the event. Saving once at the end was rejected: handlers' queries would miss pending changes and constraint violations would surface far from their cause. Durable delivery goes through the outbox (ADR-0012).

**Idempotency inside the transaction.** The key is claimed, and the result stored, in the unit of work's transaction, in a table of its own (`yaf_idempotency`: key, request name, payload hash, stored result, classification, retention), separate from the inbox. The record and the change commit or roll back together.

**Execution context with actor and initiator.** One immutable `IExecutionContext` (tenant, actor, initiator, correlation id, trace context, additional tenants, idempotency key, origin) is established once per scope by the entry point. `Actor` is the principal authorization is decided on. `Initiator` is an optional reference to the user who started the operation, for audit, stamps and correlation only; it never grants anything. For HTTP the actor is the authenticated caller. Consumers, jobs, cron and process managers run as the service's system principal, with the initiator taken from the envelope (`yafinitiator`) or the job row. A forged or stale message therefore cannot act with a user's rights. Running as the original user was rejected: it would trust an unsigned envelope for identity and keep rights alive after they were revoked.

**Application code cannot create a context.** `IExecutionContextScopeFactory` is internal to the framework's entry points. Cron fan-out over tenants goes through `CronContext.ForEachTenantAsync`. Running without a context throws. A system principal carries a reason. Elevation is a disposable, audited scope.

**Handlers do one thing.** Load, call the domain, persist through a repository, return. A request handler or a domain event handler does not open transactions, publish to a broker, or send other requests through the dispatcher. Entry points of asynchronous work (consumer, job, cron and process-manager handlers) are not request handlers: they may dispatch requests, and only `[SystemOnly]` ones.

## Consequences

**Positive**

- Application code and behaviour do not depend on any vendor's mediator; the framework has no commercial dependency on any path.
- No adapters, generated bridges or conformance suite to keep working against moving vendor versions.
- A use case cannot be reached unauthorised through a new entry point, and asynchronous work cannot borrow a user's rights.
- The outbox and the idempotency record are atomic with the change.
- Event handlers can query what the command just changed.
- Queries cannot write, by the store's own rule.
- User input is stored as typed.
- Pipeline order changes show in a diff.

**Negative / costs**

- YAF owns a dispatcher and its generator.
- A team with an existing investment in another mediator cannot plug it in by registration; it writes its own `IDispatcher` or uses YAF's.
- Retried transactions re-run handlers, so handlers must have no effects outside the transaction.
- Every request needs a marker, including internal ones; use cases reached from asynchronous work need a `[SystemOnly]` request, and a user's present rights must be asked for explicitly where they matter.
- One more store round trip per round of domain events, and a transaction for every query.
- Rejecting instead of cleaning input moves the duty of output encoding entirely to clients; a client that renders values as HTML without encoding is not protected by the server.

## Enforcement

| Mechanism | What it checks |
|---|---|
| Analyzer `YAF4001` | `TResult` is `Result` or `Result<T>` |
| Analyzer `YAF4002` | `IDispatcher` used in a request handler or a domain event handler |
| Analyzer `YAF4003` | A `[RequiresPermission]` pair exists in the service's capability catalogue |
| Analyzer `YAF4004` | An execution context created or replaced outside framework entry points |
| `AuthorizationMiddleware` | Undeclared request refused at run time; a non-`[SystemOnly]` request from a system actor refused |
| Generator and startup check | One handler per request; a sanitizer registered when a `[Markup]` member exists; the idempotency table present when an `[Idempotent]` request exists |
| Dispatcher tests | The five dispatcher guarantees |
| Approved file `pipeline.approved.txt` | Middleware order per service |
| `READ ONLY` query transaction | A write during a query is refused by the store |
| Vendor isolation test (ADR-0003) | No mediator vendor namespace in any YAF assembly |
| Middleware tests | Input guard rejects and never alters; rollback on failed result and on failed event handler; event handler sees flushed changes; cascade depth limit; idempotent replay, concurrent same key, key with different payload; no duplicate outbox rows on retry |
| Context tests | Consumers and jobs run as the system principal with the initiator recorded |

## Guidance for agents

- A new use case is a request record, an `internal sealed` handler, a validator when it has input, and exactly one authorization marker. Pick the marker from the spec; if the spec does not say, ask.
- Never reference a mediator library from any project. There is no adapter to add one to; a request for one is a scope question for a human.
- Never call `IDispatcher` from a request handler or a domain event handler. To react to a change in the same context raise a domain event; across contexts publish an integration event; for later work schedule a job.
- In a consumer, job, cron or process-manager handler you may dispatch a request to reuse a use case. It must be `[SystemOnly]`. Do not mark a user-facing request `[SystemOnly]` to make this work; add a separate request.
- Never treat `Initiator` as permission. If the work needs the user's present rights, ask `IPrivilegeSource` for them.
- Never make an external call (HTTP, broker, storage) from a command handler or a domain event handler. It would repeat on retry and cannot be rolled back. Schedule a job.
- Never open a transaction or call `SaveChanges` in a handler or repository; the unit of work does both.
- A query handler never writes; the store will refuse it.
- Never strip, escape or otherwise rewrite input strings in a handler, a validator or a middleware. Mark a member that holds HTML `[Markup]`; everything else is stored as sent.
- Put cross-cutting behaviour in a middleware, not in a base handler class or an HTTP filter. If you add or reorder middleware, update the approved pipeline file and say why.
- Never read tenant or user from anywhere but `IExecutionContext`, and never try to create or set one. For per-tenant cron work use `CronContext.ForEachTenantAsync`.

## Related

- ADR-0003 — own contracts; own implementation first
- ADR-0004 — security by design; ADR-0015 — identity and authorization
- ADR-0008 — results; ADR-0010 — unit of work, transactions and aggregate tracker
- ADR-0012 — outbox and inbox; ADR-0013 — jobs, cron, process managers
- ADR-0014 — HTTP adapters
- `spec/04-application.md`
