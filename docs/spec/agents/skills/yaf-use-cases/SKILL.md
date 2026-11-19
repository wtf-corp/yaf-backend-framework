---
name: yaf-use-cases
description: How to write YAF application-layer use cases - commands, queries, handlers, validators, authorization markers, pipeline middleware, unit-of-work rules, domain event handlers, the execution context and idempotency. Load BEFORE creating or changing a command, query, handler, validator, request middleware or domain event handler; before adding a folder under a service's Application project; before injecting IDispatcher, a DbContext or a transport anywhere; and before changing the pipeline order.
license: MIT
metadata:
  owner: yaf
  version: "1"
---

# YAF use cases

A use case is one folder: request record, handler, validator, response. Cross-cutting behaviour belongs to the pipeline, not to handlers.

## When to load

- Adding or changing a command, query, handler or validator.
- Reacting to a domain event, or publishing an integration event from one.
- Writing or ordering request middleware.
- Reading tenant, actor, initiator or correlation id in application code.
- Making a `POST` safely repeatable.

## Where does this go?

First match wins.

| The code… | Goes in |
|---|---|
| Enforces an invariant that depends on aggregate state | The aggregate (`yaf-domain-modeling`) |
| Checks shape or plausibility of input | `<UseCase>Validator : Validator<TRequest>` |
| Changes state | `ICommand<TResult>` + handler in `Application/<Aggregates>/<UseCase>/` |
| Only reads | `IQuery<TResult>` + handler using a read-model interface |
| Reacts inside the same transaction to something that happened | `IDomainEventHandler<TEvent>` in `Application/<Aggregates>/EventHandlers/` |
| Calls an external system, or may take long | A job scheduled from the handler (`yaf-jobs-and-processes`) |
| Applies to many requests (timing, tenancy, quotas) | `IRequestMiddleware<TRequest, TResult>` |
| Decides whether *this* caller may touch *this* resource | `IResourceAuthorizer<TResource>` called by the handler |
| Binds HTTP, chooses status codes | The endpoint (`yaf-http-endpoints`) |

## Rules

1. **REQUIRED** Requests are immutable records implementing `ICommand<TResult>` or `IQuery<TResult>`; `TResult` is `Result` or `Result<T>` (YAF4001).
2. **REQUIRED** Every request type carries exactly one access marker: `[RequiresPermission(feature, permission)]`, `[UserScoped]`, `[AllowAnonymousRequest]` or `[SystemOnly]`. An unmarked request is refused at run time (`core.authorization.undeclared`); the pair must exist in the service's `ICapabilityCatalog` (YAF4003). A request sent by a consumer, job, cron job or process manager is `[SystemOnly]`; `[UserScoped]` requests cannot be dispatched from them.
3. **REQUIRED** Handlers are `internal sealed`, do one thing (load → domain → repository → return), and are registered by the generated `AddYafHandlers()`; no assembly scanning.
4. **REQUIRED** A request handler or domain event handler never injects `IDispatcher` (YAF4002), never opens a transaction, never calls `SaveChanges`, never touches a transport (YAF6001). Entry points (endpoints, consumers, job, cron and process-manager handlers) are where requests are dispatched.
5. **REQUIRED** Handlers and domain event handlers have **no effect outside the transaction**: no HTTP call, no email, no direct publish. The unit of work may re-run them. Schedule a job instead.
6. **REQUIRED** Queries never write. They use a read-model interface returning DTOs. Each query runs in one `READ ONLY` transaction (`QueryTransactionMiddleware`), so a write fails in every environment.
7. **REQUIRED** Validators use `Validator<T>` rules, each with an `ErrorDescriptor`; no inline messages. An asynchronous rule that reads the store runs in a short read-only transaction the context opens for it; it sees committed data only.
8. **REQUIRED** Input is checked, never rewritten. `InputGuardMiddleware` rejects control characters (other than tab, CR, LF) and invalid Unicode in any string of a command with `Input.InvalidCharacters`. Do not strip, trim or "clean" HTML in a handler or validator. A member that holds HTML is marked `[Markup]` and passes through `IMarkupSanitizer`; the service then references `YAF.Markup.HtmlSanitizer` (startup fails without a registered sanitizer).
9. **REQUIRED** Domain event handlers return `Result`; a failure rolls the whole command back. They run after the changes that raised the event were flushed, inside the same transaction, so their queries see those changes. Integration events are published only through `IIntegrationEventPublisher.Publish` (outbox).
10. **REQUIRED** Read tenant, actor, initiator and correlation id from `IExecutionContextAccessor.Current`. `Actor` is the principal authorization is decided on; `Initiator` (the user who started an operation that now runs asynchronously) is for audit and correlation only, never for a permission decision. Application code never creates or replaces the context (YAF4004); entry points create it once.
11. **REQUIRED** Cross-tenant work uses `await using (elevation.CrossTenant("reason"))`; it is audited and needs the `yaf.cross-tenant` permission or a system principal.
12. **REQUIRED** Middleware must be safe to run twice and must not swallow a failed result.
13. **REQUIRED** A pipeline change updates `pipeline.approved.txt` deliberately.
14. **RECOMMENDED** Mark repeatable `POST` commands `[Idempotent]`; the key is claimed and the result stored in `yaf_idempotency` inside the command's transaction, and replayed for the same idempotency key.
15. **RECOMMENDED** Group use-case folders by aggregate (`Sessions/PublishSession/`), not by `Commands/` and `Queries/`.
16. **RECOMMENDED** Add `[LogPayload]` only when every member of the request is classified and the payload is worth reading in a log.

