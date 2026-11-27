# The factory kit

Ready-to-copy files that implement the software factory of ADR-0029 with Claude Code and GitHub Actions: agent definitions for each role, the gate workflows, the scripts and policy they run on, and the templates.

**None of this has been run against a real repository.** The scripts pass their own unit tests (`node --test`) and every script parses; the workflows, the setup script and the agent definitions are written from documentation and have never executed. Treat the first milestone as the kit's acceptance test (see "Install at M0", step 7) and read "Verify before relying on it".

The rules the kit implements are tool-neutral and live in the ADRs (0023–0029), `AGENTS.md` and the skills. This kit is one implementation of them. Another agent vendor can take a role by producing the same outputs (see "Tuning the policy").

## What is in it

```
factory/
├── README.md
├── .claude/
│   ├── settings.json                 permission deny rules for local agents
│   ├── commands/factory-run.md       one tick of an unattended run: /loop /factory-run
│   └── agents/                       spec-writer, spec-reviewer, planner, plan-reviewer,
│                                     implementer (worktree), code-reviewer, pr-reviewer
└── .github/
    ├── CODEOWNERS
    ├── pull_request_template.md
    ├── ISSUE_TEMPLATE/ticket.yml, epic.yml
    ├── workflows/
    │   ├── factory-gates.yml         tier, ticket, size, delta, guards, plan gate, human approval; skills mirror; test evidence
    │   ├── factory-pr-review.yml     the pull-request reviewer agent → check factory/review
    │   ├── factory-doc-review.yml    the spec / plan reviewer agent → check factory/doc-review
    │   ├── factory-automerge.yml     enables auto-merge: tier low and medium, tier high once approved; takes it off an unapproved tier high
    │   ├── factory-tickets.yml       creates issues from a merged plan
    │   ├── factory-status.yml        Project status: Done, Ready
    │   └── factory-digest.yml        daily digest
    └── factory/
        ├── policy.json, policy.schema.json
        ├── gates.mjs                 entry point of the gates job
        ├── classify.mjs, size.mjs, delta.mjs, guards.mjs, ticket.mjs, human-approval.mjs, plan.mjs
        ├── scope.mjs, post-review.mjs, review.schema.json, doc-review.schema.json
        ├── create-tickets.mjs, project.mjs, status.mjs, digest.mjs
        ├── sync-skills.mjs, test-evidence.sh, lib.mjs
        ├── labels.json, setup-github.sh
        ├── prompts/pr-review.md, prompts/doc-review.md
        └── tests/*.test.mjs
```

Files beyond the layout in the brief, added because the design needed them: `gates.mjs`, `plan.mjs`, `scope.mjs`, `post-review.mjs`, the two verdict schemas, `project.mjs`, `status.mjs`, `digest.mjs`, `sync-skills.mjs`, `test-evidence.sh`, `lib.mjs`, `tests/`.

Scripts are Node 22 ES modules with no dependencies. They call GitHub through the `gh` CLI, which the hosted runners have.

## How a ticket flows

