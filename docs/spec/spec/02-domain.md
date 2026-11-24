# 02 — Domain building blocks (`YAF.Core`, `YAF.Domain`)

Normative words (MUST, SHOULD, MAY) follow RFC 2119. Type names are the contract; member lists are the minimum.

## 1. Goals

- A service's domain project references **only** `YAF.Domain` (which references `YAF.Core`). No ORM, no mediator, no validation library, no logging, no DI container.
- No process-wide static state. Nothing in the domain reads a static provider (clock, encryption, sanitizer). Anything ambient is passed in or applied by an outer layer.
- State leaves and enters an aggregate only through a **memento** whose interface the domain owns.
- Every failure a domain operation can produce is a declared error (see `03-results-and-errors.md`); domain code does not throw for business outcomes.

## 2. `YAF.Core`

Target `net11.0`. Dependencies: `Microsoft.Extensions.Compliance.Abstractions` only. `YAF.Core` is the one package marked `IsAotCompatible` and `IsTrimmable`: it uses no reflection and no runtime code generation. `YAF.Domain` makes no such claim (specifications compile expression trees, §9).

| Type | Purpose |
|---|---|
| `Result`, `Result<T>`, `Error`, `ErrorDescriptor`, `ErrorKind`, `IErrorCatalog` | See `03-results-and-errors.md` |
| `ITypedId`, `ITypedId<TSelf>` | Typed identifier contract |
| `TenantId`, `UserId`, `DataSubjectId`, `CorrelationId` | Framework identifiers (`readonly record struct` over `Guid`) |
| `DataClassification` attributes | `[PublicData]`, `[InternalData]`, `[PersonalData]`, `[SensitivePersonalData]`, `[SecretData]`, and the storage markers `[Searchable]`, `[StoredInClear("reason")]`, `[FieldId("…")]` (see §11 and `11-privacy-and-compliance.md`) |
| `Guard` | Argument checks that throw (`ArgumentException` family) for programmer errors only |
| `Optional<T>` | Distinguishes "not supplied" from `null` in patch-style requests |

## 3. Typed identifiers

```csharp
[TypedId]                       // the generator shipped inside YAF.Core emits the rest
public readonly partial record struct SessionId;
```

The generator emits: the `ITypedId<SessionId>` implementation, `Guid Value`, `static SessionId New()` (`Guid.CreateVersion7()`), `static SessionId From(Guid)`, `TryParse`/`Parse` (`IParsable<SessionId>`), `ToString()` (the guid, format `D`), a `System.Text.Json` converter and a `TypeConverter` (route and query binding).

Nothing EF-related is generated into a domain project. `ITypedId<TSelf>` declares `static abstract TSelf From(Guid value)` next to `Guid Value`, and that is all the store needs: `YAF.Persistence.EfCore` holds one generic `TypedIdConverter<TId>` and applies it by convention to every typed-id property (`05-persistence.md`).

Rules:

- IDs are UUID version 7, assigned **by the domain at creation**. There is no "empty until saved" id. `default(SessionId)` is invalid and `Guard`ed at aggregate construction.
- An id type is never reused across aggregate types. Analyzer `YAF1001` flags a raw `Guid` parameter named `*Id` on a public or protected member of a public type, in any project that references `YAF.Core`.

  > **Amended 2026-11-24**, by the owner's decision of 2026-11-24 (`specs/0006-core/plan.md`, decision log Q4): an analyzer cannot see a layer, so the scope is every public type of a project that references `YAF.Core`, which matches "ids through typed ids".
- Child entities get their id from the aggregate that creates them.

## 4. Entities and aggregate roots

```csharp
public abstract class Entity<TId, TSelf, TMemento>
    where TId : struct, ITypedId<TId>
    where TSelf : Entity<TId, TSelf, TMemento>, IOriginator<TSelf, TMemento>
    where TMemento : IEntityMemento
{
    public TId Id { get; }
    protected Entity(TId id);
    public void Snapshot(TMemento memento);              // writes base fields, then SnapshotCore
    protected abstract void SnapshotCore(TMemento memento);
    protected abstract void RestoreCore(TMemento memento);
    protected void RestoreFrom(TMemento memento);         // called by the static Restore: sets base state (version), then RestoreCore
    protected virtual Result CheckInvariants() => Result.Success();
}

public interface IOriginator<TSelf, TMemento>
{
    static abstract TSelf Restore(TMemento memento);     // no validation: stored state is trusted
}

public abstract class AggregateRoot<TId, TSelf, TMemento> : Entity<TId, TSelf, TMemento>, IAggregateRoot
{
    public AggregateVersion Version { get; }             // concurrency token of the whole aggregate, opaque to the domain
    public IReadOnlyList<IDomainEvent> DomainEvents { get; }
    protected void Raise(IDomainEvent domainEvent);
    IReadOnlyList<IDomainEvent> IAggregateRoot.DequeueDomainEvents();
}
```