## Default pipeline (outermost first)

Commands: `TelemetryMiddleware` → `ExceptionBoundaryMiddleware` → `TimeoutMiddleware` → `AuthorizationMiddleware` → `InputGuardMiddleware` → `ValidationMiddleware` → `UnitOfWorkMiddleware` → `IdempotencyMiddleware` → handler. Queries: the same up to `AuthorizationMiddleware`, then `ValidationMiddleware` → `QueryTransactionMiddleware` → handler.

Inside the unit of work, in one transaction: handler → save → dispatch the collected domain events → save and dispatch again until no events remain (depth limit 10) → commit. Business events, audit rows, outbox rows and scheduled jobs are written by those saves. An aggregate gets its new version on the first save of the transaction and keeps it.

## How to

1. Handler test first, with hand-written fakes of the repository ports.
2. Request record with its access marker; add the permission to `Capabilities` if new.
3. Validator (if the request has input) with its `Errors`.
4. Handler.
5. API integration test once an endpoint exists.

```csharp
[RequiresPermission("sessions", "publish")]
public sealed record PublishSessionCommand(SessionId SessionId) : ICommand<Result>;

internal sealed class PublishSessionHandler(ISessionRepository sessions)
    : IRequestHandler<PublishSessionCommand, Result>
{
    public async ValueTask<Result> HandleAsync(PublishSessionCommand cmd, CancellationToken ct)
    {
        var session = await sessions.FindAsync(cmd.SessionId, ct);
        if (session is null) return Session.Errors.NotFound;

        var published = session.Publish();
        if (published.IsFailure) return published;

        await sessions.UpdateAsync(session, ct);     // no SaveChanges: the unit of work saves and commits
        return Result.Success();
    }
}
```

More (validator, query with read model, event handler publishing an integration event, custom middleware, tests): `references/examples.md`.

## Anti-patterns

| Wrong | Why | Instead |
|---|---|---|
| Request without an access marker | Refused at run time; a hole if it were not | Add the marker; update the catalogue |
| `await dispatcher.SendAsync(new OtherCommand())` in a request handler | Nested pipelines and transactions (YAF4002) | Domain event, or compose in the entry point |
| `await db.SaveChangesAsync()` in a handler or repository | Bypasses the unit of work's save-and-dispatch loop | Return; the unit of work saves |
| `cmd.Notes.Replace("<", "")` or an HTML strip "to be safe" | Silently changes what the user typed | Nothing; `[Markup]` on a member that really holds HTML |
| Checking `context.Current.Initiator` to decide access | The initiator grants nothing | Authorize on `Actor`; ask `IPrivilegeSource` explicitly when a job needs the user's present rights |
| `await httpClient.PostAsync(...)` in a handler | Runs again on retry, survives a rollback | `IJobScheduler.Schedule(...)` |
| Query handler loading aggregates through a repository | Tracks, over-fetches, may write | Read model with `AsNoTracking` projections |
| `if (cmd.Title.Length > 200)` in the handler | Input shape belongs to the validator | `Rule(x => x.Title).MaxLength(200, Errors.TitleTooLong)` |
| Validator loading the aggregate to check state | Duplicates and races the invariant | Check in the aggregate behaviour |
| `tenantId` parameter threaded through from the endpoint | Bypasses the validated context | `IExecutionContextAccessor.Current.TenantId` |
| Middleware catching everything and returning a `Failure` | Hides bugs; breaks the boundary | Let exceptions pass; only map results |

## Verify

- `dotnet build YAF.slnx` (YAF4001–YAF4004, YAF6001).
- `dotnet test --project <service>/tests/Application.Tests`.
- `pipeline.approved.txt`, `errors.approved.txt` and the authorization inventory approved file are unchanged or deliberately updated.
- A test proves the failure path rolls back (no rows, no outbox message) for any new command with side effects.

## References

- `docs/articles/application.md` — contracts, pipeline, unit of work, execution context.
- `references/examples.md` — read before writing your first use case of each kind.
- `samples/conference/` Catalog `Application/Sessions/` — one folder per use case.
- `adr/0009-application-pipeline-and-dispatcher.md` — read before reordering middleware or putting another engine behind `IDispatcher`.
- Skills: `yaf-results-and-errors`, `yaf-messaging`, `yaf-security`.
