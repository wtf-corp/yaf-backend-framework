---
name: yaf-jobs-and-processes
description: Deferred and long-running work in a YAF service - jobs and IJobScheduler, delays and quiet hours, cron jobs and tenant fan-out, process managers (correlation, timeouts, compensation), singleton workers and heartbeats. Load BEFORE adding an IJob, IJobHandler, ICronJob or ProcessManager; before calling an external system from a handler; before adding a hosted service, a timer or a background loop; and before changing Jobs or Cron configuration.
license: MIT
metadata:
  owner: yaf
  version: "1"
---

# YAF jobs, cron and process managers

Work that cannot or should not finish inside the request goes into the service's own database first and runs later, with retries, as the service's system principal in the tenant of whoever caused it, with that user recorded as the initiator.

## When to load

- A handler needs to call an external system, send a notification, or do slow work.
- Something must happen later, or on a schedule.
- A business process spans several messages and can time out.
- You are about to write `BackgroundService`, `Task.Run`, `Timer` or `Task.Delay` in a service.

## Where does this go?

First match wins.

| Need | Use |
|---|---|
| Do X once, soon, same service, with retries | `IJob` + `IJobHandler<T>`, `IJobScheduler.Schedule(job)` |
| Do X at or after a time | The same, with `JobOptions.NotBefore` |
| Tell another context | Integration event (`yaf-messaging`) |
| Do X every night / every 15 minutes | `ICronJob` + `Cron:Jobs:<name>` |
| The scheduled work touches tenant data | Cron job that **fans out** one ordinary job per tenant |
| Several messages, waiting, timeouts, compensation | `ProcessManager<TState>` |
| Exactly one instance must run a loop | `ISingletonWorker` (rare) |
| A custom hosted loop | Last resort; it must report a heartbeat |

## Rules

Jobs

1. **REQUIRED** Jobs are records with a stable `[Job("<service>.<name>")]`; payloads are ids and small values, classified like any contract; no `[SecretData]`.
2. **REQUIRED** Schedule with `IJobScheduler.Schedule` inside the current unit of work so a failed command schedules nothing. Use `ScheduleDetachedAsync` only outside any unit of work.
3. **REQUIRED** A job runs in the tenant it was scheduled in, as the service's system principal (`Actor`); the scheduling user is the `Initiator`, for stamps, audit and correlation only. Never set tenant or principal inside a handler (YAF4004), and never decide access on the initiator: if the work depends on what the user may do **now**, ask `IPrivilegeSource` for the initiator explicitly. A job handler may dispatch requests through `IDispatcher`, and only `[SystemOnly]` ones.
4. **REQUIRED** Handlers are idempotent: a job can run more than once. Use `JobContext.JobId` as the idempotency key for external calls.
5. **REQUIRED** Handlers return `Result`; use `JobErrors.Retry(after)` / `JobErrors.Abandon` to control the outcome explicitly. Do not catch-and-swallow.
6. **RECOMMENDED** Use `JobOptions.DeduplicationKey` when the same work may be requested repeatedly.
7. **RECOMMENDED** Put slow or bursty work on its own queue (`JobOptions.Queue`) with its own `MaxConcurrency`.

Cron

8. **REQUIRED** A cron job is a trigger with `[CronJob("<service>.<name>")]`; it runs as `Principal.System("cron:<name>")` with **no tenant** and sees no tenant data unless it elevates.
9. **REQUIRED** Tenant work fans out: elevate, list tenants or work items, then `CronContext.ForEachTenantAsync(tenantIds, schedule)` so each job runs under its tenant. That method is the only way application code starts work in another tenant's context; the context factory is internal to the framework.
10. **REQUIRED** Every registered cron job has configuration or `DefaultSchedule`; every configured name has a job (startup check). Do not rely on in-process timers for schedules. Expressions are parsed by Cronos behind YAF's schedule type (five fields, or six with seconds); do not reference Cronos or write a parser in a service.
11. **REQUIRED** Cron handlers are short. Long work is a job they schedule.

Process managers

12. **REQUIRED** A process manager routes; it holds no business rules. Decisions stay in aggregates.
13. **REQUIRED** Every handled message type is correlated in `Correlate(...)`; outputs go through `IProcessContext` (`Publish`, `Send`, `RequestTimeout`, `Complete`), never directly. A command passed to `Send` is `[SystemOnly]`.
14. **REQUIRED** Every waiting step has a timeout, and the timeout handler compensates explicitly. There is no automatic rollback.
15. **REQUIRED** Process state members are classified; personal data in state falls under retention and erasure.
16. **REQUIRED** Handlers tolerate duplicates and late messages (a message for a completed process is acknowledged and ignored).

Loops

17. **REQUIRED** Any hosted loop registers with `IHeartbeatRegistry`; no `Task.Run` fire-and-forget, no `Task.Delay` polling in application code.
18. **REQUIRED** `ISingletonWorker` and the runners' wake-up use session advisory locks and `LISTEN/NOTIFY`, which need a session-mode connection. Behind a transaction-mode pooler configure `ConnectionStrings:<name>-direct`; without it they fall back to a lease row (`yaf_leases`) and polling. Do not take advisory locks yourself.

