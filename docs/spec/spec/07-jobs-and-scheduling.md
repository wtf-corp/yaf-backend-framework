# 07 — Jobs and scheduling

## 1. Jobs

A job is deferred work inside one bounded context: it is stored in the service's database in the scheduling transaction and executed later by the job runner, with retries.

```csharp
[Job("catalog.send-session-reminder")]                // stable name on the row; renames are safe
public sealed record SendSessionReminder(Guid SessionId) : IJob;

internal sealed class SendSessionReminderHandler(...) : IJobHandler<SendSessionReminder>
{
    public async ValueTask<Result> HandleAsync(SendSessionReminder job, JobContext ctx, CancellationToken ct) { … }
}

public interface IJobScheduler
{
    void Schedule<T>(T job, JobOptions? options = null) where T : IJob;          // inside the current unit of work
    ValueTask ScheduleDetachedAsync<T>(T job, JobOptions? options, CancellationToken ct) where T : IJob;  // own transaction
}
public sealed record JobOptions(DateTimeOffset? NotBefore = null, int? MaxAttempts = null, string? Queue = null,
                                string? DeduplicationKey = null, TimeSpan? Timeout = null);
```

- `Schedule` adds a row to `yaf_jobs` in the current unit of work: a failed command schedules nothing.
- The row captures the tenant, the correlation id, the trace context and the **initiator**: the user who scheduled the job (or the initiator of the work that scheduled it), when there is one. A job runs in the tenant it was scheduled in, origin `job`, with `Actor` = the service's system principal and `Initiator` = the stored reference. It never runs *as* the user: the user may have lost their rights since, and the initiator grants nothing. A job never sets its own context. To schedule for another tenant, a cron job uses `CronContext.ForEachTenantAsync` (§3); application code cannot create an execution context (`IExecutionContextScopeFactory` is internal to the framework; analyzer `YAF4004`).
- A job handler is an entry point: it may dispatch requests through `IDispatcher`, and then only `[SystemOnly]` ones. A handler that needs the initiating user's present rights asks `IPrivilegeSource` for that user explicitly.
- `DeduplicationKey`: a second schedule with the same key while one is pending is a no-op (unique partial index).
- Payloads are JSON, classified like any contract; `[SecretData]` members are refused. Keep payloads to ids. Strings in a payload pass the same check as command input (control characters and invalid Unicode are refused at scheduling; nothing is rewritten). A payload with classified members is encrypted at rest with key-ring purpose `messaging`.
- `yaf_jobs` carries `tenant_id` and is under row-level security; the runner works under the system principal with an audited standing elevation, since it claims rows of every tenant.

`yaf_jobs`: `id`, `tenant_id`, `name`, `queue`, `payload`, context columns (initiator, correlation id, trace context), `not_before`, `attempts`, `max_attempts`, `claimed_by`, `claimed_until`, `last_error`, `dedup_key`, `created_at`, `completed_at`, `status` (`pending|running|succeeded|failed|parked`).

### 1.1 The runner

`JobRunner` (hosted service per queue; default queue `default`, plus `cron` and `maintenance`):