1. **Spec.** The `spec-writer` drafts `specs/NNNN-slug/spec.md`; the `spec-reviewer` checks it locally; a pull request is opened. In CI `factory/doc-review` repeats the review, and CODEOWNERS demands a human. (Initial build: skipped; `specs/0000-framework/` is the approved spec.)
2. **Plan.** The `planner` writes `plan.md`, ending with the `factory:delta` block and the Tickets table; the `plan-reviewer` checks it; pull request. `factory/plan-gate` passes without a human unless the plan declares public API, a schema change, a new dependency, an `editorconfig` entry, a deviation, or a tier-high ticket; then it waits for a human maintainer's approving review.
3. **Tickets.** When the plan reaches `main`, `factory-tickets.yml` creates an epic issue and one issue per row of the Tickets table, each with a hidden marker (so a re-run creates nothing twice), a `Blocked by:` line, labels, and a place in the Project: `Ready` when nothing blocks it, else `Backlog`.
4. **Implement.** The orchestrator picks `Ready` tickets (at most `limits.parallelImplementers`, none depending on another, no overlap in `touches` or `sharedFiles`), sets each `In progress`, and spawns one `implementer` per ticket, each in its own worktree. The implementer writes the failing test first, then the code, runs the verification, pushes its branch and reports.
5. **Local review.** The orchestrator re-runs the verification itself, then spawns a `code-reviewer` with only the ticket, the spec sections and the diff. It runs the tests, including without the change. Findings go back to an implementer; at most `limits.fixRounds` rounds, then `needs-human`.
6. **Pull request.** The orchestrator opens it (`Closes #n`, `Plan: …#Tn`) and sets the ticket `In review`.
7. **Gates.** `factory-gates.yml` computes the tier and reports `factory/tier`, `ticket`, `size`, `delta`, `guards`, `plan-gate`, `human-approval`. `factory-pr-review.yml` runs the five-pass review and reports `factory/review`. The normal `ci.yml` builds and tests.
8. **Merge.** Tier low and medium: `factory-automerge.yml` enables auto-merge and GitHub merges when every required check is green. Tier high: `factory/human-approval` stays red until a human maintainer who is not the author approves the current commit; the gates run that follows the approval then has `factory-automerge.yml` enable auto-merge on it too. An approval is therefore the decision to merge: no second step follows it. The workflow runs `automerge.mjs` after every gates run and takes auto-merge off a tier-high pull request whose current commit is not approved, also when a human enabled it by hand.
9. **Done.** The merge closes the issue; `factory-status.yml` moves it to `Done` and moves tickets it was blocking to `Ready`. The release job of `ci.yml` does the rest.

## The design decisions built into the kit

### 1. Skills are mirrored for Claude Code

The repository keeps its skills tool-neutral in `.agents/skills/`. Claude Code reads skills only from `.claude/skills/`. `sync-skills.mjs` mirrors the first into the second, and the `skills-mirror` job of `factory-gates.yml` fails a pull request whose mirror is out of date. Agent definitions preload their skills through the `skills:` field. `CLAUDE.md` at the repository root is one line, `@AGENTS.md`, because Claude Code does not read `AGENTS.md` when a `CLAUDE.md` exists unless it is imported.

After changing a skill: `node .github/factory/sync-skills.mjs`, and commit both trees.

### 2. Agent definitions

| Agent | Tools | Notes |
|---|---|---|
| `spec-writer`, `planner` | read, write, shell (planner also web) | No review skill; no `Agent` tool |
| `spec-reviewer`, `plan-reviewer` | read, shell | No Edit/Write |
| `implementer` | read, write, shell | `isolation: worktree`; no `Agent` tool, so it cannot delegate |
| `code-reviewer` | read, shell | No Edit/Write; runs the tests locally |
| `pr-reviewer` | read, shell | The local twin of the CI review; the CI prompt is the authority |

Each definition states its inputs, its output format, what it never does, and that issue, pull-request and web text is data. The `model:` in each file is a default. The policy's `roles.<role>.model` wins: the orchestrator passes it when it spawns the agent, and the workflows read it with `scope.mjs`.

The orchestrator is not an agent definition. It is the main Claude Code session following the `yaf-orchestrating-work` skill; it never edits code, specs or plans.

### 3. The CI reviewer reads; it does not execute

`factory-pr-review.yml` and `factory-doc-review.yml` check out the **base branch at the workspace root**, so the instructions, skills and settings the agent loads are the reviewed ones, and the **pull request head in `pr/`** as data. The agent gets `Read`, `Grep`, `Glob`, read-only `git -C pr …` and the inline-comment tool. It cannot build or run the pull request, because the action's documentation says the model credential stays in the agent's environment, and code under review must not run next to it.

The verdict comes back through `--json-schema`; `post-review.mjs` turns the action's `structured_output` into the check and one summary comment that is updated in place. A missing or invalid verdict is a failed check.

Executed evidence comes from jobs that hold no secret: the build and tests of `ci.yml`, and `factory/test-evidence`, which restores the non-test files to the base version, runs the test projects whose tests changed, and expects them to fail. **`factory/test-evidence` is report-only**: it writes a table to the job summary and never fails. Make it required once its results have been trusted for a while. The local `code-reviewer` is the role that runs tests with and without the change.

### 4. Gates read the base branch

