# 0011 — Gate results that the ruleset sees: plan

**Spec** none: this plan has no `spec.md`. Its basis is issue #278, its comment on #279, and the owner's goal for the factory: "i would really like the factory to be able to work on its own, so lets focus on that, as otherwise it does not make sense to use it in its current state" (decision log). No section of `docs/spec/spec/` says how the gate results are posted; the kit README names this fix as the fallback (research §2) · **Status** Draft

## Approach

**The problem (issue #278).** Six factory pull requests (#212, #259, #262, #273, #276, #279) stayed `BLOCKED` with automatic merge armed and every required check green in the API, and the owner merged them as admin. The merge box listed the seven checks that `gates.mjs` posts (`factory/tier`, `factory/ticket`, `factory/size`, `factory/guards`, `factory/delta`, `factory/plan-gate`, `factory/human-approval`) as "Expected — Waiting for status to be reported". Since #273, every opening of a factory pull request (`opened` and `labeled`) runs `factory-gates` twice to the end, so the fault now has its trigger on every factory pull request.

**The cause.** Not the number of check runs per name, as #278 supposed, but **where they land**:

1. `setCheck` in `lib.mjs` creates a check run with the workflow token. GitHub files it in the **oldest GitHub Actions check suite** of the commit, whichever workflow run posted it: 17 heads out of 17 (research §1, item 1).
2. Which workflow owns the oldest suite is chance: the suites of one opening are created within one to three seconds (research §1, item 4). Sometimes it is `ci.yml` or `factory-pr-review.yml`, which run once per head. Sometimes it is a `factory-gates` run.
3. When it is a `factory-gates` run and a **later** `factory-gates` run exists on the same commit, the `factory/*` check runs are not seen by the merge evaluation, although the API lists them as `success` from app 15368. The six admin-merged pull requests are exactly the six with that pattern; none of the eleven that merged by themselves has it. Five that merged by themselves had every name posted twice (research §1, items 2 and 3).
4. A public write-up of the same fault in another repository gives the mechanism: the pull request "only shows the checks of the latest run of each workflow", so a check run filed under an older run "disappears as soon as that workflow runs again on the same commit" (research §2). GitHub's documentation does not describe it; the observation in item 3 does not depend on it.

Every path the owner listed starts a later `factory-gates` run on the same commit: the second event of the opening, a re-run, a label change, a close and reopen, and the `pull_request_review` run after an approval. Each one can hide the gate checks whenever the first gates run's suite was the oldest.

**The fix.** `gates.mjs` reports the seven gate results as **commit statuses** instead of check runs: `POST repos/{repo}/statuses/{sha}` with `context` the check name, `state` `success` or `failure`, `description` the title, `target_url` the workflow run. A commit status belongs to no check suite, so no later run can hide it, and the latest status of a context is the one that counts (research §2, REST "Commit statuses"). The required checks keep their names, and the ruleset keeps its entries: a required status check "can be checks or statuses", and a status created with the workflow token satisfies a check pinned to GitHub Actions (research §2, the write-up; verified on the first pull request after the change, V3). This is the switch that the kit README already names for the case "an older run keeps blocking" (research §2).

In detail:

- **T1, the readers first.** `automerge.mjs` (`planGateConclusion`) and `inflight.mjs` (`newestChecks`) also read the commit statuses of the head, from `bots.gates` only, next to the check runs. For a name that has both, the one that has not passed decides, because GitHub requires both to pass (research §2, "Troubleshooting"). Without T1, T2 would leave the orchestrator reading "the gates have not reported" on every pull request, and a flagged plan would never be armed for merge. T1 is safe alone: today no status exists, so nothing changes.
- **T2, the switch.** `lib.mjs` gets `setStatus` (and a pure `statusBody` that builds the request, for the test). `gates.mjs` calls it for the seven gate checks, on `merge_group` too, and keeps `setCheck` for the "not applicable" review check (`factory/review` or `factory/doc-review`), which the ruleset takes from the reviewer app anyway. The markdown summary of each check, which a status cannot hold, goes into the job summary. The `gates` job of `factory-gates.yml` (installed copy and kit source) gets `statuses: write`, with a comment. The kit README's two "Verify before relying on it" bullets on check runs are replaced by what #278 found.
- **What stays.** `setCheck` and the reviewer app's check runs (`post-review.mjs`, `local-review.mjs`): they sit in the reviewer app's own suite and merge fine with two runs per name (research §3). The job checks (`references`, `build-test`, `pr-title`, `security`, `process`): each is in its own run's suite, and since #273 the latest run of a pull request is also the one that ran last. The concurrency, the triggers, the ruleset, the policy.

**Why both tickets are needed before it works, and in this order.** `gates.mjs` runs from the base branch, the workflow file from the pull request (research §3; plan 0010, research §2). T2's own pull request is therefore still judged by the old `gates.mjs` with check runs, and the fix takes effect from the first pull request opened after T2 merges. T1 and T2 may themselves need an admin merge, like every factory pull request today. T2 changes `.github/workflows/`: **the owner pushes its branch** (`AGENTS.md`, "Unattended runs").

Files read to decide: `AGENTS.md` (path map, "Running the factory locally", "Unattended runs", "Model reviews run locally", "Token economy", "Changing the bundle"), `docs/spec/agents/AGENTS.root.md`, `docs/spec/agents/AGENTS.nested.md` (`specs/`), `docs/spec/factory/.claude/agents/planner.md`, the skills `yaf-writing-plans` (with its template), `yaf-commits-and-pull-requests` and `yaf-security`, issues #278 and #265 with their comments, `specs/0010-gates-cancellation/plan.md` and `research.md`, `.github/workflows/factory-gates.yml` and its kit source, `.github/workflows/factory-automerge.yml`, `docs/spec/factory/.github/factory/gates.mjs`, `lib.mjs`, `automerge.mjs`, `inflight.mjs`, `plan.mjs`, `policy.json`, the tests `lib.test.mjs`, `gates.test.mjs`, `automerge.test.mjs`, `inflight.test.mjs`, `.github/scripts/local-review.mjs`, `docs/spec/factory/README.md` (§5, "Verify before relying on it"), `17` §2.2, the ruleset of `main`, and the check runs, check suites and gates runs of the 17 heads in research §1.

### Alternatives rejected

| Alternative | Why not |
|---|---|
| A: `setCheck` finds the existing check run of the name on the head and updates it (`PATCH`) | The updated run stays in the suite it was first filed in. On #279 both gates runs' check runs were filed in the first run's suite, which the second run superseded: with A the second run would update check runs that are hidden already. On #212 and #262 two runs per name were hidden; on #213 and #263 two per name merged (research §1). The count is not the cause, so removing the duplicates does not cure it |
| C: run `factory-gates` once per opened pull request (drop or guard the `labeled` trigger) | It removes one later run, not the others: a re-run, `unlabeled` or `labeled` after opening, a reopen, and the `pull_request_review` run that re-posts `factory/human-approval` after an approval all start a later gates run on the same commit. Whenever the first run's suite is the oldest, any of them hides the gate checks. It also changes when the gates see a label, which is a guard. C alone does not meet the goal; with the fix it is not needed |
| D: one job per gate check in `factory-gates.yml`, named like the check, fed from the `gates` job's outputs | No ruleset question, but seven more jobs per run, a larger workflow change, and a two-step change because the workflow comes from the pull request and the script from the base. Whether a `pull_request_review` run supersedes a `pull_request` run's suite is not settled (research §4, item 4), so a job check could still read an older verdict. Kept as the fallback if V3 shows that the ruleset does not accept the statuses (decision log, Q2) |
| E: post the gate checks with a separate GitHub App, whose check runs sit in that app's own suite (as the reviewer app's do) | A new app, a private key in CI and a ruleset change to its integration id, all owner work, for the same effect as statuses |
| Switch `setCheck` itself to statuses, as the kit README words it | `setCheck` is also the reviewer app's poster (`post-review.mjs`, `local-review.mjs`). Its check runs work, the ruleset pins the two reviews to that app, and the app may lack the statuses permission. A separate `setStatus` changes only the gates |
| Also post the "not applicable" review check as a status | The ruleset takes `factory/review` and `factory/doc-review` from the reviewer app, which posts its own "not applicable" (`local-review.mjs`); a status of the same name from GitHub Actions has not been tried, and "both must pass" (research §2) makes it a risk with no gain |
| Ask GitHub support, keep the admin merge | The owner wants the factory to work without a human; the cause is known and has a fix in our hands |
| An ADR | `adr/AGENTS.md` rule 3: no dependency, package, contract or process changes. The checks, their names, the tiers and the ruleset stay; only the way the gates report changes, and the kit README already names it |

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
| The factory kit and CI (not a layer) | `docs/spec/factory/.github/factory/automerge.mjs`, `inflight.mjs` and their tests | Read the gates' commit statuses next to their check runs (T1) |
| The factory kit and CI (not a layer) | `docs/spec/factory/.github/factory/lib.mjs`, `gates.mjs`, `tests/lib.test.mjs`, `docs/spec/factory/README.md`, `.github/workflows/factory-gates.yml`, `docs/spec/factory/.github/workflows/factory-gates.yml` | Post the seven gate checks as commit statuses; `statuses: write` on the `gates` job (T2) |

