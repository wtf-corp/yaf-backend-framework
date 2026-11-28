# 04 — Application layer (`YAF.Application`)

## 1. Use case structure

```
Application/
├── Sessions/
│   ├── PublishSession/                 # one folder per use case
│   │   ├── PublishSessionCommand.cs    # the request record
│   │   ├── PublishSessionHandler.cs    # internal sealed
│   │   ├── PublishSessionValidator.cs  # commands with input
│   │   └── PublishSessionResponse.cs   # when there is one
│   ├── GetSession/ …
│   ├── EventHandlers/                  # domain event handlers
│   ├── IntegrationEvents/              # consumers of other contexts' events
│   ├── Jobs/
│   └── ISessionRepository.cs           # ports owned by the application
├── Authorization/Capabilities.cs
└── ApplicationModule.cs                # AddCatalogApplication()
```

Use-case folders are grouped by aggregate, not by `Commands/` and `Queries/`.

## 2. Contracts

```csharp
public interface IRequest<TResult>;                                  // TResult is Result or Result<T>
public interface ICommand<TResult> : IRequest<TResult>;              // changes state; runs in a unit of work
public interface IQuery<TResult> : IRequest<TResult>;                // reads only
public interface IStreamQuery<TItem>;                                // streamed read (§5)

public interface IRequestHandler<in TRequest, TResult> where TRequest : IRequest<TResult>
{
    ValueTask<TResult> HandleAsync(TRequest request, CancellationToken ct);
}

public interface IDispatcher
{
    ValueTask<TResult> SendAsync<TResult>(IRequest<TResult> request, CancellationToken ct = default);
    IAsyncEnumerable<TItem> StreamAsync<TItem>(IStreamQuery<TItem> query, CancellationToken ct = default);
}

public interface IDomainEventHandler<in TEvent> where TEvent : IDomainEvent
{
    ValueTask<Result> HandleAsync(TEvent domainEvent, CancellationToken ct);
}
```

- Requests are immutable records. A command's input members are the request itself or one nested `Input` record shared with the HTTP contract.
- `TResult` MUST be `Result` or `Result<T>`; analyzer `YAF4001` enforces it, so middleware can always inspect the outcome.
- Handlers are `internal sealed`, registered by the dispatcher-registration generator (`AddYafHandlers()` emitted per assembly; no runtime assembly scanning).
- A handler does one thing: load, call the domain, persist through a repository, return. It does not open transactions, publish to a broker, or call another handler through the dispatcher. Analyzer `YAF4002` flags `IDispatcher` used in a **request handler or a domain event handler**; compose in the adapter or use a domain event. Entry points of asynchronous work (consumers, job and cron handlers, process managers) are not request handlers and may dispatch (§6).
- Markers on requests (attributes, read by middleware): `[RequiresPermission(feature, permission)]`, `[AllowAnonymousRequest]`, `[UserScoped]`, `[SystemOnly]`, `[LogPayload]`, `[Idempotent]`, `[NoTransaction]`, `[Timeout(seconds)]`. The authorization marker is the single declaration of who may run a use case: the HTTP endpoint helpers derive the edge authorization metadata and the OpenAPI security entry from it (`08-http.md`), so it is not repeated on the endpoint.
- Markers on request members: `[Markup]` (the member holds HTML, §3), and the classification attributes (`11-privacy-and-compliance.md`).

## 3. The pipeline

Cross-cutting behaviour is YAF middleware, run by the dispatcher around every handler:

```csharp
public interface IRequestMiddleware<in TRequest, TResult> where TRequest : IRequest<TResult>
{
    ValueTask<TResult> InvokeAsync(TRequest request, RequestDelegate<TResult> next, CancellationToken ct);
}
public delegate ValueTask<TResult> RequestDelegate<TResult>(CancellationToken ct);
```

Registration is explicit and ordered:

```csharp
services.AddYafApplication(app => app
    .AddHandlersFrom<ApplicationModule>()
    .UsePipeline(p => p
        .UseDefaults()                                   // the list below
        .InsertAfter(typeof(ValidationMiddleware<,>), typeof(MyMiddleware<,>))
        .ForCommands(c => c.Use(typeof(AnotherMiddleware<,>)))));
```

Default pipeline, outermost first:

| # | Middleware | Applies to | Behaviour |
|---|---|---|---|
| 1 | `TelemetryMiddleware` | all | Starts an `Activity` `yaf.request {RequestName}`; records duration and outcome metrics; logs `Handling` / `Handled` / `Failed {ErrorCodes}` with names and codes only. With `[LogPayload]`, logs the request through the redacting formatter |
| 2 | `ExceptionBoundaryMiddleware` | all | Lets `OperationCanceledException` pass; converts known infrastructure exceptions (`ConcurrencyConflictException` → `Conflict`, `DependencyUnavailableException` → `Unavailable`); rethrows anything else without logging it |
| 3 | `TimeoutMiddleware` | marked | Linked cancellation after `[Timeout]` or the configured default |
| 4 | `AuthorizationMiddleware` | all | Enforces the request markers against `IExecutionContext` (§7). A request with **no marker is refused** (`core.authorization.undeclared`), so a use case reachable from a job, a consumer or a new adapter is still authorised |
| 5 | `InputGuardMiddleware` | commands | Checks every string of the request and **never alters it**. A control character other than tab, CR and LF, or invalid Unicode (an unpaired surrogate), fails the request with validation error `Input.InvalidCharacters` at the member's target. A member marked `[Markup]` is the one exception to "never alters": it is passed through `IMarkupSanitizer` (`09-security.md` §7) |
| 6 | `ValidationMiddleware` | all with a validator | Runs every `IValidator<TRequest>`; returns their combined errors |
| 7 | `UnitOfWorkMiddleware` | commands (not `[NoTransaction]`) | §6 |
| 7 | `QueryTransactionMiddleware` | queries | Runs the handler in one `READ ONLY` transaction (below) |
| 8 | `IdempotencyMiddleware` | `[Idempotent]` commands | Inside the unit of work's transaction: claims the idempotency key in `yaf_idempotency`; replays the stored result, or stores the handler's result so it commits with the change (§6) |
| — | handler | | |

> **Amended 2026-11-27**, by the owner's decisions of 2026-11-26 (`specs/0013-thin-slice/plan.md`, decision log Q4): a middleware that applies to every request is an open generic type, which C# does not accept as a type argument, so the builder names a step by `Type`. `Use(Type)`, `InsertAfter(Type existing, Type middleware)` and `InsertBefore(Type existing, Type middleware)` accept open generic middleware (`typeof(ValidationMiddleware<,>)`); `Use<TMiddleware>()` adds a closed, request-specific middleware.

> **Amended 2026-11-27**, by the owner's decisions of 2026-11-26 (`specs/0013-thin-slice/plan.md`, decision log Q1): the default pipeline of M3 is steps 1, 2, 6 and both steps 7. `AuthorizationMiddleware` and `InputGuardMiddleware` join it in M4, `TimeoutMiddleware` and `[LogPayload]` in M5, `IdempotencyMiddleware` in M9; `[NoTransaction]` comes with M8 (`18-build-plan.md` §2).

> **Amended 2026-11-28**, by the owner's decision of 2026-11-28 (`specs/0013-thin-slice/plan.md`, deviation 18; recorded on issue #337): an unhandled exception is logged once, by the entry point that finally handles it, and the exception boundary rethrows it without logging (`12-observability.md` §1 rule 5). Over HTTP that is `YafExceptionHandler` (`08-http.md` §2); for the entry points that are not requests, the runtime that retries, parks or dead-letters them.

Rules:

- Middleware are resolved per request from DI (scoped), are generic or request-specific, and MUST be safe to run twice (the unit of work may retry the inner part on a transient store failure).
- Order is data, exposed by `IPipelineDescriptor`; a test in every service snapshots it (`pipeline.approved.txt`) so an accidental reorder is visible in review.
- **Input is checked, not rewritten.** Text arrives in the handler exactly as the caller sent it (`a < b`, a code snippet, a name with an ampersand). Cross-site scripting in a JSON API is prevented by output encoding in the client and by the response headers, not by stripping input (`09-security.md`). `IMarkupSanitizer` is a port of this package; the implementation is the adapter package `YAF.Markup.HtmlSanitizer`. A service that has a `[Markup]` member and no registered sanitizer fails at startup.
- **Queries run in one `READ ONLY` transaction.** `QueryTransactionMiddleware` opens it; the store rejects any write, so "queries do not write" holds in every environment and not only where a guard is switched on. The transaction also carries the tenant setting that row-level security reads (`05-persistence.md` §4) and gives the handler one consistent snapshot.
- **No store command runs outside a transaction.** Where the pipeline has not opened one yet (an asynchronous validation rule that looks something up, step 6), `YafDbContext` opens a short `READ ONLY` transaction for that command. In every transaction the tenant `set_config` travels in the same batch as the first command, so it costs no extra round trip.