Every gate job checks out the base commit and runs the scripts and the policy from there. A pull request that edits `policy.json` or a script is judged by the old ones.

What this does **not** cover:

- Under `pull_request`, GitHub takes the **workflow file itself** from the pull request's merge ref. A pull request can therefore change what a gate workflow does in its own run. The kit's answer: everything under `.github/**` is a sensitive path (tier high) and code-owned, so such a pull request cannot merge without a human, and a human sees the workflow diff. It does not stop the changed workflow from running once.
- For that reason the jobs that hold credentials worth stealing do not run on `pull_request` at all: the factory app's key is used only on `workflow_run`, `push` to `main` and `issues` events, which run the default branch's workflow. The one secret a pull-request-triggered job sees is the model API key in environment `factory-review`. Give that key a spending limit, and consider requiring a reviewer on the environment for pull requests that touch `.github/**`.
- The stronger control is **organisation-level required workflows** (rulesets that run a workflow from a fixed repository and ref), where the plan offers them. Then the pull request cannot alter the gate at all. Use them if available.

### 5. Checks, not approvals

No agent ever submits an approving review. Agents report check runs. The required checks are listed in `policy.json` (`checks`) and must each be marked required in the ruleset. Because a required check must report on every pull request, `gates.mjs` reports "not applicable" for the review that does not apply to a kind (`factory/review` on spec and plan pull requests, `factory/doc-review` on code).

### 6. The tier is a floor

`classify.mjs` computes the tier from the diff, the title, the size, the delta and the guards. A `tier:*` label applied by anyone other than the gates job counts as a request to **raise**; nothing lowers the tier below the computed floor. `needs-human` raises to high. A dependency **patch** update (only `Directory.Packages.props` lines moving to a higher patch version, and lock files) is exempt from the sensitive-path rule so it can be tier low; `Directory.Packages.props` is therefore in the policy's sensitive paths but **not** in CODEOWNERS.

A guard change the plan declared is tier medium (ADR-0034). For a file in `policy.declaredGuards.files` (`.editorconfig`, `Directory.Packages.props`), `gates.mjs` `declaredGuardFiles` reads the `factory:delta` block of the plan named by the `Plan:` line **from the base branch**, so a pull request that edits the plan declares nothing. A file counts only when that plan has an entry for it (`editorconfig`, or a `dependencies` entry `Id@Version`) and `declared.mjs` finds the change exact: for `.editorconfig`, the lines before the first section and every base section unchanged, and every added line a declared setting in a new section whose header names one file (a `/`, no glob, no `..` segment) outside `declaredGuards.neverLowered`, matched without regard to case; for `Directory.Packages.props`, existing pins and the rest of the file unchanged and each new `PackageVersion` exactly `Include` and `Version` as declared. `classify.mjs` then gives that file the reason "guard change declared by the plan" instead of high, and `guards.mjs` reports its findings without counting them, so no `guard-change` label is needed. Any other reason for high keeps the pull request high. Both categories are in `delta.flaggingCategories`, so such a plan always waits for the owner.

### 7. The delta is a subset check

A plan declares the delta of all its tickets; one pull request implements one ticket. `factory/delta` therefore checks that every line a pull request **adds** to an approved file (`PublicAPI.*.txt`, `errors.approved.txt`, and the other sources in the policy) matches an entry of the plan's block, exactly or by a `*` prefix. A **removed** line is never covered: it makes the pull request tier high. Whether the plan's whole declaration was delivered is a question for the milestone's exit check, not for a gate.

The block in `plan.md`:

````markdown
```factory:delta
{ "publicApi": ["YAF.Core.ITypedId<TSelf>*"], "schema": [], "errorCodes": ["core.ids.*"],
  "contracts": [], "configuration": [], "telemetry": [], "dependencies": [] }
```
````

The Tickets table (columns are matched by name; `Type` and `Area` are optional and default to `feat` and none):

```markdown
## Tickets

| Key | Title | Type | Area | Requirements | Depends on | Touches | Tier | Size | Failing test first | Verify |
|---|---|---|---|---|---|---|---|---|---|---|
| T1 | feat(core): typed id contract | feat | Core | 02 §2 | — | src/Core/Ids/** | medium | 120 | `TypedIdTests.New_is_v7` | `dotnet test tests/Core.Tests` |
```