## Public API, schema and error-code delta

None. No public API, schema, error code, contract, configuration key, telemetry name or dependency changes. The required check names stay as they are.

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
| `planGateConclusion` takes a `factory/plan-gate` status from `bots.gates` as the result; a status from anybody else is ignored; with a check run and a status of the name, a failure in either is a failure | T1 | unit | `tests/automerge.test.mjs` |
| `automerge` arms a flagged plan pull request in tier high whose `factory/plan-gate` is a successful status from the gates bot, with an approval on the head | T1 | unit (faked `gh`) | `tests/automerge.test.mjs` |
| `pullRequestRow` reads the gate checks from statuses of `bots.gates`: green statuses are not "the gates have not reported", a failed one is `gate-failed` with its description as the reason, a status from another creator is ignored | T1 | unit | `tests/inflight.test.mjs` |
| `statusBody` builds `{ context, state, description, target_url }`: `success` and `failure` map through, `description` is cut to 140 characters, no `target_url` when the run is unknown | T2 | unit | `tests/lib.test.mjs` |
| `gates.mjs` posts the seven gate checks with `setStatus`, also on `merge_group`, and keeps `setCheck` only for the "not applicable" review check | T2 | file check | T2's verify, the `grep` part |
| The installed workflow and its kit source both give the `gates` job `statuses: write` | T2 | file check | T2's verify |
| On the first factory pull request after T2 the seven contexts are statuses from `github-actions[bot]`, no `factory/tier` check run is posted, and the pull request merges with no human after its review, although two gates runs ran on its head | T2 | CI, on a real pull request | V3 |
| A tier-high factory pull request merges after the owner's approval, with no admin merge; a re-run of a gates run does not block a pull request | T2 | CI, on a real pull request | V4 |