Rules (each has an architecture test in `YAF.Testing.Architecture`, see `15-testing.md`):

1. Constructors are `private` or `internal`. Creation goes through a factory method or builder that returns `Result<T>`.
2. No public setters. Collections are exposed as `IReadOnlyCollection<T>` / `IReadOnlyList<T>`.
3. State changes only through behaviour methods named in the ubiquitous language. A behaviour that can fail returns `Result` / `Result<T>`; one that cannot returns `void`.
4. Behaviours **check first, then mutate**: all rules are evaluated against the current state and the inputs before any field changes, so a failed behaviour leaves the aggregate untouched. `CheckInvariants()` states the invariants that must hold after every behaviour; factories and builders call it before returning, and `YAF.Testing`'s `AggregateAssert.InvariantsHold(aggregate)` calls it in tests after each behaviour. There is no rollback helper.
5. Aggregates reference other aggregates by id only.
6. An aggregate does not know the dispatcher, the unit of work or the repository. It queues events with `Raise`; the persistence layer's aggregate tracker collects them (see `05-persistence.md`). This replaces passing a dispatcher into builders and `Restore`.
7. The version belongs to the **root**. Saving an aggregate writes a new version on the root row even when only a child entity changed, so two concurrent commands on one aggregate always conflict and an invariant that spans children (a seat limit) cannot be broken by parallel writes. Child entities carry no version of their own.
8. Partial classes split large aggregates: `Session.cs` (declaration, factory), `Session.State.cs` (properties), `Session.Memento.cs`, `Session.Errors.cs`, `Behaviors/Session.<Verb>.cs`.

### 4.1 Builders

```csharp
public abstract class AggregateBuilder<TAggregate> where TAggregate : IAggregateRoot
{
    public Result<TAggregate> Build();        // creates, applies, CheckInvariants, raises the "created" event
    protected abstract TAggregate Create();
}
```

A builder is used where creation takes more than about four inputs or has optional parts; otherwise a `static Result<T> Create(...)` factory on the aggregate is enough. Builders hold no unmanaged state and are not `IDisposable`.

### 4.2 Folder layout of an aggregate

```
Domain/
├── Sessions/                       # one folder per aggregate, plural noun
│   ├── Session.cs
│   ├── Session.State.cs
│   ├── Session.Memento.cs
│   ├── Session.Errors.cs
│   ├── SessionId.cs
│   ├── SessionStatus.cs            # state machine definition lives next to it
│   ├── Behaviors/Session.Publish.cs
│   ├── Events/SessionPublished.cs
│   ├── Rules/SeatLimitNotExceeded.cs
│   └── Specifications/OpenSessions.cs
├── Shared/                         # value objects used by more than one aggregate
└── Services/                       # domain services (stateless, pure)
```

## 5. Mementos

A memento interface is declared by the domain, nested in the originator, and implemented by a data class in the service's infrastructure (the persistence model). The data class is generated from the interface.

```csharp
public partial class Session
{
    public interface IMemento : IAggregateMemento       // Id, TenantId, Version, audit stamps
    {
        [PublicData] string Title { get; set; }
        [PersonalData(PersonalDataCategory.Contact, Purpose = "speaker-communication")]
        EmailAddress? SpeakerEmail { get; set; }
        int Capacity { get; set; }
        SessionStatus Status { get; set; }              // enum; stored as a string
        SessionFormat Format { get; set; }              // smart enumeration; stored by code
        TrackId TrackId { get; set; }                   // typed id of another aggregate
        Money Price { get; set; }                       // multi-value value object
        DateTimeOffset StartsAt { get; set; }
        IList<Seat.IMemento> Seats { get; }
        Seat.IMemento NewSeat();                        // factory: the domain never names the concrete class
    }
}

// in the service's infrastructure project: the whole hand-written part of the data class
[MementoData]
internal sealed partial class SessionData : Session.IMemento;
```