- Claims with `FOR UPDATE SKIP LOCKED` (as the outbox does), bounded by `Jobs:<queue>:MaxConcurrency`.
- Runs the handler in its own scope, rebuilt execution context and unit of work (domain events, outbox writes and the job's completion commit together).
- Extends `claimed_until` (heartbeat) while the handler runs; a crashed instance's jobs become claimable when the claim expires.
- On failure: `attempts + 1`, backoff with jitter; after `max_attempts` (default 10) → `parked`, event `yaf.jobs.parked`, health degraded. `IJobAdmin` lists, requeues and cancels.
- A handler can return `JobErrors.Retry(after)` or `JobErrors.Abandon` to control the outcome explicitly.
- Succeeded rows are kept for `Jobs:Retention` (default 3 days) for diagnosis, then purged.
- Wake-up and metrics mirror the outbox (`yaf.jobs.pending`, `yaf.jobs.lag`, `yaf.jobs.duration`, `yaf.jobs.failed`, `yaf.jobs.parked`), including the rule for poolers: `LISTEN/NOTIFY` is used only on a session-mode connection (`ConnectionStrings:<name>-direct` when the main one goes through a transaction-mode pooler), with polling as the fallback.

Handlers must be idempotent: a job may run more than once (claim expiry while still running). The `JobContext` exposes `Attempt` and `JobId`; using `JobId` as an idempotency key for external calls is the documented pattern.

## 2. Delays and timeouts

`JobOptions.NotBefore` is the framework's portable delay. It also backs process manager timeouts and long message delays on transports without native support.

`QuietHours` (in `YAF.Application.Scheduling`) computes "not before" outside a configured window in a configured IANA time zone (`QuietHoursOptions`: `Start`, `End`, `TimeZone` — no default zone; the option is required when the feature is used).

## 3. Cron jobs

```csharp
[CronJob("catalog.close-finished-sessions")]
internal sealed class CloseFinishedSessions(...) : ICronJob
{
    public async ValueTask<Result> ExecuteAsync(CronContext ctx, CancellationToken ct) { … }
}
```

```json
{ "Cron": { "TimeZone": "UTC", "Jobs": {
    "catalog.close-finished-sessions": { "Schedule": "0 */15 * * * *", "Enabled": true },
    "yaf.retention.purge": "0 0 3 * * *" } } }
```

- A cron job is a **trigger**: on each tick (default every 30 s) every instance computes due occurrences per job and tries to insert `(job_name, scheduled_for)` into `yaf_cron_occurrences` together with a `yaf_jobs` row (queue `cron`) in one transaction. The unique index picks one winner; no leader election.
- Catch-up: occurrences missed while no instance ran are collapsed to the latest one within `Cron:CatchUpWindow` (default 1 h); older ones are recorded as skipped.
- A cron job runs with `Principal.System("cron:<name>")` and **no tenant**. Tenant-owned data is invisible to it (filters and row-level security fail closed) unless it explicitly elevates. The documented pattern is fan-out: under a cross-tenant elevation, list the tenants or work items, then call `CronContext.ForEachTenantAsync(tenantIds, schedule)`, which runs the `schedule` delegate once per tenant inside that tenant's execution context (still as the cron job's system principal) so that it can `Schedule` an ordinary job there. This is the only way application code reaches another tenant's context; the factory behind it is not public.
- A cron job, like a job handler, may dispatch `[SystemOnly]` requests.
- Startup validation: every configured name has a registered job, every registered job has configuration or `[CronJob(DefaultSchedule = …)]`, every expression parses. Expressions are parsed and their occurrences computed by Cronos (five- and six-field expressions, `*`, lists, ranges, steps, `L`, `W`, `#`, `?`; daylight-saving transitions handled in the configured zone). The library is referenced only by `YAF.Infrastructure` and hidden behind YAF's `CronSchedule` type, so no application code sees it.
- Framework cron jobs: `yaf.outbox.purge`, `yaf.inbox.purge`, `yaf.jobs.purge`, `yaf.idempotency.purge`, `yaf.audit.verify`, `yaf.retention.purge`, `yaf.reencrypt`. Each is registered by the feature that needs it and can be rescheduled or disabled in configuration.

## 4. Singleton background work

`ISingletonWorker` runs a loop on exactly one instance at a time, with automatic takeover; used sparingly (the relay and runners do not need it; the audit chain sealer of `05-persistence.md` does). The guard is a session-level PostgreSQL advisory lock, which needs a session-mode connection: the worker takes it on the direct connection string (`ConnectionStrings:<name>-direct`) when the main one goes through a transaction-mode pooler. When no session-mode connection is configured, the guard is a lease row in `yaf_leases` (owner, expiry, renewed on each heartbeat; takeover after expiry) instead. A worker's work must tolerate a brief overlap at takeover in lease mode, so it is written to be idempotent.

Every hosted loop in the framework reports a **heartbeat** (`IHeartbeatRegistry`); the liveness check fails when a loop has not ticked for three intervals, so a stuck loop restarts the instance.

## 5. Acceptance criteria

- Scheduling inside a rolled-back command leaves no row; in the handler the tenant and correlation id are those of the scheduling request, `Actor` is the system principal and `Initiator` the scheduling user; a job scheduled by a user who has since lost a permission still runs, and a `[UserScoped]` request dispatched from it is refused; dedup key suppresses a duplicate.
- Two runners never run one job concurrently; a killed runner's job is retried after claim expiry; backoff and parking.
- Cron: with three instances an occurrence produces exactly one job; catch-up collapse; invalid configuration fails startup; a cron job without elevation reads zero tenant rows; fan-out schedules one job per tenant under that tenant's context; occurrences across a daylight-saving change in a configured zone match a table of expected instants.
- Singleton worker: with three instances exactly one runs the loop, in advisory-lock mode and in lease mode; another takes over after the holder is killed.
- Heartbeat: a blocked loop turns liveness unhealthy.
