# 05 — Persistence (`YAF.Persistence.EfCore`, `.PostgreSql`)

## 1. Model

The domain never sees EF Core. The service's infrastructure implements each memento interface as a plain class (the **data model**); EF Core maps those classes. Repositories translate between aggregates and data model objects with `Snapshot` and `Restore`.

```
Services/Catalog/
├── Infrastructure/                      # project Catalog.Infrastructure
│   ├── Persistence/
│   │   ├── DataModel/SessionData.cs     # [MementoData] partial class : Session.IMemento
│   │   ├── Configuration/SessionDataConfiguration.cs
│   │   ├── Repositories/SessionRepository.cs
│   │   ├── ReadModels/SessionReadModel.cs
│   │   ├── CatalogDbContext.cs          # : YafDbContext
│   │   ├── fields.approved.txt          # logical ids of protected fields (§6)
│   │   └── Migrations/
│   └── InfrastructureModule.cs          # AddCatalogInfrastructure()
└── Migrator/                            # project Catalog.Migrator, a worker that migrates and exits
```

**Data classes are generated.** The developer declares the class and the generator (in `Analyzers`, shipped inside `YAF.Core`) emits its properties from the memento interface, child collections included:

```csharp
[MementoData]
internal sealed partial class SessionData : Session.IMemento;
```

- Base data-model classes: `AggregateData` (id, tenant, version, stamps), `OwnedEntityData` (id, stamps). The generator picks the base from the memento interface. Data classes are named `<Entity>Data`, not "memento", in infrastructure.
- Members that only EF needs (a shadow navigation, a computed column) go in the developer's partial part.
- For a protected member the generator emits the storage shape described in §6.
- `Snapshot` and `Restore` stay hand-written in the aggregate; `MementoAssert.RoundTrips<T>()` checks them against the generated class.

**Typed members.** Memento members may be typed ids, enums, `Enumeration<T>` smart enumerations, value objects, their nullable forms and collections of child mementos, besides primitives. Their mapping lives only in `YAF.Persistence.EfCore`, applied by convention in `ConfigureConventions`; nothing EF-related is generated into a domain project:

| Member type | Mapping |
|---|---|
| Typed id | Generic `TypedIdConverter<TId>` over the static abstract members of `ITypedId<TSelf>` → `uuid` |
| Enum, `Enumeration<T>` | String column; `EnumerationConverter<T>` for smart enumerations |
| Single-value value object shipped in `YAF.Domain` (`Currency`, `EmailAddress`, `Percentage`) | Value converter to its primitive |
| Multi-value value object shipped in `YAF.Domain` (`Money`, `Period`, `DateRange`) | EF complex type, one column per component, each component named |

The convention maps only the value objects `YAF.Domain` ships. An app maps its own value objects in its entity configuration.

> **Amended 2026-11-27**, by the owner's decisions of 2026-11-26 (`specs/0013-thin-slice/plan.md`, decision log; deviation 10, confirmed by the owner on 2026-11-26): the value-object rows name the value objects the convention maps. Recognising an app's own value objects would need a marker interface, a contract `02` does not have. `Money`'s properties are get-only, so EF maps its components only when each is named.

## 2. `YafDbContext`

```csharp
public abstract class YafDbContext(DbContextOptions options, IYafDbContextServices services) : DbContext(options)
```

`IYafDbContextServices` bundles what the context needs (execution context accessor, `TimeProvider`, audit writer, tenant scope) so derived contexts have a two-parameter constructor.

Behaviour is implemented as **interceptors and conventions registered by `AddYafDbContext<T>()`**, not as overrides, so each is testable alone:

| Component | What it does |
|---|---|
| `StampingInterceptor` | On save: `CreatedAt/By`, `ModifiedAt/By` from `TimeProvider` and the execution context (the initiator when there is one, otherwise the actor; `04-application.md` §7); refuses a save without an execution context |
| `TenantInterceptor` | Sets `TenantId` on insert from the context; **refuses** an insert or update whose `TenantId` differs from the context (unless inside a cross-tenant elevation) |
| `VersioningInterceptor` | New `Version` (UUIDv7) for every added or updated aggregate root; original value kept, so the statement is `… WHERE id = @id AND version = @original`; a mismatch becomes `ConcurrencyConflictException`. An aggregate gets its new version on the first flush of a transaction and keeps it for later flushes (§5) |
| `SoftDeleteInterceptor` | Turns `Remove` of an `ISoftDeletableMemento` into an update of `DeletedAt/By` |
| `AuditInterceptor` | §7 |
| `ModelConventions` | Tenant and soft-delete query filters (named filters), `timestamptz` for `DateTimeOffset`, the typed-member mappings of §1, max length required on every string (model validation fails otherwise), `jsonb` for members marked `[JsonColumn]` |
| `TransactionScopeInterceptor` | §4: every command runs in a transaction that carries the tenant settings |

Snake_case names for tables and columns are a convention of `YAF.Persistence.EfCore.PostgreSql`, registered by `AddYafPostgres<TContext>()` (`01-repository-and-packages.md` §2.1).

> **Amended 2026-11-27**, by the owner's decision of 2026-11-27 (`specs/0013-thin-slice/plan.md`, deviation 8; recorded on issue #455): snake_case naming moves from `ModelConventions`, registered by `AddYafDbContext<T>()`, to `YAF.Persistence.EfCore.PostgreSql`, registered by `AddYafPostgres<TContext>()`. This resolves a contradiction between this section and ADR-0010 on one side and the package table of `01` §2.1 on the other, in favour of the package table; ADR-0010 is amended to match.

Field protection (§6) is **not** an interceptor; it runs in the repository base and the read-model helpers.

`OnModelCreating` of a derived context calls `modelBuilder.ApplyYafModel(this)` **last**; a model-validation check fails startup if it was not called.

Framework tables are added by opt-in interfaces on the context, each with a `Configure…` helper and its own migration snippet in the template:

`IOutboxDbContext` (`yaf_outbox`), `IInboxDbContext` (`yaf_inbox`), `IJobsDbContext` (`yaf_jobs`, `yaf_cron_occurrences`), `IIdempotencyDbContext` (`yaf_idempotency`), `IAuditDbContext` (`yaf_audit`, `yaf_audit_seals`, `yaf_audit_checkpoints`, `yaf_audit_anchor`), `IBusinessEventsDbContext` (`yaf_business_events`), `ISecurityEventsDbContext` (`yaf_security_events`), `IProcessDbContext` (`yaf_process_state`), `ISubjectKeysDbContext` (`yaf_subject_keys`), `ILeasesDbContext` (`yaf_leases`, §10).

## 3. Repositories

```csharp
public abstract class AggregateRepository<TAggregate, TId, TData, TMemento>(YafDbContext db, IAggregateTracker tracker)
{
    protected abstract IQueryable<TData> WithChildren(IQueryable<TData> set);   // the Includes
    public ValueTask<TAggregate?> FindAsync(TId id, CancellationToken ct);
    public ValueTask AddAsync(TAggregate aggregate, CancellationToken ct);
    public ValueTask UpdateAsync(TAggregate aggregate, CancellationToken ct);
    public ValueTask RemoveAsync(TAggregate aggregate, CancellationToken ct);
    protected ValueTask<IReadOnlyList<TAggregate>> ListAsync(Specification<TMemento> spec, Page page, CancellationToken ct);
    protected ValueTask<bool> AnyAsync(Specification<TMemento> spec, CancellationToken ct);
}
```

Rules:

- A repository is `internal sealed`, one per aggregate, implementing an interface the application owns. It exposes intention-revealing methods; it never returns `IQueryable` or data classes.
- **Reads for change** load tracked data with children, unprotect it (§6), `Restore` the aggregate, remember the data object for the scope, and register the aggregate with `IAggregateTracker`. A second `FindAsync` for the same id in the scope returns the same aggregate instance.
- **Updates** `Snapshot` onto the remembered data object (or reload it when absent) and protect it (§6). The version in the snapshot must equal the tracked original; otherwise `ConcurrencyConflictException` is thrown before reaching the database.
- **`UpdateAsync` always writes a new version on the root row**, also when only a child changed, so two commands that each add a child to the same aggregate cannot both commit. Child rows have no version column.
- Repositories **do not call `SaveChanges`**. The unit of work flushes (§5). After each flush it copies the version and stamps back into tracked aggregates through `IAggregateStateSync`, a public interface of `YAF.Domain` that `AggregateRoot<…>` implements explicitly. The optimistic check compares with the original version EF tracks, never with the aggregate's own `Version`, so a version forged through the interface fails the save with `core.concurrency.conflict`; analyzer `YAF2011` (warning) reports a call to `SyncVersion` outside the `YAF.Persistence.*` assemblies.
- `AddAsync` and `UpdateAsync` register the aggregate with the tracker, which is how its domain events reach the unit of work.
- **Query handlers** do not use repositories. They use a read-side interface (`ISessionReadModel`) implemented in infrastructure with `AsNoTracking()` projections to response DTOs or read-model rows, paged and bounded in the store. Query filters (tenant, soft delete) apply automatically. A projection that includes a protected column goes through the helpers of §6.
- Specification translation: `SpecificationTranslator.Translate<TMemento, TData>(specification)` rewrites `Expression<Func<TMemento, bool>>` to `Expression<Func<TData, bool>>` by replacing the parameter and mapping interface members to the implementing properties. Typed members translate through their converters, so a specification compares a status with the enumeration value, not a string. A member that is not a mapped column, or is protected (other than an equality test on a `[Searchable]` member, which is rewritten to the hash column), fails with an exception naming the member.
- A client-supplied expected version (`If-Match` or a body field) is compared by the handler: `aggregate.Version.Matches(expected)` else `PreconditionFailed`.

Paging: `Page(int Offset, int Limit)` clamped to `[1, PagingOptions.MaxLimit]` (default 200), or `Cursor(string? After, int Limit)` with opaque, sort-bound cursors. Results are `PagedList<T>(Items, HasMore, NextCursor?, TotalCount?)`; total counts are opt-in. Every list query has a deterministic total ordering (ties broken by id).

> **Amended 2026-11-27**, by the owner's decisions of 2026-11-26 (`specs/0013-thin-slice/plan.md`, decision log Q1; deviations 11 and 12, confirmed by the owner on 2026-11-26, 11 with three conditions). The constructor takes no `IFieldProtector` until M6, which adds field protection to the repository base (`18-build-plan.md` §2). `IAggregateStateSync` is public in `YAF.Domain`, because `InternalsVisibleTo` names only a package's own test project (`01-repository-and-packages.md` §2); the owner's conditions are the check against the tracked original, the forgery test and `YAF2011`. The translator's entry is the static method `Translate`, which needs no translator object.

## 4. Tenant isolation: two layers