The failing tests first are named in the Tickets table.

## Risks and rollback

| Risk | Likelihood | Mitigation | Rollback |
|---|---|---|---|
| The ruleset does not accept a status from the workflow token for a check pinned to GitHub Actions, so every pull request after T2 waits for the seven checks | Low: one public source says it does (research §2); not verified here | V3 on the first pull request after T2. It fails closed: nothing merges that should not | Revert T2 (an admin merge). Then the owner chooses (decision log, Q2) |
| A pull request open when T2 merges has a visible `factory/*` check run that failed, and gets a successful status of the same name: both must pass, so it stays blocked | Low: only pull requests open across the switch | The orchestrator merges T2 when no other factory pull request is open; otherwise a push to the affected pull request, or the owner's admin merge | none needed |
| `statuses: write` lets the `gates` job post any commit status | It already holds `checks: write`, which lets it post any check run, the required ones included; the job runs the base branch's script only (decision log, Q1) | The job keeps reading the pull request as data and executing nothing from it | Revert T2 |
| The markdown summary of a check (tier reasons, undeclared delta lines, guard findings) is no longer on the check | Certain | It goes into the job summary, which `target_url` links to | — |
| More than 1000 statuses per context on one commit | Not seen: a run posts one per context | — | — |
| When the reviews move back to CI, `post-review.mjs` posts `factory/review` with the workflow token and meets the same fault | Later | Out of scope; T2 records it in the kit README bullet, so the move back changes the reviewers to `setStatus` too | — |

