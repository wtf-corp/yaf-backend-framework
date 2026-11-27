# 0015 — Factory throughput: plan

**Spec** none: this plan has no `spec.md`. Its basis is the owner's decision of 2026-11-26 (decision log, first row): "the current workflow is ok-ish, but requires a lot of attention from a human operator ... i would like to increase the throughput of the factory before we continue", with five of the ideas put to them chosen (1, 2, 5, 6, 7). Not chosen, and out of scope: the reviews back in CI, smaller plans per lane, a broad lowering of tiers. No section of `docs/spec/spec/` is amended; ADR-0029 §4 is (T5) · **Status** Draft

## Approach

Each idea removes a kind of human attention the factory needs today. All five change the factory's own files (the kit under `docs/spec/factory/`, `AGENTS.md`, the skills, `CODEOWNERS`), so every ticket but one is tier high and waits for the owner's approving review. The plan proposes two policy changes (T4, T7) and one `CODEOWNERS` change (T8); no agent applies them on its own, and none applies `guard-change` (hard rule 8).

### Idea 5: ticket creation that does not stall (T1–T3)

**The cause, measured** ([research](./research.md) §1–2). `create-tickets.mjs` finds every ticket by a search (`findByMarker`, line 46, `search/issues`), one per ticket and one for the epic. Search allows 30 requests a minute. Run 45897939422, attempt 2, read T1 to T29 of plan 0013 as existing, 0.3 to 0.4 s apart, and failed on the next search with HTTP 403, while the core and GraphQL budgets had most of their hour. The other three failed runs stopped in `gh project` calls with "GraphQL: API rate limit exceeded": a secondary limit after hundreds of Project calls in minutes. Plan 0013's T65 to T93 still have no issue.

**The fix.**

- T1 adds `ratelimit.mjs`: GitHub calls made with `gh api -i`, which writes the status line and the headers also when it fails (research §3); a retry on 403 or 429 that waits `retry-after` seconds, or until `x-ratelimit-reset` when `x-ratelimit-remaining` is 0, or else one minute, doubling on each further failure; for `gh project` calls, which print no headers, it reads `GET /rate_limit` (`resources.graphql`) to choose; a pause of at least one second between writes; a total waiting budget (30 minutes), after which it throws with the last error. Clock and sleep are parameters, as `addItem`'s `wait` is now.
- T2 lists the repository's issues once per run (`repos/<repo>/issues?state=all&per_page=100`, page by page through T1, pull requests dropped) and indexes them by their `factory:ticket` and `factory:epic` markers. No search remains. When T1 gives up, the error names the next ticket and the command to resume: `gh workflow run factory-tickets.yml -f plan=<plan>`. The run is resumable because it already was idempotent: an existing marker is never created again, and `firstStatus` gives an existing open ticket with no status its first one.
- T3 sends `project.mjs`'s `gh project` calls (`view`, `field-list`, `item-add`, `item-edit`, `item-list`) through T1's retry, and the writes through its pacing. `status.mjs`, which uses the same module, gains the same.

`gh run rerun` runs the scripts of the original commit, which is why attempt 2 failed the same way. `factory-tickets.yml` already has `workflow_dispatch` with the input `plan`, and a dispatch runs `main`'s scripts: the fix needs no workflow change (research §2). The app's missing Workflows permission is the owner's call and is not changed here (Q5).

### Idea 1: mechanical plan checks (T10–T16)