1. **Query filters.** Every `ITenantOwnedMemento` entity gets `WHERE tenant_id = @current OR tenant_id = ANY(@additional)`. With no tenant in the context the filter matches nothing (fail closed). `IgnoreQueryFilters` is banned outside the framework.
2. **PostgreSQL row-level security.** `AddYafPostgres(o => o.RowLevelSecurity = true)` (default `true`):
   - Migrations generated through `migrationBuilder.EnableYafRowLevelSecurity("table")` add `ALTER TABLE … ENABLE ROW LEVEL SECURITY; FORCE ROW LEVEL SECURITY;` and a policy `USING (tenant_id = ANY ((SELECT string_to_array(current_setting('yaf.tenant_ids', true), ',')::uuid[])) OR (SELECT current_setting('yaf.bypass_rls', true)) = 'on')` with the same `WITH CHECK`. The settings are read through scalar sub-selects so the planner evaluates them once per statement, not per row.
   - **`YafDbContext` never executes a command outside a transaction.** Commands run in the unit of work's transaction (§5); each query request runs in one `READ ONLY` transaction opened by `QueryTransactionMiddleware` (`04-application.md` §3), which enforces "queries do not write" in every environment; where neither is active (an asynchronous validation rule before the unit of work) `TransactionScopeInterceptor` opens a short `READ ONLY` transaction for that command.
   - `TransactionScopeInterceptor` sends `SELECT set_config('yaf.tenant_ids', @ids, true), set_config('yaf.bypass_rls', @bypass, true)` **in the same batch as the first command of every transaction**: the settings are transaction-local, so pooled connections cannot leak a tenant, and there is no extra round trip.
   - `yaf.bypass_rls` is set only inside a cross-tenant elevation (`04-application.md` §7).
   - A test helper (`RowLevelSecurityAssert.AllTenantTablesProtected(db)`) queries `pg_class`/`pg_policies` and fails for a tenant-owned table without the policy; the service template includes it.
3. **Database roles**, created and granted by the template's migrator:

   | Role | Used by | Rights |
   |---|---|---|
   | **migrator** | The migrator worker | Owns the schema (DDL) |
   | **runtime** | The service | DML only; not the table owner; no `BYPASSRLS`; `INSERT` and `SELECT` only on the append-only tables (`yaf_audit`, `yaf_audit_seals`, `yaf_audit_checkpoints`, `yaf_security_events`, `yaf_business_events`) |
   | **maintenance** | The retention job only (§7, `11-privacy-and-compliance.md` §4) | Creates future partitions of the append-only tables and drops expired ones; no DML on business tables |

**Framework tables.** `yaf_outbox`, `yaf_inbox`, `yaf_jobs` and `yaf_idempotency` carry `tenant_id` and are under row-level security like any tenant table. The outbox relay and the job runner read them across tenants under the system principal with a standing elevation, recorded once when the worker starts (`elevation.started`, reason `outbox-relay` / `job-runner`), not per row. Outbox and job payloads that contain classified members are encrypted at rest with the key ring of purpose `messaging` and removed after their retention (`11` §4).

**What row-level security protects against.** A bug in application code: a missing filter, a raw query, a wrong context. It does **not** protect against arbitrary SQL executed as the runtime role, because that SQL could set `yaf.tenant_ids` or `yaf.bypass_rls` itself. SQL injection is prevented by other controls (parameterised access only, `09-security.md` §7); the docs state this boundary.

Tenant-less entities (reference data) implement no tenant interface and must be marked `[SharedAcrossTenants("reason")]`; an architecture test lists them for review.

## 5. Unit of work

`EfUnitOfWork<TDbContext>` implements `IUnitOfWork` as specified in `04-application.md` §6, inside `Database.CreateExecutionStrategy()`. Within one transaction:

1. run the handler;
2. `SaveChanges` (flush);
3. dispatch the domain events collected from tracked aggregates;
4. repeat 2–3 until no events remain (depth limit 10, then failure);
5. commit.

Because every round is flushed before its events are dispatched, an event handler's queries see the pending changes, and a constraint violation surfaces next to the code that caused it. Nothing becomes visible to other transactions before the commit. The idempotency record (`yaf_idempotency`) is claimed and its result stored inside this transaction (`04` §3).

On a transient retry it clears the change tracker and the aggregate tracker before re-running. One `DbContext` per scope; a service with two contexts designates one as the unit-of-work context and the framework tables live there.

Isolation level: read committed. Aggregates are protected by the optimistic version of their root (§3); framework queues by row locks.

## 6. Field protection (encryption at rest)

Driven by classification on the **memento interface** members:

| Classification | Default treatment at rest |
|---|---|
| `[SecretData]` | Encrypted with the service key ring |
| `[SensitivePersonalData]` | Encrypted with the **data subject's key** when the type is `IDataSubjectOwnedMemento`, else the key ring |
| `[PersonalData]` | Encrypted: with the data subject's key on an `IDataSubjectOwnedMemento`, with the key ring on a type marked `[NoDataSubject]`. Opt-out per member with `[StoredInClear("reason")]` (below) |
| `[InternalData]`, `[PublicData]` | Clear |

**Where it happens: the YAF mapping layer.** Protection uses no EF interceptor and no value converter, because encryption needs the row's context and an asynchronous key lookup.

- For a protected member `Email` the generated data class (§1) has:
  - `EmailProtected` — mapped column `email_protected`, the ciphertext;
  - `EmailHash` — mapped column `email_hash`, only when the member is `[Searchable]`;
  - `Email` — **unmapped** clear property that implements the memento member.
- The repository base calls `IFieldProtector.ProtectAsync(dataObjects)` before the data reaches the change tracker's save and `IFieldProtector.UnprotectAsync(dataObjects)` after a load. Both work on the whole loaded graph in one batch: the reader is closed first, then the needed subject keys are resolved asynchronously in one lookup.
- **Read models.** A read-model row type marks a protected column `[Protected]` and is materialised through the YAF query helpers `ToUnprotectedListAsync()` / `FirstOrDefaultUnprotectedAsync()`, which run the query and then unprotect the rows in one batch. Analyzer `YAF5001` (error) flags a query that projects a protected column and materialises it without a helper.
- Sorting, range filters and substring search on a protected member are not possible in the store (§3, specification translation).

**Format.** `yaf1:<key-id>:<base64url(nonce ‖ ciphertext ‖ tag)>`, AES-256-GCM, 12-byte random nonce, 16-byte tag, associated data = `logical field id ‖ row id ‖ tenant id` (a ciphertext copied to another row, field or tenant fails to decrypt).

**Logical field ids.** The field id defaults to `<memento type>.<member>` at the member's first declaration and is recorded in `fields.approved.txt` (an approval test, like the error catalogue). Table and column names are not part of the associated data, so a rename migration does not affect stored values. When the memento type or the member is renamed, the recorded id is kept by pinning it with `[FieldId("…")]`; the approval test fails on an id that disappeared without a pin.

**Searchable members.** `[Searchable]` adds the companion column `<name>_hash` holding HMAC-SHA-256 of the normalised value under a dedicated lookup key, so equality lookups work without storing clear text. The member itself is still encrypted. Hash columns are cleared on erasure (`11` §5).

**Stored in clear by decision.** `[StoredInClear("reason")]` on a `[PersonalData]` member keeps it as an ordinary column, for the cases that need sorting or prefix search (a participant list ordered by surname). The reason appears in the generated data inventory (`11` §6). Analyzer `YAF2008` (error) refuses it on `[SensitivePersonalData]` and `[SecretData]`. Such members are erased by overwrite through the generated eraser, not by crypto-shredding, and they are not protected in backups.

**Keys.**

- Key ring (`IKeyRing`, in `YAF.Infrastructure`): keys by id from `IKeyProvider` (configuration/secret store, AWS KMS; Azure Key Vault in a later release); one current key; any key in the ring decrypts; unknown key id or bad tag throws and raises a security event. Startup fails without a current 256-bit key outside Development; in Development a random per-run key is used only when `Encryption:AllowEphemeralKey` is true.
- **Rotation**: add key (deploy 1) → make current (deploy 2) → `ReEncryptionJob` rewrites values whose prefix is not the current key id, in batches with guarded updates, and reports the remaining count as a metric and log line → remove old key when zero (deploy 3).
- **Per-subject keys** (`yaf_subject_keys`: subject id, tenant id, wrapped data key, created, destroyed-at): a data key per data subject, wrapped by the key ring (envelope encryption). Destroying it is the crypto-shredding step of erasure (`11-privacy-and-compliance.md` §5). Reading a value whose subject key was destroyed yields the member's default and a flag on the restored aggregate (`IsErased`).
- Unwrapped subject keys are cached in memory for a short time (`Encryption:SubjectKeyCacheSeconds`, default 60) and the entry is evicted when the key is destroyed (in the destroying instance at once, in the others through the `privacy.subject-erased.v1` event and at the latest when the entry expires).
- A column that held clear text before being classified is read as stored and encrypted on its next save; `ReEncryptionJob` also sweeps those.