**Member types.** A memento member may be a primitive, a typed id, a C# `enum`, an `Enumeration<T>` smart enumeration, a value object, the nullable form of any of these, or a collection of child mementos. The domain writes its own types; it does not translate them to strings or raw guids. How each is stored is the persistence layer's business (`05-persistence.md`): typed ids and smart enumerations through generic converters applied by convention, single-value value objects through a converter, multi-value ones (`Money`, `Period`) as complex types.

**Generated data class.** `[MementoData]` on a `partial` class that names one memento interface makes the generator (in `Analyzers`, shipped inside `YAF.Core`) emit every property of the interface and of its base contracts, the child collections, and the `New<Child>()` factories, which return the generated data class of the child. Members that only the store needs (a navigation, a shadow column, an index helper) go into the developer's own partial part. A hand-written data class remains possible and is checked by the same round-trip test.

Base contracts in `YAF.Domain.Mementos`:

| Interface | Members | Filled by |
|---|---|---|
| `IEntityMemento` | `Guid Id` | domain (`Snapshot`) |
| `IVersionedMemento` | `Guid Version` | persistence (a new token for the root each time the aggregate is saved in a transaction) |
| `ITenantOwnedMemento` | `Guid TenantId` | persistence (from tenant context; a mismatch is refused) |
| `IStampedMemento` | `CreatedAt`, `CreatedBy`, `ModifiedAt`, `ModifiedBy` | persistence (from `TimeProvider` and identity context) |
| `ISoftDeletableMemento` | `DeletedAt`, `DeletedBy` | persistence, on `Remove` |
| `IDataSubjectOwnedMemento` | `Guid DataSubjectId` | domain |
| `IAggregateMemento` | all of the first four | |
| `IOwnedEntityMemento` | `Id`, stamps; no tenant and no version (both covered by the root) | |

Rules:

- `Snapshot` writes every field every time; `Restore` reads every field and never validates (stored state is trusted; a state that cannot be restored throws `CorruptStateException`, which is a bug or tampering signal and is raised as a security event).
- Child collections are matched **by id**: `MementoCollections.Sync(memento.Seats, _seats, memento.NewSeat)` reuses the memento of a child that stays, creates one for a new child, removes the ones that are gone. Position-based matching is not offered.
- A value object is a memento member as it is (`Money Price`); the domain does not flatten it into `PriceAmount` and `PriceCurrency`. The store decides the columns.
- Mementos carry **no behaviour**: no encryption, no input checks, no logging. Protection of classified members is applied by the persistence layer's mapping from the classification attributes on the memento interface (see `05-persistence.md` §6): the aggregate always reads and writes clear values. This removes the process-wide providers the previous design needed in the domain.
- A memento round trip (`Snapshot` → `Restore` → `Snapshot`) MUST be lossless. `Snapshot` and `Restore` are hand-written in the aggregate, so this is tested: `YAF.Testing` ships `MementoAssert.RoundTrips<T>()` (with an overload taking an aggregate and a memento factory), and the service template includes the test for every aggregate.

## 6. Value objects

- Immutable `record` (class or `readonly record struct`), equality by value, created through `static Result<T> Create(...)` when construction can fail, validated once at creation.
- `ValueObject` is an optional marker base (`abstract record ValueObject`); it exists so architecture tests can find value objects.
- A value object never has an id, a tenant, or a classification-driven side effect. Free text and personal data live in entities, where their memento members are classified.

### 6.1 Value objects shipped in `YAF.Domain.ValueObjects`

| Type | Notes |
|---|---|
| `Money(decimal Amount, Currency Currency)` | Arithmetic refuses mixed currencies with a `Result`; `Currency` is an ISO 4217 code with minor units; `Allocate` splits without losing a minor unit; rounding mode is explicit |
| `DateRange(DateOnly Start, DateOnly End)` / `Period(DateTimeOffset Start, DateTimeOffset End)` | Inclusive start, exclusive end for `Period`; `Overlaps`, `Contains`, `Intersect` |
| `EmailAddress` | Syntax check only (one `@`, non-empty parts, length limits from RFC 5321), normalised domain case; classified `[PersonalData]` at the type level |
| `Percentage(decimal Value)` | 0–100 with `Of(Money)`, `Of(decimal)` |

Nothing country-specific ships in the framework.

### 6.2 Smart enumerations

```csharp
public sealed record SessionFormat : Enumeration<SessionFormat>
{
    public static readonly SessionFormat Talk = new("talk", "Talk", maxSpeakers: 2);
    public static readonly SessionFormat Workshop = new("workshop", "Workshop", maxSpeakers: 4);
    public int MaxSpeakers { get; }
}
```

