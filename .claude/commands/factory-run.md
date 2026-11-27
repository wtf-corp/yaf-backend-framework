---
description: Run one tick of the unattended factory run (yaf-orchestrating-work) and schedule the next. Start it with /loop /factory-run.
allowed-tools:
  - Bash(gh variable get FACTORY_PAUSED)
  - Bash(node .github/scripts/factory-token.mjs --app factory -- node docs/spec/factory/.github/factory/inflight.mjs)
  - Bash(node .github/scripts/factory-token.mjs --app factory -- node docs/spec/factory/.github/factory/status.mjs set *)
  - Bash(node .github/scripts/factory-token.mjs --app factory -- gh project item-list *)
  - Bash(node .github/scripts/factory-token.mjs --app factory -- gh pr create *)
  - Bash(node .github/scripts/factory-token.mjs --app factory -- gh pr comment *)
  - Bash(node .github/scripts/factory-token.mjs --app factory -- gh issue comment *)
  - Bash(node .github/scripts/factory-token.mjs --app factory -- gh issue edit * --add-label needs-human)
  - Bash(node .github/scripts/factory-token.mjs --app factory -- gh issue edit * --add-label blocked)
  - Bash(node .github/scripts/factory-token.mjs --app factory -- gh issue edit * --remove-label blocked)
  - Bash(node .github/scripts/factory-token.mjs --app reviewer -- node .github/scripts/local-review.mjs *)
  - Bash(node .github/scripts/factory-token.mjs --app factory -- node docs/spec/factory/.github/factory/exit-check.mjs specs/*/plan.md --post)
  - Bash(git worktree unlock *)
  - Bash(git worktree remove *)
  - Bash(git branch -D *)
  - PowerShell(gh variable get FACTORY_PAUSED)
  - PowerShell(node .github/scripts/factory-token.mjs --app factory -- node docs/spec/factory/.github/factory/inflight.mjs)
  - PowerShell(node .github/scripts/factory-token.mjs --app factory -- node docs/spec/factory/.github/factory/status.mjs set *)
  - PowerShell(node .github/scripts/factory-token.mjs --app factory -- gh project item-list *)
  - PowerShell(node .github/scripts/factory-token.mjs --app factory -- gh pr create *)
  - PowerShell(node .github/scripts/factory-token.mjs --app factory -- gh pr comment *)
  - PowerShell(node .github/scripts/factory-token.mjs --app factory -- gh issue comment *)
  - PowerShell(node .github/scripts/factory-token.mjs --app factory -- gh issue edit * --add-label needs-human)
  - PowerShell(node .github/scripts/factory-token.mjs --app factory -- gh issue edit * --add-label blocked)
  - PowerShell(node .github/scripts/factory-token.mjs --app factory -- gh issue edit * --remove-label blocked)
  - PowerShell(node .github/scripts/factory-token.mjs --app reviewer -- node .github/scripts/local-review.mjs *)
  - PowerShell(node .github/scripts/factory-token.mjs --app factory -- node docs/spec/factory/.github/factory/exit-check.mjs specs/*/plan.md --post)
  - PowerShell(git worktree unlock *)
  - PowerShell(git worktree remove *)
  - PowerShell(git branch -D *)
---

You are the orchestrator of an **unattended** factory run. This command is one tick of it. The skill `yaf-orchestrating-work` is the instruction; this command adds only how a tick runs in Claude Code. `AGENTS.md` overrides the skill where it says so; follow it there. The commands above are pre-approved for the turn this command starts, and only for that turn.

Do these steps in order.

1. **Pause.** Load `yaf-orchestrating-work`. Read the pause with your own login (the app cannot read variables): `gh variable get FACTORY_PAUSED`. When it is `true`, tell the owner, report what is running, and end the loop (step 4, `stop: true`).

2. **In flight.** Run `node .github/scripts/factory-token.mjs --app factory -- node docs/spec/factory/.github/factory/inflight.mjs` and act on its output, the skill's step 2.
   - For each pull request:
     - `review` → start the local review in the background: `node .github/scripts/factory-token.mjs --app reviewer -- node .github/scripts/local-review.mjs <pr-number>`.
     - `fix` → resume the implementer of that pull request, or start a new one, with the review's summary comment verbatim and "round N of M". For the cause `references`: post the references-fix marker on the ticket first, then give the one instruction to regenerate the references and commit them.
     - `gate-failed` → a comment, and `needs-human` on the pull request and on the ticket.
     - `human` → collect it for the report, with its reason. A plan or spec pull request whose review failed is one of them.
   - Every pull request row `failed` with the error of one worktree row, on two passes in a row: `needs-human` on that worktree's ticket with the error.
   - For each worktree:
     - `remove` → remove it ("Worktrees" in the skill).
     - `human` → collect it for the report. A stacked worktree whose dependency closed unmerged gets `needs-human` on its ticket.
     - `local-review` → the skill's step 5, then the `factory:local-review` marker with the verdict, the round and the findings.
     - `local-fix` → unless `exhausted`, back to the implementer with the marker's findings verbatim, as fix round `round + 1`. When `exhausted`: label the ticket `needs-human` with the skill's summary comment (what was tried, what was observed, the options) and try nothing more.
     - `open-pr` → the skill's step 6.
     - `stacked` → nothing: it waits for its dependency's merge and takes no implementer slot.
     - `restack` → as the app: `node .github/scripts/factory-token.mjs --app factory -- git -C <worktree> fetch origin main`, then `node .github/scripts/factory-token.mjs --app factory -- git -C <worktree> rebase --onto origin/main <onto> <branch>`. On a conflict, `node .github/scripts/factory-token.mjs --app factory -- git -C <worktree> rebase --abort` and the marker `factory:local-review <head> changes-required round=<n>`, with `n` the round of the head's last `ready` marker, naming the conflicting files: fix round `n + 1` under the usual cap. The next tick gives the rebased worktree `local-review`. These three commands are not pre-approved and prompt for now.
     - `push-owed` → when the ticket's `touches` name `.github/workflows/`, collect "waiting for the owner's push" with the branch and the worktree path, and post the awaiting-push marker unless `notified`. Otherwise push as the app: `node .github/scripts/factory-token.mjs --app factory -- git -C <worktree> push -u origin <branch>`.

3. **Pick.** Label `blocked`, or remove the label, for tickets that wait for another plan's issues (the skill's rule 16). Pick `Ready` tickets within the skill's rule 5 and the parallel limit of the policy (`limits.parallelImplementers`), none that depends on an open ticket. A `Backlog` ticket may also start stacked, as the skill's step 3 says: one open dependency D whose pull request targets `main`, D's worktree not `working`, the stacked-on marker posted first. Start each implementer in the background, with the role and model the policy gives it and the base `origin/main`, or for a stacked ticket `origin/<D's branch>` at D's head.

4. **Next tick.** Schedule it with `ScheduleWakeup`:
   - about 5 minutes while a review or a writer runs, or checks are pending;
   - 60 minutes when only the owner's decisions and pushes remain, after one `PushNotification` that lists them. Send it again only when the list changes;
   - `stop: true`, with the skill's report, at a stop condition of the skill. At a milestone's end, run the exit check first, as the skill's report says: `node .github/scripts/factory-token.mjs --app factory -- node docs/spec/factory/.github/factory/exit-check.mjs specs/<folder>/plan.md --post`.

5. **Outside a tick.** When the session is woken by a writer's report or a finished command between ticks, start a tick by invoking `/factory-run` again, and do nothing else first. Outside a tick your commands are not pre-approved; a permission prompt there is the safe failure.
