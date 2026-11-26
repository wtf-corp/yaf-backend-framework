# 0012 — Maintenance of M2: plan

**Spec** `specs/0000-framework/` (here `docs/spec/`): `02` §4 (rule 1, constructors), §6.1 (`Money`, `Percentage`), §10 (state machines), §11 (data subject), `03` §2 (error kinds). Source: issue #300, the follow-up list of plan `specs/0009-domain/plan.md` (M2, epic #225), issue #301, the follow-up list of plan `specs/0011-gates-check-reporting/plan.md`, and the owner's decision on both of 2026-11-26 (decision log). No `spec.md`: this is work of the initial build, so the plan cites the specification (`specs/AGENTS.md` rule 2) · **Status** Draft

## Approach

Issues #300 and #301 list the Minor and Nit findings of the reviews of plans 0009 and 0011 that started no fix round ("Token economy" in `AGENTS.md`). The owner decided on 2026-11-26: "you can address it all, let's not leave stuff behind" (decision log, first row). Every item of both issues is resolved below; none is dropped. One item (#248) was resolved by a merged pull request before this plan and needs no ticket. The other items become eight tickets, in four groups:

1. **Value objects (T1–T3).** `Percentage.Of(decimal)` computes `amount * Value / 100m` (`src/Domain/ValueObjects/Percentage.cs:47`), so the product overflows near `decimal.MaxValue` although the result, at most `amount`, fits. T1 divides first: `amount * (Value / 100m)`. T2 adds the missing test that `PercentageOutOfRange` has kind Validation. `Money.Allocate` multiplies the amount in minor units by a ratio before it divides by the sum (`src/Domain/ValueObjects/Money.cs:100–105`), so it can throw `OverflowException` for a result that fits; T3 documents that exception on both overloads and pins it with a test.
2. **The `Order` test fixture (T4).** `Order.IMemento` has the personal-data member `EmailAddress? ContactEmail` (`EmailAddress` is `[PersonalData(PersonalDataCategory.Contact)]`) but does not say whose data it is, which `02` §11 requires ("Any memento with a `[PersonalData]` member MUST say whose data it is, or be marked `[NoDataSubject("reason")]`"). `[NoDataSubject]` ships in M6 (plan 0009, Q4); `IDataSubjectOwnedMemento` ships in M2. T4 makes `Order.IMemento` extend `IDataSubjectOwnedMemento`: the contact email is the customer's, so `SnapshotCore` writes `CustomerId.Value` to `DataSubjectId`, and `OrderData` gets the property.
3. **Documentation (T5, T6).** T5 makes `docs/articles/domain.md` say what `02` §4 rule 1 says: aggregate constructors are `private` or `internal`. Its bullet under "Aggregates" says "The constructor is `private`" while Rules item 2 says "`private` or `internal`". T6 fixes the example in the XML doc of `StateMachine<TState, TTrigger>`: `public Result Publish() => Lifecycle.Fire(Status, …).Map(next => Status = next);` does not compile, because `Map` returns `Result<TState>` and `Result<T>` has no conversion to `Result` (`src/Core/Results/Result{T}.cs`). The review asked for an example that mirrors a compiled region. T6 adds the behaviour `Order.Cancel()` to the fixture in a region `state-machine-fire`, tests it, shows it in the article under "State machines", and rewrites the XML example with the same statements.
4. **Records and the factory (T7, T8).** CA1030 made two implementers rename tests that plan 0009 names: `AggregateRootTests.Raise_QueuesEventInOrder` became `DomainEvents_TwoRaised_QueuesThemInOrder` (#232, PR #271), and `StateMachineTests.Fire_…` became `Firing_…` (#242, PR #291). T7 brings the four names in rows T7 and T17 of plan 0009 in step with the merged tests and adds a decision-log row that says why. Editing a merged plan is a plan update pull request (`yaf-writing-plans` rule 12), written by a planner, as plan 0008 T12 was. T8 restores the space after `path:` in the `WORKTREE` constant of `inflight.test.mjs` (line 388), which PR #284 removed without need.

**The test names (T7).** Plan 0008 kept the merged ticket rows of plan 0006 as they were when a diagnostic id changed later, because a merged row is the record of what its ticket did (plan 0007, decision log). Here the rows do not match what their tickets did: the tests were named differently in the same pull requests. Correcting the names makes the rows the record of what was merged, which is the reason that decision gives. `create-tickets.mjs` edits no existing issue (`firstStatus` returns `null` for a closed one; a marker is never created twice), so the closed issues #232 and #242 stay as they are.

**The `domain-event-assert` region (#248).** The finding asked the testing article (plan 0009 T29) to use the region or drop it. PR #298 (T29) includes it: `docs/articles/testing.md` line 53 is `[!code-csharp[](../../tests/Testing.Tests/DomainEventAssertTests.cs#domain-event-assert)]`. Decided: keep it; no ticket. The whole-plan verification checks the include.

**`Money.Allocate` is documented, not changed (T3).** Truncating each share needs `total * ratio / sum` in that order: dividing first loses the minor units that the remainder loop hands out. A wider intermediate does not exist for `decimal`. Overflow needs an amount above about 10^19 units with a ratio near `int.MaxValue`, or above about 7.9 × 10^26 units with any ratio; documenting it is what the review asked for. `Add`, `Subtract` and `Multiply` overflow only when their result does not fit, as `decimal` arithmetic does, so they are not documented here.

**`Percentage.Of` changes its arithmetic (T1).** With `Value` from 0 to 100, `Value / 100m` is at most 1, so `amount * (Value / 100m)` never exceeds `amount` and cannot overflow. `Value / 100m` is exact while `Value` has at most 26 decimal places. The result can differ from today's in the 28th significant digit for some inputs (risks).

Files read to decide: `AGENTS.md` (with the path map), `docs/spec/agents/AGENTS.root.md`, `docs/spec/agents/AGENTS.nested.md` (`specs/`, `docs/`, `adr/`), `specs/AGENTS.md`, `docs/spec/factory/.claude/agents/planner.md`, the skills `yaf-writing-plans` (with its template) and `yaf-commits-and-pull-requests`, issues #300 and #301, the comments on #232, #237, #238, #242, #248, #251, #253 and #282, `specs/0008-m1-maintenance/plan.md`, `specs/0009-domain/plan.md` (rows T7, T17, T28, T29, decision log), `02` §4, §5, §11 (lines 89, 173, 300, 312), `docs/spec/factory/.github/factory/policy.json`, `classify.mjs`, `create-tickets.mjs`, and on `main` at bc5f4e0: `src/Domain/StateMachines/StateMachine{TState,TTrigger}.cs`, `src/Domain/ValueObjects/Money.cs`, `Percentage.cs`, `ValueObjectErrors.cs`, `EmailAddress.cs`, `src/Domain/Mementos/IDataSubjectOwnedMemento.cs`, `src/Domain/Entity{TId,TSelf,TMemento}.cs`, `src/Core/Results/Result.cs`, `Result{T}.cs`, `Error.cs`, `tests/Domain.Tests/Fixtures/` (`Order.cs`, `OrderData.cs`, `OrderLifecycle.cs`, `OrderStatus.cs`, `OrderErrors.cs`), `tests/Domain.Tests/StateMachines/StateMachineTests.cs`, `tests/Domain.Tests/Aggregates/AggregateRootTests.cs`, `tests/Domain.Tests/Mementos/OrderMementoTests.cs`, `tests/Domain.Tests/ValueObjects/MoneyTests.cs`, `PercentageTests.cs`, `tests/Testing.Tests/` (regions of every file, `Fixtures/Booking.cs`), `docs/articles/domain.md`, `docs/articles/testing.md`, `docs/spec/factory/.github/factory/tests/inflight.test.mjs` (line 388) and the diff of PR #284.

### Where each open item goes

| Item | Source | Owner's decision (2026-11-26) | Ticket |
|---|---|---|---|
| Minor: the XML-doc example of `StateMachine<TState, TTrigger>` does not compile | #300; #242 F1 | Address it | T6 |
| Minor: `Order.IMemento` holds personal data and does not say whose | #300; #251 F1 | Address it | T4 |
| Nit: `Money.Allocate` can throw `OverflowException`, not documented | #300; #237 F1 | Address it | T3 |
| Nit: `Percentage.Of` multiplies before it divides | #300; #238 F1 | Address it | T1 |
| Nit: no test asserts that `PercentageOutOfRange` has kind Validation | #300; #238 F2 | Address it | T2 |
| Nit: `domain.md` says "is `private`" and "`private` or `internal`" | #300; #253 F1 | Address it | T5 |
| Nit: the region `domain-event-assert`; use it in the testing article or drop it | #300; #248 F1 | Address it | none: resolved by PR #298, which includes it at `docs/articles/testing.md` line 53. Kept (decision log); checked by the whole-plan verification |
| Nit: tests renamed for CA1030 (`Raise_…`, `Fire_…`); update the plan text | #300; #232 F1, #242 F2 | Address it: bring plan 0009's test names in step | T7 |
| Nit: unrelated whitespace change at `inflight.test.mjs:388` | #301; #282 F1 | Address it | T8 |

### Alternatives rejected

| Alternative | Why not |
|---|---|
| T4: replace `ContactEmail` with a member that holds no personal data | The round trip would no longer cover a classified value object, and the fixture would stop showing what `02` §11 asks of a memento with personal data. `IDataSubjectOwnedMemento` is in M2 |
| T4: a separate `DataSubject` member on `Order`, as the `Booking` fixture of `Testing.Tests` has | The contact email belongs to the customer the order already references; a second id could disagree with `CustomerId` |
| T4: mark the memento `[NoDataSubject]` | Not in M2 (plan 0009, Q4), and the data does have a subject |
| T6: fix the XML example in place without a compiled copy | The broken example was hand-written; the review asked for one mirrored from a compiled region, and the article should show firing too |
| T6: show the compiled region in the XML doc with DocFX's `<code source=… region=…>` | The DocFX site comes with M14 (`18` §2); until then the XML doc is read in the IDE, where a reference to a file shows nothing |
| T6: put `Cancel()` on a new fixture to avoid sharing `Order.cs` with T4 | The article's state machine is `OrderLifecycle` over `OrderStatus`, the status of `Order`; a second aggregate would duplicate the fixture for one method. T6 waits for T4 instead |
| T3: rewrite `Allocate` so that it cannot overflow | Dividing first loses minor units; `decimal` has no wider type (Approach). The review asked for the documentation |
| T3: document `OverflowException` on `Add`, `Subtract` and `Multiply` too | They overflow only when the result does not fit, as `decimal` does; `Allocate` is documented because it can overflow when the result fits |
| T1 and T2 in one ticket | A behaviour change and a missing test are two concerns; T2 follows T1 because they share `PercentageTests.cs` |
| T7: keep plan 0009's rows and only add a decision-log row | The rows would still name tests that do not exist; the owner asked for the names to be brought in step |
| #248: remove the region `domain-event-assert` | The testing article includes it since PR #298; removing it would break the article |
| T8 inside another ticket | No other ticket touches the factory's tests; T8 is tier high and code-owned, so it stays as small as it can be |

## Affected packages

| Layer | Package / area | Change |
|---|---|---|
| Core | none | none |
| Domain | `YAF.Domain` (`src/Domain/`) | `Percentage.Of(decimal)` divides before it multiplies (T1); XML docs of `Money.Allocate` (T3) and `StateMachine<TState, TTrigger>` (T6) |
| Application | none | none |
| Infrastructure | none | none |
| Persistence | none | none |
| Http | none | none |
| Adapters | none | none |
| Testing (`Testing`, `Testing.Integration`) | none | none. Tests and fixtures in `tests/Domain.Tests` (T1–T4, T6) |
| Analyzers | none | none |
| Sample | none | none (the sample starts in M3) |
| Templates | none | none (M13) |
| Docs | `docs/articles/domain.md` | The constructor wording (T5); the firing of a transition (T6) |
| Plans (not a layer) | `specs/0009-domain/plan.md` | Four test names in rows T7 and T17 and a decision-log row (T7) |
| Factory (not a layer) | `docs/spec/factory/.github/factory/tests/inflight.test.mjs` | One space restored (T8) |

## Public API, schema and error-code delta

None. No line of `src/Domain/PublicAPI.Shipped.txt` or `PublicAPI.Unshipped.txt` changes: T3 and T6 change XML doc comments, T1 changes the body of an existing member, and `Order` is an internal test fixture. No error code, configuration key, contract, telemetry name, migration or dependency changes.

What a consumer of `YAF.Domain` would see, and which no category of the block covers:

- **Changed** `Percentage.Of(decimal)` and `Percentage.Of(Money)` return the share for amounts near `decimal.MaxValue` instead of throwing `OverflowException` (T1).
- **Changed** `Money.Allocate(int)` and `Money.Allocate(ReadOnlySpan<int>)` document `OverflowException`; their behaviour stays (T3).

Breaking: no. Nothing is released (plan 0008, Approach, "Not a breaking change"; no tag on `origin` and no release job on 2026-11-26).
New dependency: no.

```factory:delta
{ "publicApi": [], "schema": [], "errorCodes": [], "contracts": [], "configuration": [], "telemetry": [], "dependencies": [] }
```

## Schema and migration plan

None.

## What T7 changes

In `specs/0009-domain/plan.md`, Tickets table:

- row T7, "Failing test first": `AggregateRootTests.Raise_QueuesEventInOrder` becomes `AggregateRootTests.DomainEvents_TwoRaised_QueuesThemInOrder`;
- row T17, "Failing test first": `StateMachineTests.Fire_UndefinedTransition_FailsWithTheOtherwiseError` becomes `StateMachineTests.Firing_UndefinedTransition_FailsWithTheOtherwiseError`, `Fire_DefinedTransition_ReturnsTheTarget` becomes `Firing_DefinedTransition_ReturnsTheTarget`, and `Fire_SmartEnumerationStates_Works` becomes `Firing_SmartEnumerationStates_Works`.

A new decision-log row dated 2026-11-26 says that the four tests were renamed in PRs #271 and #291 because CA1030 flags a test method whose name starts with `Raise` or `Fire`, that the rows now name the merged tests, and cites this plan. No other text, the delta and the other tickets do not change.

## Test strategy

There are no requirements `R…`; the sections of `docs/spec/` and the items of #300 and #301 are the targets.

| What must be true | Section / item | Ticket | Level | Test project / check |
|---|---|---|---|---|
| A percentage of an amount near `decimal.MaxValue` is returned, not an overflow | `02` §6.1; #238 F1 | T1 | unit | `Domain.Tests` |
| `PercentageOutOfRange` has kind Validation | `03` §2; #238 F2 | T2 | unit | `Domain.Tests` |
| `Money.Allocate` documents and throws `OverflowException` when the product leaves the range of `decimal` | `02` §6.1; #237 F1 | T3 | unit, documentation check | `Domain.Tests`; T3's verify |
| The `Order` memento says whose personal data it holds | `02` §11; #251 F1 | T4 | unit | `Domain.Tests` |
| The article states one rule for aggregate constructors, the one of `02` §4 | `02` §4 rule 1; #253 F1 | T5 | documentation check | T5's verify |
| Firing a transition from a behaviour is compiled, tested, shown in the article and mirrored in the XML doc | `02` §10; #242 F1 | T6 | unit, documentation check | `Domain.Tests`; T6's verify |
| Plan 0009 names the merged tests | #232 F1, #242 F2 | T7 | documentation check | T7's verify; `validate-instructions.mjs` |
| The in-flight tests carry no unrelated whitespace change | #282 F1 | T8 | factory unit tests | `node --test`; T8's verify |
| The testing article includes `domain-event-assert` | #248 F1 | none | documentation check | whole-plan verification |

**Tests of code that already works (T2, T3).** A test added for existing behaviour passes when it is written. T2 shows its failure by a local break (the kind of `PercentageOutOfRange` changed to Conflict), reverted before committing; the pull request names the break and the failure it gave. T3 is a documentation ticket: its check first is the `grep` in its row, which fails before the change; its test pins the documented exception.

## Risks and rollback

| Risk | Likelihood | Mitigation | Rollback |
|---|---|---|---|
| `amount * (Value / 100m)` differs from `amount * Value / 100m` in the last significant digit for some inputs | Low | `Of` documents no rounding; the existing tests (`Of_Money_KeepsTheCurrency`, `Of_Decimal_Multiplies`) keep their exact expectations; nothing is released | Revert T1 |
| T4 changes the `aggregate` region that `domain.md` includes | Certain | The article shows the fixture as it is; the change adds one interface and one line, which illustrate `02` §11 | — |
| T6 waits for T4 and T5 | Certain | Both are small and tier low | — |
| T8 waits for the owner (tier high, code-owned) | Certain | One line; runs alongside the others | — |
| Editing plan 0009's Tickets table re-runs `factory-tickets.yml` | Medium | `create-tickets.mjs` never creates a marker twice and moves no closed issue (Approach) | Revert T7 |

## Verification commands

Git Bash, repository root, on the ticket's branch; `BASE` is `origin/main`.

V1, every ticket: `git diff --name-only $BASE...HEAD` lists only the ticket's touches; `node docs/spec/factory/.github/factory/size.mjs $BASE HEAD` stays at the estimate or below and within 400; `node .github/scripts/sync-references.mjs --check` and `node .github/scripts/validate-instructions.mjs` exit 0.

V2, every code and test ticket, expected 0 warnings, 0 errors, all tests passing:

    dotnet restore YAF.slnx && dotnet build YAF.slnx -c Release --no-restore && dotnet test --solution YAF.slnx -c Release --no-build

V3, T8: `node --test docs/spec/factory/.github/factory/tests/*.test.mjs` exits 0 with the same counts as on `$BASE`.

The whole plan, on `main` after the last ticket: V2; V3; `grep -q "DomainEventAssertTests.cs#domain-event-assert" docs/articles/testing.md && grep -q "#region domain-event-assert" tests/Testing.Tests/DomainEventAssertTests.cs` exits 0; the latest `ci.yml` run on `main` is green; every open item of #300 and #301 is closed by a ticket above or, for #248, by PR #298.

## Research

None needed. The behaviour relied on is in this repository: `decimal` arithmetic as `Money` and `Percentage` use it, the members of `Result` and `Result<T>` (`src/Core/Results/`), and `create-tickets.mjs`.

## Decision log

| Date | Decision | Reason | By |
|---|---|---|---|
| 2026-11-26 | **Owner's decision on #300 and #301:** "you can address it all, let's not leave stuff behind". Every item is resolved; none is dropped. The CA1030 item is resolved by bringing plan 0009's test names in step with the merged names, as a plan update; for the `domain-event-assert` region, check whether `docs/articles/testing.md` uses it and, if not, decide | Given to the orchestrator in the owner's own message of 2026-11-26 and relayed to the planner | Human owner, 2026-11-26 |
| 2026-11-26 | Folder `0012-m2-maintenance` | Next free number | Planner |
| 2026-11-26 | The region `domain-event-assert` stays; no ticket | PR #298 includes it in `docs/articles/testing.md` (line 53) | Planner |
| 2026-11-26 | T4: `Order.IMemento` extends `IDataSubjectOwnedMemento`, and `DataSubjectId` is the customer's id | The review's first option; `[NoDataSubject]` is not in M2 (plan 0009, Q4); the contact email is the customer's (alternatives) | Planner |
| 2026-11-26 | T3 documents `OverflowException` on both `Allocate` overloads and changes no arithmetic | The review asked for the documentation; dividing first loses minor units (Approach) | Planner |
| 2026-11-26 | T1 divides before it multiplies | The review's suggested fix; the result cannot exceed the amount | Planner |
| 2026-11-26 | T6 adds `Order.Cancel()` in a region, tests it, includes it in the article and mirrors it in the XML doc | The review asked for an example mirrored from a compiled region; an unused region is what #248 reported | Planner |
| 2026-11-26 | T7 edits two rows of a merged plan's Tickets table | The rows name tests that the same pull requests named differently; corrected, they record what was merged, which is why plan 0007 keeps merged rows | Planner |
| 2026-11-26 | Tickets on `tests/Domain.Tests/Fixtures/` and on `docs/articles/domain.md` run one after another (T6 after T4 and T5) | Shared files are overlap | Planner |

## Deviations from the specification

None.

## Tickets

T1, T3, T4, T5, T7 and T8 can start at once; at most two implementers run at the same time. T2 follows T1 (`PercentageTests.cs`). T6 follows T4 (`Order.cs`) and T5 (`domain.md`). T8 is tier high and code-owned and waits for the owner; every other ticket is tier low. T7 is written by a planner, not an implementer, since it edits a plan.

| Key | Title | Type | Area | Requirements | Depends on | Touches | Tier | Size | Failing test first | Verify |
|---|---|---|---|---|---|---|---|---|---|---|
| T1 | fix(domain): apply a percentage to amounts near the decimal limit | fix | domain | `02` §6.1 (`Percentage`); #238 F1; owner's decision | — | `src/Domain/ValueObjects/Percentage.cs`, `tests/Domain.Tests/ValueObjects/PercentageTests.cs` | low | 15 | `PercentageTests.Of_AmountNearDecimalMax_ReturnsTheShare` (50 % of `decimal.MaxValue` equals `decimal.MaxValue * 0.5m`) — fails: `OverflowException` from `amount * Value` | `grep -q "amount \* (Value / 100m)" src/Domain/ValueObjects/Percentage.cs` exits 0; `dotnet test --project tests/Domain.Tests -c Release` → passes; V2; V1 |
| T2 | test(domain): assert the kind of the percentage range error | test | domain | `03` §2 (error kinds); `ValueObjectErrors.PercentageOutOfRange` doc ("kind Validation"); #238 F2; owner's decision | T1 | `tests/Domain.Tests/ValueObjects/PercentageTests.cs` | low | 10 | `PercentageTests.Create_OutOfRange_FailsWithAValidationError` (the error of `Create(100.01m)` has kind `ErrorKind.Validation`) — fails against a local break (the descriptor declared with `Error.Conflict`), reverted before commit | `grep -q "ErrorKind.Validation" tests/Domain.Tests/ValueObjects/PercentageTests.cs` exits 0; `dotnet test --project tests/Domain.Tests -c Release` → passes; V2; V1 |
| T3 | docs(domain): document the overflow of money allocation | docs | domain | `02` §6.1 (`Money.Allocate`); #237 F1; owner's decision | — | `src/Domain/ValueObjects/Money.cs`, `tests/Domain.Tests/ValueObjects/MoneyTests.cs` | low | 20 | Check first: `grep -c 'exception cref="OverflowException"' src/Domain/ValueObjects/Money.cs` prints 0. Then `MoneyTests.Allocate_ProductBeyondDecimalRange_ThrowsOverflowException` (`new Money(1e18m, eur).Allocate(int.MaxValue, 1)`) pins the documented exception | `test "$(grep -c 'exception cref="OverflowException"' src/Domain/ValueObjects/Money.cs)" = 2` exits 0; `dotnet test --project tests/Domain.Tests -c Release` → passes; V2 (XML docs build without warnings); V1 |
| T4 | test(domain): name the data subject of the order fixture's memento | test | domain | `02` §11 (data subject); plan 0009 Q4; #251 F1; owner's decision | — | `tests/Domain.Tests/Fixtures/Order.cs`, `tests/Domain.Tests/Fixtures/OrderData.cs`, `tests/Domain.Tests/Mementos/OrderMementoTests.cs` | low | 25 | `OrderMementoTests.Snapshot_WritesTheCustomerAsDataSubject` (after `Amend` with a customer, `Snapshot` writes its `Value` to `DataSubjectId`) — fails to compile: `OrderData` has no `DataSubjectId`. `RoundTrips_EveryMemberType` and `RoundTrips_NullableMembersNull` still pass | `grep -q "IAggregateMemento, IDataSubjectOwnedMemento" tests/Domain.Tests/Fixtures/Order.cs && grep -q "DataSubjectId = CustomerId.Value" tests/Domain.Tests/Fixtures/Order.cs` exits 0; `dotnet test --project tests/Domain.Tests -c Release` → passes; V2; V1 |
| T5 | docs: state one rule for aggregate constructors in the domain article | docs | docs | `02` §4 rule 1 ("`private` or `internal`"); #253 F1; owner's decision | — | `docs/articles/domain.md` | low | 2 | `grep -c "The constructor is .private.\." docs/articles/domain.md` prints 1 | `f=docs/articles/domain.md; ! grep -q "The constructor is .private.\." $f && test "$(grep -c ".private. or .internal." $f)" -ge 2` exits 0; V1 |
| T6 | docs(domain): correct the state machine example | docs | domain | `02` §10 (`Fire`); `docs/AGENTS.md` rule 2 (code from compiled tests); #242 F1; owner's decision | T4, T5 | `src/Domain/StateMachines/`, `tests/Domain.Tests/Fixtures/Order.cs`, `tests/Domain.Tests/StateMachines/StateMachineTests.cs`, `docs/articles/domain.md` | low | 45 | `StateMachineTests.Cancel_PlacedOrder_IsCancelled` — fails to compile: `Order.Cancel()` does not exist. Also `Cancel_CancelledOrder_FailsWithTransitionNotAllowed` (the status stays `Cancelled`). `Cancel()` returns `Result`, fires `OrderLifecycle.Machine` and sets `Status` only on success, inside `#region state-machine-fire`; the XML example of `StateMachine<TState, TTrigger>` has the same statements with the session names | `s="src/Domain/StateMachines/StateMachine{TState,TTrigger}.cs"; ! grep -q "Map(next =&gt; Status = next)" "$s" && grep -q "#region state-machine-fire" tests/Domain.Tests/Fixtures/Order.cs && grep -q "Order.cs#state-machine-fire" docs/articles/domain.md` exits 0; `dotnet test --project tests/Domain.Tests -c Release` → passes; V2; V1 |
| T7 | docs(specs): name the merged tests in plan 0009 | docs | specs | Owner's decision; #232 F1, #242 F2; `yaf-writing-plans` rule 12; "What T7 changes" | — | `specs/0009-domain/plan.md` | low | 4 | `grep -cE "Raise_QueuesEventInOrder\|Fire_(Undefined\|Defined\|SmartEnumeration)" specs/0009-domain/plan.md` prints 2 (rows T7 and T17) | `p=specs/0009-domain/plan.md; ! grep -qE "Raise_QueuesEventInOrder\|Fire_(Undefined\|Defined\|SmartEnumeration)" $p && grep -q "DomainEvents_TwoRaised_QueuesThemInOrder" $p && test "$(grep -oE "Firing_[A-Za-z]+_[A-Za-z]+" $p \| sort -u \| wc -l)" = 3 && grep -q "0012-m2-maintenance" $p` exits 0; `node .github/scripts/validate-instructions.mjs` exits 0; V1 |
| T8 | test: restore the spacing of the worktree fixture in the in-flight tests | test | factory | #301; #282 F1; owner's decision | — | `docs/spec/factory/.github/factory/tests/inflight.test.mjs` | high | 1 | `grep -c "path:'" docs/spec/factory/.github/factory/tests/inflight.test.mjs` prints 1 | `f=docs/spec/factory/.github/factory/tests/inflight.test.mjs; ! grep -q "path:'" $f && grep -q "const WORKTREE = { path: 'C:/dev/yaf/" $f && test "$(git diff --numstat $BASE -- $f)" = "$(printf '1\t1\t%s' $f)"` exits 0; V3; V1 |
