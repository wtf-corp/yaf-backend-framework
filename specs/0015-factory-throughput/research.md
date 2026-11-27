# 0015 — Factory throughput: research

What the plan relies on that is not in this repository, and the measurements behind its choices. Checked on 2026-11-27.

## 1. GitHub's rate limits

Source: GitHub Docs, "Rate limits for the REST API" (https://docs.github.com/en/rest/using-the-rest-api/rate-limits-for-the-rest-api), "Best practices for using the REST API" (https://docs.github.com/en/rest/using-the-rest-api/best-practices-for-using-the-rest-api) and "REST API endpoints for search" (https://docs.github.com/en/rest/search/search), read on 2026-11-27.

| Limit | What the page says |
|---|---|
| Primary, app installation | "use the installation's minimum rate limit of 5,000 requests per hour" (more for over 20 repositories, at most 12,500) |
| Search | "For authenticated requests, you can make up to 30 requests per minute for all search endpoints" |
| Secondary, concurrency | "No more than 100 concurrent requests are allowed", shared by REST and GraphQL |
| Secondary, points | "No more than 900 points per minute are allowed for REST API endpoints"; "no more than 2,000 points per minute are allowed for the GraphQL API endpoint" |
| Secondary, content | "no more than 80 content-generating requests per minute" and "no more than 500 content-generating requests per hour" |
| Status codes | A primary or a secondary limit answers 403 or 429; a secondary one adds a message that says so |

What to do when limited, in the pages' words:

- Primary: "You should not retry your request until after the time specified by the `x-ratelimit-reset` header."
- Secondary, with `retry-after`: "you should not retry your request until after that many seconds has elapsed".
- Secondary, `x-ratelimit-remaining` is `0`: wait until `x-ratelimit-reset` (UTC epoch seconds).
- Otherwise: "wait for at least one minute before retrying", and on repeated failures "wait for an exponentially increasing amount of time between retries".
- "you should make requests serially instead of concurrently"; for many writes, "wait at least one second between each request".
- `GET /rate_limit`: "does not count against your primary rate limit, but it can count against your secondary rate limit".

**What this means for plan 0013's 93 tickets.** `create-tickets.mjs` calls `search/issues` once per ticket and once for the epic (`findByMarker`, line 46): 94 searches against a limit of 30 a minute. Run 45897939422, attempt 2 (2026-11-27 04:54Z), read T1 to T29 as existing (one search each, 0.3 to 0.4 s apart) and failed on the next search with "API rate limit exceeded for installation ID …" (HTTP 403), while core and GraphQL still had most of their hour (the owner's measurement: 4993 and 4486 left). A new ticket costs about five content-generating requests (the issue, the sub-issue link, its dependency links, the Project's `item-add` and `item-edit`), so the 29 tickets still missing need about 150, under the 500 an hour; a whole plan of 93 would not be.

## 2. The runs that failed

| Run | Plan | Where it stopped | Message |
|---|---|---|---|
| 45888843738 (2026-11-26 20:09Z) | 0009 (plan update #318) | `search/issues` for `0009-domain#T30` | "API rate limit exceeded for installation ID 183724843" (HTTP 403) |
| 45897939422 attempt 1 (2026-11-26 21:31Z) | 0013 | a `gh project` call after T64 | "GraphQL: API rate limit exceeded for installation ID 183724843." |
| 45897939422 attempt 2 (2026-11-27 04:54Z) | 0013 | `search/issues` after "T29 exists as #352" | as the first row (HTTP 403) |
| 45900760880 (2026-11-26 21:49Z) | 0014 | `gh project field-list` after "T1 → #392" | "GraphQL: API rate limit exceeded for installation ID 183724843." |

The GraphQL failures came minutes after a run that had just made several hundred Project calls (`item-add`, `item-edit`, `item-list --limit 2000` reads 100 items per request): a secondary limit, not the hourly budget. `gh project …` prints no response headers, so a retry after a GraphQL failure reads `GET /rate_limit` (`resources.graphql`) to choose between waiting for the reset and waiting a minute.

`gh run rerun` runs the workflow file and the scripts of the run's original commit. `factory-tickets.yml` already has `workflow_dispatch` with the input `plan`, and a dispatch runs the default branch's scripts, so the fix needs no change to the workflow.

## 3. `gh api --include` on an error

Checked with gh 2.102.0 on 2026-11-27: `gh api -i repos/<owner>/<repo>/issues/9999999` exits 1 and still writes the status line (`HTTP/2.0 404 Not Found`), the headers (`X-Ratelimit-Limit`, `X-Ratelimit-Remaining`, `X-Ratelimit-Reset`, `X-Ratelimit-Resource`, `X-Ratelimit-Used`) and the JSON body to standard output; standard error has `gh: Not Found (HTTP 404)`. Node's `execFileSync` puts both on the error it throws (`error.stdout`, `error.stderr`). So a call made with `-i` can read `retry-after` and `x-ratelimit-reset` on a failure. `--paginate` is not combined with `-i`: the listing of T2 asks for `page=1, 2, …` itself.

`gh run list` has `-c, --commit SHA` and `-w, --workflow` (gh 2.102.0 `--help`), which the exit check uses to find the `ci.yml` run of the commit it checked.

## 4. Retargeting a stacked pull request

GitHub changelog, "Pull request retargeting", 2020-05-19 (https://github.blog/changelog/2020-05-19-pull-request-retargeting): when the head branch of a merged pull request is deleted, open pull requests based on it are retargeted to its base instead of being closed. The current docs on changing the base (https://docs.github.com/en/pull-requests/collaborating-with-pull-requests/proposing-changes-to-your-work-with-pull-requests/changing-the-base-branch-of-a-pull-request) warn that "some commits may be removed from the timeline" and say nothing about automatic retargeting. Not verified: whether the retarget fires the `edited` event that `factory-gates.yml` listens to. Option B of Q2 does not depend on it.

`factory-gates.yml` runs for `pull_request` with `branches: [main, next]` only, so a pull request whose base is another ticket's branch gets no gates, no `factory-automerge` run and no review. This is the base of Q2.

## 5. Measurements on plan 0013

A scratch prototype of the checks of T10, T11 and T13 (not committed) was run on the seven reviewed versions of `specs/0013-thin-slice/plan.md` (commits ac97688d, 591ad3d7, 8773d1f8, 876cf49f, be5aabdf, b678d1aa, and the merged 50eec01), with the files of `main` before the plan (50eec01^) as the base.

| Check | Round 1 (ac97688d) | Merged plan |
|---|---|---|
| Overlapping touches with no dependency path (T10) | 7 pairs, all `YAF.slnx` with T23: the review's F1 (Major) | 0 |
| Declared tier below the computed floor (T13) | T64 (adds to `errors.approved.txt`): the review's F6 | 0 |
| Declared tier above the computed floor (T13, a note) | — | T72 (high; its note names a `Migrations/` folder the path does not); round 8: T77, the review's F4 (Nit) |
| A code ticket names a type or file created by a ticket outside its dependencies (T11) | 0 | 0 (one docs ticket, T93, names `SessionData` of T80: a note) |

Two details came from the first prototype: scanning the Requirements cell found spec citations (`` `03` §1 (`ResultFactory` signature) ``), not uses, and tokens taken from `.csproj` and `.md` names (`Application`, `AGENTS`) matched prose. T11 scans Title, Failing test first and Verify, and takes types only from new `.cs` files, from "`X` does not exist" in a failing test, and from new ADR files.

The 33 findings of the six rounds of PR #303 (`factory/doc-review` check runs on those commits), by what could have found them:

| Kind | Findings | Mechanical here |
|---|---|---|
| Touches overlap without a dependency | r1 F1 | T10 |
| Tier against the touches | r1 F6, r2 F6, r6 F4 | T13 (r2 F6 is a folder named to avoid `Migrations`: not mechanical) |
| `factory:delta` line forms | r3 F1 (`static readonly` missing) | T12 |
| Stale ticket references | r2 F4 (research cites renumbered keys) | T14, as a note |
| Missing dependency on behaviour or on prose | r1 F2, F5, r3 F2, r4 F2, F4, r5 F2, r6 F2 | no: the use is in prose or in a test name, not in a row's code spans |
| Titles with "and", estimates, test gaps, deviations, design | the other 20 | no |

So the checks take about one finding in six out of the review, two of the ten Majors among them. They remove the bookkeeping, not the review rounds by themselves.
