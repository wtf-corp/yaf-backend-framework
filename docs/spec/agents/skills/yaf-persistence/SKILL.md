---
name: yaf-persistence
description: How a YAF service stores aggregates with EF Core on PostgreSQL - generated data classes implementing memento interfaces, entity configuration, repositories that never save, read models and paging, field protection in the mapping layer, tenant query filters plus row-level security in migrations, optimistic concurrency, migrations and the migrator worker, the audit trail and JSON columns. Load BEFORE creating or changing a *Data class, an IEntityTypeConfiguration, a repository, a read model, a DbContext, a migration or anything in a service's infrastructure Persistence or Migrator project, and before writing raw SQL or calling SaveChanges.
license: MIT
metadata:
  owner: yaf
  version: "1"
---

# YAF persistence

The domain never sees EF Core. Infrastructure declares one `[MementoData]` partial class per memento interface and the generator emits its members; repositories translate with `Snapshot` and `Restore`; the unit of work saves and commits.

## When to load

- Adding a `*Data` class, entity configuration, repository or read model.
- Adding or editing a migration, or a table that holds tenant data.
- Handling versions / `If-Match`, paging or sorting.
- Marking an entity audited, soft-deletable or shared across tenants.
- Tempted to call `SaveChanges`, `IgnoreQueryFilters` or write SQL by hand.

## Where does this go?

| The thing is… | It goes in |
|---|---|
| Storage shape of an aggregate or child | `Persistence/DataModel/<Entity>Data.cs`: `[MementoData] internal sealed partial class <Entity>Data : <Entity>.IMemento;` — the generator emits the properties, child collections and the `AggregateData` / `OwnedEntityData` base |
| A column only storage needs (a denormalised sort key, a navigation) | The other partial part of the same data class |
| Table, columns, lengths, relationships | `Persistence/Configuration/<Entity>DataConfiguration.cs` |
| Load-for-change and store of an aggregate | `Persistence/Repositories/<Aggregate>Repository.cs` : `AggregateRepository<…>` |
| A list or lookup for a query handler | Read model class implementing the application's `I<X>ReadModel` |
| A reusable predicate | `Specification<TMemento>` in the domain; pass it to `ListAsync` |
| Schema change | EF Core migration in `Persistence/Migrations/` |
| Applying migrations, roles, seed data | `<Svc>.Migrator` worker (`MigratorWorker<TDbContext>`) |
| Outbox, inbox, jobs, audit, process tables | Opt-in interfaces on the context (`IOutboxDbContext`, …) and their `Configure…` helpers |

## Rules