### 8. Status without the dependency API

`create-tickets.mjs` also creates native sub-issue and "blocked by" links, for people; a failure there is a warning. The automation reads the `Blocked by: #1, #2` line in the issue body, so it does not depend on those APIs.

## Install at M0

Done by hand, by the human owner with an agent assisting. The factory does not install its own gates.

1. Copy the contents of `factory/` (not the folder) to the repository root, keeping paths. `README.md` of the kit goes to `.github/factory/README.md`.
2. Make `CLAUDE.md` one line: `@AGENTS.md`.
3. Mirror the skills: `node .github/factory/sync-skills.mjs`. Commit `.claude/skills/`.
4. Edit `.github/factory/policy.json`: `humans.logins` (or `humans.team`), `bots.factory`, and check `sensitivePaths` against the real tree. Edit `CODEOWNERS`: replace `@yaf/maintainers`.
5. Run the script tests: `node --test ".github/factory/tests/*.test.mjs"`.
6. Do the human setup checklist below.
7. **Prove the gates before any other milestone**, with throwaway pull requests:
   - a docs change → tier low, merges by itself;
   - a `feat` with a public API line declared in a merged plan → tier medium, merges by itself; the same with an undeclared line → `factory/delta` fails and the tier is high;
   - a change under `src/Http/Authentication/` → tier high, waits; a bot "approval" does not satisfy `factory/human-approval`; yours does;
   - a change over the hard limit (400 changed hand-written lines by default; this repository uses 1000, with a soft limit of 400, during the initial build, until `1.0.0`) → `factory/size` fails; `size-accepted` applied by the app does not help, applied by you it does (and the tier stays high);
   - `tier:low` put on a tier-high pull request → still high;
   - a pull request that edits `policy.json` to shrink `sensitivePaths` and touches a sensitive path → still judged by the base policy;
   - `FACTORY_PAUSED=true` → nothing auto-merges.

## Human setup checklist

`setup-github.sh` creates the labels, the Project `YAF factory` with the Status options Backlog / Ready / In progress / In review / Done, and the variables `FACTORY_PAUSED`, `FACTORY_PROJECT_OWNER`, `FACTORY_PROJECT_NUMBER`. Everything else is manual:

1. **Team.** A team of human maintainers only (no app, bot or machine user). Put its slug in CODEOWNERS and, with the logins, in `policy.json` `humans`. With `humans.logins` filled, that list alone decides who is a human maintainer; the team lookup is the fallback and needs a token that can read team membership, which the default workflow token cannot.
2. **GitHub App `yaf-factory`**, installed on this repository only. Permissions: Contents read and write, Pull requests read and write, Issues read and write, Projects read and write (organisation), Checks read, Metadata read. Not: Administration, Workflows, Secrets, Environments, Members. Store its id in variable `FACTORY_APP_ID`.
   - Agents authenticate locally with a token of this app (`gh auth` with an installation token), never with your login: you cannot approve your own pull request, and the audit trail must show who did what.
   - Do not add the app to the maintainers team or to any ruleset bypass list.
3. **Environment `factory-merge`**: secret `FACTORY_APP_PRIVATE_KEY`. Deployment branches: `main` only. Used by auto-merge, ticket creation and status, none of which runs on a pull request's own workflow run.
4. **Environment `factory-review`**: secret `ANTHROPIC_API_KEY`, a key created for this purpose with a spending limit.
5. **Rulesets for `main` and `next`:**
   - require a pull request; required approvals **0**; **require review from code owners**; dismiss stale approvals; the author cannot approve;
   - require status checks: every name in `policy.json` `checks`, plus the build, test, security and title jobs of `ci.yml`; require branches to be up to date or use the merge queue;
   - squash merge only; no force push; no deletion; **no bypass** for the app, for bots or for administrators (keep one break-glass route that is logged);
   - enable **auto-merge** in the repository settings;
   - enable the **merge queue** where the plan has it; `factory-gates.yml` answers `merge_group` by reporting every factory check as passed on the queue's commit, and `ci.yml` must run its build and tests on `merge_group` too.
