---
name: yaf-domain-modeling
description: Rules and steps for modelling a YAF domain - aggregates, entities, typed ids, memento interfaces, value objects, smart enumerations, domain events, business rules, policies, specifications, state machines and privacy markers. Load BEFORE creating or changing any file in a service's Domain project (an aggregate, a behaviour, an Errors catalogue, a memento interface, a value object, a domain event, a rule or specification), before adding a member to a memento interface, and before reviewing domain code.
license: MIT
metadata:
  owner: yaf
  version: "1"
---

# YAF domain modelling

A domain project references only `YAF.Domain` (and through it `YAF.Core`). No ORM, mediator, validation library, logging or DI.

## When to load

- Adding an aggregate, child entity, value object or typed id.
- Adding or changing a behaviour method, a domain event, a rule, a policy, a specification or a state machine.
- Adding a member to a memento interface (it needs a classification and maybe retention).
- Reviewing a pull request that touches `Domain/`.

## Where does this go?

First match wins.

| The thing is… | It goes in |
|---|---|
| An identifier | `[TypedId] public readonly partial record struct <Name>Id;` next to its aggregate |
| Has identity and a lifecycle of its own, loaded and saved as a whole | Aggregate root, folder `Domain/<PluralName>/` |
| Has identity but lives and dies with one aggregate | Child `Entity<…>` inside that aggregate's folder |
| Defined only by its values | Value object (`record`), in the aggregate folder or `Domain/Shared/` |
| A closed set of values with data or behaviour | `Enumeration<TSelf>`; a plain `enum` when values carry nothing |
| A named condition used to check and to query | `Specification<TMemento>` in `Specifications/` |
| A single invariant with its own error | `IBusinessRule` in `Rules/` |
| A decision that varies by tenant, time or configuration | `IPolicy<TInput, TDecision>` (interface and default in the domain) |
| Allowed status transitions | `StateMachine<TState, TTrigger>` static field next to the status type |
| Logic across several aggregates, no state | Domain service in `Domain/Services/` |
| Orchestration, loading, saving, messaging | Not the domain: see `yaf-use-cases` |

## Rules

1. **REQUIRED** Ids are source-generated typed ids, UUIDv7, assigned by the domain at creation (`SessionId.New()`). No "empty until saved" id; no raw `Guid` named `*Id` on public members (YAF2009).
2. **REQUIRED** Aggregate constructors are `private` or `internal`. Creation goes through `static Result<T> Create(...)` or an `AggregateBuilder<T>` whose `Build()` returns `Result<T>`.
3. **REQUIRED** No public setters. Collections are exposed as `IReadOnlyCollection<T>` / `IReadOnlyList<T>` (architecture test).
4. **REQUIRED** State changes only through behaviour methods named in the ubiquitous language. A behaviour that can fail returns `Result` / `Result<T>`; it never throws for a business outcome.
5. **REQUIRED** Every behaviour leaves the aggregate satisfying `CheckInvariants()`. Check first with `Check(…)` or `CheckAll(…)`, then mutate in `.Then(…)`; there is no rollback helper.
6. **REQUIRED** Aggregates reference other aggregates by id only.
7. **REQUIRED** The aggregate knows no dispatcher, repository or unit of work. It queues events with `Raise(...)`; persistence collects them.
8. **REQUIRED** Domain events are immutable past-tense records carrying ids and facts. They never carry the aggregate instance.
9. **REQUIRED** Every error is an `ErrorDescriptor` field in a nested `Errors : IErrorCatalog` (YAF3001). See `yaf-results-and-errors`.
10. **REQUIRED** The memento interface is declared by the domain, nested in the originator. `Snapshot` writes every field; `Restore` reads every field and never validates. Members are typed: primitives, typed ids, enums, `Enumeration<T>`, value objects, their nullable forms and collections of child mementos. Do not flatten a status, an id or a value object to a `string` or `Guid`; specifications are written against the typed members.
11. **REQUIRED** Child collections are matched by id with `MementoCollections.Sync(...)`; the aggregate gives each child its id.
12. **REQUIRED** Mementos carry no behaviour: no encryption, input checking or logging. Protection comes from classification attributes, applied by the persistence mapping layer. The domain writes no data class: infrastructure declares `[MementoData] partial class <Entity>Data : <Entity>.IMemento;` and the generator emits its members (`yaf-persistence`).
13. **REQUIRED** Every `string`, `byte[]` and free-form member of a memento interface is classified (YAF2001). A memento with a `[PersonalData]` member implements `IDataSubjectOwnedMemento` or carries `[NoDataSubject("reason")]`, and has `[Retention(...)]` (YAF2002).
14. **REQUIRED** No static or ambient state: time is passed in as a value; nothing reads `TimeProvider`, configuration or a service from inside the domain.
15. **REQUIRED** Value objects are immutable records without id, tenant or free text holding personal data; fallible creation is `static Result<T> Create(...)`.
16. **REQUIRED** State machine definitions are static and side-effect free; guards needing aggregate data are business rules checked in the behaviour.
17. **RECOMMENDED** Split large aggregates into partials: `Session.cs`, `Session.State.cs`, `Session.Memento.cs`, `Session.Errors.cs`, `Behaviors/Session.<Verb>.cs`.
18. **RECOMMENDED** Use a builder only when creation takes more than about four inputs or has optional parts.
19. **RECOMMENDED** Use a specification only when the predicate is needed both to check and to query, or is named in the ubiquitous language.
20. **RECOMMENDED** Prefer the shipped value objects (`Money`, `DateRange`, `Period`, `EmailAddress`, `Percentage`) over new ones.

