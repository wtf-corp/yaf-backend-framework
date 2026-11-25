# ADR-0007 — Domain model patterns

> **Status** Accepted
> **Date** 2026-11
> **Deciders** Framework maintainers

## Context

The domain layer is where an application's value is, and where framework choices are hardest to change later. The prior design established the memento pattern for persistence and typed ids, which worked well. It also had weaknesses: aggregates were registered with an event dispatcher that had to be passed into builders and into `Restore`; domain events carried the aggregate instance; ids were empty until the store assigned them; encryption and sanitization ran inside the domain through process-wide providers; a validation library was a domain dependency; base types needed four generic parameters.

## Drivers

- The domain project depends on nothing but YAF's domain package.
- No static or ambient state in domain code; a domain test needs no setup.
- Persistence must not shape the model.
- Common modelling needs (rules, queryable predicates, lifecycles, enumerations with behaviour, money) should have one framework answer.
- Privacy properties of data are declared where the data is defined.

## Options considered

| Option | For | Against |
|---|---|---|
| Map aggregates directly with the ORM | Least code | ORM constraints leak in (constructors, setters, navigation); encapsulation erodes; classification and field protection lose their single declaration point |
| Event sourcing | Full history | Large shift in skills and tooling; not needed by most services |
| Domain-owned mementos with primitive members and hand-written data classes | No generator to own | A data class to write and keep in step per entity; specifications compare statuses as strings and ids as raw guids |
| **State through domain-owned mementos with typed members and generated data classes; behaviour-only aggregates that queue events** | Domain free of persistence and dispatch; explicit state contract in the domain's own types | Snapshot/restore code per aggregate; one more generator |
| Keep the prior base types unchanged | No migration | Keeps the weaknesses above |

## Decision

Contract: `spec/02-domain.md`.

**Mementos owned by the domain.** An aggregate's state leaves and enters only through a memento interface nested in the aggregate. Infrastructure implements it as a data class. `Snapshot` writes every field; `Restore` reads every field and never validates. A state that cannot be restored throws `CorruptStateException` and is a security event. A round trip must be lossless. Child collections are matched by id. Mementos carry no behaviour: no encryption, no input checks, no logging.

**Typed memento members.** A memento member may be a primitive, a typed id, an enum, an `Enumeration<T>`, a value object, a nullable form of these, or a collection of child mementos. The domain states its state in its own types; how each is stored is decided in `YAF.Persistence.EfCore` (generic converters applied by convention, complex types for multi-value value objects). Nothing EF-related is generated into or referenced from a domain project.

**Generated data classes.** In the infrastructure project the developer writes `[MementoData] internal sealed partial class SessionData : Session.IMemento;` and a generator emits the properties, child collections and child factories. Store-only members go into the other partial part. `Snapshot` and `Restore` stay hand-written in the aggregate, because they are the aggregate's own statement of what its state is, and `MementoAssert.RoundTrips<T>()` checks them.

**Typed ids.** A source-generated `readonly record struct` per aggregate and entity, UUID version 7, **assigned by the domain at creation**. There is no empty id; `default` is invalid. A raw `Guid` named `*Id` on a public member is flagged. The store converter is one generic `TypedIdConverter<TId>` in the persistence package, over the static members of `ITypedId<TSelf>`.

**Aggregates.** `AggregateRoot<TId, TSelf, TMemento>` with a static abstract `Restore`. Private or internal constructors; creation through a factory or builder returning `Result<T>`; no public setters; read-only collections; behaviour methods in the ubiquitous language; invariants checked at the end of every behaviour; other aggregates referenced by id. The concurrency version belongs to the root and changes on every save of the aggregate, also when only a child changed (ADR-0010); children have no version.

**Events are queued, not dispatched.** An aggregate calls `Raise(event)` and knows no dispatcher, unit of work or repository. The persistence layer's aggregate tracker collects events (ADR-0010) and the unit of work dispatches them inside the transaction (ADR-0009). Domain events are immutable records in the past tense that carry ids and facts, never the aggregate instance.

**Validation without a library.** Invariants are `CheckInvariants()` plus named `IBusinessRule` classes. Input-shape validation is an application concern.

**Modelling building blocks.**

| Block | Use |
|---|---|
| `IBusinessRule` | A named, pure invariant with its declared error; unit-tested alone |
| `IPolicy<TInput, TDecision>` | A decision that varies by tenant, time or configuration; interface and default in the domain, selection in the application |
| Domain service | Stateless logic spanning aggregates; receives them as arguments |
| `Specification<T>` | A named predicate over the **memento interface** and its typed members, usable in memory and translated to a store query |
| `StateMachine<TState, TTrigger>` | A static, printable definition of allowed transitions; `Fire` returns `Result`; renders to a diagram; guards needing data are rules in the behaviour |
| `Enumeration<TSelf>` | Enumerations with behaviour or data, identified by a stable code |
| Value objects | Immutable records created through `Create` returning `Result<T>`; shipped: `Money`, `DateRange`/`Period`, `EmailAddress`, `Percentage`. Nothing country-specific |