6. **Protect the labels by process.** GitHub cannot restrict who applies a label. `guard-change`, `major-release` and `size-accepted` count only when the timeline shows a human maintainer applied them; the app applying them changes nothing.
7. **Local settings.** `.claude/settings.json` denies merging, approving, pushing to protected branches, the three labels, repository settings, secrets, tags and publishing. Prefix rules have documented gaps (for example `git -C . push origin main` is not matched by `Bash(git push origin main*)`); they stop accidents, not a determined agent. The controls that hold are the app token's scopes and the rulesets.

## Running the orchestrator

In a Claude Code session at the repository root, authenticated as the app, with Docker running:

```
Follow the yaf-orchestrating-work skill. Work the Ready tickets of epic #<n>. Stop at the end of the milestone or at the first needs-human.
```

That prompt is for a run you attend. An unattended run (`/loop /factory-run`) waits instead: at a `needs-human` or when only your decisions remain it tells you, picks up nothing and keeps checking until you have acted.

For an unattended run, start the same session in the default permission mode and type:

```
/loop /factory-run
```

The command `.claude/commands/factory-run.md` runs one tick of the skill's loop and schedules the next with `ScheduleWakeup`: about every 5 minutes while reviews, writers or checks are running, every 60 minutes when only your decisions or pushes remain, after a desktop notification that lists them. Its `allowed-tools` pre-approve the orchestrator's own commands for the turn the command starts, and only then. It defers to the skill and to `AGENTS.md` for the steps, the limits and the models. Press `Esc` while the loop waits to end it.

The skill is the instruction; in short the session reads the Project, checks `FACTORY_PAUSED`, picks tickets, and for each one: `node .github/factory/status.mjs set <issue> "In progress"` → spawn `implementer`, whose tooling gives it a worktree in which it creates the branch → re-run verification in that worktree → spawn `code-reviewer` → record the verdict on the ticket → open the pull request → `status.mjs set <issue> "In review"`. It reports at the end what merged, what waits for you and why.

