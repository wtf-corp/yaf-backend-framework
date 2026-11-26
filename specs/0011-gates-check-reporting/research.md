# 0011 — Gate results that the ruleset sees: research

What the plan relies on about GitHub and about this repository, with sources. Everything below was read or queried on 2026-11-26, read-only.

## 1. Where the `factory/*` check runs land, on 17 pull requests

Method: for each pull request's head commit, `gh api repos/{owner}/{repo}/commits/<head>/check-runs?filter=all&per_page=100` and `gh api repos/{owner}/{repo}/commits/<head>/check-suites?per_page=100`. A GitHub Actions check suite (app 15368) is named by the job check runs it holds: `gates` (factory-gates), `build-test` (ci), `review` (factory-pr-review), `doc-review` (factory-doc-review). "Host" is the suite that holds the `factory/*` check runs that `gates.mjs` posted. "Superseded" means a later `factory-gates` run exists on the same head commit than the one whose suite is the host.

| PR | Merged by | `factory/tier` runs on the head | Host suite | Host is the oldest Actions suite | Host superseded |
|---|---|---|---|---|---|
| #212 | owner (admin) | 2 | factory-gates | yes | **yes** |
| #213 | factory app | 2 | factory-pr-review | yes | no |
| #259 | owner (admin) | 3 | factory-gates | yes | **yes** |
| #260 | factory app | 2 | factory-pr-review | yes | no |
| #261 | factory app | 2 | factory-doc-review | yes | no |
| #262 | owner (admin) | 2 | factory-gates | yes | **yes** |
| #263 | factory app | 2 | factory-pr-review | yes | no |
| #266 | factory app | 1 | factory-pr-review | yes | no |
| #267 | factory app (after a re-run, #265) | 2 | factory-pr-review | yes | no |
| #271 | factory app | 1 | ci | yes | no |
| #272 | factory app | 1 | factory-pr-review | yes | no |
| #273 | owner (admin) | 3 | factory-gates | yes | **yes** |
| #274 | factory app | 1 | ci | yes | no |
| #275 | factory app | 1 | factory-pr-review | yes | no |
| #276 | owner (admin) | 3 | factory-gates | yes | **yes** |
| #277 | factory app | 1 | ci | yes | no |
| #279 | owner (admin) | 2 | factory-gates | yes | **yes** |

What this shows:

1. **Every** `factory/*` check run that `gates.mjs` posted, on all 17 heads, sits in the **oldest** GitHub Actions check suite of the commit (lowest suite id), whichever workflow run posted it. On #275 the gates run of suite 123846063645 posted them into suite 123846050028, the suite of `factory-pr-review.yml` (its `review` job was skipped).
2. **The six pull requests the owner merged as admin are exactly the six whose host suite belongs to a `factory-gates` run that a later `factory-gates` run on the same commit superseded.** None of the eleven that merged by themselves has that.
3. **The number of check runs per name does not separate them.** #213, #260, #261, #263 and #267 had every `factory/*` name posted twice on the head and merged by themselves.
4. Which workflow owns the oldest suite is chance: the suites of one push or opening are created within the same one to three seconds (for example all of #275's at 07:19:48Z, #279's gates suite at 09:08:46Z and the others at 09:08:49Z).

The `factory-gates` runs of the three latest admin-merged heads (`gh run list --workflow factory-gates.yml --commit <head>`):

- #279 (`8cb3c3c`): 45789252989 `pull_request` 09:08:46Z (its suite is the host), 45789258979 `pull_request` 09:08:49Z. Both succeeded; the second one exists because of #273 (queued, not cancelled).
- #273 (`ed70997`): 45772900232 `pull_request` 07:06:42Z (host), 45772901894 `pull_request` 07:06:43Z, 45781391369 `pull_request_review` 08:13:47Z.
- #276 (`deefc01`): 45775556547 `pull_request` 07:28:19Z (host; cancelled, then re-run by the owner as attempt 2 at 08:14), 45775558189 `pull_request` 07:28:20Z, 45785197904 `pull_request` 08:40:44Z (close and reopen). Re-running the host run did not unblock it: a later run had already been created.

## 2. GitHub documentation and other sources

- "Troubleshooting required status checks", <https://docs.github.com/en/pull-requests/collaborating-with-pull-requests/collaborating-on-repositories-with-code-quality-features/troubleshooting-required-status-checks>: "If a check and a commit status have the same name, both must pass when that name is required." It says nothing about check suites or about which of several check runs of one name counts.
- "Status checks" reference, <https://docs.github.com/en/pull-requests/reference/status-checks>: checks come from GitHub Apps, GitHub Actions included; commit statuses are the simpler kind; "GitHub Actions generates checks, not commit statuses, when workflows are run." Nothing on several runs of a workflow on one commit.
- "Available rules for rulesets", <https://docs.github.com/en/repositories/configuring-branches-and-merges-in-your-repository/managing-rulesets/available-rules-for-rulesets>, "Require status checks to pass": required status checks "can be checks or statuses". A source app can be chosen; the page names the `statuses:write` permission for it, and "If the status is set by any other person or integration, merging won't be allowed." It does not say in so many words whether a status created with the workflow token counts as coming from the GitHub Actions app.
- REST "Commit statuses", <https://docs.github.com/en/rest/commits/statuses>: `state` is one of `error`, `failure`, `pending`, `success`; `target_url`, `description` ("A short description of the status", no length stated), `context` (case-insensitive); "a limit of 1000 statuses per sha and context within a repository"; the combined status is success "if the latest status for all contexts is success"; the list of a reference's statuses is "in reverse chronological order. The first status in the list will be the latest one", 30 per page by default, at most 100; each item has `creator`, a simple user or null.
- "Workflow syntax", `permissions`, <https://docs.github.com/en/actions/reference/workflows-and-actions/workflow-syntax>: `statuses` is a permission key of the workflow token, next to `checks`.
- A public write-up of the same fault in another repository, <https://github.com/asepulveda37461sc/nightfall-required-check-test> (one author, not GitHub's documentation): a check run created through the API with `GITHUB_TOKEN` "cannot say which workflow run the result belongs to", so "GitHub files it under the oldest GitHub Actions run on the commit"; "The PR page only shows the checks of the latest run of each workflow", so the result "disappears as soon as that workflow runs again on the same commit"; "The API still reports the check as `success` … but the page shows 'Expected' and the PR is blocked." Its fix: "A commit status is not filed under any workflow run, so the PR always sees it", and "a status created with `GITHUB_TOKEN` satisfies a required check that is bound to the GitHub Actions app"; the token needs `statuses: write`. Its second fix is a job named like the check, so that the check run "always belongs to the PR's own run".
- The kit's own README, "Verify before relying on it" (`docs/spec/factory/README.md`): "The kit assumes the newest check run with a name decides a required check. If an older failed run keeps blocking, switch `setCheck` in `lib.mjs` to commit statuses (`POST /repos/{owner}/{repo}/statuses/{sha}`, permission `statuses: write`), which are last-write-wins by design."

## 3. This repository

- **The required checks** (`gh api repos/{owner}/{repo}/rules/branches/main`, 2026-11-26), strict policy off: `factory/tier`, `factory/ticket`, `factory/size`, `factory/delta`, `factory/guards`, `factory/plan-gate`, `factory/human-approval`, `references`, `build-test`, `pr-title`, `security`, `process` from integration 15368 (GitHub Actions); `factory/review`, `factory/doc-review` from 5837737 (the reviewer app).
- **Who posts what.** `gates.mjs` posts the seven gate checks above, plus `factory/doc-review` (code) or `factory/review` (spec, plan) as "not applicable", through `setCheck` in `lib.mjs`: a `POST repos/{repo}/check-runs` with `status: completed` each time. On `merge_group` it posts every name of `policy.checks`. The workflow token of the `gates` job has `contents: read`, `pull-requests: write`, `issues: write`, `checks: write`.
- **Other users of `setCheck`.** `post-review.mjs` (the review poster, run by `local-review.mjs` with the reviewer app's token, and by the CI review workflows that are switched off) and `local-review.mjs` itself (the "not applicable" review). Their check runs are the reviewer app's and sit in its own suite: on #271 to #279, `factory/review` was posted twice per head by the reviewer app (in progress, then completed) and those heads merged. `setCheck` therefore stays for them.
- **Who reads the gate checks.** `automerge.mjs` (`planGateConclusion`: the newest `factory/plan-gate` check run from `bots.gates`, only for a plan pull request in tier high) and `inflight.mjs` (`newestChecks`: the newest check run per name, from the app the ruleset accepts it from; `nextAction` reports "the gates have not reported" when the gate checks are missing). Nothing else in `docs/spec/factory/.github/factory/`, `.github/scripts/` or the workflows reads them (`grep` for `check-runs`, `check_runs`, `checkRuns`, `statusCheckRollup`).
- **Reading statuses with the factory app's token works.** `node .github/scripts/factory-token.mjs --app factory -- gh api repos/{owner}/{repo}/commits/<main head>/statuses --include` answered `200` with `[]` and `X-Accepted-Github-Permissions: statuses=read`. No commit status exists on the commits queried: the repository has used check runs only.
- **The gates bot's login** is `github-actions[bot]` (`policy.json` `bots.gates`); `gates.mjs` already recognises its own `tier:*` labels in the timeline by that login.
- **The kit tests** run with `node --test "docs/spec/factory/.github/factory/tests/*.test.mjs"`: 118 tests, all pass on `origin/main` `c023b72`.

## 4. Not verified

1. **That a commit status created with the workflow token satisfies a required check pinned to integration 15368.** Only the write-up in §2 says so; GitHub's documentation does not settle it, and this repository has never posted a status. The plan verifies it on the first pull request after T2 (plan, V3) and says what happens if it does not (plan, risks and decision log, Q2).
2. **The `creator.login` of such a status.** Expected `github-actions[bot]`, as for the labels the same token sets; V3 reads it.
3. **A length limit on `description`.** The REST page states none. T2 truncates to 140 characters as a precaution.
4. **Whether a `pull_request_review` run of `factory-gates` supersedes the suite of a `pull_request` run on the same commit.** No head in §1 separates the two, and statuses do not depend on it. It matters only for the job checks (`references`), which this plan does not change.
5. **How GitHub chooses which suite a check run created with the workflow token goes to.** Observed: the oldest Actions suite, 17 out of 17. Not documented by GitHub. The fix does not depend on it.
