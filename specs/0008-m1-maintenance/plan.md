# 0008 — Maintenance of M1: plan

**Spec** `specs/0000-framework/` (here `docs/spec/`), as amended by T1: `03` §2 (`YAF3001`–`YAF3004`), `02` §3 (the raw `Guid` id analyzer), `01` §2.1 (numbering of analyzer ids), `18` §2 (row M1), ADR-0007 (Enforcement), ADR-0030. Source: issue #194, the follow-up list of plan `specs/0006-core/plan.md` (M1, epic #112), and the owner's decisions on it of 2026-11-25 (decision log). No `spec.md`: this is work of the initial build, so the plan cites the specification (`specs/AGENTS.md` rule 2) · **Status** Draft

## Approach

Issue #194 lists the Minor and Nit findings of M1's reviews that started no fix round ("Token economy" in `AGENTS.md`) and the deviations its tickets recorded. The owner decided on 2026-11-25 how each open item is resolved (decision log, first row). Items ticked on #194 (the T12 equality test, the T12 estimate) are out of scope. This plan turns every other item into a ticket, in four groups:

1. **The specification first (T1, T2).** `03` §2 says what the analyzers on error catalogues check after this plan: `YAF3002` and `YAF3004` cover a catalogue field initialised by `new ErrorDescriptor(…)` as well as one initialised by a factory of `Error`; `YAF3003` checks only what is known at compile time; `YAF3001` accepts only a catalogue field of type `Error` or `ErrorDescriptor`. `02` §3, `18` §2 (row M1) and ADR-0007 rename `YAF1001` to `YAF2009`. ADR-0030 records the in-process benchmarks and takes the four Minors of the doc review of #148. These are one specification pull request (T1), code-owned, which the code tickets that implement it wait for. The skill `yaf-domain-modeling` names `YAF1001` twice and gets its own ticket (T2), because a skill change is not a specification pull request (plan 0007, decision log: its T4 and T5 were split the same way).
2. **Analyzers (T3–T7).** Each analyzer change is one ticket, test-first, tier high (`src/Analyzers/**` is a sensitive path). T3 extends `YAF3004` and adds the shared helper that finds a `new ErrorDescriptor(…)` catalogue field and its constant code; T4 extends `YAF3002` with that helper; T5 restricts `YAF3001`; T6 reads the template of `new ErrorDescriptor(…)` by parameter name in `YAF3003`; T7 renames `YAF1001` to `YAF2009` in code, tests, release tracking and the article.
3. **Tests and documentation (T8–T11, T13).** T8 brings `docs/articles/analyzers.md` in step with T3–T5 and fixes the wording at its line 33. T9, T10 and T13 add the missing tests, one concern each, on disjoint files, so that they can run side by side. T11 rewords the `Value` doc of typed ids.
4. **Plan 0006 (T12).** Lines 63, 161 and 232 are corrected and a decision-log row records the in-process benchmarks. Editing a merged plan is a plan update pull request (`yaf-writing-plans` rule 12), written by a planner, as plan 0007 T6 was.

**The rename to `YAF2009`.** `01` §2.1: "Analyzer ids are `YAF` + the number of the spec file of the area + a three-digit sequence", with `YAF2001` for "domain and privacy markers, `02` and `11`". The raw `Guid` analyzer is specified in `02` §3, so its id starts with `YAF2`. A search of the repository at e5a67e8 finds `YAF2001`–`YAF2008` named, and taken, in `02`, `05`, `06`, `11`, `18`, ADR-0004, ADR-0005, ADR-0007, ADR-0011, ADR-0012, ADR-0013, ADR-0016, `AGENTS.nested.md` and eight skills; `YAF2009` appears nowhere, neither in `docs/spec/` nor elsewhere. `YAF1001` is named in `02` §3 (line 39), `18` §2 (row M1), ADR-0007 (Enforcement, line 88), the skill `yaf-domain-modeling` (lines 41, 107), `docs/articles/analyzers.md` (lines 5, 11, 17), `src/Analyzers/Ids/RawGuidIdAnalyzer.cs`, `src/Analyzers/AnalyzerReleases.Unshipped.md`, `tests/Analyzers.Tests/Ids/RawGuidIdTests.cs`, and in the records of plan 0006 (`plan.md`, `research.md`). `docs/articles/results-and-errors.md` does not name it. Plan 0006 and its research stay as they are: they record what was planned and merged (plan 0007 made the same choice).

**Release tracking.** `AnalyzerReleases.Shipped.md` holds only its header; every rule, `YAF1001` included, is a row of `AnalyzerReleases.Unshipped.md` under "New Rules". The release-tracking analyzers forbid changing a shipped entry; an unshipped one is changed in place. T7 replaces the row `YAF1001 | YAF.Ids | Warning | RawGuidIdAnalyzer` with `YAF2009 | …` and touches no shipped entry.

**Not a breaking change.** Rule 5 of `AGENTS.md` makes a changed public name breaking, and a diagnostic id is one (users suppress it by id). Nothing has shipped it: `origin` has no tags (`git ls-remote --tags origin` lists none on 2026-11-25), `.github/workflows/` has no release job and the repository has no `.releaserc.json` or `package.json` (semantic-release comes with M14, `18` §2), and `https://api.nuget.org/v3-flatcontainer/yaf.core/index.json` answered 404 on 2026-11-25, so no `YAF.Core` package exists on nuget.org. The same holds for the analyzers that report more after T3–T5: no consumer build can break.