## 4. Validation

```csharp
public interface IValidator<in T>
{
    ValueTask<Result> ValidateAsync(T instance, CancellationToken ct);
}

internal sealed class PublishSessionValidator : Validator<PublishSessionCommand>
{
    public PublishSessionValidator()
    {
        Rule(x => x.Title).NotEmpty(Errors.TitleRequired).MaxLength(200, Errors.TitleTooLong);
        Rule(x => x.Capacity).InRange(1, 10_000, Errors.CapacityOutOfRange);
        RuleForEach(x => x.Speakers).Must(s => s.Email is not null, Errors.SpeakerEmailRequired);
        When(x => x.StartsAt is not null, () => Rule(x => x.EndsAt).Must((cmd, end) => end > cmd.StartsAt, Errors.EndBeforeStart));
    }
    internal sealed class Errors : IErrorCatalog { /* descriptors */ }
}
```

- `Validator<T>` is YAF's own small rule builder: `Rule`, `RuleForEach`, `When`, `Must`, `MustAsync`, `NotNull`, `NotEmpty`, `MaxLength`, `MinLength`, `InRange`, `Matches`, `Include(otherValidator)`, `Nested(x => x.Child, childValidator)`. Every rule takes an `ErrorDescriptor`; there is no inline message. `Target` is derived from the expression (`speakers[1].email`, camelCase, matching the JSON contract).
- Validators check the **shape and plausibility of input**. Invariants that depend on aggregate state belong to the domain.
- `Rule(…)` takes an expression and compiles it once per validator type; `YAF.Application` is therefore not marked AOT-compatible.
- A `MustAsync` rule that reads the store runs before the unit of work; its read goes through the short `READ ONLY` transaction of §3. A check that must hold at commit (uniqueness) is a domain rule or a database constraint, not a validator.
- `YAF.Validation.FluentValidation` adapts `FluentValidation.IValidator<T>` to `IValidator<T>`; `.WithError(descriptor)` carries code and kind. Both kinds can coexist.
- Minimal-API `AddValidation()` is an optional addition at the HTTP edge; the application pipeline is the one that always runs, and the HTTP layer maps both to the same problem shape.

## 5. The dispatcher

`IDispatcher` has one job: find the handler for a request and run the pipeline around it. YAF ships one implementation and no adapters for third-party mediators.

- **Built-in, source-generated.** The dispatcher-registration generator emits, per assembly, a switch from request type to handler and the `AddYafHandlers()` registration. No reflection per call, no runtime assembly scanning, no licence.
- **Why no mediator adapters.** The request contracts, the handler contract and the pipeline are YAF's own, so a third-party engine behind `IDispatcher` could contribute only the lookup of a handler, which is the generated switch. Its own behaviours, transactional middleware and outbox would have to stay switched off (ADR-0009).
- **The seam remains.** `IDispatcher` is a public interface and application code depends on nothing else. An app that must route through another engine registers its own `IDispatcher`; the guarantees below are then that implementation's to keep and to test.

Guarantees of the dispatcher (each has a test):

1. Exactly one handler per request type; a missing or duplicate handler fails at build time where the generator can see it, otherwise at startup, never at first use.
2. The YAF pipeline runs once per `SendAsync`, in the configured order, with true around-semantics (a middleware can catch, retry and short-circuit).
3. The handler and all middleware share the caller's DI scope.
4. Cancellation flows to the handler.
5. `StreamAsync` runs the query pipeline (including the `READ ONLY` transaction, held while the stream is enumerated) around an `IStreamQueryHandler<TQuery, TItem>`.

Domain events do not go through `IDispatcher`. `IDomainEventPublisher` (§6) is YAF's own in-process, sequential, in-transaction publisher.

## 6. Unit of work and domain events

```csharp
public interface IUnitOfWork
{
    ValueTask<TResult> ExecuteAsync<TResult>(Func<CancellationToken, ValueTask<TResult>> work, CancellationToken ct)
        where TResult : IResult;
}
```

`UnitOfWorkMiddleware` → `IUnitOfWork.ExecuteAsync`:

```
begin transaction (inside the store's retry strategy)
  tenant / actor settings for row-level security travel with the first command
  [Idempotent]: claim the key; if a result is stored for it → commit, return it (handler not run)
  result = handler()
  if result.IsFailure → rollback, return result
  loop (max depth 10):
      save changes                      // flush: writes, stamps, audit and business-event rows
      events = aggregate tracker . dequeue all
      if none → break
      stamp OccurredAt; for each event, for each handler (registration order): r = handle(event)
      if r.IsFailure → rollback, return r converted to TResult
  [Idempotent]: store the result under the key
commit
after commit: signal the outbox relay (best effort)
```

- **Flush, then dispatch.** Each round saves first and dispatches the events collected so far second, and repeats until a round raises nothing. A domain event handler that queries the store therefore sees the changes that raised the event, and a constraint violation surfaces next to the code that caused it. Everything is still one transaction: nothing is visible outside before the commit.
- Domain event handlers run **inside** the transaction; what they write commits with the command. That is what makes an outbox write atomic with the change that caused it.
- A handler that raises further events is supported; the depth limit turns a cycle into a `Failure` with a clear message.
- **One version per aggregate per transaction.** An aggregate gets its new version on the first flush that writes it and keeps that version through later flushes of the same transaction; the response's ETag is the committed one. The version is written on the **root** row whenever the aggregate is saved, also when only a child changed (`02-domain.md` §4, `05-persistence.md`).
- The aggregate tracker is fed by repositories (`05-persistence.md`): every aggregate added, loaded for change or removed is tracked for the scope.
- The same unit of work wraps job handlers, integration event handlers and process manager steps (`UnitOfWorkScope.RunAsync`), so they behave like commands.
- **Entry points that are not requests** (job handlers, integration event handlers, process manager steps, cron jobs) run a reduced, fixed pipeline supplied by their runtime: telemetry → exception boundary → timeout → input guard on the payload's strings (the same check as for commands; a payload that fails it is not retried but parked or dead-lettered) → unit of work. They run as the service's **system principal** (§7), not as the user who started the operation, so nothing is authorized on the strength of an envelope or a job row.
- **Entry points may dispatch, and only `[SystemOnly]` requests.** A consumer, a job or cron handler and a process manager step may send a request through `IDispatcher` to reuse a use case. The request goes through the full pipeline, so authorization runs: the actor is the system principal, which satisfies `[SystemOnly]` and nothing else. A `[UserScoped]` or `[RequiresPermission]` request sent from such an entry point is refused (`core.authorization.system_actor`). A request dispatched inside an entry point's unit of work joins that transaction rather than opening its own.
- **Idempotency has its own table.** `yaf_idempotency` (tenant, key, request name, payload hash, stored result, classification of the result, expiry) is separate from the inbox, which only records `(consumer, message id)`. The key is claimed with an insert inside the transaction, so two concurrent requests with one key serialise on it; the same key with a different payload hash is a `Conflict`. The stored result is protected like any classified data and removed after its retention (`Idempotency:Retention`, 24 hours by default). A service with an `[Idempotent]` request whose context does not include the table (`IIdempotencyDbContext`) fails at startup.
- A retried transaction re-runs the handler: handlers and event handlers MUST have no effect outside the transaction (no HTTP call, no direct publish). External calls go into a job.

## 7. Execution context

One immutable object replaces separate providers and custom headers:

```csharp
public interface IExecutionContext
{
    TenantId? TenantId { get; }
    Principal Actor { get; }                // who is acting, and whom authorization is decided on:
                                            // Anonymous | User(UserId, …) | Service(name) | System(reason)
    PrincipalRef? Initiator { get; }        // kind + id of the user who started the operation; audit only
    CorrelationId CorrelationId { get; }
    string? TraceParent { get; }            // W3C, from Activity.Current
    IReadOnlySet<TenantId> AdditionalTenants { get; }   // widened read scope, when granted
    string? IdempotencyKey { get; }
    string Origin { get; }                  // "http", "job", "integration-event", "cron", "process", "test"
}

public interface IExecutionContextAccessor { IExecutionContext Current { get; } }

internal interface IExecutionContextScopeFactory        // framework entry points only
{
    IDisposable Begin(ExecutionContextSeed seed);       // HTTP middleware, consumers, job runner, cron trigger, test host
}
```