## How to

1. Write the failing domain test first (behaviour outcome, raised event, error **code**).
2. Add or extend the aggregate; declare errors; add the memento members with classification.
3. Add the memento round-trip test (`MementoAssert.RoundTrips`).
4. Only then move outward (generated data class, repository, use case).

```csharp
public partial class Session
{
    public Result ReserveSeat(AttendeeRef attendee)
    {
        var next = Lifecycle.Fire(Status, SessionTrigger.Reserve);
        if (next.IsFailure) return next.Errors.ToArray();

        return Check(new SeatLimitNotExceeded(_seats.Count, Capacity))
            .Then(() =>
            {
                var seat = Seat.Create(SeatId.New(), attendee);
                _seats.Add(seat);
                Raise(new SeatReserved(Id, seat.Id));
            });
    }
}
```

A full aggregate with child, memento, errors, state machine, rule, specification and tests: `references/examples.md`.

## Anti-patterns

| Wrong | Why | Instead |
|---|---|---|
| `public string Title { get; set; }` | Bypasses invariants | Behaviour method returning `Result` |
| `throw new InvalidOperationException("full")` | Business outcome as exception | `return Errors.Full.With(("Capacity", Capacity));` |
| `record SeatReserved(Session Session)` | Handlers get mutable, possibly stale state | Carry `SessionId`, `SeatId` and facts |
| `DateTimeOffset.UtcNow` inside a behaviour | Hidden state, banned API | Take `DateTimeOffset now` as a parameter |
| Encrypting or cleaning values in `SnapshotCore` | Persistence concern; needs static providers | Classify the memento member |
| `string Status { get; set; }` on a memento, `Enum.Parse` in `Restore` | Storage shape leaks into the domain; specifications compare strings | `SessionStatus Status { get; set; }`; persistence converts |
| Free-text notes on a value object | Cannot be classified per row or erased | Put it on an entity's memento, classified |
| `Session` holding a `Registration` reference | Crosses an aggregate boundary | Hold `RegistrationId` |
| Guard lambdas inside the state machine definition | Definition no longer printable or testable alone | `IBusinessRule` checked in the behaviour |
| Matching child mementos by position | Reordered or removed children corrupt state | `MementoCollections.Sync` by id |

## Verify

- `dotnet build YAF.slnx` (zero warnings; analyzers YAF2009, YAF2001, YAF2002, YAF3001 are errors).
- `dotnet test --project <service>/tests/Domain.Tests`.
- Memento round-trip test exists for every aggregate you touched.
- State machine test: every state reachable, every non-final state has an exit.
- If errors changed: `errors.approved.txt` updated deliberately (see `yaf-results-and-errors`).
- If classified members changed: `docs/compliance/data-inventory.md` regenerated.

## References

- `docs/articles/domain.md` — read for the full contract of each base type.
- `references/examples.md` — read before writing your first aggregate.
- `samples/conference/` Catalog service, `Domain/Sessions/` — the reference aggregate.
- `adr/0007-domain-model-patterns.md` — read when you want to deviate.