**`new ErrorDescriptor(…)` fields in `YAF3002` and `YAF3004`.** Such a field is not intercepted: its code is the constructor's `code` argument, so no default code and no `YafServiceCode` is involved. T3 adds one helper to `ErrorCodeNaming`: for a `new ErrorDescriptor(…)` or target-typed `new(…)` that initialises a static field of an `IErrorCatalog` type, the field and the value of the argument whose parameter is `code` (by name, never by position), when that value is a constant string. `YAF3004` reports such a code when it breaks the rules of `03` §2; `YAF3002` counts it with the factory fields' codes. A code that is not a constant is not checked (the same "known at compile time" rule as `YAF3003`). An `[ErrorCode]` on such a field does not change its code at run time, and neither diagnostic reads it. `CoreErrors.Uninitialised` (`core.uninitialised`) is the one such field in `YAF.Core`; the analyzers run on `YAF.Core`'s own build (`src/Core/Core.csproj` references them as an analyzer), and its code is valid and unique.

**`YAF3001` (T5).** `CatalogueAnalyzer.InitialisesCatalogueField` skips every implicit conversion between the creation and the field initialiser, user-defined ones included, so `public static readonly Result R = new Error(…);` in a catalogue is accepted (`src/Analyzers/ErrorCodes/CatalogueAnalyzer.cs:82`). T5 also requires each initialised field to be of type `Error` or `ErrorDescriptor`. The message and description of the diagnostic name the two types. T1 adds the field type to `YAF3001`'s sentence in `03` §2, so that the specification says what the analyzer checks.

**`YAF3003` (T6).** `TemplateArgumentAnalyzer.CollectTemplate` takes `arguments[2]` of `new ErrorDescriptor(…)` as the template (`src/Analyzers/ErrorCodes/TemplateArgumentAnalyzer.cs:57`). The arguments of an `IObjectCreationOperation` are in evaluation order, which follows the source, so named arguments out of order put another value there and the analyzer misses a placeholder. T6 selects the argument whose parameter is `messageTemplate`.

**The `Value` doc (T11).** `ITypedId.Value` says "a version 7 UUID assigned when the id was created", but `From(Guid)` wraps any `Guid` (`src/Core/Ids/ITypedId.cs:13`). The generator writes the same sentence on every typed id's `Value` (`src/Analyzers/TypedIds/TypedIdSource.cs:25`), so `TenantId.Value` repeats it. T11 rewords both: the wrapped value; ids created by `New()` use a version 7 UUID. Including the generator's copy makes T11 tier high (decision log).

Files read to decide: `AGENTS.md` (with the path map), `docs/spec/agents/AGENTS.root.md`, `docs/spec/agents/AGENTS.nested.md` (`src/`, `specs/`, `adr/`), `specs/AGENTS.md`, `docs/spec/factory/.claude/agents/planner.md`, the skills `yaf-writing-plans` (with its template), `yaf-commits-and-pull-requests`, `yaf-writing-adrs`, issue #194 with its comments, issue #136 (the owner's decisions and the review Minor), pull request #148 (doc review, F1–F4), `specs/0006-core/plan.md`, `specs/0006-core/research.md` §2, `specs/0007-no-package-lock-files/plan.md`, `01` §2.1, `02` §3, `03` §2, `15` §6, `18` §2, ADR-0003 (Enforcement, licence policy), ADR-0007, ADR-0030, ADR-0031, `docs/spec/factory/.github/factory/policy.json`, `plan.mjs`, and on `main`: `src/Analyzers/ErrorCodes/` (all files), `src/Analyzers/Ids/RawGuidIdAnalyzer.cs`, `src/Analyzers/TypedIds/TypedIdSource.cs`, `src/Analyzers/AnalyzerReleases.*.md`, `src/Core/Core.csproj`, `src/Core/Ids/ITypedId.cs`, `src/Core/Results/ErrorDescriptor.cs`, `src/Core/Results/CoreErrors.cs`, `tests/Analyzers.Tests/` (test names of every file), `tests/Core.Tests/Results/ResultTests.cs`, `tests/Core.Tests/Results/ErrorFactoryTests.cs`, `tests/Core.Tests/Classification/StorageMarkerTests.cs`, `tests/Benchmarks/Program.cs`, `docs/articles/analyzers.md`, `docs/articles/results-and-errors.md`.

### Where each open item of #194 goes

| Item on #194 | Owner's decision (2026-11-25) | Ticket |
|---|---|---|
| Tests: non-generic `Result` from `Error`; `Result<T>` from `List<Error>`; `Result.Failure(Error)`; `Result<T>.Failure(IEnumerable<Error>)` | 5 | T9 |
| Tests: generated `TryParse` success path | 5 | T10 |
| Tests: an `[ErrorCode]` of exactly 100 characters is accepted | 5 | T10 |
| Tests: `[Searchable]` and `[StoredInClear]` on one fixture member | 5 | T13 |
| Analyzers: `CatalogueAnalyzer` accepts a field of another type through an implicit conversion | 5 | T1 (sentence), T5 |
| Analyzers: `TemplateArgumentAnalyzer` reads the template by position | 5 | T6 |
| Deviation: analyzers narrower than `03` §2 | 1 | T1, T3, T4, T8 |
| Numbering of `YAF1001` | 2 | T1, T2, T7 |
| Plan 0006 line 161 (T2 and T4 wait for the owner too) | 6 | T12 |
| Plan 0006 line 63 (`Optional<T>` equality members) | 6 | T12 |
| Plan 0006 line 232 (cite research §2) | 6 | T12 |
| In-process benchmarks not recorded | 3 | T1 (ADR-0030), T12 (plan 0006) |
| ADR-0030, four Minors of the doc review of #148 | 6 | T1 |
| `ITypedId.Value` doc | 5 | T11 |
| `analyzers.md:33` "initialised by the same factory" | 6 | T8 |
| Open: the articles are in no DocFX build and no `toc.yml` | none | none: `docs/docfx.json` and the site come with M14 (`18` §2; plan 0006 deviation 5). Deferred to M14 by the owner (decision log, 2026-11-25) |

