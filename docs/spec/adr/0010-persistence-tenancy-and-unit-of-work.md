# ADR-0010 — Persistence, tenancy and the unit of work

> **Status** Accepted
> **Date** 2026-11
> **Deciders** Framework maintainers

## Context

Aggregates must be stored without the ORM shaping them (ADR-0007), every service is multi-tenant, and a command must commit atomically with its events and outbox rows (ADR-0009).

The prior design put enrichment, tenant filtering and auditing into overrides of one large context base class. Repositories saved on every call, then had to detach the stored object and re-hydrate the aggregate. Tenant isolation was a query filter only, with a waiver flag switched on and back off by hand. Outbox and job rows were claimed with optimistic checkout flags. The audit trail was plain change rows.

## Drivers

- A tenant can never read or write another tenant's rows, even if application code is wrong.
- One commit per command; nothing saved outside the unit of work.
- Each persistence behaviour is testable on its own.
- The store proves who changed what, in a form that reveals tampering.
- Schema changes are safe during rolling deployments.

## Options considered

| Option | For | Against |
|---|---|---|
| Query filters only for tenancy | Simple; ORM-native | One missed filter, raw query or bug leaks data; no second line |
| Database or schema per tenant | Strong isolation | Operational cost; migrations per tenant; pooling |
| **Query filters plus PostgreSQL row-level security with separated roles** | Two independent layers; one schema | A per-transaction setting; migrations must add policies |
| Behaviour in context overrides | One class | Untestable in isolation; grows without bound |
| **Behaviour as interceptors and conventions** | Small, separately tested units | More registrations |
| Repositories save | Simple mental model | Several commits per command; detach and re-hydrate; events handled after partial commits |
| Unit of work saves once, after all domain events | Fewest round trips | Event handlers' queries do not see the changes that raised the event; constraint errors surface far from their cause |
| **Unit of work flushes each round of events inside one transaction, commits once** | Atomic; handlers see pending changes | One extra round trip per round of events |
| Queries outside any transaction | Nothing to open | The tenant setting is transaction-local, so row-level security cannot apply; "queries do not write" is only a convention |
| **Every query in a read-only transaction** | Row-level security applies; the store refuses a write | A transaction per query (begin and commit travel with the statements) |
| Audit chain built at commit under a per-tenant lock | Every row chained when it commits | Serialises all writing transactions of a tenant |
| **Audit rows written in the transaction, chain sealed asynchronously** | No lock on the write path | The newest rows are stored but not yet chained for a short window |
| Row-level delete for retention of append-only tables | No partitioning | A role that can delete audit rows; slow, bloating deletes |
| **Monthly partitions dropped by a maintenance role after a signed checkpoint** | Runtime stays append-only; fast purge | Partition management; retention rounded up to a month |

## Decision

Contract: `spec/05-persistence.md`.

**Data model.** Infrastructure implements each memento interface as a plain data class (`<Entity>Data`); EF Core maps those. The class is generated: the developer declares `[MementoData] internal sealed partial class SessionData : Session.IMemento;` and the generator emits the properties (ADR-0007). Repositories translate with `Snapshot` and `Restore`. The domain never sees EF Core, and nothing EF-related is generated into a domain project: typed ids, smart enumerations and value objects in mementos are mapped by generic converters and complex types that `YAF.Persistence.EfCore` applies by convention.

**Interceptors, not overrides.** `YafDbContext` is thin. `AddYafDbContext<T>()` registers: stamping (who and when, from the execution context and `TimeProvider`), tenant, versioning, soft delete, audit, the transaction scope with the row-level security settings, and model conventions (named tenant and soft-delete filters, `timestamptz`, enums as strings, the typed-member mappings, mandatory string lengths, `jsonb` members). Field protection is not an interceptor; it runs in the repository base and the read-model helpers (ADR-0011). A model check fails startup when the conventions were not applied. Snake_case names are a convention of `YAF.Persistence.EfCore.PostgreSql`, registered by `AddYafPostgres<TContext>()`. Framework tables are opt-in interfaces on the context.

> **Amended 2026-11-27**, by the owner's decision of 2026-11-27 (`specs/0013-thin-slice/plan.md`, deviation 8; recorded on issue #455): snake_case naming is registered by `AddYafPostgres<TContext>()` in `YAF.Persistence.EfCore.PostgreSql`, where `spec/01-repository-and-packages.md` §2.1 places it, not by `AddYafDbContext<T>()`. The decision of this ADR is unchanged.

**Repositories do not save.** One `internal sealed` repository per aggregate behind an application-owned interface. Reads for change load tracked data with children, unprotect it, restore the aggregate and register it with the aggregate tracker. Updates snapshot onto the remembered data object and protect it. Repositories never return `IQueryable` or data classes and never call `SaveChanges`.