`status.mjs` needs `GH_TOKEN` (the app's), `GITHUB_REPOSITORY`, `FACTORY_PROJECT_OWNER` and `FACTORY_PROJECT_NUMBER` in the environment.

To pause everything: set repository variable `FACTORY_PAUSED` to `true`. Auto-merge stops being enabled and the orchestrator picks up nothing; checks keep running. Pull requests that already have auto-merge enabled still merge when green: disable it on them by hand if you need a hard stop. The workflow does that itself for a tier-high pull request that is not approved, paused or not.

## Tuning the policy

`policy.json` is validated by `policy.schema.json`. A change to it is tier high and code-owned.

| Want | Change |
|---|---|
| Larger or smaller pull requests | `size.soft`, `size.hard`, `size.exclude` |
| More or fewer agents at once, more fix rounds | `limits.parallelImplementers`, `limits.fixRounds` |
| A path always reviewed by a human | add a glob to `sensitivePaths` (and to CODEOWNERS if a code owner must be the reviewer) |
| A change type to merge automatically | `tiers.low.types`, `tiers.medium.types` |
| Another approved file to count as delta | `delta.sources` |
| What makes a plan need a human | `delta.flaggingCategories` |
| Another model for a role | `roles.<role>.model` |
| A stronger implementer for harder tickets | `roles.implementer.modelByTier` (default: tier high on the stronger model, through agent `implementer-high`; keep its `model:` in step with the policy) |
| Another vendor for a role | `roles.<role>.agent`, **and** wire that agent in: the workflows fail on purpose with a message when the agent is not `claude`. Add a step that runs it with the same prompt file and puts JSON matching the verdict schema into `REVIEW_OUTPUT`; `post-review.mjs` is vendor-neutral. For a local role, the orchestrator skill must know how to start that agent |

The digest (`factory-digest.yml`, posted daily to the issue labelled `factory-digest`) gives the numbers to tune from: merges by tier, how many merged without a human click, raw size, review rounds that ended in "changes required", and what carries `needs-human`. It does not yet report time spent in each status.

## Verify before relying on it

Nothing here has been run. Confirm these first; each is a place where the kit assumes behaviour it could not test.

**GitHub**

- **The gate checks are commit statuses** (issue #278). A check run created with the workflow token was filed in the oldest GitHub Actions check suite of the commit; when that suite was a `factory-gates` run and a later `factory-gates` run existed on the same commit, the merge box showed the checks as "Expected" and the pull request stayed blocked although the API listed them as passed. `gates.mjs` therefore reports the seven gate checks with `setStatus` in `lib.mjs` (`POST /repos/{owner}/{repo}/statuses/{sha}`, permission `statuses: write`): a status belongs to no suite, and the latest status of a context counts. The markdown summary of each gate goes into the job summary. `setCheck` stays for the review checks, which the reviewer app posts in its own suite. When the reviews move back to CI, `post-review.mjs` posts with the workflow token and meets the same fault: switch it to `setStatus` too.
- **Statuses created with the workflow token** satisfy a required check of that name pinned to GitHub Actions in a ruleset, with creator `github-actions[bot]` (`bots.gates`, which `automerge.mjs` and `inflight.mjs` read them from). Confirm both on the first pull request after the switch.
- **Merge queue**: available on your plan; `merge_group` runs of `factory-gates.yml`; whether code-owner review and the queue interact as expected.
- **Auto-merge enabled by an app token** leads to a merge whose push to `main` starts `ci.yml` and its release job.
- **`workflow_run`** payload: `factory-automerge.yml` finds the pull request from the head commit through the API.
- **Projects**: `gh project` commands with an app installation token for an organisation project; the `updateProjectV2Field` GraphQL mutation with `singleSelectOptions` used by `setup-github.sh`; a user-owned (not organisation) project needs a different token. `gh project item-add` can fail with "Content already exists in this project" for an issue the Project already holds (seen when the Project's own workflow adds the issue at the same moment); `addItem` in `project.mjs` then looks the item up in the item list.
- **Sub-issues and issue dependencies REST endpoints** (`…/sub_issues`, `…/dependencies/blocked_by`) and their payloads. Failures are warnings.
- **Issue list**: `create-tickets.mjs` lists the repository's issues once (`repos/…/issues?state=all`, page by page) and finds the hidden HTML-comment markers in their bodies; no search, so no search rate limit or indexing lag.
- **Timeline API** returns `labeled` events with the actor and its `type` for pull requests.
- **Organisation required workflows / rulesets** on your plan (the stronger form of design decision 4).

**Claude Code and the action**

- `anthropics/claude-code-action@v1`: that `allowed_bots: yaf-factory` is the expected name form for the app; that passing `github_token` makes the action use it; that `--json-schema` in `claude_args` fills the `structured_output` output; the exact name of the inline-comment tool (`mcp__github_inline_comment__create_inline_comment`) and that it works with the base branch at the root and the pull request in `pr/`; that `Bash(git -C pr diff *)` is accepted as an allowed-tool pattern.
- That the action, run in a workspace whose root is the base branch, loads `AGENTS.md`/skills from there and nothing from `pr/`.
- Subagent frontmatter: `isolation: worktree`, `skills`, `disallowedTools` behave as documented in your Claude Code version; that a spawned subagent accepts a model override from the orchestrator (if not, the `model:` in the file is what runs, and the policy's `roles.*.model` for local roles must be kept in step by hand).
- Worktree subagents: dirty worktrees are kept until cleanup; several at once share one `.git`; Docker-based tests from three worktrees at a time fit the machine.
- The deny rules of `.claude/settings.json` apply to subagents, and their documented gaps.

**The kit's own logic**

- `guards.mjs` works on patterns. It will miss a weakened guard written in a way it does not know, and may flag a harmless line. The reviewers remain responsible.
- `delta.mjs` trusts the approved files as the record of the public surface. A public change that reaches no approved file is invisible to it; package validation and the public API analyzer in `ci.yml` are what force those files to change.
- `test-evidence.sh` treats a build error as "fails without the change", which is weak evidence, and runs whole test projects rather than only the changed tests.
- The Conventional Commit and "trivial" rules are reimplemented here; `ci.yml`'s title check stays the authority for titles.
- The YAML files were not parsed by a YAML tool in the environment where they were written.