`Enumeration<TSelf>` gives `Code` (stable, stored and serialized), `Name`, `All`, `FromCode` returning `Result<TSelf>`, and a JSON converter by code. The store converter is not here: `YAF.Persistence.EfCore` has a generic `EnumerationConverter<T>` applied by convention. Use a C# `enum` when the values carry no behaviour or data; both are stored as strings.

## 7. Domain events

```csharp
public abstract record DomainEvent : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.CreateVersion7();
    public DateTimeOffset OccurredAt { get; init; }     // stamped by the aggregate tracker from TimeProvider
}

public sealed record SessionPublished(SessionId SessionId, int Capacity) : DomainEvent;
```

- Past-tense name, immutable, carries ids and the facts a handler needs. It MUST NOT carry the aggregate instance (the previous design did; handlers could then mutate or re-read stale state).
- Raised inside the behaviour that causes it. Dispatched in-process, inside the command's transaction, after the changes that raised it have been flushed to the store and before commit (see `04-application.md` §6), so a handler that queries sees them.
- No personal data unless a handler in the same bounded context needs it; analyzer `YAF2003` warns on a classified member in a type that reaches an integration event.

## 8. Business rules and policies

```csharp
public interface IBusinessRule
{
    ErrorDescriptor Error { get; }
    bool IsSatisfied();
}

// inside a behaviour
return Check(new SeatLimitNotExceeded(_seats.Count, Capacity))
    .Then(() => { _seats.Add(seat); Raise(new SeatReserved(Id, seat.Id)); });
```

- A rule is a small named class in `Rules/`, constructed with the values it needs, pure, unit-tested on its own. `Check(params IBusinessRule[])` on the entity base returns the first broken rule's error (or all of them with `CheckAll`).
- A **policy** is a decision that varies by tenant, time or configuration: `IPolicy<TInput, TDecision>` with `TDecision Decide(TInput input)`. The domain declares the interface and the default; the application layer selects the implementation. Example: `ICancellationPolicy` deciding a refund percentage.
- A **domain service** is a stateless class for logic spanning aggregates. It takes aggregates or values as arguments and never loads anything itself.

## 9. Specifications

```csharp
public abstract class Specification<T>
{
    public abstract Expression<Func<T, bool>> ToExpression();
    public bool IsSatisfiedBy(T candidate);                    // expression compiled once, cached
    public Specification<T> And(Specification<T> other);
    public Specification<T> Or(Specification<T> other);
    public Specification<T> Not();
}

public sealed class OpenSessions(DateTimeOffset now) : Specification<Session.IMemento>
{
    public override Expression<Func<Session.IMemento, bool>> ToExpression()
        => s => s.Status == SessionStatus.Published && s.StartsAt > now;
}
```

- `T` is the aggregate's **memento interface**: it is the domain-owned description of the state, and it is what the store can query. Its members are the domain's own types (§5), so a specification compares a status with the enum value and an id with a typed id, never with a string. `Specification<TAggregate>` over the aggregate itself is allowed for purely in-memory rules.
- Supported on typed members in a specification that is used to query: equality and `Contains` on typed ids, enums and smart enumerations; comparison and equality on single-value value objects; member access on multi-value ones (`s.Price.Amount > 0`). A member that is stored protected (§11) cannot be filtered or sorted on, except by equality when it is `[Searchable]`; the translation refuses anything else with a message naming the member.
- `IsSatisfiedBy` compiles the expression at run time. This is why `YAF.Domain` is not marked AOT-compatible.
- A repository method accepts a specification (`ListAsync(Specification<Session.IMemento> spec, Page page, ct)`); `YAF.Persistence.EfCore` rewrites the expression onto the concrete entity type. Specifications never expose `IQueryable`.
- A specification holds values, not services. Time is passed in.
- Use a specification when the same predicate is needed both to query and to check, or when it is named in the ubiquitous language. A one-off query filter does not need one.

## 10. State machines

```csharp
public static readonly StateMachine<SessionStatus, SessionTrigger> Lifecycle =
    StateMachine<SessionStatus, SessionTrigger>.Define(b => b
        .From(SessionStatus.Draft).On(SessionTrigger.Publish).To(SessionStatus.Published)
        .From(SessionStatus.Published).On(SessionTrigger.Cancel).To(SessionStatus.Cancelled)
        .From(SessionStatus.Published).On(SessionTrigger.Complete).To(SessionStatus.Completed)
        .Otherwise(Errors.TransitionNotAllowed));
```

