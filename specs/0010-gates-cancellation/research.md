# 0010 — Gates without cancellation: research

What the plan relies on about GitHub Actions and about this repository, with sources. Everything below was read on 2026-11-26.

## 1. Concurrency groups: pending runs, `cancel-in-progress`, `queue`

Sources:

- "Control the concurrency of workflows and jobs", <https://docs.github.com/en/actions/how-tos/write-workflows/choose-when-workflows-run/control-workflow-concurrency>
- "Workflow syntax for GitHub Actions", `concurrency`, <https://docs.github.com/en/actions/reference/workflows-and-actions/workflow-syntax>
- Changelog "GitHub Actions concurrency groups now allow larger queues", 2026-05-07, <https://github.blog/changelog/2026-05-07-github-actions-concurrency-groups-now-allow-larger-queues>

What they say:

1. **A pending run is cancelled by default, whatever `cancel-in-progress` says.** "By default, any existing `pending` job or workflow in the same concurrency group will be canceled", and the new one takes its place. `cancel-in-progress: true` *also* cancels the run that is already running. So `cancel-in-progress: false` alone still cancels: with one run going and two more arriving, the first of the two waiting runs is cancelled.
2. **`queue`.** `single` (the default): "At most one job or workflow run can be `pending` in the concurrency group." `max`: "Up to 100 jobs or workflow runs can be `pending` in the concurrency group." When the queue is full, further runs are cancelled. The changelog: queuing "can be enabled by adding `queue: max` to the concurrency block in YAML when `cancel-in-progress` is `false` or not set". The syntax reference: "The combination of `queue: max` and `cancel-in-progress: true` is not allowed", a validation error.
3. **Order.** Waiting runs are processed first in, first out, but "since the actual start time of a job or run may vary, ordering is not guaranteed".
4. **`cancel-in-progress` as an expression** is allowed ("you can specify `cancel-in-progress` as an expression with any of the allowed expression contexts"); the example is `cancel-in-progress: ${{ !contains(github.ref, 'release/')}}`. The documentation does not say whether `queue: max` together with an expression that evaluates to `true` for some events is rejected when the file is validated, accepted, or rejected only for those runs.
5. **Not documented:** whether a run that is cancelled while it is still pending in the group leaves cancelled check runs for its jobs on the head commit. The plan does not depend on the answer: with `queue: max` no run of `factory-gates` is cancelled (items 1 and 2).

## 2. Events

Source: "Events that trigger workflows", <https://docs.github.com/en/actions/reference/workflows-and-actions/events-that-trigger-workflows>

- For `pull_request`, `GITHUB_REF` is `refs/pull/<number>/merge` and `GITHUB_SHA` the last merge commit of that ref. A pull request therefore runs the version of a workflow that it carries; `AGENTS.md` ("Unattended runs") says the same. T1's own pull request is the first run of the changed file.
- "Other `GITHUB_TOKEN`-triggered events do not create workflow runs at all" (the exceptions are `workflow_dispatch` and `repository_dispatch`). The `tier:*` label that the `gates` job sets with `github.token` starts no new run.
- The page does not say whether `gh pr create --label a --label b` sends one `labeled` event per label. The factory opens its pull requests with the one label `factory`.

## 3. This repository

- **The required checks** (`gh api repos/wtf-corp/yaf-backend-framework/rules/branches/main`, read 2026-11-26), strict policy off: `factory/tier`, `factory/ticket`, `factory/size`, `factory/delta`, `factory/guards`, `factory/plan-gate`, `factory/human-approval`, `references`, `build-test`, `pr-title`, `security`, `process` (GitHub Actions, integration 15368), and `factory/review`, `factory/doc-review` (the reviewer app, 5837737).
- **`factory-gates.yml`** runs on `pull_request` (`opened`, `reopened`, `synchronize`, `edited`, `labeled`, `unlabeled`, `ready_for_review`), `pull_request_review` (`submitted`, `dismissed`) and `merge_group`, in group `factory-gates-<pr number or sha>` with `cancel-in-progress: true`. Its job `references` is a required check under its job name; the `factory/*` checks are check runs that `gates.mjs` posts through the checks API. `factory-self-test`, `factory/test-evidence` and `gates` are not required.
- **What the `gates` job writes** (`docs/spec/factory/.github/factory/gates.mjs`, `lib.mjs`): it reads the pull request, its labels, timeline and reviews fresh from the API, not from the event payload (`gates.mjs` line 31). It removes a `tier:*` label that it set itself and that no longer applies, adds the computed `tier:*` label when missing, and posts completed check runs on the current head (`setCheck`, a `POST` of a new check run; "the newest of a name counts", `lib.mjs`). Nothing else. Run again on the same state, it writes the same labels and the same check results.
- **How long a run takes** (`gh run list --workflow factory-gates.yml --limit 30`, 2026-11-25): 17 to 39 s from creation to completion, whole run, every job included. One outlier is the re-run of 45706051634 on #267.
- **Observed cancellations** in the same list: run 45703575612 (#266, cancelled after 2 s by its same-second twin), run 45706051591/45706051634 (#267: the pair of issue #265), and run 45691174967, a `pull_request_review` run on head `c46f2f6` cancelled after 30 s by a `pull_request` run on the same head. So a cancelled run on the current head is not limited to `gh pr create --label`; any second event on the same head while a run is going leaves one.
- **`ci.yml`** runs on `pull_request` (`opened`, `synchronize`, `reopened`, `edited`), `push` to `main` and `next`, and `merge_group`, in group `ci-${{ github.ref }}` with `cancel-in-progress: true`. Its cancelled runs in the last 40 (`gh run list --workflow ci.yml --limit 40`, 2026-11-26) are all `push` runs on `main`, where nothing is required for a merge.
- **The other workflows.** `factory-pr-review.yml` and `factory-doc-review.yml` cancel in progress but do not start on `labeled`, their job is off (`FACTORY_REVIEW_IN_CI`) and their job checks `review` and `doc-review` are not required. `factory-status.yml` (issues) and `factory-tickets.yml` (push) report no pull request check and use `cancel-in-progress: false`. `factory-automerge.yml` (on `workflow_run` of `factory-gates`) and `factory-digest.yml` (schedule) have no concurrency group and report no required check.
- **The check in V3 of the plan** was tried on #267 (head `4febba5b1fcc68fe481824e7f18115ff9b14d1e0`) on 2026-11-26: `gh run list --workflow factory-gates.yml --commit <full sha> --json conclusion,attempt` lists two runs, both `success`, one at attempt 2 (the owner's re-run), so the plan's expression prints `false` there, as it should. `--commit` needs the full SHA; with a short one the list is empty.
- **Documentation of the concurrency.** A search of `docs/spec/` and `AGENTS.md` for `factory-gates`, `concurrency` and `cancel` finds no text that describes the concurrency of `factory-gates.yml`: `17` §2.2 and ADR-0029 list its triggers and checks, the kit README its checks. `17` §2 shows only `ci.yml`'s group.