**No static state.** No static provider of time, encryption, input handling or dispatch. Time is passed in; event timestamps are stamped by the tracker from `TimeProvider`.

**Privacy markers.** Classification attributes, the data-subject marker, retention and erasure actions are declared on memento interface members and types, together with the storage markers: `[Searchable]` (encrypted, plus a keyed hash for exact match), `[StoredInClear("reason")]` (the reasoned opt-out from encryption for a `[PersonalData]` member that must be sorted or searched) and `[FieldId]` (pins the logical field identity that ciphertext is bound to). The domain only declares and always sees clear values; persistence and the privacy package act (ADR-0005, ADR-0011).

## Consequences

**Positive**

- A domain test is `new`, call, assert. No container, no fakes for infrastructure.
- Events cannot expose mutable or stale aggregate state to handlers.
- Ids exist before persistence, so children, events and outbox messages can refer to them in the same transaction.
- Protection of stored data no longer depends on call order inside the domain.
- Specifications and state machines give recurring needs one shape, and specifications read in the domain's types.
- Adding a state field touches the memento interface, `Snapshot` and `Restore`; the data class follows by itself.

**Negative / costs**

- Snapshot and restore code for every aggregate and child entity.
- The framework owns the data-class generator and the store mapping of every allowed member type; a member type outside the list needs a hand-written converter.
- Specification translation supports mapped members only, and a protected member can be matched only by equality, and only when `[Searchable]`.
- Classification and retention attributes add annotations to every memento interface.
- Aggregate-level versioning makes two concurrent changes to different children of one aggregate conflict; the second is retried or reported. This is the intended meaning of an aggregate boundary, and a reason to keep aggregates small.
- UUIDv7 ids reveal creation time; acceptable for internal identifiers, to be considered before exposing them publicly.

## Enforcement

| Mechanism | What it checks |
|---|---|
| Architecture rules in `YAF.Testing.Architecture` | Domain references only `YAF.Domain`/`YAF.Core`; no public constructors or setters on aggregates; collections read-only; events immutable and not referencing aggregate types |
| Analyzer `YAF2009` | Raw `Guid` id parameters on public domain or application members |
| Analyzers `YAF2001`, `YAF2002` | Memento members classified; retention on personal-data types |
| Analyzer `YAF2008` | `[StoredInClear]` on sensitive or secret data, or without a reason |
| Analyzer `YAF3001` | Errors declared only in catalogues |
| `MementoAssert.RoundTrips` (template test) | Lossless snapshot and restore, including child add, keep, remove, and every member type |
| Generator tests | The data class emitted for `[MementoData]` |
| Approved file `fields.approved.txt` | Logical field ids of protected members do not change by accident |
| Concurrency test | Two commands changing different children of one aggregate cannot both commit |
| State machine test helper | Every state reachable; every non-final state has an exit |
| Banned API list | Clock and `Guid.NewGuid()` in domain code |

> **Amended 2026-11-25**, by the owner's decisions of 2026-11-25 (`specs/0008-m1-maintenance/plan.md`, decision log): the analyzer id is renamed from `YAF1001` to `YAF2009`, following the numbering of `spec/01-repository-and-packages.md` §2.1. The decision of this ADR is unchanged.

## Guidance for agents

- Start an aggregate from the template's layout: declaration, state, memento, errors, id, `Behaviors/`, `Events/`, `Rules/`.
- Put a business rule in the aggregate or a rule class. If you are writing an `if` about business state in a handler, move it.
- Return `Result` from anything that can fail for a business reason; never throw for it.
- Raise events inside the behaviour that causes them; give them ids and facts only. Never pass `this`.
- Generate ids with the typed id's `New()` in the factory. Never use `Guid.NewGuid()`, never leave an id unset, never accept a raw `Guid` where a typed id exists.
- When you add a state field: add it to the memento interface with a classification, to `Snapshot`, to `Restore`, to a migration, and extend the round-trip test. The data class is generated; do not write its properties by hand.
- Give a memento member the domain's type (typed id, enum, smart enumeration, value object). Do not flatten it to a string or a `Guid`, and do not compare a status with a string in a specification.
- Never add an EF Core reference, converter or attribute to a domain project. Store mapping lives in the persistence package and the service's infrastructure.
- Use `[StoredInClear("reason")]` only when the spec says the member must be sorted or searched, and write the real reason. Never on sensitive or secret data.
- When you rename a memento type or a protected member, keep its recorded field id with `[FieldId]`; a changed line in `fields.approved.txt` means stored data would become unreadable.
- Never add a dependency, a logger, a clock, a service or a static to the domain project. Pass values in.
- Do not add a setter, a public constructor or a mutable collection to satisfy a test or a serializer.
- Use a specification only when a predicate is both queried and checked, or named in the language; use a state machine when there are more than two states with rules between them.

## Related

- ADR-0008 — results and errors
- ADR-0009 — pipeline and domain event dispatch
- ADR-0010 — persistence and the aggregate tracker
- ADR-0011 — data protection driven by classification
- `spec/02-domain.md`