1. **REQUIRED** The service context derives from `YafDbContext`, is registered with `AddYafDbContext<T>()` / `AddYafPostgres<T>("name")`, and calls `modelBuilder.ApplyYafModel(this)` **last** in `OnModelCreating` (startup model validation fails otherwise).
2. **REQUIRED** Repositories are `internal sealed`, one per aggregate, expose intention-revealing methods, and never return `IQueryable` or `*Data` objects.
3. **REQUIRED** Repositories never call `SaveChanges`. `AddAsync` / `UpdateAsync` / `RemoveAsync` register the aggregate with the tracker; the unit of work saves, dispatches domain events, saves again until none remain, and commits. Do not open a transaction or a connection of your own: the context runs every command inside a transaction (the unit of work's, a query's `READ ONLY` one, or a short read-only one it opens itself).
4. **REQUIRED** Query handlers use read models with `AsNoTracking()` projections to DTOs, paged and ordered in the store. They do not go through repositories. A read-model row type marks protected columns `[Protected]` and is materialised with `ToUnprotectedListAsync()` / `FirstOrDefaultUnprotectedAsync()`; materialising a protected column any other way returns ciphertext and is YAF5001.
5. **REQUIRED** Every list has a deterministic total ordering (ties broken by id) and a clamped `Page` or `Cursor`; total counts are opt-in.
6. **REQUIRED** Tenant-owned data classes implement `ITenantOwnedMemento` (via `AggregateData`). A table without a tenant is marked `[SharedAcrossTenants("reason")]` (architecture test lists them).
7. **REQUIRED** Every migration that creates a tenant-owned table calls `migrationBuilder.EnableYafRowLevelSecurity("table")`. `RowLevelSecurityAssert.AllTenantTablesProtected(db)` must pass.
8. **REQUIRED** Never call `IgnoreQueryFilters` (banned API) or set `yaf.bypass_rls` yourself. Use the cross-tenant elevation scope.
9. **REQUIRED** Every string column has an explicit max length; `DateTimeOffset` is `timestamptz`; names are snake_case (conventions apply them; model validation fails on a string without a length). Typed memento members need no converter code in the service: `ConfigureConventions` of `YafDbContext` applies `TypedIdConverter<TId>` to typed ids, `EnumerationConverter<T>` to smart enumerations, stores enums as strings, maps single-value value objects by converter and multi-value ones (`Money`, `Period`) as complex types.
10. **REQUIRED** Child ids come from the domain: map them `ValueGeneratedNever()`.
11. **REQUIRED** Do not encrypt, hash or alter values in data classes or repositories. Classification on the memento interface drives field protection in the mapping layer: for a protected member `Email` the generated data class has the mapped `EmailProtected` (ciphertext), a mapped `EmailHash` when the member is `[Searchable]`, and an unmapped clear `Email`; the repository base calls `IFieldProtector.ProtectAsync` before save and `UnprotectAsync` after load. Configure and query the `…Protected` / `…Hash` columns, never the clear property. Add `[Searchable]` when you must look up a protected value by equality; ask for `[StoredInClear("reason")]` on the memento member (`yaf-privacy-and-data-classification`) when the store must sort or range-filter it.
12. **REQUIRED** A client-supplied expected version is checked in the handler (`aggregate.Version.Matches(expected)` → `PreconditionFailed`). The version lives on the **root** row only, and `UpdateAsync` writes a new one on every update, also when only a child changed; child tables have no version column. Stale stored versions surface as `ConcurrencyConflictException` → `Conflict`; do not catch it.
13. **REQUIRED** SQL is LINQ or parameterised. Interpolating into `FromSqlRaw` / `ExecuteSqlRaw` is a banned API.
14. **REQUIRED** Services never migrate at startup. Migrations are expand/contract: compatible with the previous release's code; destructive steps ship one release later. `EnsureCreated` is banned.
15. **REQUIRED** Aggregates are audited by default; opt out only with `[NotAudited("reason")]`. Mark queries that read a data subject's data `[AuditRead]` where required.
16. **RECOMMENDED** Use `[JsonColumn]` for small value collections that are replaced as a whole; use owned tables for child entities.
17. **RECOMMENDED** Keep one `DbContext` per service; with two, one is the unit-of-work context and holds the framework tables.
18. **REQUIRED** A field's associated data uses its stable field id, recorded in `fields.approved.txt`. Renaming a table, column, memento type or member must keep the recorded id (`[FieldId("…")]` pins it); a changed id makes stored ciphertext unreadable, so the approval test fails on it.
19. **REQUIRED** `yaf_audit`, `yaf_audit_seals`, `yaf_security_events` and `yaf_business_events` are append-only and partitioned by month. Never update or delete their rows and never grant the runtime role more: the retention job, under the maintenance role, drops expired partitions after the sealer wrote a checkpoint. Audit rows are written in your transaction; the `AuditChainSealer` chains them afterwards.
20. **REQUIRED** Framework tables (`yaf_outbox`, `yaf_inbox`, `yaf_jobs`, `yaf_idempotency`) carry `tenant_id` and are under row-level security like your own; do not read or write them directly.

## How to

1. Integration test first (PostgreSQL container): add, reload, update with a stale version, foreign-tenant read.
2. `[MementoData] partial class SessionData : Session.IMemento;` (one line; members are generated); configuration; `DbSet`.
3. Repository deriving from `AggregateRepository`.
4. `dotnet ef migrations add <Name>`; add the RLS call; review the SQL.
5. Read model for queries.