## Verification commands

Git Bash, repository root, on the ticket's branch; `BASE` is `origin/main`; `K=docs/spec/factory/.github/factory`.

V1: `git diff --name-only $BASE...HEAD` lists only the ticket's `touches`; `node $K/size.mjs $BASE HEAD` stays at the estimate or below; `node .github/scripts/sync-references.mjs --check` and `node .github/scripts/validate-instructions.mjs` exit 0.

V2: `node --test "$K/tests/*.test.mjs"` passes, with the ticket's new tests among them.

V3, on the first factory pull request opened after T2 merged (with `--label factory` as usual), `<head>` the full SHA of its head commit, `<number>` the pull request:

    gh run list --workflow factory-gates.yml --commit <head> --json conclusion --jq 'length >= 2 and all(.conclusion == "success")'   # true
    gh api "repos/{owner}/{repo}/commits/<head>/statuses?per_page=100" --jq '[.[] | select(.context | startswith("factory/")) | .context] | unique | length'   # 7
    gh api "repos/{owner}/{repo}/commits/<head>/statuses?per_page=100" --jq '[.[] | select(.context | startswith("factory/")) | .creator.login] | unique'   # ["github-actions[bot]"]
    gh api "repos/{owner}/{repo}/commits/<head>/check-runs?check_name=factory/tier&filter=all" --jq .total_count   # 0
    gh pr view <number> --json mergedBy --jq .mergedBy.login   # app/yaf-factory, once its review has passed

V4, on the next tier-high factory pull request after T2: after the owner's approval it merges with `mergedBy.login` `app/yaf-factory`. When the owner re-runs a `factory-gates` run of a pull request before its review is posted (optional, any pull request), it still merges by itself.

The whole plan, on `main` after T2: V2; the next factory pull requests merge by themselves (tier low and medium) or after the owner's approval (tier high), and the orchestrator's stuck-merge procedure of #265 is not needed for them.

## Research

[`research.md`](./research.md): where the gate check runs landed on 17 pull requests and which of them stuck; what GitHub's documentation and a public write-up say about check runs, suites, commit statuses and required checks; who posts and who reads the gate checks here; what is not verified.

## Decision log

| Date | Decision | Reason | By |
|---|---|---|---|
| 2026-11-26 | Plan the fix of #278 so that a factory pull request merges with no human, after re-runs, label changes and, in tier high, an approval | "i would really like the factory to be able to work on its own, so lets focus on that, as otherwise it does not make sense to use it in its current state". Given to the orchestrator on 2026-11-26 and relayed to the planner | Human owner, 2026-11-26 |
| 2026-11-26 | The cause is the suite the check runs land in, not their number | Research §1: the six admin merges are exactly the heads whose host suite is a superseded `factory-gates` run; five heads with two runs per name merged by themselves | Planner |
| 2026-11-26 | Option B of #278, commit statuses, for the seven gate checks only; A and C rejected | Alternatives rejected | Planner |
| 2026-11-26 | Two tickets, readers (T1) before the switch (T2) | After T2 without T1 the orchestrator would see no gate results and a flagged plan would never be armed; T1 alone changes nothing today | Planner |
| 2026-11-26 | No ruleset change | Names and source stay; a status from the workflow token is expected to satisfy a check pinned to GitHub Actions (research §2), verified by V3 | Planner |
| 2026-11-26 | Folder `0011-gates-check-reporting` | Next free number | Planner |
| open | **Q1. `statuses: write` on the `gates` job.** Hard rule 1 counts a widened permission as a weakened guard, so this needs the owner's yes. What it adds: the job may post commit statuses; it may already post check runs of any name (`checks: write`), the required ones included, and it executes nothing from the pull request. Recommended: yes. The owner's approving review of this plan, which is flagged (tier-high tickets), records it; T2 is not started without it | — | Human owner |
| open | **Q2, only if V3 fails** because the ruleset does not take the statuses as GitHub Actions'. Options: (a) revert T2, then a plan update to option D, job checks, with no ruleset change (recommended); (b) set the source of the seven required checks in the ruleset to "any source", which lets anybody with push access, the factory app included if it may write statuses, post them: a weaker guard and a change only the owner makes | — | Human owner |