- The context is established **once per scope** by the entry point and is read-only afterwards. Application code cannot create or replace one: `IExecutionContextScopeFactory` is internal to the framework (visible to YAF's own entry-point packages and to `YAF.Testing.Integration`), and analyzer `YAF4004` (error) flags creating or replacing an execution context outside framework entry points, including an own implementation of `IExecutionContext` or `IExecutionContextAccessor` in a service.

  > **Amended 2026-11-27**, by the owner's decisions of 2026-11-26 (`specs/0013-thin-slice/plan.md`, decision log Q2): the scope factory is visible to `YAF.Testing.Integration`, against `01` §2's "only the package's own test project". Until M4 replaces it with `TestAuth`, the test host begins each in-process request's scope with the seed a test sets by `ActAs`, without an authentication handler; a request without one gets no context and fails closed (`15-testing.md` §3.2).
- **Actor and initiator.** `Actor` is the principal that authorization, row-level security and rate limits are decided on. `Initiator` names the user on whose behalf asynchronous work happens; it grants nothing and is never consulted for a permission.

| Entry point | `Actor` | `Initiator` |
|---|---|---|
| HTTP request | The authenticated caller (user, service, or anonymous) | none |
| Integration event consumer | The consuming service's system principal | From the envelope attribute `yafinitiator`, when the producing operation had a user |
| Job, process manager step | The service's system principal | From the job row |
| Cron job | The service's system principal, `System("cron:<name>")` | none |

- Stamps (`CreatedBy`, `ModifiedBy`) record the initiator when there is one, otherwise the actor; audit rows record both. A handler of asynchronous work that needs the user's **present** rights asks `IPrivilegeSource` for the initiator explicitly; a user deactivated in the meantime then simply has none.
- A cron job that works tenant by tenant uses `CronContext.ForEachTenantAsync(tenants, async (tenant, ct) => …)`, which the framework runs with one scope per tenant under the cron's system principal. That is the only way application code obtains a context for another tenant besides elevation.
- Running without a context throws `MissingExecutionContextException`. `Principal.System("cron:purge-retention")` always carries a reason and is audited.
- `Principal` is one sealed class: `PrincipalKind Kind` (`Anonymous`, `User`, `Service`, `System`), `UserId? UserId`, `string? Name`, and the factories `Principal.Anonymous`, `Principal.User(UserId id)`, `Principal.Service(string name)` and `Principal.System(string reason)`; a name or reason is required. `PrincipalRef` is `sealed record PrincipalRef(PrincipalKind Kind, UserId Id)`.

  > **Amended 2026-11-27**, by the plan approved on 2026-11-26 (`specs/0013-thin-slice/plan.md`, decision log, the planner's rows): the shapes of `Principal` and `PrincipalRef`. Nested cases named `System` would hide the namespace, and `YAF2009` refuses a raw `Guid` id.
- Elevation is explicit and scoped: `await using (elevation.CrossTenant("reason"))` (`ITenantElevation`, injected) returns a scope that widens tenant visibility until disposed, requires the permission `yaf.cross-tenant` (or a `System` principal), writes a security event with the reason, and cannot be nested silently. This replaces a flag that had to be switched back by hand.
- Every outbox row, job row and message carries tenant, initiator (the producing operation's user, if any), correlation id and trace context. The consumer or runner builds its context from them as in the table above: tenant and correlation are taken over, the initiator is kept for audit, the actor is always its own system principal. The broker is thus trusted for the tenant id and the initiator, and a forged envelope cannot obtain a user's rights (`09-security.md`).

## 8. Authorization model

- A **capability** is a feature with permissions, declared in code:

```csharp
public sealed class Capabilities : ICapabilityCatalog
{
    public static readonly Feature Sessions = Feature.Define("sessions",
        Permission.Define("view"), Permission.Define("manage"), Permission.Define("publish", privileged: true));
}
```

- `[RequiresPermission("sessions", "publish")]` on the request; an analyzer (`YAF4003`) checks the pair exists in a catalogue of the same service. `privileged: true` marks permissions whose use is always a security event and may require step-up (`09-security.md`).
- `IPermissionEvaluator` answers "does this principal hold this permission in this tenant", backed by the app's `IPrivilegeSource` (the one required extension point: where grants live). Results are cached per principal for a short, configurable time with stale-while-error bounds, with two limits: a permission declared `privileged: true` is **never** answered from the cache (the source is asked on every such request, so revoking a dangerous right takes effect at once), and `IPrivilegeCacheInvalidator.Evict(userId)` lets the app drop a user's entry when it changes their grants.
- **Resource-level checks** (is this caller allowed to touch *this* registration): `IResourceAuthorizer<TResource>` invoked by the handler, returning `Result`. The framework gives the hook and the tests; the rule is the service's.
- `GET /capabilities` publishes the catalogue; collecting catalogues across services is the app's business.

## 9. Process managers

For a business process that spans several messages and takes time (a registration that reserves a seat in another context, waits for confirmation, and times out).

```csharp
public sealed class RegistrationProcess : ProcessManager<RegistrationProcess.State>,
    IStartedBy<RegistrationSubmitted>, IHandles<SeatReserved>, IHandles<SeatReservationRejected>,
    IHandlesTimeout<ReservationTimedOut>
{
    public sealed class State : ProcessState { public Guid RegistrationId; public string Step = "new"; }

    protected override void Correlate(ICorrelationBuilder c) => c
        .On<RegistrationSubmitted>(e => e.RegistrationId)
        .On<SeatReserved>(e => e.RegistrationId)
        .On<SeatReservationRejected>(e => e.RegistrationId);

    public ValueTask<Result> HandleAsync(RegistrationSubmitted e, IProcessContext ctx, CancellationToken ct)
    {
        Data.RegistrationId = e.RegistrationId; Data.Step = "awaiting-seat";
        ctx.Publish(new ReserveSeatRequestedV1(e.SessionId, e.RegistrationId));      // via outbox
        ctx.RequestTimeout(new ReservationTimedOut(e.RegistrationId), TimeSpan.FromMinutes(10)); // via jobs
        return Result.Success();
    }
    // …
}
```

- State is persisted in `yaf_process_state` (process type, correlation key, JSON data, status, version, tenant) through `IProcessStateStore`; loaded with a row lock, saved in the same transaction as the outbox writes and the inbox record of the triggering message.
- Inputs are domain events (same context), integration events (other contexts) and timeouts (jobs). Outputs are commands sent through the dispatcher, integration events through the outbox, and timeouts.
- `IProcessContext`: `Publish(integrationEvent)`, `Send(command)` (queued as a job that dispatches the command under the service's system principal, so the command MUST be `[SystemOnly]`; the job row commits with the state), `RequestTimeout(message, delay)`, `Complete()`.
- `ctx.Complete()` ends the process; a message for a completed or unknown process is acknowledged and logged at Information, except `IStartedBy` messages, which create it.
- Compensation is explicit code in the handler of the failure or timeout message; the framework provides no automatic rollback.
- A process manager holds no business rules; it routes. Decisions stay in aggregates.
- Process state may hold personal data only if classified; it is covered by retention and erasure like any memento.

## 10. Ports declared here

`IIntegrationEventPublisher` (writes to the outbox; `06-messaging.md`), `IJobScheduler` (`07-jobs-and-scheduling.md`), `IBusinessEventRecorder`, `ISecurityEventRecorder`, `IObjectStore`, `IFieldProtector` / `IKeyProvider`, `IMarkupSanitizer` (implemented by `YAF.Markup.HtmlSanitizer`), `IPrivilegeSource` and `IPrivilegeCacheInvalidator`, `INotificationBroadcaster` (server-sent events), `IFeatureFlags` (thin wrapper so `Microsoft.FeatureManagement` stays optional), `IClock` is **not** declared — use `TimeProvider`.

## 11. Acceptance criteria

- Tests for the five dispatcher guarantees of §5, including a missing and a duplicate handler.
- Tests for each default middleware, including: undeclared request refused; a control character or an unpaired surrogate in a command string fails with `Input.InvalidCharacters` and text such as `a < b` reaches the handler unchanged; a `[Markup]` member is sanitized, and a service with one and no sanitizer does not start; failed result rolls back and nothing is published; a domain event handler's failure rolls back the command; a domain event handler's query sees the change that raised the event; event cascade depth limit; idempotent replay returns the stored result without running the handler, two concurrent requests with one key run the handler once, and the same key with another payload is a conflict; retried transaction does not duplicate outbox rows.
- Query tests: a write attempted in a query handler is rejected by the store in every environment; an asynchronous validation rule reads under the caller's tenant.
- Concurrency test: two commands that each add a child to the same aggregate cannot both commit.
- Context tests: a consumer and a job run as the system principal with the initiator recorded in stamps and audit; a `[UserScoped]` or `[RequiresPermission]` request dispatched from them is refused and a `[SystemOnly]` one runs; `YAF4004` fires on a service that creates a context; a `privileged` permission is checked against the source on every request and `Evict` drops a cached entry.
- The built-in dispatcher adds less than 100 ns and zero allocations per call beyond the pipeline's own (benchmark, informational).
- Process manager tests: start, correlate, timeout, completion, duplicate message (inbox), concurrent messages for one process (serialised by the row lock).