- The definition is static, immutable and side-effect free. `Lifecycle.Fire(current, trigger)` returns `Result<TState>`; the behaviour assigns the state and raises the event. Guards that need aggregate data are business rules checked in the behaviour, not lambdas inside the definition, so the definition stays printable.
- `Lifecycle.ToMermaid()` renders the diagram; the docs build embeds it, and a test (`StateMachineAssert.IsWellFormed(Lifecycle, initial, finals)` in `YAF.Testing`) asserts every state is reachable and every non-final state has an exit.
- `PermittedTriggers(current)` feeds API responses that tell a client what it may do next.

## 11. Privacy-aware domain

| Concept | Type | Rule |
|---|---|---|
| Classification | `[PersonalData(category, Purpose = …)]`, `[SensitivePersonalData]`, `[SecretData]` on memento interface members and on request/response/event members | Every `string`, `byte[]` and free-form member of a memento interface MUST be classified (`[PublicData]` or `[InternalData]` count); a value object classified at the type level (`EmailAddress`) brings its classification with it. Analyzer `YAF2001` fails the build otherwise |
| Data subject | `IDataSubjectOwnedMemento.DataSubjectId` | Any memento with a `[PersonalData]` member MUST say whose data it is, or be marked `[NoDataSubject("reason")]` |
| Retention | `[Retention("P2Y", After = RetentionAnchor.Closed, Basis = "contract")]` on the memento interface | Required on every type holding personal data (`YAF2002`). The anchor is a timestamp member the aggregate maintains |
| Soft delete | `ISoftDeletableMemento` | Opt-in; a soft-deleted type MUST have a retention policy, which is its purge path |
| Legal hold | `LegalHold` aggregate in `YAF.Privacy` (stored through `YAF.Privacy.EfCore`) | Suspends purge and erasure for named subjects or records, with a reason and an owner |
| Erasure | `[OnErasure(ErasureAction.Delete | Anonymise | Shred | Keep, Reason = …)]` per member or type | `Keep` requires a reason naming the legal basis; it appears in the generated inventory |
| Protection at rest | implied by the classification | Every `[PersonalData]`, `[SensitivePersonalData]` and `[SecretData]` member is stored encrypted. The domain declares nothing more and always sees clear values |
| Exact-match lookup | `[Searchable]` on a protected member | The member stays encrypted **and** gets a keyed-hash column, so the store can find rows by equality. Nothing else (sorting, ranges, prefixes) works on a protected member |
| Stored in clear | `[StoredInClear("reason")]` on a `[PersonalData]` member | The opt-out from encryption, for a member the store must sort, range-filter or prefix-search (a surname in a sorted list). The reason is mandatory and appears in the generated data inventory. Analyzer `YAF2008` (error): the attribute on a `[SensitivePersonalData]` or `[SecretData]` member, or without a reason. Such a member is erased by overwriting through the generated eraser, not by destroying a key |
| Stable field identity | `[FieldId("registration.attendee-email")]`, optional | Ciphertext is bound to a logical field id, not to table and column names. The id defaults to `<memento type>.<member>` when the member is first declared and is recorded in `fields.approved.txt`; after a rename of the type or the member the recorded id is pinned with `[FieldId]`, so existing ciphertext stays readable (`05-persistence.md` §6) |

The mechanisms that act on these markers (encryption, per-subject keys, redaction, purge jobs, export and erasure) are specified in `05-persistence.md` §6 and `11-privacy-and-compliance.md`. The domain only declares.

## 12. Acceptance criteria

- `YAF.Domain` and `YAF.Core` build with zero warnings, public API fully XML-documented, and have no package dependency other than the one named in §2.
- Unit tests cover: typed id generation and parsing; `Result` propagation through `Check` and `Apply`; memento round trip including child add, keep and remove and members of every allowed type (typed id, enum, smart enumeration, single- and multi-value value object, nullable forms); every shipped value object including `Money.Allocate` remainders; `Enumeration` lookup; specification composition over typed members; state machine reachability and `ToMermaid`.
- Generator tests compile snippets in memory and assert the data class emitted for `[MementoData]`: base-contract members, typed members, child collections and factories, and the diagnostic for a class that is not `partial` or names no memento interface.
- The analyzers named here have tests that compile a violating snippet and assert the diagnostic id.
