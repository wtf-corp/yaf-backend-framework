# ADR-0013 — Jobs, cron and process managers

> **Status** Accepted · **Date** 2026-11 · **Deciders** maintainers

## Context

A service needs deferred work inside its own context (send a reminder, call an external system), work on a schedule, and coordination of business processes that span several messages and take time. All three must be reliable across restarts and instance counts from zero to many, and must respect tenant isolation.

## Drivers

- Scheduling is atomic with the command that schedules.
- Runs once per occurrence with any number of instances, without a coordination service.
- A job runs in the tenant it was scheduled in, as the service, and remembers who started it; scheduled work has no implicit tenant.
- No extra infrastructure beyond the service's PostgreSQL database.
- Multi-step processes are explicit, testable and persisted.

## Options considered

| Option | For | Against |
|---|---|---|
| External scheduler / job server library | Dashboards, features | Another store or schema, another retry and context model, often a licence |
| Broker delays for deferred work | No table | Delay limits differ per broker; not transactional with the command |
| Leader election for cron | Familiar | Extra moving part; fails awkwardly at scale-to-zero |
| Saga feature of a messaging framework | Ready made | Brings its own bus and persistence (see ADR-0012) |
| **Jobs table + runner; cron as a trigger; process managers on outbox, inbox and jobs** | One mechanism, transactional, portable | We own the runner |
| Own cron expression parser | No dependency | Time zones and daylight-saving transitions are where own parsers fail; a permissively licensed library without dependencies (Cronos) already gets them right |
| A job runs as the user who scheduled it | Per-user checks in deferred work | The user's rights may have been revoked by the time it runs; a job row would be a stored right to act as someone |

## Decision