## 7. Audit trail

- Aggregate data classes (`AggregateData`) and their owned children are audited by default (opt out with `[NotAudited("reason")]`); any other entity opts in with `[Audited]`. An audited entity gets one row per insert, update and delete in `yaf_audit`, written **in the same transaction** as the change, so no change is ever unaudited. Writing the row takes no lock.
- Row: `id` (UUIDv7), `tenant_id`, `occurred_at`, `actor`, `initiator`, `origin`, `correlation_id`, `trace_id`, `entity`, `entity_id`, `operation`, `changes` (`jsonb`: member → `{old, new}`). It has no chain fields.
- Values in `changes` follow classification: `[SecretData]` never recorded (member name only); personal and sensitive members recorded **encrypted with the subject key** (so shredding erases them from the audit trail too); other values in clear.
- **Hash chain per tenant, sealed asynchronously.** A singleton `AuditChainSealer` links committed rows per tenant in commit-safe order: it takes only rows that lie below the snapshot of the oldest running transaction, so a row committed late cannot land before an already sealed one. For each row it appends to `yaf_audit_seals` (`audit_id`, `tenant_id`, `sequence`, `prev_hash`, `hash`), with `hash = SHA-256(prev_hash ‖ canonical JSON of the audit row)`. Nothing is updated in place. The chain covers ciphertext, so shredding does not break verification.
- **Unsealed window.** Between commit and sealing (normally seconds) a row is stored but not yet chained. `Audit:SealLagThreshold` (default 1 minute) bounds what is acceptable: the verifier reports unsealed rows older than it, the gauge `yaf.audit.unsealed` shows the backlog, and readiness is degraded while it is exceeded.
- `AuditChainVerificationJob` (cron) re-computes chains incrementally from the latest checkpoint, emits `audit.chain_broken` as a critical security event on a mismatch or on a sealed row missing from `yaf_audit`, and writes the latest head per tenant to `IAuditAnchor` (default: `yaf_audit_anchor` table plus a structured log line; adapters may write to object storage with object lock). The trail is tamper-**evident**, and only as strong as the anchor is out of reach of whoever can alter the tables.
- **Retention by partition.** `yaf_audit`, `yaf_audit_seals`, `yaf_security_events` and `yaf_business_events` are range-partitioned by month. The retention job, connecting as the **maintenance** role, creates future partitions and drops expired ones; there is no row-level delete on these tables for any role. Before a partition of the audit tables is dropped, the sealer writes a signed **checkpoint** per tenant to `yaf_audit_checkpoints` (tenant, last sequence, last hash, row count, signature under the key ring of purpose `audit`), which is anchored externally and becomes the start of the chain for verification. A legal hold on a tenant or subject blocks the drop of the partitions that contain its rows.
- The runtime role has `INSERT` and `SELECT` only on these tables (§4).
- **Read audit** is opt-in per query: `[AuditRead]` on a query request records who read which subject's data (needed to answer "whose data did identity X access"). It is written by the query middleware in a transaction of its own after the read-only one.

## 8. Business events and security events

Two append-only stores written through the unit of work (or immediately, for security events raised outside one):

- `yaf_business_events`: `code` (`registration.confirmed`), subject id, data bag (classified values redacted), context. For analytics and timelines. A no-op recorder can be registered instead.
- `yaf_security_events`: taxonomy in `09-security.md` §8. Also emitted as structured logs and metrics, and to `ISecurityEventSink` implementations.

Both are partitioned and purged as described in §7.

## 9. Migrations

