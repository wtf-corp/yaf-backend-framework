# 0010 — Gates without cancellation: plan

**Spec** none: this plan has no `spec.md`. Its basis is issue #265 (the cause found on pull request #267) and the owner's decision of 2026-11-26 to take option A of that issue (decision log). No section of `docs/spec/spec/` describes the concurrency of `factory-gates.yml` (research §3), so nothing there is amended · **Status** Draft

## Approach

**The problem (issue #265).** `gh pr create --label factory` sends `pull_request` `opened` and `labeled` in the same second, and each one starts `factory-gates`. The workflow has `concurrency: group: factory-gates-${{ github.event.pull_request.number || github.sha }}` with `cancel-in-progress: true`, so the second run cancels the first after about a second. Both check suites carry the same timestamp. On #267 at head `4febba5`, GitHub's `statusCheckRollup` marked the **cancelled** run's `references` (a required check, research §3) as the required one, the rollup was `FAILURE`, and the pull request stayed `BLOCKED` with every check green. #266 had the same pair and merged. Re-running the cancelled run unblocked #267. The run list shows the same kind of cancellation without `--label` too: a `pull_request_review` run on head `c46f2f6` was cancelled after 30 s by a `pull_request` run on the same head (research §3). Any cancelled `references` run on the current head is a chance of a stuck pull request.

**The fix.** In `factory-gates.yml`, and in its source in the kit, the concurrency block becomes:

```yaml
concurrency:
  group: factory-gates-${{ github.event.pull_request.number || github.sha }}
  cancel-in-progress: false
  queue: max
```

with a short comment above it saying why (issue #265). No run of `factory-gates` is cancelled any more: runs of one pull request wait in the group and run one after another. `cancel-in-progress: false` alone is not enough. GitHub cancels a *pending* run in the group whenever a newer one arrives, whatever `cancel-in-progress` says. Only `queue: max` lets up to 100 runs wait (research §1). Without it, a third event while one run is going and one is waiting still cancels a run on the head, for example a review submitted while the opening pair is being served.

**Why keep the group at all.** Removing the `concurrency` block would also stop every cancellation, but the runs of one pull request would then run side by side. The `gates` job reads the pull request, its labels and its reviews fresh from the API, and every check run it posts is a new one, of which the newest counts (research §3). Run one after another, the run that posts last is also the one that read last, so the checks show the newest state: a `needs-human` label, a raised tier, `size-accepted` or an approval that arrives while a run is going is seen by the run after it. Run side by side, a run that started first can post last with an older view, and two runs can both try to remove the same stale `tier:*` label, so that one fails on the second removal. Queuing costs little: a whole run takes 17 to 39 s (research §3), so a run that waits behind another starts at most about half a minute later.

**What it costs.** A push no longer cancels the run of the previous head. That run completes, its job checks land on the old head, and the run of the new head waits for it. Its `factory/*` check runs are posted on the head the API returns, which is the new one; they are then overwritten by the new head's own run. This is how a run that had already posted before being cancelled behaved before. It costs some Actions minutes and up to half a minute of latency per push.

**What does not change.** The triggers, the jobs, the ruleset, the policy, `gates.mjs` and `factory-automerge.yml`. `factory-automerge` already runs once per completed `factory-gates` run, a cancelled one included; it now sees completed runs only. Nothing in `docs/spec/spec/`, the ADRs, the kit README or `AGENTS.md` describes this concurrency (research §3), so no other text needs a line; the comment in the workflow carries the reason.

**Who pushes.** The ticket changes `.github/workflows/`. The factory app has no Workflows permission, so **the owner pushes the branch**, after reading its diff of `.github/workflows/`, and again after each fix round (`yaf-orchestrating-work` rule 17; `AGENTS.md`, "Unattended runs"). The pull request runs its own version of the workflow (research §2), so its own opening is the first test of the fix.

### Other workflows

Only a workflow that cancels runs on the head of an open pull request and owns a required check can block a merge this way. The required checks are listed in research §3.

| Workflow | Starts on `opened` and `labeled`? | Cancels in progress? | Required check? | In this plan |
|---|---|---|---|---|
| `factory-gates.yml` | yes | yes | `references`, and the `factory/*` checks posted by its `gates` job | **yes** |
| `ci.yml` | `opened`, not `labeled` | yes, group `ci-<ref>` | `build-test`, `pr-title`, `security`, `process` | no: `gh pr create --label` starts it once. Its cancelled runs in the last 40 are all pushes to `main`, which no merge waits for (research §3). An `edited` event while a run is going would cancel one on the head; nothing in the factory edits a pull request that soon after opening it. Decision log, open question 1 |
| `factory-pr-review.yml`, `factory-doc-review.yml` | `opened`, not `labeled` | yes | no: their job checks are `review` and `doc-review`, the required ones are `factory/review` and `factory/doc-review` from the reviewer app; their job is off while the reviews run locally | no |
| `factory-status.yml`, `factory-tickets.yml` | no (issue and push events) | no | no | no |
| `factory-automerge.yml`, `factory-digest.yml` | no | no group | no | no |

Files read to decide: `AGENTS.md` (with the path map, "Running the factory locally", "Token economy", "Changing the bundle"), `docs/spec/agents/AGENTS.root.md`, `docs/spec/agents/AGENTS.nested.md` (`specs/`, `adr/`), `specs/AGENTS.md`, `docs/spec/factory/.claude/agents/planner.md`, the skills `yaf-writing-plans` (with its template) and `yaf-commits-and-pull-requests`, `yaf-orchestrating-work` rule 17, issue #265 with its comments, `specs/0007-no-package-lock-files/plan.md`, `specs/0008-m1-maintenance/plan.md`, `.github/workflows/*.yml`, `docs/spec/factory/.github/workflows/factory-gates.yml`, `docs/spec/factory/.github/factory/gates.mjs`, `lib.mjs`, `plan.mjs`, `policy.json`, `docs/spec/factory/README.md`, `17` §2 and §2.2, ADR-0029, `.github/scripts/validate-instructions.mjs`, the ruleset of `main`, and the run lists of `factory-gates.yml` and `ci.yml`.

### Alternatives rejected

| Alternative | Why not |
|---|---|
| B: open pull requests without `--label` and add the label afterwards | The owner chose A (decision log). It still cancels one run on the head, only no longer in the same second, and it does nothing for the other same-head cancellations (research §3) |
| `cancel-in-progress: false` alone | A pending run is still cancelled when a newer one arrives (research §1) |
| Remove the `concurrency` block | No cancellation either, but runs of one pull request run side by side: the last check run posted may come from the run that read the older state, and two runs can race on removing a stale `tier:*` label (Approach) |
| `cancel-in-progress: ${{ github.event.action == 'synchronize' }}` | A push still cancels a run, and the documentation does not say whether that expression may be combined with `queue: max` (research §1, item 4). Without `queue: max`, pending runs are still cancelled. On `synchronize` the cancelled run is mostly the old head's, but not always: a `labeled` or review event on the new head that arrived just before the push is cancelled too. It saves at most half a minute per push |
| Job-level concurrency on the `gates` job only, so that `references`, `factory-self-test` and `factory/test-evidence` run side by side | More configuration for at most half a minute of waiting; runs are short (research §3) |
| Also changing `ci.yml` | It cannot meet the opening pair, and no merge has been seen to wait for one of its cancellations (Other workflows; open question 1) |
| A line in the kit README, `17` §2.2 or ADR-0029 | None of them describes the concurrency today; the comment in the workflow is where a reader of the block looks |
| An ADR | `adr/AGENTS.md` rule 3: no dependency, package, contract, security or privacy control or process changes; the gates and their checks stay as they are |

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
| CI and the factory kit (not a layer) | `.github/workflows/factory-gates.yml`, `docs/spec/factory/.github/workflows/factory-gates.yml` | The concurrency block and its comment (T1) |

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
| The installed workflow and its source have the same concurrency block: the group as before, `cancel-in-progress: false`, `queue: max` | T1 | file check | T1's verify, the `grep` and `diff` part |
| GitHub accepts the changed file, and opening a pull request with `--label factory` leaves no cancelled `factory-gates` run on its head | T1 | CI | T1's verify, the `gh run list` part, on T1's own pull request (it runs its own version, research §2) |
| `references` and the `factory/*` checks pass on T1's head, and the pull request is not held by a cancelled required check | T1 | CI | the checks of T1's pull request; `statusCheckRollup` has no `CANCELLED` entry |

A workflow file has no unit test here. The failing check first is a `grep` that fails on `main` (Tickets).

## Risks and rollback

| Risk | Likelihood | Mitigation | Rollback |
|---|---|---|---|
| GitHub rejects `queue` in the workflow file, so `factory-gates` does not run and its required checks never report | Low: documented on github.com since 2026-05-07 (research §1) | T1's own pull request runs the changed file before it merges; a rejected file shows there as a workflow error and missing checks, which fails closed | Revert T1 |
| A run of an old head posts `factory/*` checks on the new head after a push | Low: the run reads the head from the API and computes the checks for it; the new head's own run comes after it in the queue and posts again | Queuing: the last run to post is the last to read | Revert T1 |
| A stale run fails because the new head is not in its checkout | Low | It fails before it posts anything, and the new head's run follows; the same happens today when a run is not cancelled in time | none needed |
| More than 100 runs wait in one group | Not seen: a run takes 17 to 39 s | — | — |
| GitHub counts a stale or duplicate *successful* run instead of the newest | Not a blocker: both are successes | — | — |
| `ci.yml` cancels a run on a pull request head after an `edited` event | Low | Open question 1 | — |

## Verification commands

Git Bash, repository root, on the ticket's branch; `BASE` is `origin/main`.

V1: `git diff --name-only $BASE...HEAD` lists only the two workflow files; `node docs/spec/factory/.github/factory/size.mjs $BASE HEAD` stays at the estimate or below; `node .github/scripts/sync-references.mjs --check` and `node .github/scripts/validate-instructions.mjs` exit 0.

V2, the two concurrency blocks are the same and as planned:

    for f in .github/workflows/factory-gates.yml docs/spec/factory/.github/workflows/factory-gates.yml; do grep -c -e '^  cancel-in-progress: false$' -e '^  queue: max$' "$f"; done   # prints 2 twice
    ! grep -q 'cancel-in-progress: true' .github/workflows/factory-gates.yml docs/spec/factory/.github/workflows/factory-gates.yml
    diff <(sed -n '/^concurrency:/,/^jobs:/p' .github/workflows/factory-gates.yml) <(sed -n '/^concurrency:/,/^jobs:/p' docs/spec/factory/.github/workflows/factory-gates.yml)   # exits 0

V3, on T1's pull request after it was opened with `--label factory`, `<head>` the full SHA of its head commit:

    gh run list --workflow factory-gates.yml --commit <head> --json conclusion,attempt --jq 'length >= 2 and all(.conclusion == "success" and .attempt == 1)'   # true
    gh pr view <number> --json statusCheckRollup --jq '[.statusCheckRollup[] | select(.conclusion == "CANCELLED")] | length'   # 0

The whole plan, on `main` after T1: V2; the next factory pull requests opened with `--label factory` show no cancelled `factory-gates` run and merge without a re-run.

## Research

[`research.md`](./research.md): what GitHub's documentation says about pending runs, `cancel-in-progress`, `queue` and event-triggered runs, and what this repository has (required checks, workflow triggers and groups, what the `gates` job writes, run durations, observed cancellations).

## Decision log

| Date | Decision | Reason | By |
|---|---|---|---|
| 2026-11-26 | **Option A of issue #265:** stop the concurrency of `factory-gates` from cancelling a duplicate run, rather than B (open pull requests without `--label`) | Recommended on #265: a cancelled required run on the head can hold the pull request at `BLOCKED` (#267) | Owner, in the orchestrator's session |
| 2026-11-26 | Within A: keep the group, `cancel-in-progress: false` and `queue: max` | `false` alone still cancels pending runs; removing the group lets runs of one pull request post side by side with older views; the expression on `synchronize` still cancels and its combination with `queue: max` is not documented (Approach, research §1) | Planner |
| 2026-11-26 | The installed workflow and its source in `docs/spec/factory/` change together, in one ticket | The source is the kit's version of the same file; V2 keeps the two blocks equal | Planner |
| 2026-11-26 | No line in the kit README, `docs/spec/spec/`, the ADRs or `AGENTS.md`; a comment in the workflow | None of them describes this concurrency (research §3) | Planner |
| 2026-11-26 | `ci.yml` and the other workflows stay as they are | Other workflows, table | Planner |
| 2026-11-26 | Folder `0010-gates-cancellation` | Next free number | Planner |
| 2026-11-26 | **Open question 1, for the owner:** should `ci.yml` stop cancelling runs of a pull request on `edited` too? Today only a title or body edit while a run is going could leave a cancelled `build-test`, `pr-title`, `security` or `process` on the head; it has not been seen. Not planned; if wanted, it is a second ticket of the same shape | Out of the owner's decision, which names `factory-gates` | Open |
| 2026-11-26 | **Open question 2, for the owner:** accept that a push no longer cancels the old head's `factory-gates` run, which then completes, at up to about half a minute of latency per push and its Actions minutes | It is the cost of the fix (Approach, "What it costs") | Open |

## Deviations from the specification

1. **No `spec.md`** for a change that is not an initial-build milestone (ADR-0023, ADR-0025). The change fixes a fault of the factory's CI by the owner's decision and adds no behaviour to YAF; plans 0001 to 0004 and 0007 took the same route.

## Tickets

One ticket, tier high (`.github/workflows/` and `docs/spec/` are sensitive and code-owned). **T1 changes `.github/workflows/`: the owner pushes its branch**, after reading its diff of `.github/workflows/`, and again after each fix round (`yaf-orchestrating-work` rule 17). The orchestrator opens the pull request once the branch is on the remote, with `--label factory` as usual, so that the opening pair runs on the fix.

| Key | Title | Type | Area | Requirements | Depends on | Touches | Tier | Size | Failing test first | Verify |
|---|---|---|---|---|---|---|---|---|---|---|
| T1 | ci: queue the factory gates runs instead of cancelling them | chore | ci | Issue #265; decision log 2026-11-26; Approach, "The fix"; research §1 | — | `.github/workflows/factory-gates.yml`, `docs/spec/factory/.github/workflows/factory-gates.yml` | high | 10 | `grep -q '^  queue: max$' .github/workflows/factory-gates.yml` exits 1 and `grep -q 'cancel-in-progress: true' .github/workflows/factory-gates.yml docs/spec/factory/.github/workflows/factory-gates.yml` exits 0: the gates still cancel. **The owner pushes this branch** | V2 (both files print 2, no `cancel-in-progress: true`, the two blocks equal); V3 on the pull request (at least two `factory-gates` runs on the head, each succeeded at its first attempt; no `CANCELLED` in `statusCheckRollup`); V1 |