**One transaction, flushed per round of events.** `EfUnitOfWork` runs inside the store's execution strategy: begin transaction, run the handler, save, dispatch the domain events collected, and repeat save and dispatch until no events remain (depth limit 10), then commit. Each flush copies version and stamps back into tracked aggregates. Event handlers therefore see pending changes, and nothing is visible to other transactions before the commit. The idempotency record (`yaf_idempotency`, a table of its own) is claimed and its result stored inside this transaction. On a transient retry it clears both trackers and re-runs. Isolation is read committed; aggregates are protected by versions, framework queues by row locks.

**Every command of the context runs in a transaction.** Commands use the unit of work's. Each query request runs in one `READ ONLY` transaction, which makes the store refuse a write from a query in every environment. Where neither is active (an asynchronous validation rule before the unit of work) the context opens a short read-only one.

**Query side.** Query handlers use read-model interfaces implemented with no-tracking projections to response contracts or read-model rows, paged and bounded in the store, with a deterministic total order. Filters apply automatically. Paging limits are clamped. Protected columns are read through the unprotecting helpers (ADR-0011).

**Two-layer tenant isolation.**

1. *Query filters*: every tenant-owned entity is filtered by the current tenant and any additional tenants granted. No tenant means no rows. `IgnoreQueryFilters` is banned outside the framework.
2. *PostgreSQL row-level security*: migrations enable and force RLS with a policy on a transaction-local setting (`yaf.tenant_ids`), read through a scalar sub-select; the setting is sent in the same batch as the first command of every transaction, so pooled connections cannot leak a tenant and there is no extra round trip. A bypass setting is on only inside an audited cross-tenant elevation.
3. *Roles*: the **migrator** role owns the schema; the **runtime** role has DML only, is not the table owner, has insert and select only on audit and event tables, and no `BYPASSRLS`; the **maintenance** role, used only by the retention job, creates and drops partitions of the append-only tables.

Framework tables (`yaf_outbox`, `yaf_inbox`, `yaf_jobs`, `yaf_idempotency`) carry a tenant id and are under the same policy; the relay and the runners read them under the system principal with a standing elevation recorded when they start. Outbox and job payloads with classified members are encrypted at rest (key-ring purpose `messaging`).

A write whose tenant id differs from the context is refused and raises a security event. Tenant-less tables must be marked `[SharedAcrossTenants("reason")]`.

*Threat model.* Row-level security protects against application bugs: a missing filter, a raw query, a wrong context. It does not protect against arbitrary SQL executed as the runtime role, which could set the session variables itself.

**Optimistic concurrency.** The aggregate is the unit of consistency, so the version lives on its **root row** only. `UpdateAsync` always writes a new root version, also when only a child changed, checked in the `WHERE` clause; a mismatch becomes a conflict. Two commands that each add a child to the same aggregate therefore cannot both commit. An aggregate gets its new version on the first flush of a transaction and keeps it for later flushes. The aggregate carries its version, so a stale aggregate is refused before reaching the database. A client's expected version is compared in the handler (`PreconditionFailed`).

**Connection pooling.** Everything above is transaction-scoped and works through a transaction-mode pooler. `LISTEN/NOTIFY` and session advisory locks do not: the relay, the job runner and singleton workers use a separate direct connection string (`ConnectionStrings:<name>-direct`), and without one they fall back to polling and to lease rows (`yaf_leases`).

**Migrations.** Ordinary EF Core migrations, applied by a separate migrator worker under an advisory lock, which also applies grants and checks policies, seeds idempotently and exits. Services never migrate at startup; readiness fails while migrations are pending. Changes follow expand/contract so the previous release keeps working. `EnsureCreated` is banned. An in-memory provider is not supported; tests run on PostgreSQL in a container.