- Ordinary EF Core migrations in the infrastructure project, created with `dotnet ef`. CI runs `dotnet ef migrations has-pending-model-changes` for every sample and template context.
- Applied by a **separate migrator worker** (`MigratorWorker<TDbContext>`): takes a PostgreSQL advisory lock, creates the database if missing (bounded retry with backoff), migrates, applies role grants and RLS checks, creates the current and next partitions of the partitioned tables, runs idempotent seeders, stops the host with exit code 0, or non-zero on failure. Services never migrate at startup and their readiness check fails while migrations are pending.
- Expand/contract: a release's migrations must be compatible with the previous release's code. Destructive steps go in a later release. `EnsureCreated` is banned.
- Renaming a table or a column that holds protected values needs no data step (§6, logical field ids).

## 10. Other store facilities

- `[JsonColumn]` members: camelCase JSON in `jsonb` with a value comparer that detects in-place changes.
- `IDistributedLock` for singleton work: a session advisory lock on a session-mode connection, otherwise a lease row in `yaf_leases` (holder, expires-at, renewed by heartbeat).
- **Connection pooling.** `LISTEN/NOTIFY` and session advisory locks need a session-mode connection. When the app's main connection string goes through a transaction-mode pooler (PgBouncer, RDS Proxy), the outbox relay, the job runner and the singleton workers use a separate direct connection string, `ConnectionStrings:<name>-direct`. Without it they fall back to polling instead of notifications and to `yaf_leases` instead of advisory locks; a startup log line says which mode is active. Everything else the framework does is transaction-scoped and works through such a pooler.
- Connection: `AddYafPostgres<TDbContext>("connection-name")` with retry-on-failure, command timeout, `Include Error Detail` off outside Development, TLS required outside Development (`SslMode=VerifyFull`; startup check).
- An in-memory provider is not supported for tests; integration tests use PostgreSQL in a container.

## 11. Acceptance criteria

Integration tests on PostgreSQL (Testcontainers), each named after the rule it proves:

- Data model: the generated data class implements every memento member; typed ids, enumerations and value objects round-trip; a specification over a typed member translates to SQL.
- Tenant: filter hides other tenants; with no tenant nothing is returned; a raw SQL query through the runtime role without the setting returns nothing (RLS); insert with a foreign tenant id is refused; elevation widens and is audited; two concurrent requests on one pooled connection never see each other's tenant; framework tables are under RLS; the settings travel in the first command's batch (one round trip).
- Transactions: a write attempted in a query request fails in every environment; a database rule in a validator runs in a read-only transaction with the tenant set.
- Concurrency: stale aggregate version → conflict; two concurrent commands that each change only a child of the same aggregate → one conflict.
- Field protection: stored value is ciphertext with the key id, the clear property is not a column; associated-data mismatch fails; a table and column rename leaves values readable; a removed field id without a pin fails the approval test; rotation over three "deploys" keeps every value readable and ends at zero; `[Searchable]` lookup finds the row; a read model returns clear values through the helper; destroyed subject key yields erased values, also in an instance that had the key cached; a `[StoredInClear]` member is a plain column and is listed in the inventory.
- Audit: insert/update/delete rows; secrets absent; rows are sealed in commit order under concurrent writers; chain verifies; a manually altered or removed row is detected; unsealed rows past the threshold are reported; runtime role cannot update or delete audit rows; an expired partition is dropped by the maintenance role only after a checkpoint, and verification continues from it; a legal hold blocks the drop.
- Unit of work: an event handler's query sees the change that raised the event; failed result and failed domain event handler roll everything back; transient failure retry does not duplicate rows; an aggregate flushed twice in one transaction has one new version.
- Specification translation including `And`/`Or`/`Not`, an unmapped member and a protected member.
- Migrator: concurrent starts apply migrations once; pending migrations fail readiness.
- Pooling: with a transaction-mode pooler and no direct connection string, the relay and singleton workers run on polling and leases.
