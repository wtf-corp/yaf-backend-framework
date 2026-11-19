# 0002 — Local factory operation: research

What the plan in [`plan.md`](./plan.md) relies on. Nothing here is a secret: no key material, no token, no API key, no email address. The App IDs are identifiers, not secrets. Layout and provenance marks follow [`../0001-factory-setup-runbook/research.md`](../0001-factory-setup-runbook/research.md):

- **[repo]** read or run in this repository on 2026-11-19, on branch `spec/0002-local-factory-operation` cut from `main` at `eb3448a`. The implementer re-reads the cited file; where the file and this summary differ, the file wins and the difference is a comment on the ticket.
- **[live]** read from GitHub on 2026-11-19 with read-only calls (`gh run view`, `gh api` GET, `gh project item-list`). Nothing was written.
- **[owner]** reported by the human owner on 2026-11-19, relayed by the orchestrator. An agent cannot re-check these.
- **[docs]** third-party behaviour checked against the vendor's documentation on 2026-11-19; the source is given.
- **[assumed]** not established by any of the above; the ticket must confirm it or say that it could not.

## 1. Where the keys are, and the two apps [owner]

| | Factory app | Reviewer app |
|---|---|---|
| Slug / bot login | `yaf-factory` / `yaf-factory[bot]` | `yaf-reviewer` / `yaf-reviewer[bot]` |
| App ID (an identifier, not a secret) | `5834995` | `5837737` |
| Used by | Implementer and orchestrator: branches, pushes, pull requests, issues, Project status | The local model review only: checks `factory/review` and `factory/doc-review`, the summary comment |
| Permissions | Contents, Pull requests, Issues read and write; Projects read and write (organisation); Checks and Metadata read | Checks write, Pull requests write, Contents read |
| Environment variable holding the key file's **path** | `YAF_FACTORY_KEY_FILE` | `YAF_REVIEWER_KEY_FILE` |
| Key file, GitHub's download name kept | `yaf-factory.<date>.private-key.pem` | `yaf-reviewer.<date>.private-key.pem` |

- The folder is `~/.wtf-corp/yaf-factory/` on Windows, Linux and macOS; on Windows `~` is `%USERPROFILE%`. It is outside every repository.
- The two variables are set at user level and hold full paths. They never hold the key.
- On Windows the owner reports the folder's ACL as: inheritance removed; full control for the user, SYSTEM and Administrators only; and names `icacls <dir> /inheritance:r /grant:r "%USERNAME%:(OI)(CI)F"` as the command that gives this.
- Verified by the owner on 2026-11-19: a token was minted from each file for its app, each scoped to `wtf-corp/yaf-backend-framework`.
- On Windows a user-level variable reaches only processes started after it was set: the terminal and Claude Code are restarted once.