```csharp
[MementoData]
internal sealed partial class SessionData : Session.IMemento;      // properties and SeatRows are generated

[MementoData]
internal sealed partial class SeatData : Seat.IMemento;

internal sealed class SessionRepository(CatalogDbContext db, IAggregateTracker tracker)
    : AggregateRepository<Session, SessionId, SessionData, Session.IMemento>(db, tracker), ISessionRepository
{
    protected override IQueryable<SessionData> WithChildren(IQueryable<SessionData> set)
        => set.Include(x => x.SeatRows);

    public ValueTask<IReadOnlyList<Session>> ListOpenAsync(DateTimeOffset now, Page page, CancellationToken ct)
        => ListAsync(new OpenSessions(now), page, ct);
}

// migration
protected override void Up(MigrationBuilder migrationBuilder)
{
    migrationBuilder.CreateTable(name: "sessions", /* … */);
    migrationBuilder.EnableYafRowLevelSecurity("sessions");
}

// read model with a protected column
internal sealed record AttendeeRow(AttendeeId Id, [property: Protected] string Email, AttendeeStatus Status);

public async ValueTask<IReadOnlyList<AttendeeRow>> ListForSessionAsync(SessionId sessionId, Page page, CancellationToken ct)
    => await db.Attendees.AsNoTracking()
        .Where(a => a.SessionId == sessionId)
        .OrderBy(a => a.Id)
        .Skip(page.Offset).Take(page.Limit)
        .Select(a => new AttendeeRow(a.Id, a.EmailProtected, a.Status))
        .ToUnprotectedListAsync(ct);                                // decrypts after the reader is closed
```

## Anti-patterns

| Wrong | Why | Instead |
|---|---|---|
| `await db.SaveChangesAsync()` in a repository | Bypasses the unit of work's save-and-dispatch loop | Let the unit of work save |
| Hand-writing the properties of a `*Data` class | Drifts from the memento interface; misses the protected and hash columns | `[MementoData]` partial class |
| `HasConversion(...)` for a typed id or a status in your configuration | Duplicates the conventions | Nothing: the conventions convert typed members |
| `.Select(a => a.EmailProtected).ToListAsync()` | Returns ciphertext (YAF5001) | `[Protected]` row member + `ToUnprotectedListAsync()` |
| `OrderBy(a => a.Email)` on a protected member | The clear property is not mapped | Sort in memory on a small page, or `[StoredInClear("reason")]` after review |
| A version column on a child table | The aggregate is the unit of consistency | Root version only |
| Returning `IQueryable<SessionData>` | Leaks EF and storage shape | Named method or specification |
| `db.Sessions.IgnoreQueryFilters()` | Disables tenant isolation silently | `elevation.CrossTenant("reason")` |
| New tenant table without `EnableYafRowLevelSecurity` | Only one isolation layer | Add it; the RLS assert test catches it |
| `Where(x => x.TenantId == tenantId)` by hand | Redundant, and wrong under elevation | Rely on the filter |
| Encrypting in the repository | Duplicates the repository base's protection step; wrong key and associated data | Classify the memento member |
| `OrderBy(x => x.StartsAt)` only | Unstable paging on ties | `.ThenBy(x => x.Id)` |
| `Database.Migrate()` in `Program.cs` of the API | Runtime role needs DDL; races between instances | Migrator worker |
| Dropping a column in the same release that stops using it | Breaks the previous version during rollout | Contract in the next release |
| EF in-memory provider or SQLite in tests | Different semantics; no RLS | PostgreSQL container |

## Verify

- `dotnet build YAF.slnx` (YAF5001).
- `dotnet ef migrations has-pending-model-changes` (in the infrastructure project) reports none.
- `dotnet test --project <service tests>/Api.IntegrationTests` (Docker needed): tenant isolation, concurrency (two commands that each change only a child: one gets `Conflict`), RLS assert, memento round trip through the database, a protected value stored as ciphertext and read back clear through the read model.
- `fields.approved.txt` unchanged, or changed only by added lines.
- Data inventory regenerated if classified members changed.

## References

- `docs/articles/persistence.md` — interceptors, generated data classes, field protection, audit chain and sealing, database roles, migrator.
- `adr/0010-persistence-tenancy-and-unit-of-work.md`, `adr/0011-data-protection-at-rest.md` — read before changing isolation or the save model.
- `samples/conference/` `src/Services/Catalog/Infrastructure/Persistence/` and `src/Services/Catalog/Migrator/`.
- Skills: `yaf-domain-modeling` (memento side), `yaf-privacy-and-data-classification`, `yaf-testing`.