**Audit trail.** Audited entities get a row per insert, update and delete in `yaf_audit` in the same transaction, without any lock, with values treated by classification (secrets never, personal data encrypted with the subject key) and with both actor and initiator. A singleton `AuditChainSealer` then **hash-chains the committed rows per tenant** in commit-safe order (only rows below the oldest running transaction's snapshot) by appending to `yaf_audit_seals`; nothing is updated in place. A job verifies chains and writes heads to an external anchor. A mismatch is a critical security event; unsealed rows older than `Audit:SealLagThreshold` (default 1 minute) are reported. The trail is tamper-evident, as strong as the anchor's protection. Read audit is opt-in per query.

**Retention of append-only tables.** `yaf_audit`, `yaf_audit_seals`, `yaf_security_events` and `yaf_business_events` are range-partitioned by month. The retention job, as the maintenance role, drops expired partitions. Before an audit partition goes, the sealer writes a signed checkpoint (tenant, last sequence, last hash, row count, signature under the `audit` key) to `yaf_audit_checkpoints`; it is anchored externally and is where verification starts from then on. A legal hold blocks the drop.

## Consequences

**Positive**

- A tenant leak needs two independent failures.
- Commands are atomic including events, outbox and audit; no detach and re-hydrate choreography.
- Domain event handlers work on a store that already holds the change that raised the event.
- An aggregate's invariants hold under concurrent commands, whichever of its rows they touch.
- Writing audit rows does not make a tenant's transactions wait for each other.
- "Queries do not write" is enforced by the store, in production too.
- Each interceptor has focused tests.
- Audit records support integrity claims honestly.
- Rolling deployments are safe by construction of migrations.

**Negative / costs**

- PostgreSQL-specific; another store needs its own provider package and an equivalent second layer.
- Every tenant table's migration must add its policy; every statement runs in a transaction, also a single read.
- Three database roles and their grants to manage, and a second (and with a pooler a third) connection string.
- One extra round trip per round of domain events.
- Every update of an aggregate rewrites its root row, and two commands on different children of one aggregate conflict. Large aggregates with independent children should be split.
- The newest audit rows are stored but not chained for a short window; an attacker who can alter the table within that window is caught only by the row's presence in backups and logs, not by the chain.
- Partition management for the append-only tables; their retention is rounded up to a month.
- Behind a transaction-mode pooler the relay and singleton workers need a direct connection or run on polling and leases.
- Expand/contract makes some schema changes take two releases.

## Enforcement

| Mechanism | What it checks |
|---|---|
| Startup model check | `ApplyYafModel` called; every string has a length; framework tables configured when their feature is on |
| `RowLevelSecurityAssert.AllTenantTablesProtected` (template test) | Every tenant-owned table has RLS enabled and the policy |
| Tenant interceptor | Foreign tenant id on write refused (`tenant.violation`) |
| Banned API list | `IgnoreQueryFilters` outside persistence; interpolated raw SQL; `EnsureCreated` |
| Architecture rules | Repositories internal, no `IQueryable` or data class in their public signatures, no `SaveChanges` call outside the unit of work; `[SharedAcrossTenants]` list reviewed |
| `READ ONLY` query transaction | The store refuses a write during a query, in every environment |
| CI | `has-pending-model-changes` for every sample and template context |
| Readiness check | Pending migrations; audit seal lag above the threshold (degraded) |
| Integration tests on PostgreSQL | Filter, RLS through the runtime role without the setting, framework tables under RLS, pooled connections, stale version, concurrent child-only changes conflict, event handler sees pending changes, audit sealing order under concurrent writers, altered-row detection, runtime role cannot alter audit rows, partition drop only after a checkpoint and only by the maintenance role, rollback paths, concurrent migrators, polling and lease fallback behind a pooler |
| Audit verification job | Chain integrity and unsealed backlog, with metrics (`yaf.audit.unsealed`) and security events |

## Guidance for agents

- A new aggregate needs: the `[MementoData]` partial data class (do not write its properties by hand), its configuration with explicit lengths, a repository, a migration, and for tenant data the RLS call in that migration. Run the RLS assertion test.
- Do not write a value converter for a typed id, a smart enumeration or a value object; the conventions map them. Never put anything EF-related into a domain project.
- Never call `SaveChanges`, begin a transaction, or detach entities in a repository or handler.
- Never write to the store from a query handler; it runs in a read-only transaction and the write will fail.
- Do not add a version column to a child entity, and do not skip `UpdateAsync` because "only a child changed".
- Never use `IgnoreQueryFilters`, set `yaf.bypass_rls`, or write raw SQL that omits the tenant. Cross-tenant work uses the elevation scope, and only when the spec says so.
- Never set `TenantId`, `Version`, or the stamp fields by hand; interceptors do.
- Never return `IQueryable` or a data class from a repository, and never use a repository in a query handler; write a projection in a read model.
- Every list query is paged and has a total order ending in the id.
- Never edit an existing migration that has been released; add a new one. Destructive changes go in a later release than the code that stops using the column.
- Never grant the runtime role DDL, ownership, `BYPASSRLS`, or update/delete on audit and event tables; never use the maintenance connection outside the retention job; never weaken the audit interceptor or the sealer, and never take a lock to "seal at commit".
- Never delete rows from an append-only table. Retention drops partitions.
- Do not add an in-memory or SQLite test path; use the PostgreSQL container fixture.
- If a concurrency conflict appears in a test, find the stale read; do not retry blindly or drop the version check.

## Related

- ADR-0004 — security tenets S2, S4, S5, S10
- ADR-0007 — mementos; ADR-0009 — unit of work in the pipeline
- ADR-0011 — field protection; ADR-0012, ADR-0013 — stores for outbox, inbox, jobs
- ADR-0018 — testing on a real store
- `spec/05-persistence.md`