## How to

Job:

1. Test: command succeeds → one `yaf_jobs` row with the caller's tenant and the caller as initiator; command fails → none. Handler test with fakes.
2. Job record, handler, `Schedule` call.

```csharp
[Job("registrations.send-confirmation")]
public sealed record SendConfirmation(Guid RegistrationId) : IJob;

internal sealed class SendConfirmationHandler(IRegistrationReadModel registrations, IMailGateway mail)
    : IJobHandler<SendConfirmation>
{
    public async ValueTask<Result> HandleAsync(SendConfirmation job, JobContext ctx, CancellationToken ct)
    {
        var r = await registrations.GetForMailAsync(job.RegistrationId, ct);
        if (r is null) return JobErrors.Abandon;
        return await mail.SendAsync(r.ToMessage(), idempotencyKey: ctx.JobId.ToString(), ct);
    }
}
```

Cron fan-out:

```csharp
[CronJob("catalog.close-finished-sessions", DefaultSchedule = "0 */15 * * * *")]
internal sealed class CloseFinishedSessions(ISessionReadModel sessions, ITenantElevation elevation, TimeProvider time) : ICronJob
{
    public async ValueTask<Result> ExecuteAsync(CronContext ctx, CancellationToken ct)
    {
        IReadOnlyList<TenantId> tenants;
        await using (elevation.CrossTenant("cron: find tenants with finished sessions"))
            tenants = await sessions.TenantsWithFinishedSessionsAsync(time.GetUtcNow(), ct);

        await ctx.ForEachTenantAsync(tenants, scheduler => scheduler.Schedule(new CloseFinishedSessionsForTenant()));
        return Result.Success();
    }
}
```

Process manager skeleton:

```csharp
public sealed class RegistrationProcess : ProcessManager<RegistrationProcess.State>,
    IStartedBy<RegistrationSubmitted>, IHandles<SeatReservedV1>, IHandles<SeatReservationRejectedV1>,
    IHandlesTimeout<ReservationTimedOut>
{
    protected override void Correlate(ICorrelationBuilder c) => c
        .On<RegistrationSubmitted>(e => e.RegistrationId.Value)
        .On<SeatReservedV1>(e => e.RegistrationId)
        .On<SeatReservationRejectedV1>(e => e.RegistrationId);

    public ValueTask<Result> HandleAsync(ReservationTimedOut t, IProcessContext ctx, CancellationToken ct)
    {
        ctx.Send(new RejectRegistrationCommand(RegistrationId.From(Data.RegistrationId), reason: "timeout"));   // a [SystemOnly] command
        ctx.Complete();
        return ValueTask.FromResult(Result.Success());
    }
    // other handlers…
}
```

## Anti-patterns

| Wrong | Why | Instead |
|---|---|---|
| `_ = Task.Run(() => SendMailAsync())` in a handler | Lost on crash, no context, no retry | Schedule a job |
| HTTP call inside the command handler | Re-runs on retry; outlives a rollback | Job with an idempotency key |
| Setting the tenant inside a job handler | The job already runs in its scheduler's tenant | Schedule under the right context |
| Sending a `[RequiresPermission]` command from a job "as the user" | The job's actor is the system principal; the user's rights may have changed since | A `[SystemOnly]` command; check `IPrivilegeSource` for the initiator when it matters |
| Cron job querying tenant tables directly | Sees nothing (fail closed), or everything under elevation | Fan out per tenant |
| `PeriodicTimer` for a nightly task | Runs on every instance, skips on restart | `ICronJob` |
| Business decisions in a process manager | Untested rules outside the aggregate | Send a command; the aggregate decides |
| Waiting step without a timeout | The process hangs forever | `ctx.RequestTimeout(...)` + compensation |
| Large object graphs in a job payload | Stale data, personal data at rest in the queue | Ids only |
| `catch { }` in a job handler | Hides failures; job "succeeds" | Return the failed result |

## Verify

- `dotnet test --project <service>/tests/Application.Tests` (handlers, process manager steps).
- Integration tests: rollback schedules nothing; tenant and initiator restored in the handler with the system principal as actor; cron with several hosts triggers once; fan-out schedules one job per tenant under that tenant; process start/correlate/timeout/duplicate.
- Startup check passes with your `Cron` configuration (`dotnet run --project <Api> -- --yaf-check`).
- No parked jobs after the test run.

## References

- `docs/articles/jobs.md`, `docs/articles/cron-jobs.md`, `docs/articles/process-managers.md`.
- `adr/0013-jobs-cron-and-process-managers.md`.
- `samples/conference/` Registrations `Application/Registrations/Processes/RegistrationProcess.cs`, `Jobs/`; Catalog `CronJobs/`.
- Skills: `yaf-messaging`, `yaf-use-cases`, `yaf-testing` (testing background work).