### Alternatives rejected

| Alternative | Why not |
|---|---|
| Narrow `03` §2 to what the analyzers do today | The owner chose to extend the analyzers (decision 1) |
| Keep `YAF1001` and correct `01` §2.1's rule instead | The owner chose the rename (decision 2); the rule is applied by every later analyzer |
| `YAF2001` for the raw `Guid` analyzer | `YAF2001`–`YAF2008` are taken by the specification (Approach) |
| A "Changed rules" or "Removed rules" section in the release tracking files | Those sections describe a shipped rule; `YAF1001` never shipped |
| Supersede ADR-0007 with a revised copy to change one id | ADR-0031 shows that superseding restates the whole record; the owner named ADR-0007 for the rename. The in-place edit carries a dated note (deviation 1) |
| Leave ADR-0007 naming `YAF1001` | The owner's decision 2 names ADR-0007; the record would name an id that no longer exists |
| One ticket for `YAF3002` and `YAF3004` | Two diagnostics, two concerns; `18` §5 and plan 0006 cut one ticket per diagnostic. T4 builds on T3's helper |
| The spec, the ADRs and the skill in one pull request | A skill change makes it a code pull request (plan 0007, decision log); the specification pull request stays a document the doc reviewer reads |
| Updating `analyzers.md` inside each analyzer ticket | Four tickets would share the file and run one after another, and three of them are tier high; one docs ticket (T8) after them stays tier low. T7 is the exception: the article names the id, so it changes with the id |
| The missing tests in one ticket | T9 (results), T10 (`Analyzers.Tests`) and T13 (storage markers) are separate concerns on disjoint files and can run side by side |
| Only `ITypedId.cs` in T11, keeping it tier medium | The generated `Value` of every id would still promise a version 7 UUID that `From(Guid)` does not give |
| Editing plan 0006's history (its T21 row, Q4) to say `YAF2009` | A merged ticket's row is the record of what it did (plan 0007, decision log) |

## Affected packages

| Layer | Package / area | Change |
|---|---|---|
| Core | `YAF.Core` (`src/Core/Ids/ITypedId.cs`) | XML doc of `Value` only (T11) |
| Domain | none | none |
| Application | none | none |
| Infrastructure | none | none |
| Persistence | none | none |
| Http | none | none |
| Adapters | none | none |
| Testing (`Testing`, `Testing.Integration`) | none | none. New tests in `tests/Core.Tests` (T9, T13) and `tests/Analyzers.Tests` (T3–T7, T10) |
| Analyzers | `Analyzers` (`src/Analyzers/`) | `YAF3004` (T3) and `YAF3002` (T4) cover `new ErrorDescriptor(…)` fields; `YAF3001` checks the field type (T5); `YAF3003` reads the template by name (T6); `YAF1001` becomes `YAF2009` (T7); the generated `Value` doc (T11) |
| Sample | none | none (the sample starts in M3) |
| Templates | none | none (M13) |
| Docs | `docs/articles/analyzers.md`; `docs/spec/` | The article (T7, T8). `03` §2, `02` §3, `18` §2, ADR-0007, ADR-0030 (T1); the skill `yaf-domain-modeling` (T2) |
| Plans (not a layer) | `specs/0006-core/plan.md` | Lines 63, 161, 232 and a decision-log row (T12) |

## Public API, schema and error-code delta

None in the categories of `factory:delta`. No line of `src/Core/PublicAPI.Shipped.txt` or `PublicAPI.Unshipped.txt` changes: T11 changes an XML doc comment only, and the analyzer classes are `internal`, so `src/Analyzers` has no public API file (plan 0006, research §10). No error code, configuration key, contract, telemetry name, migration or dependency changes.

What a consumer of `YAF.Core` would see, and which no category of the block covers:

- **Changed** the diagnostic id `YAF1001` becomes `YAF2009`; title, message, category `YAF.Ids`, severity Warning and behaviour stay (T7).
- **Changed** `YAF3002` and `YAF3004` also report on catalogue fields initialised by `new ErrorDescriptor(…)` with a constant code (T3, T4); `YAF3001` also reports a creation that initialises a catalogue field of another type than `Error` or `ErrorDescriptor` (T5); `YAF3003` reports a missed placeholder when the template of `new ErrorDescriptor(…)` is passed as a named argument out of order (T6). The ids, titles, categories and severities stay; the message and description of `YAF3001` name the two field types.

Breaking: no. Nothing is released (Approach, "Not a breaking change").
New dependency: no.

```factory:delta
{ "publicApi": [], "schema": [], "errorCodes": [], "contracts": [], "configuration": [], "telemetry": [], "dependencies": [] }
```

## Schema and migration plan

None.

## What T1 writes

Each changed place of the specification carries a note `> **Amended 2026-11-25**, by the owner's decisions of 2026-11-25 (`specs/0008-m1-maintenance/plan.md`, decision log): …`, as #146 and plan 0007 T4 did. A note that names `YAF1001` names it on the same line as "Amended", so that T1's verify can tell it from a stale mention.