`ticketProblems` (`plan.mjs`, line 103 on `main` at 9def9ee) checks keys, forward dependencies, sizes, a failing test, a verify command and non-empty touches. The review of plan 0013 (PR #303) took six rounds and 33 findings. Research §5 sorts them: about one in six was bookkeeping a script can decide, two of the ten Majors among them; the rest was design, prose and judgment. These checks take the bookkeeping out of the review; they will not make one round of a plan like 0013 by themselves.

Five checks, each in a module of its own so that they can be built in parallel, then joined:

| Check | Problem (fails `factory/plan-gate`) | Note (reported only) | Ticket |
|---|---|---|---|
| Overlapping touches | Two tickets, neither an ancestor of the other through `Depends on`, whose touches overlap: the same path; a folder (ends with `/`, or its last segment has no extension) and a path inside it; a glob and a path it matches; two paths in the same `Migrations/` folder | — | T10 |
| Uses without a dependency | A ticket of a type other than `docs` names, in a code span of its Title, Failing test first or Verify cell, a type or file that another ticket creates, and that ticket is not among its ancestors (or comes later). "Creates": the first ticket whose touches name a `.cs` file that does not exist on the base (the type is the file name without `{…}` and extension), whose failing test says "`X` does not exist", or whose touches name a new `adr/NNNN-…` file (matched as `ADR-NNNN`) | The same for a `docs` ticket | T11 |
| `factory:delta` forms | An entry of a category in the wrong form: error codes `a.b.c` in lower case (or a prefix ending in `*`); configuration keys `A:B`; schema names identifiers; dependencies `Id` or `Id@Version`; `editorconfig` entries `[<section>] <key> = <value>` (T6). A namespace-wide public API prefix (`YAF.X.*`) declared without all sixteen line forms the PublicAPI analyzer writes for it (plan 0013's settled list: `""`, `static`, `static readonly`, `static abstract`, `const`, `abstract`, `virtual`, `override`, `override sealed`, and with `~`: `""`, `static`, `static readonly`, `abstract`, `virtual`, `override`, `override sealed`), naming the missing ones | — | T12 |
| Tier against touches | The declared tier is below the floor the policy gives the touches: high for a sensitive path (folders probed with a file inside them), except `.editorconfig` and `Directory.Packages.props` when the plan declares entries for them (T6, T7) and no declared section names a `neverLowered` file, which give medium; medium for an approved file of `policy.delta.sources` other than a `PublicAPI.*.txt` that does not exist on the base (a new package's PublicAPI files start with `#nullable enable` only, which the delta does not count); medium for a `feat` title unless every touch is in the low paths | The declared tier is above the floor (raising is allowed, but say so); a folder touch that contains a sensitive subfolder | T13 |
| Ticket references | A key in the plan's Test strategy table that is not in the Tickets table; on a plan update, a key of the base branch's version that is gone (keys are never renumbered after the merge) | A `T<n>` in the prose of `plan.md` or `research.md` that is not a key (it may be another plan's, as in this plan); a key whose title changed since the previous commit of the branch or the base version, with each line that cites it | T14 |

T15 joins them in `plan-check.mjs`: `planCheck(markdown, { policy, exists, research, previous, base })` returns `{ problems, notes }`, `factory/plan-gate` fails on problems (title "N problem(s) in the plan"; the job summary lists problems and notes), and a command line lets the planner run the same check before pushing: `node docs/spec/factory/.github/factory/plan-check.mjs specs/NNNN-slug/plan.md [--base <rev>]` exits 1 on problems and prints both lists. `inflight.mjs` already treats a plan gate that failed without the title "flagged" as `gate-failed`, so the model review never runs on a plan with problems. T16 tells the plan reviewer that checklist items 7 and 8 are checked mechanically for what the table lists, and to judge what a script cannot (a dependency on behaviour, honest touches, a tier raised on purpose), and tells the planner to run the command.

Measured with a prototype on plan 0013 (research §5): round 1 gives the overlap of F1 (Major) and the tier of F6; the merged plan gives no problem.

### Idea 2: guard and dependency changes the plan declared (T5–T9)

**Today.** #389 (`Directory.Packages.props`, seven `PackageVersion` lines with comments) was tier high because only a patch update of an existing package is exempt (`classify.mjs`, `isPatchBumpOnly`). #388 (`.editorconfig`, eight new file-scoped sections) was tier high because `.editorconfig` is a sensitive path, and `factory/guards` failed on eight "analyzer severity lowered" findings until the owner applied `guard-change`. Both only did what plan 0013 T1 and T2 declared, in a plan the owner had approved.

**The rule.** A change to `.editorconfig` or `Directory.Packages.props` whose every added line the merged plan declares exactly is approved by the owner's approval of that plan. The pull request is then tier medium for that file, and its guard findings in that file are reported as declared, not counted. Exactly:

- **`.editorconfig`.** The plan's `factory:delta` block gets an eighth category, `editorconfig`, one entry per setting: the section header, a space, and the setting line exactly as the file will hold it, for example `"[src/Application/IRequest\\{TResult\\}.cs] dotnet_diagnostic.CA1040.severity = <level>"`, where `<level>` stands for the severity the section sets (in #388 the word for "off"). `declared.mjs` (T6) reads both versions of the file as sections (comments and blank lines ignored): every section of the base is unchanged in the head; every new section names one file (no unescaped `*`, `?`, `[`, `{`), and every setting in it is a declared entry; the named file matches none of `policy.declaredGuards.neverLowered`.
- **`Directory.Packages.props`.** A dependency entry may carry its version: `"Npgsql.EntityFrameworkCore.PostgreSQL@11.0.0"`. Every `PackageVersion` of the base is unchanged in the head; every new one is a declared `Id@Version`; the rest of the file, with comments and the `PackageVersion` elements taken out, is unchanged. An entry without a version still flags the plan, as today, and lowers nothing.
- **The declarations come from the plan on the base branch.** `gates.mjs` already reads the plan named by the `Plan:` line from the base checkout (lines 56–63), never from the pull request. T7 computes the declared files from that copy only, so a pull request that edits the plan and the guard file together cannot declare its own entries: it stays tier high (and `factory/ticket` fails it, as a plan changes in a pull request of its own).
- **Everything else is as today.** Any other sensitive path in the pull request keeps it high. `neverLowered` holds the paths the owner named (authentication, crypto, tenancy, CI and agent instructions) and, as the owner accepted (Q4), the rest of ADR-0029's security list: `src/Http/Authentication/**`, `src/Application/Authorization/**`, `src/Application/Context/**`, `src/Persistence.EfCore*/Tenancy/**`, `src/Infrastructure/Cryptography/**`, `src/Persistence.EfCore/Protection/**`, `src/Persistence.EfCore*/Audit/**`, `src/Privacy*/**`, `src/Infrastructure/Redaction/**`, `.github/**`, `.claude/**`, `.agents/**`, `AGENTS.md`, `**/AGENTS.md`, `CLAUDE.md`, `docs/spec/**`. A section that relaxes an analyzer on a file there keeps the pull request high.
- **A plan that declares them is always seen by the owner.** Package pins already flag a plan (`dependencies` is in `policy.delta.flaggingCategories`). T7 adds `editorconfig` to that list, so a plan, or a plan update, with any `editorconfig` entry is flagged and needs the owner's approving review (`factory/plan-gate`). Without that, a plan that only declared sections for a tier-medium ticket would merge after the model review alone, and its `.editorconfig` pull request would then merge at tier medium too: an analyzer relaxation on `main` with no human approval.

Under this rule #389 would have been tier medium. #388 would still have been high: its last section names `src/Application/Context/Principal.cs`. Without that section it would have been medium, but `.editorconfig` is in `CODEOWNERS`, so GitHub would still have asked the owner for a review. T8 takes `.editorconfig` out of `CODEOWNERS`, as the owner agreed (Q3): an undeclared change stays tier high, and tier high already needs a maintainer's approving review through `factory/human-approval`, as `Directory.Packages.props` does today (`CODEOWNERS` header).

ADR-0029 §4 puts "a guard file change" and "a new dependency" in tier high. T5 records the exception as ADR-0034, which amends that section; the code follows it (T6, T7) and the docs describe it (T9).

**The guard scan and prose (T22).** `guards.mjs` matches its patterns against the added lines of every file, Markdown included, so a plan that quotes an `.editorconfig` setting fails `factory/guards` (plan 0013, and the first push of this plan). T22 leaves `specs/**/*.md` out of the scan: prose there configures nothing. Every other file is scanned as before, `docs/**` included.

### Idea 6: the milestone exit check (T17–T19)

The owner's check at the end of a milestone means reading the plan's "Verification commands", running them and finding the evidence. T17 adds `exit-check.mjs`. A milestone plan declares its exit checks in a block the script reads:

````markdown
```factory:exit
{
  "criteria": [{ "text": "An HTTP test creates and reads a session on PostgreSQL", "checks": ["E1"] }],
  "checks": [{ "id": "E1", "title": "session endpoints", "run": "dotnet test --solution YAF.slnx -c Release --filter-query \"/*/*/SessionEndpointsTests/*\"", "expect": 0 }]
}
```
````

`node docs/spec/factory/.github/factory/exit-check.mjs <plan.md> [--list | --dry-run | --post]` takes one mode. `--list` prints the criteria and checks and runs nothing. `--dry-run` runs the checks and prints the comment it would post, and posts nothing. `--post` runs them and posts the comment. To run, it refuses unless the checkout is clean and its `HEAD` is `origin/main`; runs each check with `bash -c` from the repository root, one after another, with `GH_TOKEN` and `GITHUB_TOKEN` taken out of their environment; records exit code, duration and the last 40 lines; reads the plan's tickets from the issue listing of T2 (closed or open) and the `ci.yml` run of that commit (`gh run list -w ci.yml -c <sha>`, research §3); and with `--post` writes one comment on the epic, marked `<!-- factory:exit-check <sha> -->`: each criterion with its checks and verdict, each check with command, exit and time, the tickets not closed, the CI run, and the output tails in `<details>`. It exits 0 only when every check gave its expected exit code, every ticket is closed and the CI run passed. It is run as the factory app. **A plan with a `factory:exit` block is always flagged** (T17 adds the reason to `planFlags` in `plan.mjs`): the block holds commands that run on the owner's machine, unattended once T19 allows the command, and taking the tokens out of their environment does not protect the app's key files, which any process running as the user can read ("What the setup does not give" in `AGENTS.md`). So every exit block, new or changed in a plan update, passes the owner's approving review (`factory/plan-gate`) before it can run. T18 (a planner) adds the block to plan 0013, from its exit criteria and "The whole milestone" paragraph. T19 adds the block to the plan template and to the milestone-end report of `yaf-orchestrating-work`, and allows the command in `factory-run.md`.

### Idea 7: three implementers and stacking (T4, T20, T21)

**Three implementers (T4).** `policy.json` `limits.parallelImplementers` goes from 2 to 3, the value of ADR-0029 §6 and `AGENTS.root.md`. In `AGENTS.md`, "Token economy", "Lean orchestration" (line 170 on `main` at 9def9ee), the line

```text
- At most two implementers at once (`limits.parallelImplementers`).
```

becomes

```text
- At most three implementers at once (`limits.parallelImplementers`). A stacked ticket that waits for its dependency's merge is not one of them.
```

**Stacking (T20, T21).** Today ("Unattended runs", "Limits") a ticket that depends on an open one waits for its merge, because the gates run only for pull requests to `main` (`factory-gates.yml`, `branches: [main, next]`). The plan stacks the work, not the pull request (Q2, option B, the owner's answer of 2026-11-27):

1. **Pick.** A `Backlog` ticket may start stacked when all its dependencies are closed but one, D; D's pull request is open with base `main` (so D is not itself stacked: one level only); and its touches overlap no running ticket's other than D's. The orchestrator posts `<!-- factory:stacked-on <D's head sha> pr=<D's pull request> -->` on the ticket, sets it `In progress`, and briefs the implementer with the base `origin/<D's branch>` at that commit.
2. **While D is open.** The worktree goes through the local review as any other (state `local-review`, `local-fix`). Once it is `ready`, its state is `stacked`: nothing is pushed and no pull request is opened.
   **D and the stacked ticket are never written at the same time.** Their touches may overlap, and `AGENTS.root.md` "Limits" allows parallel implementers "only on tickets that neither depend on each other nor touch the same files". So a stacked ticket is started only while no implementer works on D (D's worktree is not `working`), and a fix round on D waits while the stacked ticket's implementer works (its worktree is `working`): `inflight.mjs` (T20) then gives D's pull request the action `wait`, reason "a ticket stacked on it is being written". Whichever writer started first finishes first; the other starts after it. The rule of "Limits" is kept, not relaxed.
3. **D merges.** The state becomes `restack`. The orchestrator, as the factory app, runs `git -C <worktree> fetch origin main` and `git -C <worktree> rebase --onto origin/main <sha>`: only the ticket's own commits are replayed on the squash commit of D. On a conflict it runs `git rebase --abort` and records a `factory:local-review <head> changes-required` marker whose findings name the conflicting files, so the implementer resolves it as a local fix round within `limits.fixRounds`.
4. **After the rebase** the head no longer contains `<sha>`, so the worktree is an ordinary one: the local review of the new head (steps 5.1 and 5.2, no model), the push, the pull request to `main`, the gates, one `factory/review`. D closed without a merge: the state is `human`.

**How the gates and `inflight.mjs` see it.** No pull request exists while the branch is stacked, so the gates, `factory-automerge` and the local review only ever see pull requests to `main`, with the ticket's own diff. `inflight.mjs` (T20) reads the newest `stacked-on` marker of the ticket, checks with `git merge-base --is-ancestor` whether the head still contains its commit, and reads D's pull request: the new worktree states `stacked` and `restack` (with `onto`). For pull requests only one thing changes: a `fix` on D becomes `wait` while a worktree stacked on D is `working` (step 2).

**Risks.** A D that waits days for the owner keeps its stacked ticket waiting (but not an implementer). Fix rounds on D after the stack was cut can conflict with the stacked commits; the conflict becomes a local fix round. The stacked ticket's tests ran against D's earlier head; they are run again after the rebase (step 4). The owner reads one pull request at a time, in order, as today.

In `AGENTS.md`, "Unattended runs", "Limits" (line 135 on `main` at 9def9ee), the line

```text
- Nothing is stacked: a ticket that depends on an open one waits for its merge.
```

becomes

```text
- Stacking is one level deep. A ticket whose one open dependency has an open pull request starts on that branch; its own pull request is opened once the dependency has merged and the branch is rebased onto `main` ([plan 0015](specs/0015-factory-throughput/plan.md), Q2). Every pull request targets `main`.
```

No other line of `AGENTS.md` changes. T21 also changes the sentence of `yaf-orchestrating-work` step 3 that says nothing is stacked in an unattended run, its worktree table, `factory-run.md` steps 2 and 3 with the commands it allows, and `yaf-commits-and-pull-requests` ("Branches", the stacked branch's pull request).

Files read to decide: `AGENTS.md` (path map, "Running the factory locally", "Unattended runs", "Model reviews run locally", "Token economy", "Changing the bundle"), `docs/spec/agents/AGENTS.root.md`, `docs/spec/agents/AGENTS.nested.md` (`specs/`), `specs/AGENTS.md`, `docs/spec/factory/.claude/agents/planner.md`, the skills `yaf-writing-plans` (with its template), `yaf-commits-and-pull-requests`, `yaf-orchestrating-work`, `yaf-reviewing-specs-and-plans`, `yaf-implementing-tickets` (rules 13, gate), `specs/0012-m2-maintenance/plan.md`, `specs/0014-ticket-touches-paths/plan.md`, `specs/0013-thin-slice/plan.md` (Approach, delta, Verification commands, Tickets) and its seven reviewed versions, ADR-0029 (§4–6), `docs/spec/spec/18-build-plan.md` (row M3), and in `docs/spec/factory/.github/factory/`: `policy.json`, `policy.schema.json`, `lib.mjs`, `plan.mjs`, `classify.mjs`, `delta.mjs`, `guards.mjs`, `gates.mjs`, `ticket.mjs`, `scope.mjs`, `create-tickets.mjs`, `project.mjs`, `status.mjs`, `inflight.mjs`, `automerge.mjs`, `markers.mjs`, `prompts/doc-review.md`, the test list of `tests/`, the kit `README.md`, `docs/spec/factory/.claude/commands/factory-run.md`, `docs/spec/factory/.github/CODEOWNERS`; `.github/CODEOWNERS`, `.github/workflows/factory-tickets.yml`, `factory-gates.yml`, `factory-automerge.yml`, `factory-status.yml`, `.github/scripts/local-review.mjs`, `sync-references.mjs`, `validate-instructions.mjs`; PRs #303 (summary comment and the `factory/doc-review` check runs of its commits), #388, #389 and their commits 4da1bcb and 968d202; the logs of runs 45888843738, 45897939422 (both attempts) and 45900760880.

### Alternatives rejected

| Alternative | Why not |
|---|---|
| Idea 5: keep the search and sleep two seconds between searches | Still one search per ticket, and the Project's GraphQL limit, which stopped three of the four runs, is untouched |
| Idea 5: list only issues labelled `factory` | A ticket whose label was removed would be created again. The whole listing is four pages for this repository |
| Idea 5: `gh api graphql` for the Project instead of `gh project` | More code for the same calls; `GET /rate_limit` tells the retry what it needs |
| Idea 5: give the app the Workflows permission so that the orchestrator can re-run workflows | The owner's security call (Q5) |
| Idea 1: one module with all checks | Five tickets would edit one file one after another; separate modules run in parallel and are joined by T15 |
| Idea 1: match public API lines without their modifiers in `delta.mjs`, so that `YAF.X.*` covers every form | Changes what `factory/delta` accepts on every pull request, a guard change nobody asked for. T12 tells the planner the missing forms instead |
| Idea 1: a script check that a title has no "and" | A title with "and" can name one concern (PR #303, round 6 F5); that is judgment |
| Idea 1: all findings as notes, nothing failing | The model review would still have to check them; failing on problems is what moves them out of the review (Q1) |
| Idea 2: a `guard-change`-like label the gates apply when the diff matches | Only a human's label counts (ADR-0029); a bot's label would be a new kind of approval. The plan approval is the human act |
| Idea 2: lower the tier of any guard file the plan names in `touches` | Touches name files, not lines: a pull request could add anything to the file. Exact entries are what the owner approved |
| Idea 2: keep `.editorconfig` code-owned | Then a declared section still waits for the owner's review, and idea 2 helps only `Directory.Packages.props` (Q3) |
| Idea 6: a workflow that runs the exit checks in CI | A new workflow file, which only the owner can push, with Docker and .NET set up again; the checks need the same machine as the tickets' verification |
| Idea 6: parse the "Verification commands" prose | Its form differs per plan (indented blocks, inline commands, "expected …" in words) |
| Idea 7, stacking option A: open the stacked pull request at once against D's branch, retarget it to `main` when D merges | No gates, automerge or review run for that base (`branches: [main, next]`), so the early pull request shows nothing; the retarget leaves a diff with D's commits until the rebase is force-pushed onto an open pull request; whether the retarget fires `edited` is not verified (research §4). It is the owner's word "retargeted", hence Q2 |
| Idea 7: stack more than one level | Each level adds a rebase that can conflict; one level takes the waiting out of a chain of two |

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
| Docs | none | none (the DocFX site does not change) |
| The factory kit (not a layer) | `docs/spec/factory/.github/factory/` | New `ratelimit.mjs` (T1), `declared.mjs` (T6), `plan-deps.mjs` (T10, T11), `plan-forms.mjs` (T12), `plan-tier.mjs` (T13), `plan-refs.mjs` (T14), `plan-check.mjs` (T15), `exit-check.mjs` (T17), each with tests; changed `create-tickets.mjs` (T2), `project.mjs` (T3), `plan.mjs` (T6; T17: a plan with a `factory:exit` block is flagged), `classify.mjs`, `gates.mjs`, `guards.mjs`, `policy.json` (with `editorconfig` in `delta.flaggingCategories`), `policy.schema.json` (T4, T7, T15), `guards.mjs` again (T22: no guard findings in the prose of `specs/**/*.md`), `inflight.mjs`, `markers.mjs` (T20), `prompts/doc-review.md` (T16), `README.md` (T9, T19), `.claude/commands/factory-run.md` (T19, T21), `.github/CODEOWNERS` (T8) |
| Agent guidance (not a layer) | `docs/spec/agents/skills/`, `AGENTS.md` | `yaf-writing-plans` and its template (T9, T16, T19), `yaf-reviewing-specs-and-plans` (T16), `yaf-commits-and-pull-requests` (T9, T21), `yaf-orchestrating-work` (T19, T21); two lines of `AGENTS.md` (T4, T21) |
| Decisions (not a layer) | `docs/spec/adr/` | ADR-0034, amending ADR-0029 §4 (T5) |
| Plans (not a layer) | `specs/0013-thin-slice/plan.md` | The `factory:exit` block (T18) |
| Repository files | `.github/CODEOWNERS`, `.claude/commands/factory-run.md` | `.editorconfig` line removed (T8, if Q3 is yes); the regenerated command (T19, T21) |

## Public API, schema and error-code delta

None. No public API, schema, error code, contract, configuration key, telemetry name or package dependency of YAF changes.

What changes for the factory, which no category of the block covers:

- **Added** the delta category `editorconfig` and the form `Id@Version` for `dependencies` (T6); the block `factory:exit` (T17); the policy key `declaredGuards` with `files` and `neverLowered` (T7); the marker comments `factory:stacked-on` (T20) and `factory:exit-check` (T17); the worktree states `stacked` and `restack` (T20).
- **Changed** `limits.parallelImplementers` from 2 to 3 (T4); `factory/plan-gate` fails on the problems of T10–T14 (T15); a declared change of `.editorconfig` or `Directory.Packages.props` is tier medium (T7); a plan with an `editorconfig` entry is flagged (T7, `delta.flaggingCategories`); `factory/guards` no longer reads the prose of `specs/**/*.md` (T22).

Breaking: no. A plan merged before T6 has no `editorconfig` entries and versionless dependencies: its pull requests are classified as today.
New dependency: no.

```factory:delta
{ "publicApi": [], "schema": [], "errorCodes": [], "contracts": [], "configuration": [], "telemetry": [], "dependencies": [] }
```

## Schema and migration plan

None.

## Test strategy

There are no requirements `R…`; what must be true after the plan:

| What must be true | Idea | Ticket | Level | Check |
|---|---|---|---|---|
| A rate-limited call waits as GitHub's headers say, or a minute doubling, within a budget; writes are at least a second apart | 5 | T1 | unit | `tests/ratelimit.test.mjs` |
| Ticket creation makes no search, lists the issues once, and names the ticket and command to resume with when it gives up | 5 | T2 | unit | `tests/create-tickets.test.mjs` |
| The Project calls are retried on a GraphQL rate limit and paced | 5 | T3 | unit | `tests/project.test.mjs` |
| Plan 0013's T65 to T93 exist after one dispatch | 5 | T2, T3 | by running | V5 |
| Three implementers may run at once | 7 | T4 | check | T4's verify |
| ADR-0034 records the declared-guard rule and amends ADR-0029 §4 | 2 | T5 | documentation check | T5's verify |
| A declared `.editorconfig` section or package pin is recognised exactly; anything else, or a `neverLowered` file, is not | 2 | T6 | unit | `tests/declared.test.mjs`, on #388's and #389's diffs |
| Such a pull request is tier medium and its declared guard findings do not fail `factory/guards`; anything more keeps it high | 2 | T7 | unit | `tests/classify.test.mjs`, `tests/gates.test.mjs` |
| A plan that declares an `editorconfig` entry is flagged for the owner | 2 | T7 | unit | `tests/delta.test.mjs` |
| `.editorconfig` is not code-owned | 2 | T8 | check | T8's verify |
| The template, the skills and the kit README describe the new entries | 2 | T9 | documentation check | T9's verify |
| Overlapping touches without a dependency path fail the plan gate | 1 | T10 | unit | `tests/plan-deps.test.mjs`; V4 |
| A code ticket that uses what a non-dependency creates fails it; a docs ticket gets a note | 1 | T11 | unit | `tests/plan-deps.test.mjs`; V4 |
| Malformed delta entries and missing public API forms fail it | 1 | T12 | unit | `tests/plan-forms.test.mjs`; V4 |
| A declared tier below the touches' floor fails it; above is a note | 1 | T13 | unit | `tests/plan-tier.test.mjs`; V4 |
| Unknown keys in the Test strategy and removed merged keys fail it; stale prose references are notes | 1 | T14 | unit | `tests/plan-refs.test.mjs` |
| The plan gate and the command line run all five checks | 1 | T15 | unit, by running | `tests/plan-check.test.mjs`; V4 |
| The plan reviewer and the planner know what the gate checks | 1 | T16 | documentation check | T16's verify |
| The exit checks of a plan run from a clean `origin/main` and are posted once on the epic; `--list` runs nothing and `--dry-run` posts nothing | 6 | T17 | unit | `tests/exit-check.test.mjs` |
| A plan with a `factory:exit` block is flagged for the owner | 6 | T17 | unit | `tests/exit-check.test.mjs` |
| Guard declarations come from the plan on the base branch, never from the pull request's copy | 2 | T7 | unit | `tests/gates.test.mjs` |
| Plan 0013 declares its exit checks | 6 | T18 | check | T18's verify |
| The orchestrator runs the exit check at a milestone's end | 6 | T19 | documentation check | T19's verify |
| A stacked worktree waits, is restacked when its dependency merges, and is then ordinary; a fix round on the dependency waits while the stacked ticket is written | 7 | T20 | unit | `tests/inflight.test.mjs`, `tests/markers.test.mjs` |
| The skills, the command and `AGENTS.md` say how to stack | 7 | T21 | documentation check | T21's verify |
| Guard patterns in the prose of a spec or plan are no guard change; in code, config and `.editorconfig` they still are | 2 | T22 | unit | `tests/gates.test.mjs` |

## Risks and rollback

| Risk | Likelihood | Mitigation | Rollback |
|---|---|---|---|
| The plan itself costs about twenty owner reviews | Certain | Most tickets are under 150 lines; T1–T3 come first, as they unblock M3 | — |
| A ticket run stays above GitHub's 500 content requests an hour (a plan of about 100 new tickets) | Medium | T1 waits as the headers say within a 30-minute budget; T2 makes the run resumable by a dispatch | Dispatch again later |
| A heuristic of T11 or T13 fails a sound plan | Medium | Measured on plan 0013 (research §5); docs tickets get notes; the planner runs the check before pushing; a false problem is answered by a dependency or a clearer cell, and reported | Revert T15, or turn the check into notes |
| A declared guard change slips something past the owner | Low | Exact entries only; base sections and packages unchanged; `neverLowered`; every other sensitive path keeps tier high; the plan that declares them is always flagged, because `dependencies` and (T7) `editorconfig` are in `policy.delta.flaggingCategories`, so `factory/plan-gate` waits for the owner's approving review (test `planFlags_EditorconfigEntry_IsFlagged`) | Revert T7; `guard-change` and tier high work as before |
| Removing `.editorconfig` from `CODEOWNERS` lets an undeclared change through | Low | It is tier high, so `factory/human-approval` requires a maintainer | Revert T8 |
| The exit check runs a command a plan declares on the owner's machine | Low | A plan with a `factory:exit` block is always flagged, so the owner approves every exit command before it can run (T17, test `planFlags_ExitBlock_IsFlagged`); only plans on `main`, from a clean `origin/main`; the commands are of the same kind as the tickets' verification, which the orchestrator re-runs today; each command is printed before it runs; the tokens are taken out of its environment | Remove the `exit-check.mjs * --post` entry from the `allowed-tools` of `factory-run.md` (revert T19's change of the command), so that every run of the exit check prompts the owner |
| Ignoring guard patterns in `specs/**/*.md` hides a real guard change | Low | Markdown under `specs/` is read by people and agents, never by the compiler or an analyzer; every other file, `.editorconfig`, `*.props` and code are scanned as before | Revert T22 |
| D and its stacked ticket wait for each other's writer | Medium | Whichever started first finishes first (step 2 of "Stacking") | — |
| Three implementers hit the usage limit again (the cause in "Token economy") | Medium | The owner chose it; Opus, one review per ticket and short reports stay | Set `limits.parallelImplementers` back to 2 |
| A restack conflicts after fix rounds on the dependency | Medium | It becomes a local fix round within `limits.fixRounds`, then `needs-human` | Close the stacked work and start the ticket from `main` |
| A stacked ticket waits long behind a tier-high dependency | Medium | It holds no implementer slot; one level only | — |

## Verification commands

Git Bash, repository root, on the ticket's branch; `BASE` is `origin/main`.

V1, every ticket: `git diff --name-only $BASE...HEAD` lists only the ticket's touches; `node docs/spec/factory/.github/factory/size.mjs $BASE HEAD` stays at the estimate or below and within 400; `node .github/scripts/sync-references.mjs --check` and `node .github/scripts/validate-instructions.mjs` exit 0.

V2, every ticket that changes a script, expected all passing and the counts of `$BASE` plus the new tests:

    node --test "docs/spec/factory/.github/factory/tests/*.test.mjs" && node --test ".github/scripts/tests/*.test.mjs"

V3, every ticket that changes a skill, the command or a template: `node .github/scripts/sync-references.mjs` changes nothing after the commit (the regenerated files are committed).

V4, after T15, the checks on plan 0013's history (`D` a scratch folder; the research file is copied next to each plan, as the command reads `research.md` from the plan's folder):

    D=$(mktemp -d); git show ac97688d:specs/0013-thin-slice/plan.md > $D/plan.md; git show ac97688d:specs/0013-thin-slice/research.md > $D/research.md
    node docs/spec/factory/.github/factory/plan-check.mjs $D/plan.md --base 50eec01^    # exits 1; lists T23 with T27 (YAF.slnx) and T64's tier
    node docs/spec/factory/.github/factory/plan-check.mjs specs/0013-thin-slice/plan.md --base 50eec01^    # exits 0
    node docs/spec/factory/.github/factory/plan-check.mjs specs/0015-factory-throughput/plan.md    # exits 0

V5, after T2 and T3 merge (the owner dispatches, Q5): `gh workflow run factory-tickets.yml -f plan=specs/0013-thin-slice/plan.md` completes green; the issues of T65 to T93 exist once each (`gh issue list --state all --search "factory:ticket specs/0013-thin-slice in:body" --limit 200 --json number --jq length` prints 93); a second dispatch creates nothing.

The whole plan, on `main` after the last ticket: V2; V4; the `factory-self-test` and `references` jobs of the last merge are green.

## After the tickets merge

1. **After T2 and T3:** dispatch `factory-tickets.yml` for `specs/0013-thin-slice/plan.md` (V5). Dispatch it once for `specs/0009-domain/plan.md` too, whose run of 2026-11-26 (#318) stopped at T30; it creates nothing and only completes the pass.
2. **After T18 and T19, at the end of M3:** run the exit check as the factory app and read the one comment on epic #323 of plan 0013: `node .github/scripts/factory-token.mjs --app factory -- node docs/spec/factory/.github/factory/exit-check.mjs specs/0013-thin-slice/plan.md --post`.
3. **After T21:** the next unattended run may stack. The orchestrator's notes elsewhere that cap implementers at two are out of date.

## Questions for the owner

Q1 to Q4 were answered by the owner on 2026-11-27 (decision log). Q5 stays open and is not planned.

| # | Question | Recommendation | Answer |
|---|---|---|---|
| Q1 | Should the findings of T10–T14 fail `factory/plan-gate` (problems), or only be reported? | Fail, as in the table of idea 1: only then does the model review stop checking them. The two heuristic rules (uses without a dependency, tier against touches) gave no false problem on the merged plan 0013; docs tickets and tiers above the floor are notes | Fail `factory/plan-gate` |
| Q2 | Stacking: open the stacked pull request only after the dependency merged and the branch is rebased (B), or at once against the dependency's branch and retarget it (A, the word "retargeted" in the goal)? | B: no gates, automerge or review run for a pull request whose base is not `main`, so A's early pull request shows nothing, and its retarget needs a force-push onto an open pull request. B keeps every pull request on `main` (alternatives) | B |
| Q3 | Take `.editorconfig` out of `CODEOWNERS` (T8)? | Yes. A declared section then merges at tier medium; an undeclared change is tier high and still needs a maintainer's approving review (`factory/human-approval`). Without it the editorconfig half of idea 2 saves only the `guard-change` label | Yes; T8 stays |
| Q4 | Which paths stay high whatever the plan says (`neverLowered`)? | The four groups you named (authentication, crypto, tenancy, CI and agent instructions) plus authorization, the execution context, field protection, audit, privacy, redaction and `docs/spec/**`, ADR-0029's security list. It is a policy value you can narrow | As proposed; ADR-0034 accepted as proposed, on condition that a plan declaring `editorconfig` entries is flagged (T7) |
| Q5 | The factory app has no Workflows permission, so the owner pushes workflow changes and dispatches nothing the app cannot. Change it? | Not planned: your security call. None of this plan's tickets changes `.github/workflows/`. The dispatches of "After the tickets merge" are yours (or the app's, if its installation has Actions write) | Open |

## Research

[research.md](./research.md): GitHub's rate limits and what the four failed runs show (§1, §2), `gh api -i` on an error and `gh run list -c` (§3), retargeting (§4), and the measurements on plan 0013 (§5).

## Decision log

| Date | Decision | Reason | By |
|---|---|---|---|
| 2026-11-26 | **Owner's decision:** increase the factory's throughput before M3 goes on, with ideas 1 (mechanical plan checks), 2 (guard and dependency changes declared by the plan), 5 (infrastructure stalls in ticket creation), 6 (a milestone exit-check script) and 7 (three implementers and stacking); not the reviews back in CI, smaller plans per lane or a broad lowering of tiers | Given to the orchestrator in the owner's own message and relayed to the planner | Human owner, 2026-11-26 |
| 2026-11-27 | Folder `0015-factory-throughput` | Next free number | Planner |
| 2026-11-27 | One listing of all issues instead of a search per ticket; retry and pacing in one module both scripts use | Research §1–2; alternatives | Planner |
| 2026-11-27 | The five plan checks in four new modules, joined by `plan-check.mjs`, which the gate and the planner both run | Parallel tickets; the planner finds the problems before the review | Planner |
| 2026-11-27 | Public API forms are checked in the plan, not normalised in `delta.mjs` | No change to what `factory/delta` accepts | Planner |
| 2026-11-27 | The sixteen public API forms are plan 0013's merged list | That list passed review and the analyzer's output on `main` (`src/*/PublicAPI.*.txt`) uses no other prefix | Planner |
| 2026-11-27 | A declared guard change matches line by line and may cover part of the plan's declaration; it need not add all of it | Two tickets may share one declaration, as with the other delta categories | Planner |
| 2026-11-27 | The exception to ADR-0029 §4 is recorded in ADR-0034 before the code | ADR-0029 makes guard files and new dependencies tier high; an exception to an accepted decision needs a record | Planner |
| 2026-11-27 | Exit checks are declared in a `factory:exit` block, run locally from a clean `origin/main`, posted on the epic | Alternatives; the checks need Docker and .NET as the tickets do | Planner |
| 2026-11-27 | Stacking one level deep, option B | Alternatives; the owner's answer to Q2 | Planner |
| 2026-11-27 | A restack conflict is a local fix round | Reuses the `local-fix` state and its limit | Planner |
| 2026-11-27 | **Owner's answers:** Q1, the new checks fail `factory/plan-gate`. Q2, stacking option B: the stacked pull request opens only after its dependency merges, rebased onto `main`. Q3, `.editorconfig` leaves `CODEOWNERS` (T8 stays). Q4 and deviation 2, ADR-0034 is accepted as proposed, with the proposed `neverLowered` list, on condition that a plan declaring `editorconfig` entries is flagged (review finding F1, fixed in T7). T4 and idea 7 were chosen on 2026-11-26 | Given to the orchestrator in the owner's own message and relayed to the planner | Human owner, 2026-11-27 |
| 2026-11-27 | `editorconfig` joins `policy.delta.flaggingCategories` (T7) | Without it a plan that only declares sections for a tier-medium ticket would merge unflagged, and its `.editorconfig` pull request at tier medium: no human approval anywhere (review F1; the owner's condition) | Planner |
| 2026-11-27 | A stacked ticket and its dependency D are never written at the same time; a fix round on D waits while the stacked ticket's implementer works | Keeps `AGENTS.root.md` "Limits" (no parallel implementers on dependent tickets or the same files) instead of relaxing it (review F4) | Planner |
| 2026-11-27 | `guards.mjs` does not scan the Markdown under `specs/` (T22) | This plan and plan 0013 failed `factory/guards` because their prose quoted an `.editorconfig` setting; prose configures nothing. Only `specs/**/*.md`, as the orchestrator asked; `docs/**` stays scanned | Planner |
| 2026-11-27 | **Owner's choice:** fix the three findings of the second review (line of `ticketProblems`, exit blocks flag a plan, declarations from the base plan) before approving, and flag plans with exit blocks rather than accept the risk | Given to the orchestrator in the owner's own message and relayed to the planner | Human owner, 2026-11-27 |
| 2026-11-27 | A plan with a `factory:exit` block is flagged (T17, in `planFlags`) | Exit commands run on the owner's machine, unattended once T19 allows them; removing the tokens does not protect the key files. Otherwise a plan update that only adds a block merges after the model review alone (second review F2) | Planner |
| 2026-11-27 | `declaredGuardFiles` reads the plan from the base checkout only, pinned by a test in T7 | A pull request must not declare its own guard entries (second review F3) | Planner |
| 2026-11-27 | `exit-check.mjs` has three modes, `--list`, `--dry-run` and `--post` | Review F2: `--dry-run` is defined (runs, posts nothing) and tested in T17 | Planner |

## Deviations from the specification

1. **No `spec.md`** for a change that is not an initial-build milestone (ADR-0023, ADR-0025). The change is to the factory and adds no behaviour to YAF; plans 0010, 0011 and 0014 took the same route.
2. **ADR-0029 §4 is amended** by ADR-0034 (T5): a guard file change and a new dependency are tier medium when the merged plan declares them exactly, and a plan that declares them is always flagged. Accepted by the owner on 2026-11-27 (Q4).
3. **The stacked pull request** of `yaf-commits-and-pull-requests` ("its pull request targets that branch until the first merges") is opened after the dependency merges instead (T21), as the owner answered Q2.
4. **Two lines of `AGENTS.md`'s overrides change** (T4, T21), as quoted in the Approach.

## Tickets

T1, T4, T5, T10, T14 and T20 can start at once. Every ticket is tier high (`docs/spec/**`, `.github/**`, `.claude/**` and `AGENTS.md` are sensitive and code-owned) and waits for the owner's approving review, except T18, a plan update a planner writes (tier low, but plan 0013 is flagged, so the owner approves it too). No ticket changes `.github/workflows/`, so the factory app pushes every branch. T8 stays: the owner answered Q3 yes. T22 can start once T7 has merged.

| Key | Title | Type | Area | Requirements | Depends on | Touches | Tier | Size | Failing test first | Verify |
|---|---|---|---|---|---|---|---|---|---|---|
| T1 | ci: throttle GitHub calls to its rate limits | chore | ci | Idea 5; research §1, §3; Approach, "Idea 5" | — | `docs/spec/factory/.github/factory/ratelimit.mjs`, `docs/spec/factory/.github/factory/tests/ratelimit.test.mjs` | high | 150 | In the new `tests/ratelimit.test.mjs`: `parseIncluded_ErrorOutput_GivesStatusHeadersAndBody` (the `gh api -i` text of research §3 gives 404, `x-ratelimit-reset` and the JSON); `waitFor_RetryAfter_WaitsThoseSeconds` (403 with `retry-after: 7` gives 7000 ms); `waitFor_RemainingZero_WaitsUntilTheReset`; `waitFor_SecondaryLimitWithoutHeaders_WaitsAMinuteThenDoubles`; `waitFor_NotARateLimit_IsNull` (a 404); `withRetry_BudgetUsedUp_ThrowsTheLastError`; `paced_TwoWrites_AreASecondApart` (fake clock and sleep) — all fail: the module does not exist | `node --test docs/spec/factory/.github/factory/tests/ratelimit.test.mjs` passes; V2; V1 |
| T2 | ci: find a plan's existing tickets in one listing of the issues | chore | ci | Idea 5; research §1–2; Approach, "Idea 5" | T1 | `docs/spec/factory/.github/factory/create-tickets.mjs`, `docs/spec/factory/.github/factory/tests/create-tickets.test.mjs` | high | 130 | In `tests/create-tickets.test.mjs`, with a fake `gh`: `createFromPlan_TwoOfThreeExist_ListsTheIssuesOnceAndSearchesNever` (one `repos/…/issues?state=all` page, no `search/issues`, one issue created for the third); `indexByMarker_PullRequestsAndOtherPlans_AreLeftOut`; `createFromPlan_RateLimitBudgetUsedUp_NamesTheNextTicketAndTheDispatch` (the error has `T3` and `gh workflow run factory-tickets.yml -f plan=`) — fail: `createFromPlan` is not exported and searches per ticket | `node --test docs/spec/factory/.github/factory/tests/create-tickets.test.mjs` passes; `grep -c "search/issues" docs/spec/factory/.github/factory/create-tickets.mjs` prints 0; V2; V1 |
| T3 | ci: throttle the Project calls to the rate limits | chore | ci | Idea 5; research §1–2; Approach, "Idea 5" | T1 | `docs/spec/factory/.github/factory/project.mjs`, `docs/spec/factory/.github/factory/tests/project.test.mjs` | high | 80 | In `tests/project.test.mjs`: `setStatus_GraphQLRateLimitOnce_WaitsForTheResetAndRetries` (a fake `gh` that throws "GraphQL: API rate limit exceeded" once, `rate_limit` with `graphql.remaining` 0 and a reset 30 s ahead: one wait of 30000 ms, then the edit); `setStatus_TwoTickets_WritesAreASecondApart` — fail: `setStatus` takes no `gh` and no clock, and does not retry. The five `addItem` tests still pass | `node --test docs/spec/factory/.github/factory/tests/project.test.mjs` passes; V2; V1 |
| T4 | chore: allow three implementers at once | chore | factory | Idea 7; ADR-0029 §6; Approach, "Three implementers" | — | `docs/spec/factory/.github/factory/policy.json`, `AGENTS.md` | high | 4 | Check first: `node -p "require('./docs/spec/factory/.github/factory/policy.json').limits.parallelImplementers"` prints 2, and `grep -c "At most two implementers at once" AGENTS.md` prints 1 | The `node -p` prints 3; `grep -c "At most three implementers at once" AGENTS.md` prints 1; `grep -c "At most two implementers" AGENTS.md` prints 0; V2; V1 |
| T5 | docs(adr): let a merged plan approve the guard changes it declares | docs | adr | Idea 2; ADR-0029 §4; Approach, "Idea 2" (with "A plan that declares them is always seen by the owner"); Q3, Q4 (accepted by the owner) | — | `docs/spec/adr/0034-guard-changes-declared-by-a-plan.md`, `docs/spec/adr/0029-software-factory.md` (status line only), `docs/spec/adr/README.md` | high | 120 | Check first: `test -f docs/spec/adr/0034-guard-changes-declared-by-a-plan.md` exits 1 | The file exists with the template's sections and names ADR-0029 §4, `.editorconfig`, `Directory.Packages.props` and `neverLowered`; `grep -c "0034" docs/spec/adr/README.md docs/spec/adr/0029-software-factory.md` prints at least 1 for each; V1 |
| T6 | ci: recognise the guard file changes a plan declared | chore | ci | Idea 2; ADR-0034 (T5); Approach, "The rule" | T5 | `docs/spec/factory/.github/factory/declared.mjs`, `docs/spec/factory/.github/factory/plan.mjs`, `docs/spec/factory/.github/factory/tests/declared.test.mjs` | high | 220 | In the new `tests/declared.test.mjs`, on the base and head texts of #388 and #389: `editorconfigVerdict_SectionsAsDeclared_AreDeclared` (#388's first seven sections); `editorconfigVerdict_NeverLoweredFile_IsNotDeclared` (the `Principal.cs` section, the reason names it); `editorconfigVerdict_ChangedBaseSection_IsNotDeclared`; `editorconfigVerdict_GlobSection_IsNotDeclared`; `packagesVerdict_PinsAsDeclared_AreDeclared` (#389); `packagesVerdict_OtherVersion_IsNotDeclared`; `packagesVerdict_PropertyAdded_IsNotDeclared`; `parseDelta_EditorconfigCategory_IsRead` — fail: the module does not exist and `parseDelta` throws "unknown categories: editorconfig" | `node --test docs/spec/factory/.github/factory/tests/declared.test.mjs` passes; V2; V1 |
| T7 | ci: classify declared guard changes as tier medium | chore | ci | Idea 2; ADR-0034; Approach, "The rule" (with "A plan that declares them is always seen by the owner"); Q4; review F1 | T4, T6 | `docs/spec/factory/.github/factory/classify.mjs`, `docs/spec/factory/.github/factory/gates.mjs`, `docs/spec/factory/.github/factory/guards.mjs`, `docs/spec/factory/.github/factory/policy.json`, `docs/spec/factory/.github/factory/policy.schema.json`, `docs/spec/factory/.github/factory/tests/classify.test.mjs`, `docs/spec/factory/.github/factory/tests/gates.test.mjs`, `docs/spec/factory/.github/factory/tests/delta.test.mjs` | high | 175 | In `tests/delta.test.mjs`: `planFlags_EditorconfigEntry_IsFlagged` (a plan whose only declaration is one `editorconfig` entry and whose tickets are all medium is flagged, with the policy file of the repository) — fails: `editorconfig` is not in `delta.flaggingCategories`. In `tests/classify.test.mjs`: `classify_DeclaredPackagePins_IsMedium` (`Directory.Packages.props` in `declaredFiles`, title `build:` gives medium with the reason "guard change declared by the plan"); `classify_DeclaredFileAndAnotherSensitivePath_IsHigh`; `classify_UndeclaredPackageChange_IsHigh` (as today). In `tests/gates.test.mjs`: `guardVerdict_OnlyDeclaredFindings_Passes`; `declaredGuardFiles_DeclarationOnlyInTheHeadPlan_IsNone` (the pull request adds the `editorconfig` entry to its own copy of the plan and the section to `.editorconfig`; the base plan has no entry: no declared file, and the pull request stays tier high). `declaredGuardFiles` is the function of `gates.mjs` that takes the plan text read from the base checkout (as for `factory/delta`, lines 56–63) and the base and head texts of the two files — fail: `classify` takes no `declaredFiles`, `guardVerdict` counts every finding and `declaredGuardFiles` does not exist | `node --test docs/spec/factory/.github/factory/tests/classify.test.mjs docs/spec/factory/.github/factory/tests/gates.test.mjs` passes; `node -p "require('./docs/spec/factory/.github/factory/policy.json').declaredGuards.files.join()"` prints `.editorconfig,Directory.Packages.props`; `node -p "require('./docs/spec/factory/.github/factory/policy.json').delta.flaggingCategories.includes('editorconfig')"` prints `true`; V2; V1 |
| T8 | chore: leave declared editorconfig changes to the tier rules | chore | factory | Idea 2; Q3 (only if yes); `CODEOWNERS` header | T7 | `.github/CODEOWNERS`, `docs/spec/factory/.github/CODEOWNERS` | high | 4 | Check first: `grep -c "^\.editorconfig" .github/CODEOWNERS docs/spec/factory/.github/CODEOWNERS` prints 1 for each | The same `grep` prints 0 for each; the header comment of both files names `.editorconfig` beside `Directory.Packages.props` as sensitive but not code-owned; V1 |
| T9 | docs(agents): describe guard changes a plan declares | docs | agents | Idea 2; ADR-0034; Approach, "The rule" | T7 | `docs/spec/agents/skills/yaf-writing-plans/SKILL.md`, `docs/spec/agents/skills/yaf-writing-plans/references/plan-template.md`, `docs/spec/agents/skills/yaf-commits-and-pull-requests/SKILL.md`, `docs/spec/factory/README.md` | high | 60 | Check first: `grep -c "editorconfig" docs/spec/agents/skills/yaf-writing-plans/SKILL.md` prints 0 | `grep -q '"editorconfig"' docs/spec/agents/skills/yaf-writing-plans/references/plan-template.md && grep -q "Id@Version" docs/spec/agents/skills/yaf-writing-plans/SKILL.md && grep -q "declared by the plan" docs/spec/agents/skills/yaf-commits-and-pull-requests/SKILL.md && grep -q "ADR-0034" docs/spec/factory/README.md` exits 0; V3; V1 |
| T10 | ci: report touches that overlap without a dependency | chore | ci | Idea 1; research §5; Approach, "Idea 1" (row 1) | — | `docs/spec/factory/.github/factory/plan-deps.mjs`, `docs/spec/factory/.github/factory/tests/plan-deps.test.mjs` | high | 140 | In the new `tests/plan-deps.test.mjs`: `overlaps_SameFileNoDependency_IsAProblem` (round 1 of plan 0013: T23 and T27 both touch `YAF.slnx`); `overlaps_FolderAndFileInside_IsAProblem`; `overlaps_GlobAndMatchedFile_IsAProblem`; `overlaps_OneMigrationsFolder_IsAProblem`; `overlaps_TransitiveDependency_IsNone`; `ancestors_Chain_IsTransitive` — fail: the module does not exist | `node --test docs/spec/factory/.github/factory/tests/plan-deps.test.mjs` passes; V2; V1 |
| T11 | ci: report a ticket that uses what a non-dependency creates | chore | ci | Idea 1; research §5; Approach, "Idea 1" (row 2) | T10 | `docs/spec/factory/.github/factory/plan-deps.mjs`, `docs/spec/factory/.github/factory/tests/plan-deps.test.mjs` | high | 150 | In `tests/plan-deps.test.mjs`: `uses_TypeCreatedByALaterTicket_IsAProblem` (T3's failing test says "`Foo` does not exist", T2 names `Foo`); `uses_NewFileOfANonDependency_IsAProblem` (T1 touches a new `src/X/Bar.cs`, T2 names `Bar`); `uses_FileOnTheBase_IsNone`; `uses_Ancestor_IsNone`; `uses_DocsTicket_IsANote`; `uses_NewAdr_IsMatchedAsAdrNumber`; `uses_RequirementsCell_IsNotRead` — fail: `uses` does not exist | `node --test docs/spec/factory/.github/factory/tests/plan-deps.test.mjs` passes; V2; V1 |
| T12 | ci: report delta entries of the wrong form | chore | ci | Idea 1; research §5 (round 3 F1); Approach, "Idea 1" (row 3) | T6 | `docs/spec/factory/.github/factory/plan-forms.mjs`, `docs/spec/factory/.github/factory/tests/plan-forms.test.mjs` | high | 120 | In the new `tests/plan-forms.test.mjs`: `deltaForms_NamespacePrefixWithoutStaticReadonly_NamesTheMissingForms` (round 3 of plan 0013: `YAF.Application.*` without `static readonly YAF.Application.*` and six others); `deltaForms_AllSixteenForms_IsNone`; `deltaForms_ErrorCodeInUpperCase_IsAProblem`; `deltaForms_ConfigurationKeyWithADot_IsAProblem`; `deltaForms_DependencyWithVersion_IsAccepted`; `deltaForms_EditorconfigEntryWithoutSection_IsAProblem` — fail: the module does not exist | `node --test docs/spec/factory/.github/factory/tests/plan-forms.test.mjs` passes; V2; V1 |
| T13 | ci: report a declared tier below what the touches give | chore | ci | Idea 1; research §5; Approach, "Idea 1" (row 4); `policy.declaredGuards` (T7) | T7 | `docs/spec/factory/.github/factory/plan-tier.mjs`, `docs/spec/factory/.github/factory/tests/plan-tier.test.mjs` | high | 130 | In the new `tests/plan-tier.test.mjs`: `tierFloor_ErrorsApprovedDeclaredLow_IsAProblem` (round 1 of plan 0013, T64); `tierFloor_MigrationsFolder_IsHigh`; `tierFloor_NewPublicApiFiles_StayLow` (T7 of plan 0013); `tierFloor_DeclaredAboveTheFloor_IsANote` (round 8, T77); `tierFloor_EditorconfigWithDeclaredEntries_IsMedium`; `tierFloor_FolderWithASensitiveSubfolder_IsANote`; `tierFloor_FeatTitleOutsideLowPaths_IsMedium` — fail: the module does not exist | `node --test docs/spec/factory/.github/factory/tests/plan-tier.test.mjs` passes; V2; V1 |
| T14 | ci: report ticket references that do not match the table | chore | ci | Idea 1; research §5 (round 2 F4); Approach, "Idea 1" (row 5) | — | `docs/spec/factory/.github/factory/plan-refs.mjs`, `docs/spec/factory/.github/factory/tests/plan-refs.test.mjs` | high | 110 | In the new `tests/plan-refs.test.mjs`: `refs_UnknownKeyInTestStrategy_IsAProblem`; `refs_MergedKeyRemoved_IsAProblem` (the base version has T5, the head not); `refs_UnknownKeyInProse_IsANote`; `refs_KeyRetitledSinceThePreviousCommit_IsANoteWithItsLines` (research cites T44, whose title changed); `refs_ReferencesInCodeSpans_AreRead` — fail: the module does not exist | `node --test docs/spec/factory/.github/factory/tests/plan-refs.test.mjs` passes; V2; V1 |
| T15 | ci: run every plan check through one script | chore | ci | Idea 1; Approach, "Idea 1"; Q1 | T7, T10, T11, T12, T13, T14 | `docs/spec/factory/.github/factory/plan-check.mjs`, `docs/spec/factory/.github/factory/gates.mjs`, `docs/spec/factory/.github/factory/tests/plan-check.test.mjs` | high | 140 | In the new `tests/plan-check.test.mjs`: `planCheck_OverlapAndLowTier_GivesBothProblems` (a small plan with both); `planCheck_CleanPlan_HasNoProblems`; `planCheck_Notes_DoNotFail`; `cli_PlanWithProblems_ExitsOne` (runs the command on a fixture) — fail: the module does not exist | `node --test docs/spec/factory/.github/factory/tests/plan-check.test.mjs` passes; V4; V2; V1 |
| T16 | docs(agents): let the plan review rely on the plan gate | docs | agents | Idea 1; Approach, "Idea 1" (last paragraph) | T9, T15 | `docs/spec/factory/.github/factory/prompts/doc-review.md`, `docs/spec/agents/skills/yaf-reviewing-specs-and-plans/SKILL.md`, `docs/spec/agents/skills/yaf-writing-plans/SKILL.md` | high | 40 | Check first: `grep -c "plan-check.mjs" docs/spec/agents/skills/yaf-writing-plans/SKILL.md docs/spec/factory/.github/factory/prompts/doc-review.md` prints 0 for each | `grep -q "plan-check.mjs" docs/spec/agents/skills/yaf-writing-plans/SKILL.md && grep -q "factory/plan-gate" docs/spec/factory/.github/factory/prompts/doc-review.md && grep -q "plan-gate" docs/spec/agents/skills/yaf-reviewing-specs-and-plans/SKILL.md` exits 0; V3; V1 |
| T17 | ci: post a milestone's exit checks on its epic | chore | ci | Idea 6; research §3; Approach, "Idea 6" (with "A plan with a `factory:exit` block is always flagged"); fix round 2 F2 | T2, T6 | `docs/spec/factory/.github/factory/exit-check.mjs`, `docs/spec/factory/.github/factory/plan.mjs`, `docs/spec/factory/.github/factory/tests/exit-check.test.mjs` | high | 320 | In the new `tests/exit-check.test.mjs`, with fake `exec`, `git` and `gh`: `planFlags_ExitBlock_IsFlagged` (a plan whose only flag would be a `factory:exit` block, with no tier-high ticket and an empty delta, is flagged with the reason "declares exit checks that run on the owner's machine"); `parseExitChecks_Block_GivesCriteriaAndChecks`; `parseExitChecks_CriterionNamesAnUnknownCheck_Throws`; `exitCheck_DirtyCheckoutOrNotOriginMain_Refuses`; `exitCheck_FailingCommand_RecordsExitTimeAndTail`; `exitCheck_ChecksRunWithoutTokens` (no `GH_TOKEN` or `GITHUB_TOKEN` in their environment); `exitComment_CriteriaChecksOpenTicketsAndCi_AreRendered` (marker `<!-- factory:exit-check <sha> -->`); `exitCheck_OpenTicket_ExitsOne`; `exitCheck_List_RunsNothing`; `exitCheck_DryRun_RunsTheChecksAndPostsNothing` (the comment is printed, no `gh` call writes); `exitCheck_TwoModes_Refuses` — fail: the module does not exist | `node --test docs/spec/factory/.github/factory/tests/exit-check.test.mjs` passes; V2; V1 |
| T18 | docs(specs): declare the exit checks of plan 0013 | docs | specs | Idea 6; `18` §2 row M3 (exit criteria); plan 0013, "Verification commands" ("The whole milestone") | T17 | `specs/0013-thin-slice/plan.md` | low | 40 | Check first: `grep -c "factory:exit" specs/0013-thin-slice/plan.md` prints 0 | `node docs/spec/factory/.github/factory/exit-check.mjs specs/0013-thin-slice/plan.md --list` exits 0 and lists the three exit criteria of row M3, each with at least one check; no other line of plan 0013 changes; V1 |
| T19 | docs(agents): run the exit checks at a milestone's end | docs | agents | Idea 6; Approach, "Idea 6"; `yaf-orchestrating-work` "The report at a milestone end" | T9, T16, T17 | `docs/spec/agents/skills/yaf-writing-plans/SKILL.md`, `docs/spec/agents/skills/yaf-writing-plans/references/plan-template.md`, `docs/spec/agents/skills/yaf-orchestrating-work/SKILL.md`, `docs/spec/factory/.claude/commands/factory-run.md`, `.claude/commands/factory-run.md`, `docs/spec/factory/README.md` | high | 50 | Check first: `grep -c "exit-check.mjs" docs/spec/agents/skills/yaf-orchestrating-work/SKILL.md docs/spec/factory/.claude/commands/factory-run.md` prints 0 for each | `grep -q "factory:exit" docs/spec/agents/skills/yaf-writing-plans/references/plan-template.md && grep -q "exit-check.mjs" docs/spec/agents/skills/yaf-orchestrating-work/SKILL.md && grep -q "exit-check.mjs \* --post" docs/spec/factory/.claude/commands/factory-run.md` exits 0; V3; V1 |
| T20 | ci: track the state of stacked worktrees | chore | ci | Idea 7; Approach, "Stacking" (steps 1–4, "How the gates and inflight.mjs see it"); Q2 (answered B); review F4 | — | `docs/spec/factory/.github/factory/inflight.mjs`, `docs/spec/factory/.github/factory/markers.mjs`, `docs/spec/factory/.github/factory/tests/inflight.test.mjs`, `docs/spec/factory/.github/factory/tests/markers.test.mjs` | high | 240 | In `tests/markers.test.mjs`: `stackedOn_NewestFactoryMarker_GivesTheCommitAndThePullRequest` (a marker by someone else does not count). In `tests/inflight.test.mjs`: `a reviewed stacked worktree whose dependency is open is stacked`; `a stacked worktree whose dependency merged is owed a restack onto its commit`; `a stacked worktree whose dependency closed unmerged is a human's`; `a stacked worktree without a local review gets one first`; `a restacked worktree follows the ordinary states`; `the command reads the stacked-on marker and the dependency's pull request`; `a fix round on a dependency waits while a ticket stacked on it is being written` (D's pull request gives `wait`, not `fix`, while the stacked worktree is locked) — fail: `stackedOn` does not exist and `worktreeState` has no state `stacked` | `node --test docs/spec/factory/.github/factory/tests/inflight.test.mjs docs/spec/factory/.github/factory/tests/markers.test.mjs` passes; V2; V1 |
| T21 | docs(agents): stack a ticket on an open dependency | docs | agents | Idea 7; Approach, "Stacking" (with step 2: D and the stacked ticket are never written at the same time); Q2 (answered B); review F4 | T4, T9, T19, T20 | `docs/spec/agents/skills/yaf-orchestrating-work/SKILL.md`, `docs/spec/agents/skills/yaf-commits-and-pull-requests/SKILL.md`, `docs/spec/factory/.claude/commands/factory-run.md`, `.claude/commands/factory-run.md`, `AGENTS.md` | high | 80 | Check first: `grep -c "Nothing is stacked" AGENTS.md` prints 1, and `grep -c "In an unattended run nothing is stacked" docs/spec/agents/skills/yaf-orchestrating-work/SKILL.md` prints 1 | Both `grep`s print 0; `grep -c "Stacking is one level deep" AGENTS.md` prints 1; `grep -q "restack" docs/spec/agents/skills/yaf-orchestrating-work/SKILL.md && grep -q "never written at the same time" docs/spec/agents/skills/yaf-orchestrating-work/SKILL.md && grep -q "rebase --onto origin/main" docs/spec/factory/.claude/commands/factory-run.md` exits 0; V3; V1 |
| T22 | ci: skip the Markdown under specs/ in the guard scan | chore | ci | Extra of fix round 1 (`factory/guards` failed on PR #394 and on plan 0013 for a quoted `.editorconfig` setting); decision log | T7 | `docs/spec/factory/.github/factory/guards.mjs`, `docs/spec/factory/.github/factory/tests/gates.test.mjs` | high | 30 | In `tests/gates.test.mjs`: `findGuardChanges_SeverityLineInAPlan_IsNoFinding` (an added line of `specs/0015-x/plan.md` that quotes an `.editorconfig` setting which turns an analyzer off gives no finding) — fails: `findGuardChanges` scans every file. Also `findGuardChanges_SeverityLineInEditorconfig_IsAFinding` and `findGuardChanges_PragmaInADocsArticle_IsAFinding` (`docs/**` is still scanned), which pass before and after | `node --test docs/spec/factory/.github/factory/tests/gates.test.mjs` passes; V2; V1 |