## Deviations from the specification

1. **No `spec.md`** for a change that is not an initial-build milestone (ADR-0023, ADR-0025). The change fixes a fault of the factory's CI on the owner's instruction and adds no behaviour to YAF; plans 0001 to 0004, 0007 and 0010 took the same route.

## Tickets

Two tickets, both tier high (`docs/spec/**` and `.github/**` are sensitive and code-owned), one after the other. **T2 changes `.github/workflows/`: the owner pushes its branch**, after reading its diff of `.github/workflows/`, and again after each fix round (`AGENTS.md`, "Unattended runs"). T2 starts only after the owner's yes to Q1 (the approving review of this plan) and merges when no other factory pull request is open (risks).

| Key | Title | Type | Area | Requirements | Depends on | Touches | Tier | Size | Failing test first | Verify |
|---|---|---|---|---|---|---|---|---|---|---|
| T1 | ci: read the gate results from commit statuses as well as check runs | chore | ci | Issue #278; Approach, "T1, the readers first"; research §2 ("both must pass"), §3 | — | `docs/spec/factory/.github/factory/automerge.mjs`, `docs/spec/factory/.github/factory/inflight.mjs`, `docs/spec/factory/.github/factory/tests/automerge.test.mjs`, `docs/spec/factory/.github/factory/tests/inflight.test.mjs` | high | 120 | In `automerge.test.mjs`: `planGateConclusion([], 'github-actions[bot]', [status('factory/plan-gate', 'success', 'github-actions[bot]')])` is `'success'` (on `main` it is `null`), and the command test arms a flagged plan whose plan gate is only a status. In `inflight.test.mjs`: a row with the gate checks only as successful statuses from `github-actions[bot]` does not give "the gates have not reported" (on `main` it does) | `node --test docs/spec/factory/.github/factory/tests/automerge.test.mjs docs/spec/factory/.github/factory/tests/inflight.test.mjs` passes, the new tests included; V2; V1 |
| T2 | ci: report the gate results as commit statuses | chore | ci | Issue #278; Approach, "The fix" and "T2, the switch"; research §1, §2; decision log Q1 | T1 | `docs/spec/factory/.github/factory/lib.mjs`, `docs/spec/factory/.github/factory/gates.mjs`, `docs/spec/factory/.github/factory/tests/lib.test.mjs`, `docs/spec/factory/README.md`, `.github/workflows/factory-gates.yml`, `docs/spec/factory/.github/workflows/factory-gates.yml` | high | 90 | In `lib.test.mjs`: `statusBody('factory/tier', 'failure', 'x'.repeat(300), 'https://example.test/run')` is `{ context: 'factory/tier', state: 'failure', description: <140 characters>, target_url: 'https://example.test/run' }`; on `main` the import of `statusBody` fails. **The owner pushes this branch** | V2; `grep -cE "setCheck\(sha, 'factory/(tier\|ticket\|size\|guards\|delta\|plan-gate\|human-approval)'" docs/spec/factory/.github/factory/gates.mjs` prints 0, `grep -c "setStatus(" docs/spec/factory/.github/factory/gates.mjs` prints at least 8 (the seven names and the `merge_group` loop), and the `merge_group` loop calls `setCheck` only for `factory/review` and `factory/doc-review`; `grep -c '^      statuses: write' .github/workflows/factory-gates.yml docs/spec/factory/.github/workflows/factory-gates.yml` prints 1 for each; V1; after the merge, V3 and V4 |