- **`03` §2**, the analyzer bullet. `YAF3001`: an `Error` or `ErrorDescriptor` created anywhere other than the initialiser of a static field of type `Error` or `ErrorDescriptor` of an `IErrorCatalog` type. `YAF3002` and `YAF3004`: they cover catalogue fields initialised by a factory of `Error` and fields initialised by `new ErrorDescriptor(…)` whose code is a constant; the code of the latter is its `code` argument, so it needs no `YafServiceCode`. `YAF3003`: it checks a `With` call only with what is known at compile time, namely a descriptor that is a field of the same compilation, a constant template and constant argument names; other calls are not reported. One note below the bullet.
- **`02` §3**: `YAF1001` becomes `YAF2009` in the rule, and a note: renamed from `YAF1001` to follow `01` §2.1 (domain = `02`; `YAF2001`–`YAF2008` are taken).
- **`18` §2**, row M1: `YAF1001` becomes `YAF2009`, with a note below the table.
- **ADR-0007**, Enforcement: the row "Analyzer `YAF1001`" becomes "Analyzer `YAF2009`", and a note below the table says that the id was renamed from `YAF1001` on 2026-11-25 by the owner's decision, the decision itself unchanged. The status line stays `Accepted`. This edits an accepted ADR (deviation 1).
- **ADR-0030** (status `Proposed`, so it may be edited):
  - Context: `15` §6 already lists `tests/Benchmarks | BenchmarkDotNet, informational`; the Options table notes that options B and C would depart from it. Related names `15` §6 (F4 of #148).
  - Context, the licence: the MIT licence was verified for the BenchmarkDotNet package only, not for its transitive packages. ADR-0003 names a CI licence check over all restored packages, which does not exist yet; until it does, the transitive licences are kept by review, and the ADR says so (F3).
  - Enforcement: the row that credits `IsPackable` and the CI `dotnet pack` step goes; the pack step checks nothing about it. The closing sentence says that non-packable benchmark projects are kept by review (F1).
  - Enforcement: the architecture-test row is narrowed to "each built core assembly" (ADR-0003's row); the closing sentence adds that adapter projects under `src/` are kept by review (F2).
  - Decision, a new item: benchmarks run with BenchmarkDotNet's in-process toolchain while BenchmarkDotNet 0.15.8 does not recognise `net11.0` out of process, and the benchmark classes live in a non-packable class library that the console project runs, so CA1515 needs no suppression (owner's decisions on #136, 2026-11-24). Consequences, a cost: process isolation, listed for option A, is given up until a stable release supports .NET 11. Related names this plan.

`01` §2.1 stays: its rule is what the rename follows. ADR-0031 stays: its Enforcement rows summarise the four diagnostics and stay true at that level; `03` §2 is the detail.

## Test strategy

There are no requirements `R…`; the sections of `docs/spec/` as amended by T1 and the items of #194 are the targets.

| What must be true | Section / item | Ticket | Level | Test project / check |
|---|---|---|---|---|
| The specification and ADRs say what the analyzers do and name `YAF2009`; ADR-0030 records the in-process run and answers F1–F4 | `03` §2; `02` §3; `18` §2; ADR-0007; ADR-0030 | T1 | documentation check | T1's verify; the doc reviewer reads it against "What T1 writes" |
| The domain skill names `YAF2009` | `02` §3 | T2 | documentation check | T2's verify; `sync-references.mjs --check` |
| `YAF3004` reports an invalid constant code of a `new ErrorDescriptor(…)` catalogue field, target-typed and with named arguments; a valid one, a non-constant one and one in a project without `YafServiceCode` are not reported | `03` §2 as amended | T3 | analyzer | `Analyzers.Tests` |
| `YAF3002` reports a constructor field whose code equals another constructor field's or a factory field's, on both | `03` §2 as amended | T4 | analyzer | `Analyzers.Tests` |
| `YAF3001` reports a creation that initialises a catalogue field of type `Result` or `object`; fields of type `Error` and `ErrorDescriptor` still pass | `03` §2 as amended; #194 | T5 | analyzer | `Analyzers.Tests` |
| `YAF3003` reads the template of `new ErrorDescriptor(…)` with named arguments out of order | `03` §2; #194 | T6 | analyzer | `Analyzers.Tests` |
| The raw `Guid` analyzer reports `YAF2009`, release tracking and article agree | `02` §3 as amended; `01` §2.1 | T7 | analyzer, documentation check | `Analyzers.Tests`; T7's verify |
| The article describes the changed diagnostics and its line 33 matches the snippet | `src/AGENTS.md` (every diagnostic in the article) | T8 | documentation check | T8's verify |
| The four result conversions and factories are tested | `03` §5 item 1 | T9 | unit | `Core.Tests` |
| The storage markers are tested on separate members | `02` §11 | T13 | unit | `Core.Tests` |
| The generated `TryParse` succeeds for a valid string; a code of exactly 100 characters is accepted | `02` §3; `03` §2 | T10 | generator, analyzer | `Analyzers.Tests` |
| The `Value` doc promises no UUID version for `From(Guid)` | `02` §3 | T11 | documentation check, build | T11's verify; V2 |
| Plan 0006 says what was decided and cites its sources | #194 | T12 | documentation check | T12's verify; `validate-instructions.mjs` |

**Tests of code that already works (T9, T10, T13).** A test added for existing behaviour passes when it is written. Its "failing test first" is shown by breaking the covered member locally (for example, the conversion from `Error` returns a success, or `MaxCodeLength` becomes 99), seeing the new test fail, and reverting the break before committing. The pull request names the break and the failure it gave.

## Risks and rollback

| Risk | Likelihood | Mitigation | Rollback |
|---|---|---|---|
| T1 waits for the code owner, and T2–T5, T7 and T8 wait for it | Certain | T6, T9, T11, T12 and T13 do not depend on T1 and run meanwhile | — |
| The extended analyzers report in `YAF.Core`'s own build (`CoreErrors.Uninitialised`) | Low | Its code `core.uninitialised` is valid and unique; V2 builds `YAF.Core` with the analyzers on, warnings as errors | Revert the ticket |
| The editing of accepted ADR-0007 is read as breaking `adr/AGENTS.md` rule 1 | Medium | The owner named ADR-0007 (decision 2); the edit is one id, with a dated note; recorded as deviation 1 and confirmed by the owner on 2026-11-25 (decision log) | Restore the row and supersede ADR-0007 instead |
| A user-defined conversion to `Error` or `ErrorDescriptor` of a creation still slips through `YAF3001` | Low | T5 checks the field's type, whatever conversion leads to it | — |
| Eight tier-high tickets wait for the owner | High | Two can run at once (`limits.parallelImplementers`); each is small | — |
| `analyzers.md` is changed by T7 and T8 | Certain | T8 depends on T7 | — |

## Verification commands

Git Bash, repository root, on the ticket's branch; `BASE` is `origin/main`.

V1, every ticket: `git diff --name-only $BASE...HEAD` lists only the ticket's touches; `node docs/spec/factory/.github/factory/size.mjs $BASE HEAD` stays at the estimate or below and within 400; `node .github/scripts/sync-references.mjs --check` and `node .github/scripts/validate-instructions.mjs` exit 0. A ticket that changes a skill (T2) runs `node .github/scripts/sync-references.mjs` first and commits what it regenerates (no front matter changes, so nothing is expected).

V2, every code and test ticket, expected 0 warnings, 0 errors, all tests passing:

    dotnet restore YAF.slnx && dotnet build YAF.slnx -c Release --no-restore && dotnet test --solution YAF.slnx -c Release --no-build

The whole plan, on `main` after the last ticket: V2; `grep -rn YAF1001 --exclude-dir=.git --exclude-dir=.claude --exclude-dir=bin --exclude-dir=obj .` prints only lines of `specs/0006-core/`, `specs/0008-m1-maintenance/` and lines of `docs/spec/` that contain "Amended"; the latest `ci.yml` run on `main` is green; every open item of #194 is closed by a ticket above, except the DocFX item, which the owner deferred to M14 (decision log).

## Research

None needed. The behaviour relied on is in this repository: the release tracking files (`src/Analyzers/AnalyzerReleases.*.md`), the argument order of `IObjectCreationOperation` as `TemplateArgumentAnalyzer` uses it, and plan 0006's research §2 and §10. Publication was checked on 2026-11-25: no tags on `origin`, no release job, `YAF.Core` absent from nuget.org (HTTP 404).

## Decision log

| Date | Decision | Reason | By |
|---|---|---|---|
| 2026-11-25 | **Owner's decisions on #194**, recorded on the issue as the source of this plan: (1) analyzers narrower than `03` §2: extend the analyzers; `YAF3002` and `YAF3004` also cover catalogue fields set by `new ErrorDescriptor(…)`; `YAF3003` stays limited to what is known at compile time, and the spec says so. (2) Diagnostic numbering: rename `YAF1001` to `YAF2009`, following `01` §2 (domain = `02`; `YAF2001`–`YAF2008` are already planned): code, tests, `AnalyzerReleases`, the article, and in the spec `02` §3 and ADR-0007. (3) In-process benchmarks: recorded in both plan 0006's decision log and ADR-0030. (4) Spec pull request: yes, as the epic's first ticket (`03` §2, `02` §3, ADR-0007, and ADR-0030 if it is changed there); code tickets that depend on it wait for its merge. (5) Code fixes, all as tickets: the four missing tests and the fixture split; `CatalogueAnalyzer` accepts only catalogue fields of type `Error` or `ErrorDescriptor`; `TemplateArgumentAnalyzer` reads the template by parameter name; reword the `ITypedId` `Value` doc. (6) Text fixes, all: plan 0006 lines 161, 63 and 232; the four ADR-0030 Minors; `analyzers.md` line 33. (7) T12 estimate overrun: accepted, closed. (8) Planning: a planner writes plan 0008 from this issue and these decisions; the factory runs its tickets under a new epic once the owner approves it | Answers given to the orchestrator in an attended session, recorded in the comment "Owner's decisions on this list, 2026-11-25" on #194 | Human owner, 2026-11-25 |
| 2026-11-24 | Benchmarks run in process (BenchmarkDotNet 0.15.8 does not recognise `net11.0` out of process), and the benchmark classes live in a separate non-packable class library, so CA1515 needs no suppression. Recorded by T1 (ADR-0030) and T12 (plan 0006) | Owner's decisions on #136, options 2 (a) and 1 (b) | Human owner, 2026-11-24 |
| 2026-11-25 | Folder `0008-m1-maintenance` | Next free number | Planner |
| 2026-11-25 | The specification and ADRs are one ticket (T1); the skill is another (T2), after it | Owner's decision 4; a skill change is not a specification pull request (plan 0007, decision log) | Planner |
| 2026-11-25 | T1 also amends `18` §2 (row M1) and the skill `yaf-domain-modeling` is amended by T2 | Both name `YAF1001`; `18` is the recipe for rebuilding YAF (plan 0007 amended it for the same reason) | Planner |
| 2026-11-25 | ADR-0007 is edited in place, its Enforcement row only, with a dated note; not superseded | The owner named ADR-0007 for the rename (decision 2); superseding restates the whole record for one id. This departs from `adr/AGENTS.md` rule 1 (deviation 1) | Planner, on the owner's decision 2 |
| 2026-11-25 | T1 adds the field type (`Error` or `ErrorDescriptor`) to `YAF3001`'s sentence in `03` §2 | Owner's decision 5 restricts the analyzer; the specification should say what it checks. T5 waits for T1 | Planner |
| 2026-11-25 | The code of a `new ErrorDescriptor(…)` catalogue field is its `code` argument, selected by parameter name; only a constant is checked; `[ErrorCode]` on such a field is not read | The constructor is not intercepted, so that argument is the code at run time; a value not known at compile time cannot be checked (the rule of `YAF3003`) | Planner |
| 2026-11-25 | The rename edits the unshipped release-tracking row in place | Nothing is in `AnalyzerReleases.Shipped.md`; the "Changed" and "Removed" sections are for shipped rules | Planner |
| 2026-11-25 | The rename is not a breaking change | Nothing is published: no tags, no release job, no package on nuget.org (Approach) | Planner |
| 2026-11-25 | T11 also rewords the generated `Value` doc in `TypedIdSource.cs`, which makes it tier high | The same sentence on every typed id would otherwise keep the defect #194 reports | Planner |
| 2026-11-25 | `analyzers.md` changes with the rename (T7) and once more for T3–T5 (T8) | The article names the id, so it moves with it; the other changes are batched in one tier-low ticket | Planner |
| 2026-11-25 | The missing tests show their failure by a local break that is reverted | They cover code that already works; the break proves each test can fail | Planner |
| 2026-11-25 | The DocFX item of #194 is left to M14 | The owner's decisions do not cover it; `docs/docfx.json` comes with M14 (`18` §2; plan 0006 deviation 5) | Planner |
| 2026-11-25 | Plan 0006 and its research keep their mentions of `YAF1001`; ADR-0031 is not edited | Records of what was planned and merged; ADR-0031's rows stay true as summaries | Planner |
| 2026-11-25 | **Owner's answers to the plan's open questions:** (1) ADR-0007 gets the dated note in place, kept as deviation 1; no superseding ADR. (2) T11 rewords both `src/Core/Ids/ITypedId.cs` and `src/Analyzers/TypedIds/TypedIdSource.cs`, tier high as planned. (3) The DocFX and `toc.yml` item of #194 stays out of plan 0008 and is deferred to M14 | Answers given to the orchestrator in an attended session, recorded on #194 | Human owner, 2026-11-25 |

## Deviations from the specification

1. **ADR-0007 is edited although it is accepted.** `adr/AGENTS.md` rule 1 and `yaf-writing-adrs` rule 7 allow only the status line of an accepted ADR to change. T1 changes the id in one row of its Enforcement table, with a dated note, because the owner's decision 2 names ADR-0007 for the rename and an explicit human instruction comes first in `AGENTS.md`'s precedence. The decision of ADR-0007 does not change. The owner confirmed on 2026-11-25 that the dated note in place stands and that no superseding ADR is written (decision log).

Everything else this plan changes in `docs/spec/` is amended by T1 before the code that needs it; after T1 the tickets follow the specification.

## Tickets

T1, T6, T9, T11, T12 and T13 can start at once; at most two implementers run at the same time. T2, T3, T5 and T7 wait for T1's merge. T4 and T10 follow T3 (T4 uses its helper, T10 shares `ErrorCodeDiagnosticTests.cs`). T8 follows T3, T4, T5 and T7. T3–T7 share no file with one another except as stated, and none adds to `AnalyzerReleases.Unshipped.md` except T7. Every ticket in tier high waits for the owner; T1 is code-owned. T12 is written by a planner, not an implementer, since it edits a plan.

| Key | Title | Type | Area | Requirements | Depends on | Touches | Tier | Size | Failing test first | Verify |
|---|---|---|---|---|---|---|---|---|---|---|
| T1 | docs(specs): amend the analyzer rules of M1 after its reviews | docs | specs | Owner's decisions 1–4, 6; "What T1 writes"; #148 F1–F4; `yaf-writing-adrs` | — | `docs/spec/spec/03-results-and-errors.md`, `docs/spec/spec/02-domain.md`, `docs/spec/spec/18-build-plan.md`, `docs/spec/adr/0007-domain-model-patterns.md`, `docs/spec/adr/0030-benchmark-library.md` | high | 70 | `grep -l YAF1001 docs/spec/spec/02-domain.md docs/spec/spec/18-build-plan.md docs/spec/adr/0007-domain-model-patterns.md` lists all three, and `grep -c 0008-m1-maintenance docs/spec/spec/03-results-and-errors.md` prints 0 | `d=docs/spec; ! grep -rqP '^(?!.*Amended).*YAF1001' $d/spec $d/adr && grep -q YAF2009 $d/spec/02-domain.md && grep -q YAF2009 $d/spec/18-build-plan.md && grep -q YAF2009 $d/adr/0007-domain-model-patterns.md && grep -q "0008-m1-maintenance" $d/spec/03-results-and-errors.md && grep -q "new ErrorDescriptor" $d/spec/03-results-and-errors.md && grep -qi "in-process" $d/adr/0030-benchmark-library.md && grep -q "15-testing" $d/adr/0030-benchmark-library.md && grep -q "Accepted" $d/adr/0007-domain-model-patterns.md` exits 0; V1 |
| T2 | docs(agents): name YAF2009 in the domain modelling skill | docs | agents | Owner's decision 2; `02` §3 as amended by T1 | T1 | `docs/spec/agents/skills/yaf-domain-modeling/SKILL.md` | high | 2 | `grep -c YAF1001 docs/spec/agents/skills/yaf-domain-modeling/SKILL.md` prints 2 | `f=docs/spec/agents/skills/yaf-domain-modeling/SKILL.md; ! grep -q YAF1001 $f && test "$(grep -c YAF2009 $f)" = 2` exits 0; `node .github/scripts/sync-references.mjs --check` exits 0; V1 |
| T3 | feat(analyzers): check the codes of error descriptors created with new | feat | analyzers | `03` §2 as amended (`YAF3004`, code rules); owner's decision 1 | T1 | `src/Analyzers/ErrorCodes/ErrorCodeAnalyzer.cs`, `src/Analyzers/ErrorCodes/ErrorCodeNaming.cs`, `tests/Analyzers.Tests/ErrorCodes/ErrorCodeDiagnosticTests.cs` | high | 160 | `ErrorCodeDiagnosticTests.ConstructorFieldUpperCaseCode_ReportsYAF3004` — fails: a `new ErrorDescriptor("Catalog.Session.Full", …)` catalogue field is not checked. Also `TargetTypedConstructorFieldCodeOver100Characters_ReportsYAF3004`, `ConstructorFieldNamedArgumentsOutOfOrder_ReportsYAF3004`, `ConstructorFieldWithoutServiceCode_ReportsNothing`, `ConstructorFieldNonConstantCode_ReportsNothing` | `dotnet test --project tests/Analyzers.Tests -c Release` → passes; V2; V1 |
| T4 | feat(analyzers): report duplicate codes of error descriptors created with new | feat | analyzers | `03` §2 as amended (`YAF3002`); owner's decision 1 | T3 | `src/Analyzers/ErrorCodes/DuplicateCodeAnalyzer.cs`, `tests/Analyzers.Tests/ErrorCodes/DuplicateCodeTests.cs` | high | 110 | `DuplicateCodeTests.ConstructorCodeEqualsDefaultCode_ReportsYAF3002OnBoth` — fails: the constructor field is not counted. Also `TwoConstructorFieldsSameCode_ReportsYAF3002OnBoth`, `ConstructorFieldsDistinctCodes_ReportsNothing`. Uses T3's helper; does not change `ErrorCodeNaming.cs` | `dotnet test --project tests/Analyzers.Tests -c Release` → passes; V2; V1 |
| T5 | fix(analyzers): accept only error and descriptor fields in error catalogues | fix | analyzers | `03` §2 as amended (`YAF3001`); owner's decision 5; #194 (`CatalogueAnalyzer.cs:82`) | T1 | `src/Analyzers/ErrorCodes/CatalogueAnalyzer.cs`, `tests/Analyzers.Tests/ErrorCodes/CatalogueAnalyzerTests.cs` | high | 60 | `CatalogueAnalyzerTests.CatalogueFieldOfTypeResult_ReportsYAF3001` — fails: `public static readonly Result R = new Error(…);` in a catalogue is accepted through the implicit conversion. Also `CatalogueFieldOfTypeObject_ReportsYAF3001`; `CatalogueStaticField_ReportsNothing` and `CoreErrors_ReportsNothing` still pass | `dotnet test --project tests/Analyzers.Tests -c Release` → passes; V2; V1 |
| T6 | fix(analyzers): read the message template of a new error descriptor by parameter name | fix | analyzers | `03` §2 (`YAF3003`); owner's decision 5; #194 (`TemplateArgumentAnalyzer.cs:57`) | — | `src/Analyzers/ErrorCodes/TemplateArgumentAnalyzer.cs`, `tests/Analyzers.Tests/ErrorCodes/TemplateArgumentTests.cs` | high | 40 | `TemplateArgumentTests.ConstructorNamedArgumentsOutOfOrder_ReportsYAF3003` — fails: with `new ErrorDescriptor(messageTemplate: "… {Capacity}", kind: …, code: …)` the analyzer reads the code as the template and reports nothing for `Full.With(("Seats", 40))` | `dotnet test --project tests/Analyzers.Tests -c Release` → passes; V2; V1 |
| T7 | fix(analyzers): number the raw Guid id diagnostic YAF2009 | fix | analyzers | `01` §2.1; `02` §3 as amended by T1; owner's decision 2 | T1 | `src/Analyzers/Ids/RawGuidIdAnalyzer.cs`, `src/Analyzers/AnalyzerReleases.Unshipped.md`, `tests/Analyzers.Tests/Ids/RawGuidIdTests.cs`, `docs/articles/analyzers.md` | high | 30 | `RawGuidIdTests.PublicMethodGuidSessionId_ReportsYAF2009` (renamed, asserting `YAF2009`) — fails: the diagnostic is `YAF1001` | `! grep -rq YAF1001 src tests docs/articles && grep -q "^YAF2009 . YAF.Ids . Warning . RawGuidIdAnalyzer" src/Analyzers/AnalyzerReleases.Unshipped.md && grep -q "YAF2009. belongs to the domain" docs/articles/analyzers.md` exits 0; `git diff --quiet $BASE -- src/Analyzers/AnalyzerReleases.Shipped.md` exits 0 (shipped file untouched); `dotnet test --project tests/Analyzers.Tests -c Release` → passes; V2; V1 |
| T8 | docs: describe the extended error catalogue diagnostics | docs | docs | `src/AGENTS.md` (every diagnostic in `docs/articles/analyzers.md`); `03` §2 as amended; owner's decision 6 (`analyzers.md:33`) | T3, T4, T5, T7 | `docs/articles/analyzers.md` | low | 25 | `grep -c "initialised by the same factory" docs/articles/analyzers.md` prints 1, and the `YAF3002` section says "declared with the factories of `Error`" only | `f=docs/articles/analyzers.md; ! grep -q "initialised by the same factory" $f && test "$(grep -c "new ErrorDescriptor" $f)" -ge 3 && grep -q "of type .Error. or .ErrorDescriptor." $f` exits 0; V1 |
| T9 | test(core): cover the remaining result conversions | test | core | `03` §5 item 1 (implicit conversions); owner's decision 5 | — | `tests/Core.Tests/Results/ResultTests.cs` | low | 50 | `ResultTests.ImplicitConversion_NonGenericFromError_IsFailure`, `ImplicitConversion_GenericFromErrorList_IsFailure`, `Failure_NonGenericFromError_IsFailure`, `Failure_GenericFromEnumerable_KeepsOrder`; each fails against a local break of its member, reverted before commit. The `#region` markers stay unchanged | `dotnet test --project tests/Core.Tests -c Release` → passes; `git diff --quiet -G'#(end)?region' $BASE -- tests/Core.Tests` exits 0 (no region marker added or removed); V2; V1 |
| T10 | test(analyzers): add the boundary tests the M1 reviews asked for | test | analyzers | `02` §3 (`TryParse`); `03` §2 (at most 100 characters); owner's decision 5 | T3 | `tests/Analyzers.Tests/TypedIds/TypedIdGeneratorTests.cs`, `tests/Analyzers.Tests/ErrorCodes/ErrorCodeDiagnosticTests.cs` | low | 35 | `TypedIdGeneratorTests.TryParse_Guid_ReturnsTrueAndId` (returns true, the id equals `From(value)`) fails against a generated `TryParse` that always returns false; `ErrorCodeDiagnosticTests.CodeOf100Characters_ReportsNothing` fails with `MaxCodeLength` set to 99; both breaks local and reverted | `dotnet test --project tests/Analyzers.Tests -c Release` → passes; V2; V1 |
| T11 | docs(core): describe the value of a typed id without promising a UUID version | docs | core | `02` §3 (`New()` uses `Guid.CreateVersion7()`, `From(Guid)`); owner's decision 5; #194 (`ITypedId.cs:13`) | — | `src/Core/Ids/ITypedId.cs`, `src/Analyzers/TypedIds/TypedIdSource.cs` | high | 6 | `grep -l "a version 7 UUID assigned when the id was created" src/Core/Ids/ITypedId.cs src/Analyzers/TypedIds/TypedIdSource.cs` lists both | `! grep -rq "a version 7 UUID assigned when the id was created" src && grep -q "New()" src/Core/Ids/ITypedId.cs` exits 0; V2 (XML docs build without warnings); V1 |
| T12 | docs(specs): correct plan 0006 and record its in-process benchmarks | docs | specs | Owner's decisions 3, 6; #136; `yaf-writing-plans` rule 12 | — | `specs/0006-core/plan.md` | low | 12 | `grep -c "likely lose the union behaviour" specs/0006-core/plan.md` prints 1, and no decision-log row names the in-process toolchain | `p=specs/0006-core/plan.md; ! grep -q "likely lose the union behaviour" $p && ! grep -q "the medium .YAF.Core. lane goes on while high tickets wait" $p && grep -qE "^- .YAF.Core.: .*Optional<T>.*IEquatable<Optional<T>>" $p && grep -qi "in-process" $p && grep -q "0008-m1-maintenance" $p` exits 0; `node .github/scripts/validate-instructions.mjs` exits 0; V1 |
| T13 | test(core): test the storage markers on separate members | test | core | `02` §11 (`[Searchable]` and `[StoredInClear]` are alternatives); owner's decision 5; #194 (`StorageMarkerTests.cs:38`) | — | `tests/Core.Tests/Classification/StorageMarkerTests.cs` | low | 25 | `StorageMarkerTests.Searchable_OnMember_IsRead` and `StoredInClear_OnMember_IsReadWithReason`, each on its own fixture member; each fails against a local break (the attribute removed from its member), reverted before commit. Then `StorageMarkers_OnMember_AreReadTogether` and the member carrying both markers go | `f=tests/Core.Tests/Classification/StorageMarkerTests.cs; ! grep -q "AreReadTogether" $f && dotnet test --project tests/Core.Tests -c Release` → passes; V2; V1 |

**What T12 changes** in `specs/0006-core/plan.md`: in the delta prose (line 63), `Optional<T>` lists its equality members (`IEquatable<Optional<T>>`, `Equals`, `GetHashCode`, `==`, `!=`), which `factory:delta` already covers; in the risk row "The analyzers lane waits for the owner" (line 161), the mitigation says that T2, T4, T9 and T12 of the `YAF.Core` lane wait for the owner too (guard change); in the decision-log row of T9 (line 232), "likely lose the union behaviour across assemblies" becomes the sourced statement that the language reads union members only from a public nested `IUnionMembers` interface (research §2); a new decision-log row dated 2026-11-25 records the owner's decisions on #136 (in-process toolchain while BenchmarkDotNet 0.15.8 does not recognise `net11.0` out of process; the benchmark classes in a separate non-packable class library) and cites this plan. No other row, the delta and the tickets do not change.