What the `icacls` reference says about that command [docs: https://learn.microsoft.com/en-us/windows-server/administration/windows-commands/icacls, page dated 2025-06-06]: `/inheritance:r` "disables inheritance and removes only inherited ACEs"; `/grant:r` grants the rights and "permissions replace previously granted explicit permissions"; `(OI)` and `(CI)` make files and subfolders inherit the entry; `F` is full access. By the reference, the command by itself leaves the named user's entry and any explicit entries that were already there. It adds none for SYSTEM or Administrators. Whether a given machine ends with those two entries is therefore **[assumed]**: T7 gives the command and tells the reader to confirm the result with `icacls <dir>`, and names the two further `/grant:r` entries for a reader who wants them.

Linux and macOS: `mkdir -p ~/.wtf-corp/yaf-factory`, `chmod 700` on the folder, `chmod 600` on each file. Standard POSIX modes; no source needed beyond `chmod(1)`.

## 2. The token procedure

Unchanged from research 0001 §5, steps 1 to 5 and 7, which hold the sources: the JWT (RS256; `iat` 60 seconds back, `exp` at most 10 minutes ahead, `iss` the App ID), `GET /repos/{owner}/{repo}/installation`, `POST /app/installations/{installation_id}/access_tokens` with `{ "repositories": ["<repo>"] }`, then the child process with `GH_TOKEN`, `GITHUB_TOKEN`, `GIT_TERMINAL_PROMPT=0` and the `GIT_CONFIG_COUNT` / `GIT_CONFIG_KEY_n` / `GIT_CONFIG_VALUE_n` pairs: an empty `credential.helper`, then `!f() { echo username=x-access-token; echo "password=$GH_TOKEN"; }; f`. The owner's script did this and opened pull requests #3 and #4 and pushed as the app [owner].

Checked for this plan on 2026-11-19 with Node v24.21.0 on Windows [repo]:

- **RS256 with no dependency.** `crypto.generateKeyPairSync('rsa', { modulusLength: 2048 })`, `crypto.sign('RSA-SHA256', data, pem)` and `crypto.verify('RSA-SHA256', data, publicKey, signature)` round-trip, with the three parts encoded by `Buffer#toString('base64url')`. A test can therefore generate its own key and verify the signature of the JWT the helper builds; no key is stored in the repository (hard rule 3).
- **The credential helper serves HTTPS only.** `credential.helper` is consulted when git needs a username and password for an HTTP(S) remote [docs: https://git-scm.com/docs/gitcredentials]. With an SSH `origin` (`git@github.com:…`), `git push` authenticates with the user's SSH key and never asks the helper: the push would be the person's, with the app's token unused. The helper therefore refuses a `git` command when `origin` is not HTTPS (plan, T5).
- **`fetch` is a global** (`typeof fetch` is `function`). The helper takes the function as a parameter, so tests pass a fake and make no network call.
- **File modes on Windows say nothing about the ACL.** `fs.statSync(file).mode & 0o777` printed `666` for a file on this machine. The Node documentation says the same of `fs.chmod`: on Windows only the write permission can be changed, and the distinction among user, group and others is not implemented [docs: https://nodejs.org/docs/latest-v22.x/api/fs.html, "File modes"]. So the check `mode & 0o077` is meaningful on Linux and macOS only, and on Windows the helper can only warn.
- **`node --test` with a pattern inside a dot-folder.** `node --test ".github/scripts/tests/*.test.mjs"` found and ran a test file in a scratch copy of that layout (1 test, 1 pass). CI runs Node 22 (`factory-gates.yml`, `node-version: 22`); the existing step there already passes a quoted pattern to `node --test`. That step is partial evidence: its pattern also runs through dot-folders (`docs/spec/factory/.github/factory/tests/`), named in full as here, and it finds the kit's 31 tests on Node 22 in every pull request's run. That a pattern which *starts* with a dot-folder behaves the same on Node 22 is **[assumed]**; T2's own pull request proves it, because under `pull_request` GitHub takes the workflow file from the pull request.
- **Spawning without a shell on Windows.** `gh`, `git` and `node` are executables and start without a shell. A `.cmd` or `.bat` file does not: Node refuses to spawn one without `shell: true` [docs: https://nodejs.org/docs/latest-v22.x/api/child_process.html, "Spawning .bat and .cmd files on Windows"]. The helper does not add a shell; it says in its error that such a command must be wrapped in its interpreter. `local-review.mjs` is started as `node .github/scripts/local-review.mjs`, which is an executable.

**The commit identity.**

- `GIT_AUTHOR_NAME`, `GIT_AUTHOR_EMAIL`, `GIT_COMMITTER_NAME` and `GIT_COMMITTER_EMAIL` set the identity of a commit and override the `user.name` and `user.email` configuration [docs: https://git-scm.com/docs/git, "Environment Variables", "Git Commits"]. Tried here with git 2.55.0 on Windows: with the four variables set, `git var GIT_AUTHOR_IDENT` and `git var GIT_COMMITTER_IDENT` print the given name and address, not the configured ones [repo].
- The form of an app's address is `<bot-user-id>+<app-slug>[bot]@users.noreply.github.com`, with the name `<app-slug>[bot]`. GitHub's page on commit addresses refers to a reference page for "the exact form the no-reply email address can take" and does not mention apps [docs: https://docs.github.com/en/account-and-profile/setting-up-and-managing-your-personal-account-on-github/managing-email-preferences/setting-your-commit-email-address]. The evidence used is the repository itself: the squash commits GitHub made on `main` for the app's pull requests (#3, #4) carry exactly this name and this form of address as their author [live: `git log --format='%an %ae' origin/main`].
- The bot user id is the `id` of the account `<app-slug>[bot]`, from `GET /users/<app-slug>%5Bbot%5D`, a public call [live]: `358792446` for `yaf-factory[bot]` and `358867400` for `yaf-reviewer[bot]`, both of type `Bot`. The first equals the number in the address of the app's commits on `main`. It is not the App ID. Like the App ID it is an identifier, not a secret.
- The reviewer app has Contents read and writes no commit; its identity is set all the same, so that the helper has one code path.

**The Project variables.** `status.mjs` and `project.mjs` need `FACTORY_PROJECT_OWNER` and `FACTORY_PROJECT_NUMBER` in the environment [repo: `project.mjs`; kit README, "Running the orchestrator"]. In CI they come from the repository variables of the same names. `GET /repos/{owner}/{repo}/actions/variables/{name}` exists; its page states the requirement for classic tokens only (`repo` scope, collaborator access) and no app permission [docs: https://docs.github.com/en/rest/actions/variables, API version 2022-11-28]. The factory app has Contents, Pull requests, Issues, Projects, Checks and Metadata, and neither Variables nor Actions (research 0001 §3, item 5). That its token is refused there is **[assumed]**: it was not tried, because no agent holds a token. The plan does not depend on it: the user sets the two variables. Their values here are `wtf-corp` and `1` (research 0001 §1).

## 3. Claude Code permission rules [docs]

Source: https://code.claude.com/docs/en/permissions, read on 2026-11-19. The Claude Code on this machine is 2.1.328 [repo: `claude --version`].

- **Path anchors of `Read` and `Edit` rules.** `//path` is absolute from the filesystem root; `~/path` is from the home directory (the page's example: `Read(~/Documents/*.pdf)`); `/path` is relative to the project root; `path` or `./path` is relative to the current directory. "On Windows, paths are normalized to POSIX form before matching."
- **The rule for the key folder** is therefore `Read(~/.wtf-corp/yaf-factory/**)`, the same string on all three operating systems.
- **What a `Read` deny rule covers.** The built-in file tools; best effort for Grep, Glob and `@file` mentions; and it "also blocks the Edit and Write tools on the same path". In a Bash command Claude Code can analyse, the rule also applies "to redirection targets and to file commands it recognizes, such as `cat`, `head`, `tail`, `sed`, and `tee`".
- **What it does not cover**, quoted: "They don't apply to a command that reads files without naming them, such as `grep -r pattern .` run from the directory that holds the file. They also don't apply to arbitrary subprocesses that read or write files indirectly, like a Python or Node script that opens files itself."
  - This is what lets the helper work: it is a Node subprocess that opens the key file itself, so the deny rule does not stop it.
  - It is also the limit of the rule: an agent can write a one-line script that reads the file. The rule stops accidents, not a determined agent. The page names the sandbox as the control that does not depend on the command text; the plan does not turn it on (decision log).
- **Bash rules.** "A `*` in a Bash rule matches any text, including spaces." A Bash rule "matches the command text Claude writes" and "isn't a security boundary around the program": the same program started in another form is not matched. The kit's `settings.json` already uses a `*` in the middle of a deny rule (`Bash(gh pr review *--approve*)`).
- **Order.** "Rules are evaluated in order: deny, then ask, then allow." An allow rule cannot carve an exception out of a deny rule.
- **No comments.** `settings.json` is JSON. The note that the rules stop accidents only is written where people read it: the `AGENTS.md` section of T7, and the kit README's checklist item 7, which already says it for the existing rules.

`.claude/settings.json` is a verbatim copy of `docs/spec/factory/.claude/settings.json`, written by `node .github/scripts/sync-references.mjs`; CI job `references` fails when they differ [repo: `sync-references.mjs`, `VERBATIM`].

## 4. Why the ticket creation of plan 0001 stopped [live]

Run 44603456784 of `factory-tickets.yml` (push of #4 to `main`, 2026-11-19 19:56 UTC) failed after 21 seconds. Its log, in order:

    19:56:26.6  epic #7 created
    19:56:30.0  gh: Not Found (HTTP 404)
    19:56:31.2  T1 → #8
    19:56:34.6  GraphQL: Content already exists in this project (addProjectV2ItemById)
                Error: Command failed: gh project item-add 1 --owner wtf-corp --url …/issues/8 --format json
                  at addItem (project.mjs:34) at setStatus (project.mjs:42) at createFromPlan (create-tickets.mjs:84)

State afterwards: epic #7 and ticket #8 (0001's T1) exist; 0001's T2 was never created, because the error ended the loop.

**The 404 is not the sub-issue link.**

- The log holds no line "sub-issue link failed" or "dependency link failed" (0 matches). `create-tickets.mjs` prints that warning when the link call throws; it did not throw.
- The link was made: `GET repos/…/issues/7/sub_issues` returns issue #8, and #8 carries `parent_issue_url: …/issues/7`.
- The 404 comes from `ensureLabel`: it asks for label `area:docs`, `gh` prints "Not Found (HTTP 404)" to the job's log, the `catch` creates the label. Label `area:docs` exists with the colour `ededed` that `ensureLabel` gives. The 404 is expected the first time an area is used; it is noise, and it reads like an error.
- The kit's call is right as it stands: `POST /repos/{owner}/{repo}/issues/{issue_number}/sub_issues` takes `sub_issue_id`, an integer, "the ID of the sub-issue to add", and `create-tickets.mjs` passes `issue.id` of the created issue, which is the numeric id (6497867276 for #8), not the number and not the node id [docs: https://docs.github.com/en/rest/issues/sub-issues, "Add sub-issue", API version 2022-11-28].
- So there is **no kit bug in the sub-issue link**, and T1 changes nothing there. The plan's brief expected one; this is the finding.

**The failure is `addItem`.** `project.mjs` says "Adding twice returns the same item" and passes on whatever `gh project item-add` returns. Here the call failed with "Content already exists in this project".

- Something had already put #8 in the Project between its creation at 19:56:30 and the call at 19:56:34. The epic was added by the script itself four seconds earlier, without an error, and #8 had just been linked as its sub-issue. The likely cause is one of the Project's built-in workflows, which add a repository's new issues or the sub-issues of an item. Which one is **[assumed]**: the documentation page read describes the auto-add workflow and its filters and says nothing on sub-issues [docs: https://docs.github.com/en/issues/planning-and-tracking-with-projects/automating-your-project/adding-items-automatically], and an agent cannot read the Project's workflow settings.
- **What is not known.** Whether `item-add` fails this way for *every* issue the Project already holds, or only when another actor added the issue moments before, in a race with the Project's workflow. The kit's comment assumed the first never happens; the run shows only that the error can happen. One observation points to the race: `status.mjs set` and the status workflow go through `setStatus`, which calls `addItem` for items that are long in the Project, and #8 has status `Ready`, so if someone set it with `status.mjs`, `item-add` succeeded for an existing item. Who set it is not recorded, so this settles nothing. An agent cannot try it: adding an item is a write.
- **The fix covers both.** When `item-add` fails with that message, the item exists or is about to be listed, and its id is what `addItem` must return. In the race the list may not show the item at once, so the lookup is repeated a few times, a short wait apart, before the original error is thrown again.
- **How to find the item.** `gh project item-list 1 --owner wtf-corp --format json` returns `items[]`, each with `id` (the item id, `PVTI_…`) and `content.url` (the issue's URL, the same string `addItem` receives), `content.number`, and `status` when one is set. Read from the live Project with gh 2.102.0. `project.mjs` `statuses()` already calls `item-list` with `--limit 2000`, so the lookup adds no new command.
- Live state of the two items: #8 has status `Ready`; #7 has none. An epic gets no status from the script, by design.

**Tickets that exist without a status.** A run that stops in `setStatus` leaves the ticket it had just created in the Project with no status, and a second run skips it: `createFromPlan` sets the status only inside `if (!issue)`. #8 has `Ready` today (who set it is not recorded), but the same will happen to the next ticket a failing run creates, see the plan's risks. T1 therefore also gives an existing, open ticket its first status when the Project shows none, and leaves any status it finds alone.

**Re-running.** `factory-tickets.yml` has `workflow_dispatch` with a required input `plan`, the path of one `plan.md`; the step passes it to `create-tickets.mjs` [repo]. The job uses environment `factory-merge`, whose deployment branches allow `main` only, so the run is dispatched on `main`. The factory app has no Actions permission, so a human dispatches it: `gh workflow run factory-tickets.yml -f plan=specs/0001-factory-setup-runbook/plan.md`. The script finds existing issues by their hidden marker through issue search; the kit README notes that search indexing lags by seconds.

**The push trigger and this plan.** The workflow runs on a push to `main` that changes `specs/**/plan.md`. The pull request of this plan changes two such files, 0001's and 0002's. The step lists them with `git diff --name-only`, which sorts by path, so 0001 is processed first [repo: `factory-tickets.yml`]. What that run will do depends on the unknown above: it stops at epic #7 if the error fires for every existing item, and later if it fires only in the race. The consequence is in the plan's risks.

## 5. Facts about the gates the plan depends on [repo]

Checked by running the kit's own code on 2026-11-19:

- The pull request of this plan changes four files, all matching `docPaths.plan` (`specs/**/plan.md`, `specs/**/research.md`), so its kind is `plan`.
- `sensitivePaths` holds `.github/**`, `AGENTS.md`, `docs/spec/**`, `docs/spec/factory/.github/**` and `docs/spec/factory/.claude/**`, and `.claude/**`. Every ticket of this plan touches at least one of them: all eight are tier high. `.github/CODEOWNERS` covers `/.github/`, `/.claude/`, `/docs/spec/` and `AGENTS.md`, so each also needs a code owner's review.
- `size.exclude` covers none of the touched paths: every line counts, blank lines and comments included (`size.mjs` sums `git diff --numstat`).
- The kit's tests: `node --test "docs/spec/factory/.github/factory/tests/*.test.mjs"` → 31 tests, 31 pass. The kit README states that count in its second paragraph; T1 adds tests and corrects the number.
- Job `factory-self-test` of `.github/workflows/factory-gates.yml` runs that command on the pull request's head. It is **not a required check**: the `main` ruleset requires the nine `factory/*` checks and `references` (research 0001 §4). A red self-test does not block a merge by itself; the reviewer and, for tier high, the owner see it.
- `ticketProblems` fails the plan gate for an estimate above 200, a missing failing test on a ticket that is not `docs`, a missing verification command and empty `touches`. `create-tickets.mjs` puts the `Verify` cell into a code block and `plan.mjs` splits cells on an unescaped `|`: a `Verify` cell holds a command only, with no pipe.
- The `Type` column feeds the `type:*` label, and `plan.mjs` accepts `feat`, `fix`, `refactor`, `docs`, `test` and `chore` there. A pull request titled `ci: …` therefore has type `chore` in the table. `ci` is the Conventional Commit type for workflows and CI configuration (`yaf-commits-and-pull-requests`), and pull requests #2 and #6 used it for the factory.

## 6. Not available at planning time, and open points

- The reviewer's token is in the environment of the `claude -p` child that `local-review.mjs` starts, because the script passes its environment on. That session has read-only tools and three `git -C pr` commands, so it has no tool to use the token with; removing `GH_TOKEN` and `GITHUB_TOKEN` from that child's environment would still be the tidier state. Not in this plan: it is a change to the review script, which is temporary. **Open point for the owner.**
- `factory-self-test` is not a required check (section 5). Making it one is a ruleset change, done by the human. **Open point for the owner.**
- The `ensureLabel` 404 in the ticket workflow's log (section 4) is harmless and misleading. Not changed by T1; mentioned so that the next reader of that log does not chase it.
- Which built-in workflow of the Project added #8, and whether `item-add` fails for every existing item or only in a race with it (section 4).
- Whether the factory app's token can read repository variables (section 2). Not needed.
- Node 22 and the dot-folder test pattern (section 2).

## 7. What reads and what states the size limits [repo]

Looked up on 2026-11-19 for T8 (`size.soft` 200 → 400, `size.hard` 400 → 1000).

**Code that reads them, all from the policy, none with a number of its own:** `size.mjs` (`sizeVerdict`: over soft, over hard, the check's title), `plan.mjs` (`ticketProblems`: an estimate above `size.soft` fails `factory/plan-gate`), and through them `gates.mjs` and `classify.mjs` (`sizeOverHard` makes the tier high). `policy.schema.json` asks for integers of at least 1. The gates run the base branch's policy, so a pull request that changes the limits is judged by the old ones.

**Tests.** `tests/size.test.mjs` takes both limits from the live policy (`policy.size.soft`, `policy.size.soft + 1`, `policy.size.hard + 1`) and passes with any values. `tests/delta.test.mjs`, test "ticket list problems a script can decide", writes an estimate of `320` into a fixture plan and expects a problem that includes "soft limit" from the live policy. Tried on a scratch copy of the kit with the policy set to 400 and 1000: 31 tests, 30 pass, 1 fail, that test. No other test fails.

**Prompts.** `prompts/pr-review.md` and `prompts/doc-review.md` name "the soft size limit of the policy" and state no number.

**Texts that state the numbers.** "Changes" means: the line keeps the kit's default, 200 and 400, as "by default", and gains the clause that this repository uses 400 and 1000 during the initial build. The kit's default does not change; this repository's policy value does (the orchestrator's choice, plan decision log).

| File, line | Text | T8 |
|---|---|---|
| `docs/spec/README.md`, 87 | "small pull request (200 lines soft, 400 hard)" | changes |
| `docs/spec/agents/AGENTS.root.md`, 58 | "Above 200 changed hand-written lines the reviewer asks why it was not split; above 400 the size check fails" | changes |
| `yaf-commits-and-pull-requests/SKILL.md`, 33 | "Above 200 changed hand-written lines (`size.soft`) … above 400 (`size.hard`)" | changes |
| `yaf-implementing-tickets/SKILL.md`, 32 | "(`size.soft` 200, `size.hard` 400 changed hand-written lines by default)" | changes |
| `yaf-orchestrating-work/SKILL.md`, 80 | "Limits: 200 changed hand-written lines (hard 400)", in the brief given to an implementer | changes |
| `yaf-reviewing-changes/SKILL.md`, 43 | "(`size.soft`, 200 changed hand-written lines by default)" | changes |
| `yaf-reviewing-specs-and-plans/SKILL.md`, 55 | "`size.soft` (200 changed hand-written lines by default)" | changes |
| `yaf-writing-plans/SKILL.md`, 41 | "(`size.soft` in the policy file, 200 changed hand-written lines by default)" | changes |
| `yaf-writing-plans/references/plan-template.md`, 96 | "within the soft size limit (200 changed hand-written lines by default)" | changes |
| `docs/spec/factory/README.md`, 147 | the trial "a 500-line change → `factory/size` fails" | changes: 500 lines no longer fail here; it becomes "a change over the hard limit" |
| `yaf-writing-plans/SKILL.md`, 70; `yaf-reviewing-specs-and-plans/SKILL.md`, 128 | "a reviewer finds defects in 200 lines and misses them in 2 000"; "every row has a size under 200" | stay: a reason and an example, not the limit |
| ADR-0025 (69), ADR-0026 (63), ADR-0027 (86), ADR-0029 (113, 114), `17` (78), `18` (10) | 200 and 400 as the decided starting values | stay (plan, deviation 6) |
| `docs/spec/history/2026-11-software-factory.md`, 47 | "Soft limit 200 lines, hard limit 400, both adjustable" | stays: a history record |

The skills are under `docs/spec/agents/skills/`. The generated stubs in `.claude/skills/` and `.agents/skills/` hold a skill's front matter and a pointer; none of these lines is front matter, and `AGENTS.root.md` is imported, not copied [repo: `sync-references.mjs`]. So no reference is expected to change; `--check` after the edit shows it.

**Plan 0001.** Its V3 expected the literal "(soft 200, hard 400)" and its research described the size trial as "over 400 changed lines". Both are reworded in this plan's pull request to name the policy's limits, so that they hold before and after T8.
