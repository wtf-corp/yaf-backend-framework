# 0014 — Ticket touches that keep their paths whole: plan

**Spec** none: this plan has no `spec.md`. Its basis is issue #329 (plan 0013 T6), observed on 2026-11-26, and the orchestrator's task to fix the factory script behind it. No section of `docs/spec/spec/` describes how the Tickets table is parsed, so nothing there is amended · **Status** Draft

## Approach

**The problem.** Issue #329 lists its touches as `src/Domain/AggregateRoot{TId`, `TSelf` and `TMemento}.cs`. The Touches cell of plan 0013 T6 names `src/Domain/AggregateRoot{TId,TSelf,TMemento}.cs`, the real file: a file of a generic type with several type parameters is named `Type{T1,T2}.cs` in this repository (`src/Domain/AggregateRoot{TId,TSelf,TMemento}.cs`, `src/Domain/StateMachines/StateMachine{TState,TTrigger}.cs`, `src/Domain/Rules/IPolicy{TInput,TDecision}.cs` exist on `main`).

**The cause.** `parseTickets` in `docs/spec/factory/.github/factory/plan.mjs` reads the Touches and Depends on cells with one helper, `list` (line 52):

```js
const list = (text) => text.split(/[,;]|<br\s*\/?>/).map((s) => s.trim().replace(/^`|`$/g, '')).filter(…);
```

It splits on every `,` and `;`, whatever they stand in, and then strips one backtick from each end of each piece. That breaks two kinds of entry:

1. **A comma inside braces**: `` `src/Domain/AggregateRoot{TId,TSelf,TMemento}.cs` `` gives three pieces (plan 0013 T6, T63). A glob alternation such as `src/**/*.{cs,md}` would break the same way.
2. **A note after a path**: `` `.editorconfig` (three sections, YAF2011 only) `` gives `` .editorconfig` (three sections `` and `` YAF2011 only) ``; `` `src/Application/*.cs` (the eight contract files) `` keeps its trailing note but loses only its first backtick. In the issue each piece is wrapped in backticks again, so the body shows a broken code span (plan 0013 T8, T36, T39, T40; plans 0006 T1, T23, T26 and 0007 T2).

A scratch test against `main` (three cases: braces with and without backticks, a note with a comma) fails on all three with exactly the pieces seen in #329 and #359. The failing tests first of T1 are those cases.

**Who reads touches.** Found by searching the kit, `.github/scripts/` and `.github/workflows/` for `touches`, `Touches` and `parseTickets`:

| Reader | What it does with touches | Affected |
|---|---|---|
| `plan.mjs` `parseTickets`, `list` (line 52, used at 57 and 58) | Splits the cell | **The cause** |
| `create-tickets.mjs` `ticketBody` (lines 22–23) | Writes `### Touches`, one `` - `<entry>` `` per entry, into the issue body | Yes: writes the broken pieces, and wraps an entry that already holds backticks in another pair |
| `plan.mjs` `ticketProblems` (line 95), through `gates.mjs` (line 128) and `.github/scripts/validate-instructions.mjs` (line 145) | Only that the list is not empty | No |
| The orchestrator (`yaf-orchestrating-work` rules 5 and 17, step 5.2; `.claude/commands/factory-run.md`, `push-owed`) | Reads the issue: no overlap between parallel tickets, diff within touches, whether touches name `.github/workflows/` | Yes, by eye: a split path matches no file, so the diff check and the overlap check see a wrong list |
| The implementer and the reviewers (`yaf-implementing-tickets` rule 5, `yaf-reviewing-changes` pass 1, `prompts/pr-review.md`) | Read the issue's touches | Yes, by eye, as above |
| `prompts/doc-review.md` item 7 | Reads the plan's table itself | No |
| `inflight.mjs`, `scope.mjs`, `classify.mjs`, `size.mjs`, `delta.mjs`, `ticket.mjs`, `gates.mjs` otherwise | Do not read touches | No |

No gate compares a diff with touches by script; the comparison is the orchestrator's and the reviewer's, from the issue body. So the fix is in the parser and the one writer of the issue body.

**The fix (T1).** `list` splits at a `,`, `;` or `<br>` only at depth 0: outside a code span (between two backticks) and outside `{…}`, `(…)` and `[…]`. A closing bracket with no opening one is ignored (the depth stays at 0); an opening bracket that never closes keeps the rest of the cell as one entry, which is visible in the issue and splits nothing wrongly. Then each entry is trimmed. An entry that is exactly one code span (`` `path` ``) loses its backticks, as today. Any other entry is kept as written, its backticks and its note included. `ticketBody` writes an entry that contains a backtick as it is (`- <entry>`) and wraps every other one in backticks, as today. For the cells of plan 0013:

| Cell | `main` gives | After T1 |
|---|---|---|
| `` `src/Domain/AggregateRoot{TId,TSelf,TMemento}.cs` `` | `src/Domain/AggregateRoot{TId`, `TSelf`, `TMemento}.cs` | `src/Domain/AggregateRoot{TId,TSelf,TMemento}.cs` |
| `` `.editorconfig` (three sections, YAF2011 only) `` | `` .editorconfig` (three sections ``, `` YAF2011 only) `` | `` `.editorconfig` (three sections, YAF2011 only) ``, written into the issue as it is |
| `` `src/Persistence.EfCore.PostgreSql/` `` | `src/Persistence.EfCore.PostgreSql/` | the same |
| `T1, T2` (Depends on) | `T1`, `T2` | the same |

Depends on goes through the same helper; a ticket key contains none of the characters that change, so its result does not change. Touches stays a list of strings, so `ticketProblems` and the callers are unchanged.

**The issues already created.** `create-tickets.mjs` finds an existing ticket by its marker and never rewrites its body, so T1 alone repairs nothing. The open issues with a broken Touches list, all from plan 0013:

| Issue | Ticket | Broken entry |
|---|---|---|
| #329 | T6 | `AggregateRoot{TId,TSelf,TMemento}.cs` split in three |
| #386 | T63 | `InMemoryRepository{TAggregate,TId}.cs` split in two |
| #331 | T8 | `` `src/Application/*.cs` (the eight contract files) `` with a stray backtick |
| #359 | T36 | `` `.editorconfig` (three sections, YAF2011 only) `` split in two |
| #362 | T39 | `` `tests/Persistence.EfCore.PostgreSql.Tests/` (not `Fixtures/`) `` with stray backticks |
| #363 | T40 | `` `src/Http/Http.csproj` (with `YafServiceCode` `core`) `` with stray backticks |

They are repaired once, after T1 merges, by the orchestrator in an attended session, as the factory app (section "After T1 merges"). The closed issues with broken entries (#113, #135, #138 of plan 0006 and #154 of plan 0007) are left as they are: they are the record of finished work and nobody reads their touches to decide anything. Plans 0008 to 0012 have no entry that breaks.

**The tickets of plan 0013 not created yet.** The `factory-tickets` run for plan 0013 (run 45897939422, 2026-11-26) stopped after T64 with "GraphQL: API rate limit exceeded", so T65 to T93 have no issue. Six of them would break the same way: T66, T69, T70, T72, T76, T80 (all notes after a path, T76's with commas). The run that creates them should start after T1 has merged; then nothing more needs repairing. If it runs earlier, the issues created for those six join the repair list. The rate limit itself is not in this plan.

Files read to decide: `AGENTS.md` (path map, "Running the factory locally", "Unattended runs", "Token economy", "Changing the bundle"), `docs/spec/agents/AGENTS.root.md`, `docs/spec/agents/AGENTS.nested.md` (`specs/`), `docs/spec/factory/.claude/agents/planner.md`, the skills `yaf-writing-plans` (with its template), `yaf-commits-and-pull-requests`, `yaf-orchestrating-work`, `yaf-implementing-tickets`, `yaf-reviewing-changes`, `specs/0010-gates-cancellation/plan.md`, `specs/0011-gates-check-reporting/plan.md`, every `specs/*/plan.md` (parsed with `parseTickets`), `docs/spec/factory/.github/factory/plan.mjs`, `create-tickets.mjs`, `scope.mjs`, `inflight.mjs`, `gates.mjs`, `project.mjs`, `policy.json`, `prompts/doc-review.md`, `prompts/pr-review.md`, `tests/delta.test.mjs`, `tests/project.test.mjs`, `.github/scripts/validate-instructions.mjs`, `.github/workflows/factory-tickets.yml`, `.claude/commands/factory-run.md`, issues #329, #331 and #359, and the log of run 45897939422.

### Alternatives rejected

| Alternative | Why not |
|---|---|
| Write the Touches cells differently (no commas in a path, notes elsewhere) | The file names are given by the repository's convention, and a planner cannot see the fault until the issues exist. The parser must take what the template allows |
| Split on `,` only when a space follows it | Works for `{TId,TSelf}` by chance, not for a note such as `(three sections, YAF2011 only)` or a planner who writes `{TId, TSelf}` |
| Make each touches entry an object `{ path, note }` | Changes the shape every caller reads, for a note that only people read. The issue body shows the note as written |
| An idempotent update mode in `create-tickets.mjs` that rewrites the Touches section of an existing open issue | A lasting write path to existing issues in a tier-high script, with its own tests and risks (an issue an implementer has started, a plan row changed since), for six issues once. Can be planned on its own if plan updates need it |
| Correct the closed issues too | Nobody acts on them; editing them only moves their update time |

## Affected packages

| Layer | Package / area | Change |
|---|---|---|
| Core | none | none |
| Domain | none | none |
| Application | none | none |
| Infrastructure | none | none |
| Persistence | none | none |
| Http | none | none |
| Adapters | none | none |
| Testing (`Testing`, `Testing.Integration`) | none | none |
| Analyzers | none | none |
| Sample | none | none |
| Templates | none | none |
| Docs | none | none |
| The factory kit (not a layer) | `docs/spec/factory/.github/factory/plan.mjs`, `create-tickets.mjs`, two new test files | Split touches only at depth 0; keep an entry with a note as written (T1) |

## Public API, schema and error-code delta

None. No public API, schema, error code, contract, configuration key, telemetry name or dependency changes.

Breaking: no.
New dependency: no.

```factory:delta
{ "publicApi": [], "schema": [], "errorCodes": [], "contracts": [], "configuration": [], "telemetry": [], "dependencies": [] }
```

## Schema and migration plan

None.

## Test strategy

There are no requirements `R…`; what must be true after the plan:

| What must be true | Ticket | Level | Check |
|---|---|---|---|
| A comma inside `{…}` does not split a touches entry, with and without backticks | T1 | unit | `tests/plan.test.mjs` |
| A comma or semicolon inside a code span, or inside `(…)` or `[…]`, does not split an entry; one at depth 0 does, as does `<br>` | T1 | unit | `tests/plan.test.mjs` |
| An entry that is exactly one code span loses its backticks; an entry with a note keeps its text, backticks included | T1 | unit | `tests/plan.test.mjs` |
| An unmatched `)` changes nothing; an unclosed `{` keeps the rest of the cell as one entry | T1 | unit | `tests/plan.test.mjs` |
| Depends on still gives `['T1', 'T2']` for `T1, T2` and `[]` for `—` | T1 | unit | `tests/plan.test.mjs`, and the existing `tests/delta.test.mjs` |
| `ticketBody` writes `` - `path` `` for a bare path and `- <entry>` for an entry with backticks | T1 | unit | `tests/create-tickets.test.mjs` |
| Every touches entry of every plan on `main` has balanced braces and an even number of backticks | T1 | script over the repository | V3 |
| The six open issues show the plan's touches | after T1 | by reading | V4 |

## Risks and rollback

| Risk | Likelihood | Mitigation | Rollback |
|---|---|---|---|
| An existing plan relied on a split inside parentheses, so an entry with a note now holds more than one path | Certain for a few rows (plan 0013 T76 names four paths in one note) | The entry is shown as written, with every path in it; the orchestrator and the reviewers read all of it | none needed |
| The repair of an issue loses a hand edit of its body | Low: only `create-tickets.mjs` writes ticket bodies; status, evidence and deviations are comments | The repair replaces only the lines between `### Touches` and the next blank line, and prints each new body before it writes | Restore the old body from the issue's edit history |
| `factory-tickets` for plan 0013 is re-run before T1 merges | Possible | The six later tickets are listed above; they join the repair | — |

## Verification commands

Git Bash, repository root, on the ticket's branch; `BASE` is `origin/main`; `K=docs/spec/factory/.github/factory`.

V1: `git diff --name-only $BASE...HEAD` lists only the ticket's touches; `node $K/size.mjs $BASE HEAD` stays at the estimate or below; `node .github/scripts/sync-references.mjs --check` and `node .github/scripts/validate-instructions.mjs` exit 0.

V2: `node --test "$K/tests/*.test.mjs"` passes, the new tests included.

V3: every touches entry of every plan is whole (prints nothing, exits 0):

    node --input-type=module -e "import fs from 'node:fs'; import { parseTickets } from './$K/plan.mjs'; let bad = 0; for (const d of fs.readdirSync('specs')) { const f = 'specs/' + d + '/plan.md'; if (!fs.existsSync(f)) continue; for (const t of parseTickets(fs.readFileSync(f, 'utf8'))) for (const p of t.touches) if (p.split('{').length !== p.split('}').length || p.split('\`').length % 2 === 0) { console.log(d, t.key, p); bad++; } } process.exit(bad ? 1 : 0);"

On `main` today it lists 21 entries in plans 0006, 0007 and 0013.

V4, after the repair: for each of #329, #331, #359, #362, #363, #386, `gh issue view <n> --json body --jq .body` shows a `### Touches` list equal to what `ticketBody` renders from the plan row on `main`; for #329 it has the line `` - `src/Domain/AggregateRoot{TId,TSelf,TMemento}.cs` ``.

## After T1 merges

1. **Repair the six open issues**, by the orchestrator, in an attended session, as the factory app (the command is not on the unattended allow list). For each issue in the table "The issues already created": read its body; replace the lines from `### Touches` up to the next blank line with `### Touches` and the list `ticketBody` renders for the plan row on `main` (`parseTickets` on `specs/0013-thin-slice/plan.md`, the row of the issue's marker); show the new body; write it with `node .github/scripts/factory-token.mjs --app factory -- gh issue edit <n> --body-file <file>`; add one comment, "Touches repaired after plan 0014 T1; the list now matches the plan row." Nothing else in the body changes. Then V4.
2. **Create the rest of plan 0013's tickets** with a `factory-tickets` run for `specs/0013-thin-slice/plan.md` once T1 is on `main`. If that run went first, add the issues of T66, T69, T70, T72, T76 and T80 to step 1.

## Research

None: no third-party behaviour is relied on. The observations (issue bodies, the run log) are cited where they are used.

## Decision log

| Date | Decision | Reason | By |
|---|---|---|---|
| 2026-11-26 | Fix the parser rather than the way plans write touches | Alternatives rejected | Planner |
| 2026-11-26 | Also fix entries with a note after the path, not only commas in braces | Same helper, same fault: four open issues show broken code spans from it, and six tickets of plan 0013 not yet created would too | Planner |
| 2026-11-26 | Repair the six open issues once, by the orchestrator as the factory app; leave the closed ones; no update mode in `create-tickets.mjs` | Alternatives rejected | Planner |
| 2026-11-26 | Create plan 0013's tickets T65 to T93 after T1 merges | Then no further repair is needed; the repair list covers the other order | Planner |
| 2026-11-26 | Folder `0014-ticket-touches-paths` | Next free number | Planner |

## Deviations from the specification

1. **No `spec.md`** for a change that is not an initial-build milestone (ADR-0023, ADR-0025). The change fixes a fault of the factory's scripts and adds no behaviour to YAF; plans 0010 and 0011 took the same route.

## Tickets

One ticket, tier high (`docs/spec/**` is sensitive and code-owned): it waits for the owner's approving review. It changes no file under `.github/workflows/`, so the factory app pushes it.

| Key | Title | Type | Area | Requirements | Depends on | Touches | Tier | Size | Failing test first | Verify |
|---|---|---|---|---|---|---|---|---|---|---|
| T1 | ci: keep paths with commas and notes whole in ticket touches | chore | ci | Issue #329; Approach, "The cause" and "The fix" | — | `docs/spec/factory/.github/factory/plan.mjs`, `docs/spec/factory/.github/factory/create-tickets.mjs`, `docs/spec/factory/.github/factory/tests/plan.test.mjs`, `docs/spec/factory/.github/factory/tests/create-tickets.test.mjs` | high | 90 | In the new `tests/plan.test.mjs`: the Touches cell `` `src/Domain/AggregateRoot{TId,TSelf,TMemento}.cs`, `src/Domain/PublicAPI.Unshipped.txt` `` gives `['src/Domain/AggregateRoot{TId,TSelf,TMemento}.cs', 'src/Domain/PublicAPI.Unshipped.txt']` (on `main` it gives four entries); `src/Testing/InMemoryRepository{TAggregate,TId}.cs, tests/Testing.Tests/` without backticks gives two (on `main` three); `` `src/Analyzers/Domain/`, `.editorconfig` (three sections, YAF2011 only) `` gives two entries, `src/Analyzers/Domain/` and the second as written, backticks and note included (on `main` three entries); further cases as in the test strategy. In the new `tests/create-tickets.test.mjs`: `ticketBody` for that ticket contains the line `` - `.editorconfig` (three sections, YAF2011 only) `` (on `main` it contains `` - `.editorconfig` (three sections` ``) | `node --test docs/spec/factory/.github/factory/tests/plan.test.mjs docs/spec/factory/.github/factory/tests/create-tickets.test.mjs` passes; V2; V3; V1 |