**Jobs.** A job is a record with `[Job("<stable name>")]` handled by `IJobHandler<T>`. `IJobScheduler.Schedule` adds a row to `yaf_jobs` in the current unit of work, capturing tenant, initiator (the user who scheduled it, when there is one), correlation and trace context. `yaf_jobs` carries `tenant_id` and is under row-level security; a payload with classified members is encrypted at rest with key-ring purpose `messaging`. `JobRunner` (one per queue: `default`, `cron`, `maintenance`) works under the system principal with an audited standing elevation, claims with `FOR UPDATE SKIP LOCKED`, builds the context (origin `job`, `Actor` = the service's system principal, `Initiator` = the stored reference, the stored tenant), runs the handler in its own unit of work, heartbeats the claim, retries with backoff and jitter, and parks after `max_attempts`. `NotBefore` is the framework's portable delay; `DeduplicationKey` suppresses a pending duplicate. Handlers must be idempotent; `JobContext.JobId` is the documented idempotency key for external calls. Job, cron and process-manager handlers are entry points: they may dispatch requests through `IDispatcher`, and only `[SystemOnly]` ones.

**Cron.** A cron job is a trigger. On each tick every instance computes due occurrences and tries to insert `(job_name, scheduled_for)` into `yaf_cron_occurrences` together with a job row in one transaction. The unique index picks one winner; there is no leader election. Missed occurrences inside `Cron:CatchUpWindow` collapse to the latest. Expressions (five and six fields) are parsed, and occurrences computed in the configured time zone, by Cronos, referenced only by `YAF.Infrastructure` and hidden behind YAF's `CronSchedule` type. Configuration is validated at startup against registered jobs.

**Cron has no tenant.** It runs as `Principal.System("cron:<name>")`; tenant data is invisible (filters and row-level security fail closed) unless it elevates explicitly. The pattern is fan-out: under a cross-tenant elevation list tenants or work items, then schedule one ordinary job per tenant through `CronContext.ForEachTenantAsync`, which runs the given delegate inside each tenant's execution context. That method is the only way application code reaches another tenant's context: `IExecutionContextScopeFactory` is internal to the framework, and analyzer `YAF4004` flags any other creation or replacement of an execution context.

**Process managers.** `ProcessManager<TState>` with `IStartedBy<T>`, `IHandles<T>`, `IHandlesTimeout<T>` and a correlation map. State lives in `yaf_process_state`, loaded with a row lock and saved in the same transaction as the triggering message's inbox record and any outbox writes. Outputs are commands through the dispatcher, integration events through the outbox and timeouts through jobs. Compensation is explicit code in the handler of the failure or timeout message. A process manager routes; business rules stay in aggregates.

**Heartbeats.** Every hosted loop registers with `IHeartbeatRegistry`; liveness fails when a loop misses three intervals. `ISingletonWorker` exists for the rare loop that must run on one instance (the audit chain sealer is one). Its guard is a session advisory lock with takeover, taken on a session-mode connection (`ConnectionStrings:<name>-direct` when the main connection string goes through a transaction-mode pooler); without one it is a lease row in `yaf_leases`. The same rule governs the runners' `LISTEN/NOTIFY` wake-up, whose fallback is polling.

## Consequences

- One claim-and-retry mechanism serves outbox, jobs, cron and timeouts; one set of metrics and admin operations.
- Throughput is bounded by database polling; this is deferred work, not a stream processor.
- A job can run more than once (claim expiry); idempotency is the handler's duty.
- Cron authors must think about tenancy explicitly; a naive query returns nothing rather than everything.
- Deferred work cannot use the scheduling user's permissions. Handlers dispatch `[SystemOnly]` requests, so what a job may do is decided when the job type is written and reviewed, not by who scheduled it; a job that must respect the user's present rights checks them explicitly.
- One more third-party dependency in `YAF.Infrastructure` (Cronos, MIT, on the allow-list of ADR-0003), which must be kept current.
- Behind a transaction-mode pooler a direct connection string is needed for advisory locks and notifications; in lease mode a takeover can briefly overlap, so singleton work must be idempotent.
- No automatic saga rollback; compensation is written and tested per process.

## Enforcement

- Tests: rolled-back command schedules nothing; two runners never run one job concurrently; three instances produce one job per occurrence; cron without elevation reads zero tenant rows; occurrences across a daylight-saving change match expected instants; a job's handler sees the system principal as actor and the scheduler as initiator; a `[UserScoped]` request dispatched from a job is refused; one singleton worker runs among three instances in lock mode and in lease mode; blocked loop fails liveness; concurrent messages for one process are serialised.
- Startup checks: every configured cron name has a job, every job has a schedule, every expression parses.
- Analyzer `YAF2001`: job payloads and process state members are classified; secret data refused.
- Security events for elevation (`elevation.started`/`.ended`) and for parked jobs (`jobs.parked`); readiness degrades on parked items.
- `[SystemOnly]` marker enforced by the pipeline for requests meant for jobs, cron, consumers and process managers.
- Analyzer `YAF4004`: an execution context created or replaced outside framework entry points.
- Architecture test: Cronos is referenced only by `YAF.Infrastructure`, and none of its types appears in a public signature.

## Guidance for agents

- Need work after the response, or a call to an external system? Schedule a job; do not start a task or call out from a handler.
- Keep job payloads to ids. Never set a tenant or user inside a job handler: it runs in the scheduler's tenant as the service's system principal, with the scheduling user available as `Initiator` for audit only.
- From a job, cron or process-manager handler dispatch only `[SystemOnly]` requests.
- A cron job only decides *what is due* and fans out jobs per tenant with `CronContext.ForEachTenantAsync`. Do not process tenant data inside the cron job itself, and do not look for another way to enter a tenant's context.
- Use a process manager only when a process spans messages and time; a single command with domain events does not need one.
- Write the timeout and the compensation path with the happy path, and test duplicates and out-of-order messages.
- Do not add a third-party scheduler, and do not use Cronos types outside `YAF.Infrastructure`; application code states schedules as configuration strings.
- Do not assume an advisory lock or `LISTEN/NOTIFY` is available: go through `ISingletonWorker` and the runners' wake-up abstraction, which handle poolers.

## Related

0009, 0010, 0012, 0015, 0016, 0018
