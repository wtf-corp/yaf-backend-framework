# 0002 — Local factory operation: plan

**Spec** none: this plan has no `spec.md`. Its approved basis is ADR-0029 (`docs/spec/adr/0029-software-factory.md`) §1, §3, §8, the kit README (`docs/spec/factory/README.md`), "Human setup checklist" items 2 and 7 and "Running the orchestrator", and `18` §4, "Agent identity for local work" (`docs/spec/spec/18-build-plan.md`), all on `main` since #1; and the `AGENTS.md` section "Model reviews run locally (temporary)", on `main` since #6 · **Status** Draft

Why there is no spec: the change makes the factory that ADR-0029 decided operable from a developer's machine. It adds no behaviour to YAF and no requirement of its own: who acts as which identity, and that agents never act as the human, is decided in ADR-0029 §8 and the kit README. What is new is tooling for it and three decisions of the owner: where the app keys are kept, that one local session may hold both apps' identities for now, and larger size limits until `1.0.0`. Plan 0001 took the same route. Having no spec is a deviation from the process and is listed as one below.

## Approach

Three things stand between the installed factory and running it from a developer's machine on Windows, Linux or macOS. The plan closes each with the smallest change.

1. **Ticket creation stops when the Project already holds an issue.** The run for plan 0001 created epic #7 and ticket #8 and then failed in `project.mjs` `addItem`, so 0001's T2 does not exist (research §4). T1 makes a re-run of ticket creation safe: `addItem` returns the existing item's id on that error, and a ticket a failed run left without a status gets its first one. It is a kit fix under `docs/spec/factory/.github/factory/`.
2. **No reviewed way to act as an app.** Agents must act as `yaf-factory`, and the local model review must post as `yaf-reviewer`; until now a script outside the repository did the first. T2 to T5 add `.github/scripts/factory-token.mjs`: it reads an app's private key from a file, mints an installation token scoped to this one repository, and runs one command with it, as the app for `gh`, for `git` and for the author of a commit. T6 adds two deny rules to the local agent settings.
3. **No instructions.** T7 adds a section "Running the factory locally" to the root `AGENTS.md` and points the existing section on local reviews at the helper.

A fourth change is in this plan by the owner's decision and is independent of the three: T8 raises the size limits of the policy for the initial build, `size.soft` from 200 to 400 and `size.hard` from 400 to 1000, until `1.0.0`.

**Plan update of 2026-11-19.** The first version of this plan merged as #9 (`6c1eff1` on `main`) with tickets T1 to T8. This update adds two tickets that came out of its reviews and corrects two rows. T9 lets a finding that only the owner can answer be reported without failing `factory/doc-review`: today such a finding can never be cleared, so a flagged plan with a security deviation cannot pass its required check. T10 keeps the app's token out of the environment of the reviewer session that `local-review.mjs` starts. T5 gains a tested command runner and T7's first failing check is stated as it prints. T1 to T8 keep their keys; what the update means for the issues that already exist is under "Tickets".

The facts are in [`research.md`](./research.md), each with its provenance. The plan's first pull request, #9, also updated plan 0001 and its research (C7 and its budget, research §5, the decision log, the risks and a fourth deviation), so that the runbook 0001's T2 will write describes the key storage decided here. All four files are plan files, so that pull request was of kind `plan`; so is the update's, which changes this plan and its research only.

Files read to decide: `AGENTS.md`, `docs/spec/agents/AGENTS.root.md`, `docs/spec/agents/AGENTS.nested.md` (section `specs/`), `docs/spec/factory/README.md`, ADR-0023, ADR-0025, ADR-0029, `docs/spec/spec/18-build-plan.md` §1, §4, §5, `specs/0001-factory-setup-runbook/plan.md` and `research.md`, `.github/scripts/local-review.mjs`, `.github/scripts/sync-references.mjs`, `.github/workflows/factory-tickets.yml`, `.github/workflows/factory-gates.yml` (job `factory-self-test`), `.github/CODEOWNERS`, `docs/spec/factory/.claude/settings.json`, and in `docs/spec/factory/.github/factory/`: `create-tickets.mjs`, `project.mjs`, `lib.mjs`, `plan.mjs`, `ticket.mjs`, `classify.mjs`, `gates.mjs`, `policy.json`, `tests/size.test.mjs`.

### What is built

The coverage targets are items `L1…L11`. There is no spec to take `R…` ids from; each item is derived from the cited basis.

| Item | Ticket | What must be true | Basis | Research |
|---|---|---|---|---|
| L1 | T1 | Creating tickets from a plan succeeds when the Project already holds an issue, and a re-run creates what is missing, nothing twice, and gives a first status to a ticket that has none | ADR-0029 §3 ("creates the issues idempotently"); kit README, "How a ticket flows" 3 | §4 |
| L2 | T2 | A function builds the RS256 JWT of an app and exchanges it for an installation token scoped to one repository; a failure reports the HTTP status and GitHub's message and never the key, the JWT or a token | ADR-0029 §8; kit README checklist 2; `18` §4 | §2 |
| L3 | T3 | The key comes from a file whose path is in the app's environment variable, or from `--key-file`, or from the output of `--key-cmd`; a key file readable by group or others is refused on Linux and macOS; on Windows the helper warns that it cannot check; the key is never printed | The owner's decision on key storage; `18` §4 | §1, §2 |
| L4 | T4 | The environment of the wrapped command makes it the app: for `gh` (the token variables), for `git` over HTTPS (the credential helper, for that process only), and as author and committer of a commit (the app's bot name and noreply address) | ADR-0029 §8 ("agent work must not carry a person's identity"); the owner's decision on the commit author | §2 |
| L5 | T5 | `factory-token.mjs --app <app> -- <command…>` runs the command without a shell and exits with its exit code; the token is printed only with `--print` | ADR-0029 §8; kit README checklist 2, "Running the orchestrator" | §2 |
| L6 | T2 | The helper's tests run in CI on every pull request | ADR-0029, Enforcement ("Tests of the scripts") | §2, §5 |
| L7 | T6 | Local agents are denied reading the key folder with the file tools, and running the helper with `--print` | ADR-0029 §8; kit README checklist 7 | §3 |
| L8 | T7 | `AGENTS.md` says who uses which identity, the order of steps per pull request, what keeps author and reviewer apart, how the keys are set up on each operating system, and which variables the kit's scripts need | ADR-0029 §8; kit README checklist 2, "Running the orchestrator"; the owner's decisions | §1, §3 |
| L9 | T8 | The policy's size limits are 400 (soft) and 1000 (hard); the kit's tests pass with them; the instructions agents act from keep the kit's defaults and say that this repository uses 400 and 1000 until `1.0.0` | ADR-0029 §5 ("Both numbers are policy values"); kit README, "Tuning the policy"; the owner's decision on the size limits | §7 |
| L10 | T9 | A finding of the document review that is a question for the owner is reported and listed, and does not by itself fail `factory/doc-review`; every other Blocker or Major still does | ADR-0023, "What counts as approval"; ADR-0029 §7 (`factory/plan-gate`: "the plan is not flagged, or a human maintainer approved it"); ADR-0025 (a deviation that changes scope, security or a public contract "is a question for the human owner") | §8 |
| L11 | T10 | The reviewer session that `local-review.mjs` starts does not have the reviewer app's token in its environment | ADR-0029 §8; kit README, design decision 3 ("code under review must not run next to" a credential); `AGENTS.md`, "Model reviews run locally" | §6 |

### The helper (T2 to T5)

One file, `.github/scripts/factory-token.mjs`: Node 22+, no dependencies, next to `sync-references.mjs` and `local-review.mjs`. It exports its functions and runs as a command only when it is the entry script, the way `create-tickets.mjs` does. Its tests are `.github/scripts/tests/factory-token.test.mjs`, on Node's built-in test runner, with no network call and no stored key: tests generate their key pair.

    node .github/scripts/factory-token.mjs --app factory|reviewer [--key-file <path> | --key-cmd "<command>"] [--repo <owner>/<name>] -- <command> [args…]
    node .github/scripts/factory-token.mjs --app factory|reviewer [--key-file … | --key-cmd …] [--repo …] --print

| Part | Ticket | Behaviour |
|---|---|---|
| `APPS` | T2, T4 | T2: `factory` → App ID `5834995`, variable `YAF_FACTORY_KEY_FILE`; `reviewer` → App ID `5837737`, variable `YAF_REVIEWER_KEY_FILE`. T4 adds to each its slug (`yaf-factory`, `yaf-reviewer`) and the user id of its bot account (`358792446`, `358867400`; research §2). All are identifiers, not secrets |
| `buildJwt(appId, pem, nowSeconds)` | T2 | Header `alg: RS256`, `typ: JWT`; claims `iat` = now − 60, `exp` = now + 540, `iss` = the App ID; signed with `node:crypto` |
| `mintToken({ fetch, appId, pem, repo, now })` | T2 | `GET https://api.github.com/repos/{repo}/installation`, then `POST https://api.github.com/app/installations/{id}/access_tokens` with body `{ "repositories": ["<name>"] }`; both with `Authorization: Bearer <jwt>`, `Accept: application/vnd.github+json` and `X-GitHub-Api-Version: 2022-11-28`. Returns `{ token, expiresAt }`. On a status that is not 2xx it throws an error holding the method, the path, the status and the response's `message` field, nothing else |
| `keyFileProblem(mode, platform)` | T3 | On `win32`: `null`. Elsewhere: a message when `mode & 0o077` is not zero, else `null` |
| `loadKey({ app, keyFile, keyCmd, env, platform })` | T3 | `--key-cmd` wins: the command is run through the platform's shell and its standard output is the key. Otherwise the path is `--key-file`, or the app's variable. No path: an error naming the variable. The file is checked with `keyFileProblem` before it is read; a problem is an error that names the path and the `chmod 600` to run. On `win32` one warning line on standard error says the permissions were not checked. Giving both flags is an error. The value must hold a PEM private-key header, or the error says so without echoing it |
| `repoFromRemote(url)` | T4 | `owner/name` from an HTTPS or SSH GitHub URL, with or without `.git` |
| `childEnv(env, { token, repo, app })` | T4 | The given environment, unchanged except for: `GH_TOKEN`, `GITHUB_TOKEN`; `GIT_TERMINAL_PROMPT=0`; `GITHUB_REPOSITORY` when it is not already set; `GIT_CONFIG_COUNT=2` with `credential.helper` set first to empty and then to `!f() { echo username=x-access-token; echo "password=$GH_TOKEN"; }; f`; and `GIT_AUTHOR_NAME` and `GIT_COMMITTER_NAME` = `<slug>[bot]`, `GIT_AUTHOR_EMAIL` and `GIT_COMMITTER_EMAIL` = `<bot-user-id>+<slug>[bot]@users.noreply.github.com`, built from the two parts of `APPS`. An environment that already has `GIT_CONFIG_COUNT` is refused, so nothing is overwritten silently. Everything else passes through, `FACTORY_PROJECT_OWNER` and `FACTORY_PROJECT_NUMBER` included |
| `parseArgs(argv)` | T5 | The flags above; everything after `--` is the command. An unknown flag, an unknown app, a missing command without `--print`, or `--print` together with a command exits 2 with the usage text |
| The command line | T5 | The repository is `--repo`, else `repoFromRemote` of `git remote get-url origin`. When the command is `git` and the URL of `origin` is not HTTPS, the helper refuses before it mints anything and exits 2: the credential helper it sets serves HTTPS only, so over SSH `git` would authenticate with the user's own key, as the person. The message says so and gives the way out, `git remote set-url origin https://github.com/<owner>/<name>.git`. The decision is an exported function of the command's first word and the URL. Otherwise the command is started with `spawn`, no shell, standard streams inherited, in `childEnv`. Starting the command and turning its end into an exit code is an exported function, `runCommand(command, args, env)`, so a test can run a harmless child through it. The helper exits with the command's exit code, or 1 when it ended by a signal or could not start; the error for a command that could not start says that a `.cmd` or `.bat` file must be wrapped in its interpreter. `--print` writes the token and its expiry to standard output and runs nothing |

Rules for the code:

- The key and the JWT exist only in memory. Neither they nor the token appear in any message, on any stream, except the token under `--print`. Errors are built from fixed text plus the status, the path and GitHub's `message`.
- The helper has no default key path: the file names carry a date, and a wrong guess would be silent. No variable and no flag is an error.
- `--key-cmd` is accepted as a flag only, never from a variable or a file: it is run through a shell, so it must be what the person at the keyboard typed. It is the route to a credential store or a key manager later, with no change to the helper.
- The bot user ids are constants next to the App IDs, not looked up at run time: an account's id does not change, a lookup is one more network call and one more way to fail on every run, and both ids were read from `GET /users/<slug>%5Bbot%5D` (research §2). The policy file holds the factory bot's login only and nothing for the reviewer app.
- The helper does not fetch `FACTORY_PROJECT_OWNER` and `FACTORY_PROJECT_NUMBER`: they are repository variables, and the factory app has neither the Variables nor the Actions permission (research 0001 §3, item 5; research §2). The user sets them as environment variables, T7 says how, and `childEnv` passes them on.
- No third-party API is called from memory: the endpoints, claims and `GIT_CONFIG_*` semantics are in research 0001 §5 with their sources, the commit identity variables in research §2, and T2 to T5 re-read them.

### The kit fix (T1)

- `project.mjs` `addItem`: when `gh project item-add` fails and the error text holds "already exists in this project", list the Project's items (`gh project item-list … --limit 2000 --format json`, as `statuses()` does) and return the `id` of the item whose `content.url` equals the issue's URL. When the list does not show the item yet, it is read again, three times in all, two seconds apart: whether the error can come before the item is listed is not known (research §4). Any other error, and that error when no such item is found after the last read, is thrown as before. The comment "Adding twice returns the same item" is corrected. The function takes the `gh` runner and the wait as parameters that default to the present behaviour, so a test passes fakes and does not sleep.
- `create-tickets.mjs`: for a ticket that already exists, is open and has no status in the Project, set the status it would have got when created (`Ready` when nothing blocks it, else `Backlog`). A ticket with any status is left alone, so a re-run never moves work that is in progress. The decision is an exported pure function; the Project is read once per plan, and only when a ticket exists.
- The sub-issue link is **not** changed: it works, and the 404 in the log is the label lookup of `ensureLabel` (research §4).
- `docs/spec/factory/README.md`: its second paragraph stops stating a number of tests ("31 tests"), which every ticket that adds a kit test would otherwise have to correct, and in "Verify before relying on it", under the Projects bullet, one sentence that `item-add` can fail for an issue the Project already holds and that `addItem` then looks the item up.

### The settings (T6)

Two entries added to `permissions.deny` in `docs/spec/factory/.claude/settings.json`, then `node .github/scripts/sync-references.mjs`, which rewrites `.claude/settings.json`:

- `Read(~/.wtf-corp/yaf-factory/**)`
- `Bash(node .github/scripts/factory-token.mjs *--print*)`

Syntax and limits are in research §3, checked against the Claude Code permissions documentation on 2026-11-19 (Claude Code 2.1.328 on this machine). The first rule does not stop the helper, which is a Node subprocess, and for the same reason does not stop a script an agent writes. Both rules stop accidents, not a determined agent; JSON takes no comment, so T7's text says so.

Both entries name things of this repository, its key folder and its script, and they go into the kit's generic settings file. There is no other place today: `.claude/settings.json` is a verbatim copy that `sync-references.mjs` owns, and a rule written only there fails check `references` (deviation 4).

### The instructions (T7)

A new section `## Running the factory locally` in the root `AGENTS.md`, placed before "Model reviews run locally (temporary)", with this content and nothing else:

| Part | Content |
|---|---|
| Identities | Implementer and orchestrator act as `yaf-factory`: `node .github/scripts/factory-token.mjs --app factory -- <gh or git command>`, `git commit` included, so that author and committer are the app. The local review posts as `yaf-reviewer`: `node .github/scripts/factory-token.mjs --app reviewer -- node .github/scripts/local-review.mjs <pr-number>`. `origin` must be an HTTPS remote: over SSH `git` would use the person's key, and the helper refuses to run it. Never the user's login for a factory action: committing or pushing a ticket's branch, opening or editing a pull request, commenting, labelling, setting status, posting a check |
| What keeps author and reviewer apart | A rule of procedure, stated as one: the implementer never runs the review. It never runs the helper with `--app reviewer` and never runs `local-review.mjs`. The orchestrator holds both identities: it opens the pull request as the factory app and runs the review of that same pull request as the reviewer app, and the reviewer it starts is a fresh session that the orchestrator does not brief. One sentence says what the rule does not give: the orchestrator's session can mint both tokens, so a pull request in tier low or medium could pass `factory/review` and merge with no human; the owner accepted this on 2026-11-19 until the reviews are back in CI, with a link to this plan's decision log |
| Order per pull request | The orchestrator opens the pull request as the factory app → the gates run in CI → the orchestrator runs the local review as the reviewer app, again after every push → tier low and medium merge by themselves; a tier-high pull request and a flagged plan wait for the owner's approving review |
| Key setup | The folder `~/.wtf-corp/yaf-factory/`, outside every repository; the two files under GitHub's download names; `YAF_FACTORY_KEY_FILE` and `YAF_REVIEWER_KEY_FILE` holding full paths, never the key; the commands for Linux and macOS (`mkdir -p`, `chmod 700`, `chmod 600`, an `export` line in the shell profile) and for Windows in PowerShell (`icacls <dir> /inheritance:r /grant:r "$($env:USERNAME):(OI)(CI)F"`, `[Environment]::SetEnvironmentVariable(…, 'User')`), with "confirm with `icacls <dir>`" (research §1); on Windows, restart the terminal and Claude Code after setting the variables. This is the one copy of these commands: plan 0001's runbook points here |
| Project variables | `FACTORY_PROJECT_OWNER` and `FACTORY_PROJECT_NUMBER` are set as user-level environment variables too, with the values of the repository variables of the same names: `status.mjs` needs them and the app's token cannot read them |
| What the setup does not give | Any process running as the user, agents included, can read the key files; the deny rules of `.claude/settings.json` stop accidents, not a determined agent. A key that may have been read is deleted on the app's settings page and replaced. This departs from the default of `18` §4 by the owner's decision, with a link to this plan |
| `--print` | For the human's own terminal only; never in an agent session, a log or CI |

In "Model reviews run locally (temporary)":

- the "How" bullet names the helper command in place of "with `GH_TOKEN` set to …";
- in the "What it costs in trust" bullet, the condition "the reviewer app's key must be kept where agents do not read it" no longer holds and is replaced, not dropped: by the rule of procedure above, in the same words, and by the sentence that the owner accepted on 2026-11-19 that the separation rests on that rule alone until the reviews move back to CI, with the link to this plan;
- the last paragraph adds the reviewer entry of the helper's `APPS` to what goes when the reviews move back to CI (the entry, not the helper).

No secret, key material, token or email address is written; the App IDs and bot ids are not repeated there.

### The size limits (T8)

The owner's decision of 2026-11-19: "lift the pr lines limit for the initial dev. when hard 400 is difficult to not exceed, and a change is reasonable", with the values soft 400 and hard 1000, until `1.0.0`. One ticket, because the number and the texts that state it are one concern, the limit:

- `docs/spec/factory/.github/factory/policy.json`: `size.soft` 200 → 400, `size.hard` 400 → 1000. Nothing else in the policy changes. The schema asks only for integers of at least 1.
- `docs/spec/factory/.github/factory/tests/delta.test.mjs`: the test "ticket list problems a script can decide" builds a ticket with an estimate of 320 and expects the problem "soft limit" from the live policy. With soft 400 it fails (tried on a scratch copy: 31 tests, 30 pass, this one fails). The fixture's estimate becomes one that is over the limit whatever the policy says, computed from `policy.size.soft`, as `size.test.mjs` does. The assertion is not touched: the test still demands the problem. No other kit test states a limit: `size.test.mjs` reads both from the policy.
- The texts that state 200 and 400, one line each, ten in all (research §7): `docs/spec/README.md`, `docs/spec/agents/AGENTS.root.md`, the skills `yaf-commits-and-pull-requests`, `yaf-implementing-tickets`, `yaf-orchestrating-work`, `yaf-reviewing-changes`, `yaf-reviewing-specs-and-plans` and `yaf-writing-plans`, the plan template, and the kit README's trial "a 500-line change". **The kit's defaults stay as they are stated.** The change is temporary and is this repository's: the default of the kit does not change, only this repository's policy value does. So each line keeps 200 and 400, as "by default" where it already says so and with those words added where it states the numbers flat, and gains one clause: this repository uses 400 and 1000 during the initial build, until `1.0.0`. The README's trial becomes "a change over the hard limit", with the same clause, because 500 lines no longer fail here.
- Not changed: ADR-0025, ADR-0026, ADR-0027 and ADR-0029 and `17` §2.2 and `18` §1, which state 200 and 400. So does `docs/spec/history/2026-11-software-factory.md` (line 47), a history record. They are the record of the decision and of the starting values, ADR-0029 §5 says that both are policy values, and the limits return to them (deviation 6). `plan.mjs`, `size.mjs` and the two review prompts read the policy or name it and need no change.
- `node .github/scripts/sync-references.mjs --check` afterwards: the generated stubs hold only a skill's front matter, which does not change, so nothing is expected to be regenerated; the check proves it.

What it changes for this plan: nothing. `factory/plan-gate` judges this plan's pull request by the policy of the base branch, soft 200, so every estimate here stays at or under 200, and the split of the helper into four tickets stands: it is not merged back after T8. T8's own pull request is judged by the old limits too. From the merge of T8 on, `plan.mjs` accepts estimates up to 400 and `factory/size` fails above 1000.

### Owner-addressed findings (T9)

The owner's decision of 2026-11-19. The problem, as it stands on `main`:

- `prompts/doc-review.md`, plan checklist item 10, tells the reviewer to report a deviation that changes scope, security or a public contract "as a Major finding addressed to" the owner.
- `post-review.mjs` `verdictOf` fails the check on any Major.
- `factory/doc-review` is a required check. So a plan with such a deviation cannot merge, whatever the owner answers: the answer is a GitHub review, and no review clears a finding. This plan's own pull request, #9, was that case: its document review reported deviations 2, 3 and 6 as Majors for the owner, and the owner merged it at its first version on 2026-11-19.
- The owner's answer already has its place: a plan with any deviation is flagged, and `factory/plan-gate` then demands a human maintainer's approving review.

The change, kept to the document review:

- `doc-review.schema.json`: a finding may carry the optional property `for`, with the one allowed value `"owner"`. Nothing else in the schema changes; a verdict without the property is valid as before.
- `prompts/doc-review.md`: item 10 tells the reviewer to report such a deviation as a finding with `for: "owner"` and the severity that describes it, and the section "Findings and verdict" says that a finding for the owner does not make the verdict `changes-required` and is not posted as a demand on the author. The prompt limits the marker to item 10: a question only the owner can decide. A defect the author can fix is never marked.
- `post-review.mjs` `verdictOf`: for check `factory/doc-review`, findings with `for: "owner"` are left out of the Blocker and Major counts; the title says how many there are ("pass: no open Blocker or Major; 3 for the owner"). The reviewer's own `verdict` and the checklist rows count as before. For `factory/review` nothing changes: a finding there fails by its severity whatever it carries.
- `post-review.mjs` `renderSummary`: those findings are listed under a heading "For the owner", apart from "Findings", with a line that the owner answers them in the approving review `factory/plan-gate` waits for.
- Tests in `tests/post-review.test.mjs`.

`factory/review`, the check on code pull requests, does not need the same. Its prompt has no class of finding addressed to the owner, `review.schema.json` is not changed, and what a code pull request needs from the owner already has a gate: tier high waits for `factory/human-approval`.

What keeps the marker from becoming a way round the review: it is honoured for the document review only; a plan that has a deviation is always flagged (`plan.mjs` `hasDeviations`), so `factory/plan-gate` holds it for a human, and a spec always needs a code owner; and the findings stay in the summary, in a section of their own, where the approver reads them.

### The reviewer session's environment (T10)

`local-review.mjs` starts the reviewer, `claude -p`, with the environment it was given. Run through the helper, that environment holds the reviewer app's token in `GH_TOKEN` and `GITHUB_TOKEN`, and after T4 every wrapped command has it. The reviewer session has read-only tools and three `git -C pr` commands and no use for a token; the script itself needs it before and after, for `gh api` and for `post-review.mjs`.

- `factory-token.mjs` exports `withoutToken(env)`: the given environment without `GH_TOKEN`, `GITHUB_TOKEN` and the `GIT_CONFIG_COUNT`, `GIT_CONFIG_KEY_n` and `GIT_CONFIG_VALUE_n` variables `childEnv` set. It is the inverse of `childEnv` and sits next to it; the commit identity variables stay, they are not credentials.
- `local-review.mjs` imports it and passes `env: withoutToken(process.env)` to the one `run('claude', …)` call. Nothing else in that script changes. The helper is safe to import: it runs as a command only when it is the entry script.
- Tests in the helper's test file: `withoutToken(childEnv(env, …))` holds none of the variables and keeps the rest.

The function is in the helper and not in the review script because the review script runs on import and cannot be loaded by a test.

### Alternatives rejected

| Alternative | Why not |
|---|---|
| The key itself in an environment variable | Rejected by the owner: every child process inherits it, and it leaks into process dumps and logs |
| A passwordless operating-system credential store (Credential Manager, libsecret, Keychain) | Rejected by the owner: different code for each system, and on Windows and Linux any process of the user still reads it silently. `--key-cmd` keeps the route open |
| A key manager's CLI | Rejected by the owner: not passwordless, so no unattended run. `--key-cmd` keeps the route open |
| The key on standard input, from the clipboard (plan 0001 as merged) | Needs a person for every command, and the clipboard is readable by everything meanwhile. Reversed by the owner's decision |
| A machine user with a fine-grained token (the fallback of `18` §4) | A long-lived token on disk with a person-like identity; the app's installation token lives one hour and is scoped per run |
| Keeping the reviewer's key away from the authoring session: a second operating-system user, or a second machine | It would make the separation a control. The owner chose to accept the risk until the reviews are back in CI, which is where the separation belongs (deviation 3) |
| The helper inside the kit, `docs/spec/factory/.github/factory/` | The App IDs and variable names are this repository's. `.github/scripts/` is where its own two scripts are; see deviation 4 |
| The helper as one ticket, or as two | About 505 lines with its tests by the estimates below, some 190 of code and 315 of tests: over `size.soft` twice. Cut into minting, key loading, the command's environment and the command line, each reviewable by itself and each with headroom |
| A default key path built into the helper | The file names carry a date; a guessed path fails late or picks an old key |
| Looking the bot's user id up at run time (`GET /users/<slug>%5Bbot%5D`) | One more call and one more failure on every run, for a value that does not change. Taking it from the policy: the policy has no such field and no reviewer entry, and adding them is a policy change |
| Leaving the commit author to the user's git configuration | Refused by the owner: the commits of a ticket would carry a person's name and address, against ADR-0029 §8 |
| Reading `FACTORY_PROJECT_*` from the repository variables in the helper | The factory app's token cannot: it has neither the Variables nor the Actions permission, and widening the app for two values that never change is the wrong trade |
| Turning on the Claude Code sandbox to enforce the key folder rule | The control that would hold (research §3), but it changes how every local session runs and is a decision of its own. Not in this plan |
| Making Project errors in `create-tickets.mjs` warnings | A ticket outside the Project is invisible to the orchestrator; hiding the error would trade a loud failure for a silent one |
| Looking the item up through GraphQL (`resource(url:)`, `projectItems`) | A second API surface to verify; `item-list` is already used by `statuses()` and its output was read from the live Project |
| Changing the sub-issue call | It is correct and it worked (research §4) |
| The deny rules folded into the instructions ticket | A guard file change and a text change are two concerns; apart, each diff is one thing to approve |
| The size limits: policy in one ticket, the texts in a second | The owner's decision is one concern, the limit, and the whole is about 45 lines. Two tickets would leave the instructions and the gate disagreeing in between |
| The size limits: also editing the ADRs and `17`, `18` | They record the decision and the starting values, and the limits return to them at `1.0.0`; a temporary value does not belong in a decision record |
| The size limits: `size-accepted` on each oversize pull request instead | It is what the owner found too slow: a human label for every reasonable change over 400 |
| Owner findings: a fifth severity, "Owner" | Severity says how bad a finding is; who must answer it is a second property. A deviation for the owner can be grave or slight, and the summary should still say which |
| Owner findings: leave the prompt and have `verdictOf` skip any Major whose text mentions the owner | A check that parses prose; the marker must be a field |
| Owner findings: make `factory/doc-review` pass whenever the plan is flagged | It would also wave through defects the author must fix; only the questions for the owner move to the plan gate |
| Owner findings: the same field for `factory/review` | Not needed: tier high already waits for a human, and the code review has no finding addressed to the owner |
| The token in the reviewer session: leave it an open point | It was one; T4 puts the token into every wrapped command, and the fix is three lines in a script this plan already builds on |
| The token in the reviewer session: a function inside `local-review.mjs` | That script runs when imported, so no test could reach the function |
| The test step also in the kit's copy of `factory-gates.yml` | The step runs tests of `.github/scripts/`, which a repository that installs the kit does not have. The installed workflow already differs from the kit's by design: the provenance header, the rewritten paths, job `references` |

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
| Docs | none | none. Not part of the layers, all guard paths: the kit (`docs/spec/factory/.github/factory/project.mjs`, `create-tickets.mjs`, `tests/project.test.mjs`, `docs/spec/factory/README.md`, `docs/spec/factory/.claude/settings.json`); this repository's scripts (`.github/scripts/factory-token.mjs`, `.github/scripts/tests/factory-token.test.mjs`); one step in `.github/workflows/factory-gates.yml`; the generated `.claude/settings.json`; the root `AGENTS.md`; for T9 the document review's schema, prompt and poster (`doc-review.schema.json`, `prompts/doc-review.md`, `post-review.mjs`, `tests/post-review.test.mjs`); for T10 `.github/scripts/local-review.mjs`; for T8 the policy (`docs/spec/factory/.github/factory/policy.json`), one kit test, and one line in each of the bundle's README and root instructions, six skills and the plan template |

Nothing under `src/`, `tests/`, `samples/`, `templates/` or `docs/` outside `docs/spec/` exists yet, and nothing there is created.

## Public API, schema and error-code delta

- **Added** none
- **Changed** none
- **Removed** none

Breaking: no.
New dependency: no. The helper and the tests use Node's standard library only.

The environment variables `YAF_FACTORY_KEY_FILE` and `YAF_REVIEWER_KEY_FILE` are names of this repository's tooling, not configuration keys of a `YAF.*` package; no approved file records them.

```factory:delta
{ "publicApi": [], "schema": [], "errorCodes": [], "contracts": [], "configuration": [], "telemetry": [], "dependencies": [] }
```

## Schema and migration plan

none

## Test strategy

No test project of the solution is involved; there is no solution yet. The tests are Node's built-in runner, at the level ADR-0018 calls unit: pure functions and injected fakes, no network, no real key.

| Scenario | Requirements | Ticket | Level (unit / integration / architecture / conformance) | Test project | Notes |
|---|---|---|---|---|---|
| `addItem` returns the existing item's id when `item-add` reports "already exists in this project" | L1 | T1 | unit | `docs/spec/factory/.github/factory/tests/project.test.mjs` | Fake `gh` runner; runs in CI job `factory-self-test` |
| `addItem` reads the list again when the item is not in it yet, and finds it on a later read | L1 | T1 | unit | same | Fake wait: the test does not sleep |
| `addItem` rethrows another error, and the same error when no item matches after the last read | L1 | T1 | unit | same | |
| An existing open ticket with no status gets its first status; one with a status, or a closed one, is left alone | L1 | T1 | unit | same | The exported pure function of `create-tickets.mjs` |
| A re-run for plan 0001 creates T2 and duplicates neither #7 nor #8 | L1 | T1 | none (post-merge check by the owner) | none | V7 |
| The JWT verifies against the generated public key and carries `iss`, `iat`, `exp` as specified | L2 | T2 | unit | `.github/scripts/tests/factory-token.test.mjs` | Key pair generated in the test |
| `mintToken` calls the two endpoints with the bearer JWT and asks for the one repository | L2 | T2 | unit | same | Fake `fetch` |
| A failed call's error holds the status and GitHub's message and no part of the key, the JWT or a token | L2 | T2 | unit | same | Fake `fetch` returning 401 and 404 |
| The helper's tests run on every pull request | L6 | T2 | none (CI) | none | A step in `factory-self-test`; the pull request's own run shows it |
| A key file with mode `0644` or `0640` is refused on `linux` and `darwin`; `0600` is accepted; `win32` gives no refusal | L3 | T3 | unit | same | `keyFileProblem` takes mode and platform, so every case runs on every system |
| The path comes from the app's variable; `--key-file` overrides it; `--key-cmd` overrides both; both flags together fail; no source fails naming the variable | L3 | T3 | unit | same | Temporary files; the generated key |
| Content that is not a PEM private key is refused and not echoed | L3 | T3 | unit | same | |
| `repoFromRemote` for HTTPS and SSH URLs, with and without `.git` | L4 | T4 | unit | same | |
| `childEnv` sets the token variables and the two `credential.helper` entries and refuses an existing `GIT_CONFIG_COUNT` | L4 | T4 | unit | same | |
| `childEnv` makes the app the author and the committer: name `<slug>[bot]`, address built from the bot's user id and the slug, for each app | L4 | T4 | unit | same | Asserted against the parts of `APPS`, so the test file holds no address |
| `childEnv` leaves every other variable as it was, `FACTORY_PROJECT_OWNER` and `FACTORY_PROJECT_NUMBER` included, and does not replace a `GITHUB_REPOSITORY` that is set | L4 | T4 | unit | same | |
| Arguments: each flag, the command after `--`, and each usage error | L5 | T5 | unit | same | |
| A `git` command is refused when `origin` is an SSH URL, with the reason and the `set-url` command in the message; it is allowed for an HTTPS `origin`; `gh` and `node` are allowed for either | L5 | T5 | unit | same | The exported function, with URLs of both forms |
| `runCommand` returns the child's exit code (`node -e "process.exit(3)"` gives 3), hands the child the environment it was given (a child that exits 0 only when `GH_TOKEN` equals a made-up value), and returns 1 with a message for a command that does not exist | L5 | T5 | unit | same | The child is `process.execPath`; a made-up token, no key, no network |
| The command line exits 2 with the usage text and makes no network call when no command is given | L5 | T5 | unit | same | Starts the script as a child process with no key configured |
| A real token: `gh api`, `git ls-remote` and the author of a commit, as the app | L2–L5 | T5 | none (manual, by the owner) | none | V6; needs the keys, so no agent and no CI job runs it |
| The settings hold the two rules, and the generated copy is in step | L7 | T6 | none (check) | none | V3, V4 |
| The `AGENTS.md` section exists, names both commands, the folder, the four variables and the rule of procedure, and holds no secret | L8 | T7 | none (documentation check) | none | V5, V8 |
| The policy holds soft 400 and hard 1000, and validates against its schema | L9 | T8 | none (check) | none | V10 |
| "ticket list problems a script can decide" still reports an estimate over the soft limit, with the limit taken from the policy; "soft and hard limits" passes unchanged | L9 | T8 | unit | `docs/spec/factory/.github/factory/tests/delta.test.mjs`, `size.test.mjs` | V1 |
| Each of the ten texts that state the limits keeps the kit's default and names 400 and 1000 for the initial build | L9 | T8 | none (documentation check) | none | V10 |
| A document review whose only Major findings are for the owner passes; its title counts them | L10 | T9 | unit | `docs/spec/factory/.github/factory/tests/post-review.test.mjs` | `verdictOf` with fixture verdicts |
| A Major or Blocker without the marker still fails the document review, with or without owner findings beside it; a failed checklist row and a `changes-required` verdict still fail | L10 | T9 | unit | same | |
| On `factory/review` a finding fails by its severity whether or not it carries the marker | L10 | T9 | unit | same | |
| The summary lists owner findings under "For the owner" and not under "Findings" | L10 | T9 | unit | same | `renderSummary` |
| The schema allows `for: "owner"` on a finding, no other value, and does not require it | L10 | T9 | unit | same | Reads the schema file |
| `withoutToken` removes the token variables and the `GIT_CONFIG_*` entries and keeps everything else, the commit identity included | L11 | T10 | unit | `.github/scripts/tests/factory-token.test.mjs` | |
| `local-review.mjs` gives the reviewer session an environment without the token | L11 | T10 | none (check; `--dry-run` by the orchestrator) | none | V12 |

Where the tests run in CI: job `factory-self-test` of `.github/workflows/factory-gates.yml` runs the kit's tests today. T2 adds one step to it, `node --test ".github/scripts/tests/*.test.mjs"`. The job is not a required check (research §5). The step is added to the installed workflow only; the kit's copy, `docs/spec/factory/.github/workflows/factory-gates.yml`, is not changed (decision log).

## Risks and rollback

| Risk | Likelihood | Mitigation | Rollback |
|---|---|---|---|
| **A pull request is reviewed by the session that wrote it.** On one machine, as one user, one session can mint the factory token and the reviewer token. A pull request in tier low or medium could then pass `factory/review` and merge automatically with no human having seen it. The two apps are kept apart by procedure only (deviation 3) | Low that it happens by accident; nothing prevents it | Accepted by the owner (decision log). The procedure: the implementer never uses `--app reviewer` or runs `local-review.mjs`; the orchestrator, which holds both identities, runs the review for the pull requests it opened, in a fresh reviewer session; `AGENTS.md` states it. A tier-high pull request and a flagged plan still need the owner, whom no token can stand in for; all ten tickets of this plan are tier high. The daily digest lists every automatic merge | Move the reviews back to CI (the `AGENTS.md` section lists the steps) and remove the reviewer entry from the helper. A merge that should not have happened is reverted through a pull request |
| An app key is read by something it was not meant for: the keys are files, and any process running as the user, agents included, can read them (deviation 2) | Medium | Folder outside every repository, readable by the user only; variables hold paths; the helper refuses a file readable by group or others on Linux and macOS; a deny rule for the agents' file tools; no message ever holds the key. None of this stops a process of the user that sets out to read the file. The owner accepts this | Delete the key on the app's settings page, generate a fresh one, update `FACTORY_APP_PRIVATE_KEY` for the factory app. A revert does not undo an exposure |
| Ticket creation still runs the unfixed script until T1 has merged, and every run can stop in the same race (research §4). After #9 the run created #10 (0001's T2), epic #11, #12 (T1) and #13 (T2) and failed on `item-add` for #13; T3 to T8 did not exist after it. Merging this update runs the workflow again for this plan | High | T1 is issue #12 and is `Ready`: the orchestrator works it first. A run that fails has still created the issues before the failure, each found again by its marker, so re-running loses nothing and may add more. Until T1 has merged, `status.mjs set` may fail with the same error for an issue that was added moments before, because it goes through the same `addItem`. That is expected: the orchestrator notes it on the issue and does not retry at once. After T1 merges, V7 creates what is missing, T9 and T10 included, and gives a status to tickets left without one | Nothing to roll back: issues created by a failed run are the right issues |
| A re-run within seconds of a failed one creates a duplicate, because issue search lags (kit README, "Verify before relying on it") | Low | V7 waits a minute between runs and counts the issues per marker | Close the duplicate by hand; the marker of the older issue wins on later runs |
| `addItem` returns the wrong item, or the item is still missing after three reads | Low | The match is on the exact issue URL the caller passed; no match after the last read rethrows the original error, as loudly as today | Revert T1; re-run later |
| The first-status rule moves a ticket someone deliberately left without a status | Low | Only open tickets with no status at all, and only to the status creation would have given | Set the status by hand |
| The helper leaks a token or key into a log through an error path | Low | Errors are fixed text plus status, path and GitHub's `message`; a test asserts that a failing call's error holds no part of the key, the JWT or the token; `--print` is the only output of a token and is denied to agents by T6 | Revoke: delete the key on the app's settings page. A token expires within the hour |
| `--key-cmd` runs a shell command | Low | Flag only, never read from a variable or a file; what runs is what the person typed | Remove the flag in a follow-up ticket |
| A commit made outside the helper carries the user's name and address | Medium | `AGENTS.md` says that a ticket's commits go through the helper; the squash merge is made by GitHub with the pull request's author, the app, as author; the reviewer sees the commits of the branch | Amend the branch before the merge |
| `origin` is an SSH remote, so a `git push` through the helper would go out with the user's SSH key and carry the person's identity, while the token sits unused | Medium: SSH remotes are common | The helper refuses a `git` command when `origin` is not HTTPS, before minting, and says how to switch; a test covers it; `AGENTS.md` says that `origin` must be HTTPS. Not covered: a push to a remote other than `origin`, or `git -c` tricks; the reviewer sees who pushed | Switch the remote; a wrongly attributed push is visible on the pull request and is redone |
| The deny rules are read as a control | Medium | T7 and the kit README's item 7 say what they are: they stop accidents. The controls that hold are the app's permissions and the ruleset | Correct the wording |
| The two deny rules are refused or ignored by a later Claude Code version | Low | Syntax checked against the current documentation (research §3), version recorded; the rules sit in a list that already uses both forms | Revert T6 |
| A ticket grows beyond its estimate | Medium for T5 | The helper is four tickets with estimates of 135, 100, 110 and 160 against 200, counted with blank lines and comments; an implementer who passes 200 stops and comments on the ticket | Split by a plan update |
| The test step added to `factory-gates.yml` breaks the job, for example because Node 22 treats the dot-folder pattern differently | Low | The step next to it already passes a quoted pattern to `node --test` on Node 22 and runs the kit's tests, though that pattern's own dot-folders are named, as here; the pull request's own run uses the changed workflow and shows it; the fallback is to name the test file | Revert the step |
| A red `factory-self-test` does not block a merge, because the check is not required | Certain | All ten tickets are tier high: the owner sees the checks before approving. Making the job required is an open point for the owner (research §6) | none needed |
| Plan 0001's runbook goes stale against this plan: its research counts 31 kit tests and four workflow changes | Medium | 0001's implementers re-read each cited file and the repository wins (plan 0001, Approach); this plan's pull request updates 0001's key handling text, and the key setup commands exist once, in `AGENTS.md` | A follow-up docs ticket |
| The local review, and with it the reviewer half of the helper, is temporary | Certain | `AGENTS.md` lists what goes when the reviews move back; T7 adds the reviewer entry to that list | Remove it then |
| With soft 400 and hard 1000, pull requests grow: a review of 400 to 1000 lines finds fewer defects than one of 200 (`yaf-writing-plans`: "a reviewer finds defects in 200 lines and misses them in 2 000"), the reviewer's question "why was this not split" moves from 200 to 400, a plan may estimate a ticket at up to 400, and `factory/size` stops only above 1000. In tier low and medium such a pull request merges with no human | High: it is the purpose of the change | The owner's decision, for the initial build only. One ticket stays one concern; the digest reports sizes daily; sensitive paths stay tier high at any size; the planner still slices small where it can. The limits return to 200 and 400 at `1.0.0` (decision log) | Revert T8: one pull request, tier high |
| The return to 200 and 400 at `1.0.0` is forgotten | Medium | Recorded as an open point for the release-preparation plan; the ten lines T8 changes keep the default and say "until `1.0.0`" where agents read them | A ticket then |
| T8's change to a kit test is taken for a loosened assertion (hard rule 1) | Low | The assertion is unchanged; only the fixture's number stops being a constant that the new limit overtakes. The diff says so, and the ticket is tier high: the owner sees it | Revert T8 |
| T1 and T8 both change `docs/spec/factory/README.md`, in different paragraphs | Certain | Their `Touches` say so: the orchestrator runs them one after another | Rebase the later one. T9 adds kit tests and does not touch the README, because T1 removes the count |
| The owner marker is used for a finding the author should fix, and a defect passes the document review | Low | The prompt limits the marker to checklist item 10; the finding stays in the summary under "For the owner"; a plan with a deviation is always flagged, so a human approves it and reads that section; a spec always needs a code owner. An unflagged plan cannot have such a finding honestly: no deviation, no question | Revert T9; the check is as strict as before |
| Until T9 has merged, a flagged plan with a security, scope or contract deviation cannot pass `factory/doc-review`, whatever the owner answers. That holds for the pull request of this update too: it still lists deviations 2, 3 and 6 | Certain for this update; medium for other plans | The owner decides how such a pull request merges, as for #9; no agent takes part in that. T9 depends on nothing and is small: the orchestrator works it first. The prompt, schema and poster are read from the base branch, so T9 helps only pull requests reviewed after it has merged | none needed |
| The reviewer session needs something `withoutToken` removed | Low | Its tools are Read, Grep, Glob and three local `git -C pr` commands, none of which authenticates; V12's dry run shows a verdict | Revert T10 |

## Verification commands

Run in Git Bash from the repository root, on the ticket's branch. There is no solution yet, so `dotnet build` and `dotnet test` do not apply. `BASE` is `origin/main`, or the branch below for a stacked ticket (T3 on T2, T4 on T3, T5 on T4).

V1, the kit's tests (T1; and every ticket, to show nothing else moved):

    node --test "docs/spec/factory/.github/factory/tests/*.test.mjs"

Expected: exit 0. Before T1: 31 tests. After T1: more than 31, 0 fail; `tests/project.test.mjs` is among the files run.

V2, the helper's tests (T2 to T5, T10):

    node --test ".github/scripts/tests/*.test.mjs"

Expected: before T2 the pattern matches no file, and `node --test` then runs 0 tests and still exits 0 (tried on Node v24.21.0), which is why the `Verify` cells first test that the file exists. The cells of T3, T4, T5 and T10 also test for the export the ticket adds (`loadKey`, `childEnv`, `parseArgs`, `withoutToken`, each written as `export function <name>`), so that a stacked ticket's command cannot pass on the ticket below it. After each of T2 to T5: exit 0, 0 fail, and the tests the ticket names are in the output.

V3, the generated references (every ticket; T6 changes them):

    node .github/scripts/sync-references.mjs --check

Expected: `… reference(s) to docs/spec/ are in step`, exit 0.

V4, the deny rules (T6):

    grep -cF -e "Read(~/.wtf-corp/yaf-factory/**)" -e "factory-token.mjs *--print*" .claude/settings.json docs/spec/factory/.claude/settings.json

Expected: `2` for each of the two files. Before T6: `0` for each, exit 1 (tried on 2026-11-19). Both files parse: `node -e "JSON.parse(require('fs').readFileSync('.claude/settings.json','utf8'))"` exits 0.

V5, the instructions (T7):

    for p in "^## Running the factory locally" "factory-token.mjs --app factory" "factory-token.mjs --app reviewer" "~/.wtf-corp/yaf-factory/" "YAF_FACTORY_KEY_FILE" "YAF_REVIEWER_KEY_FILE" "FACTORY_PROJECT_OWNER" "FACTORY_PROJECT_NUMBER" "remote set-url origin https://"; do grep -q -e "$p" AGENTS.md || echo "missing: $p"; done
    grep -q "must be kept where agents do not read it" AGENTS.md && echo "the withdrawn condition is still there"

Expected: no output from either. One `grep -q` per pattern, so a line that holds two of them cannot hide a missing one. Before T7 the first prints nine `missing:` lines and the second prints its message (tried on 2026-11-19).

V6, a real token, by the owner in their own terminal, after T5 (no agent, no CI):

    node .github/scripts/factory-token.mjs --app factory -- gh api repos/wtf-corp/yaf-backend-framework --jq .full_name
    node .github/scripts/factory-token.mjs --app factory -- gh api user
    node .github/scripts/factory-token.mjs --app reviewer -- git ls-remote origin HEAD
    node .github/scripts/factory-token.mjs --app factory -- git var GIT_AUTHOR_IDENT

Expected: the first prints `wtf-corp/yaf-backend-framework`. The second **fails**, and the helper exits with `gh`'s exit code: an installation token cannot read `/user`, a person's token can (`local-review.mjs` uses the same test). The third prints one commit id without asking for credentials. The fourth prints `yaf-factory[bot]`, the bot's noreply address and a time; the address is the one the app's commits on `main` already carry (`git log -1 --author="yaf-factory" --format=%ae origin/main`). The result is a comment on T5's issue.

V7, the re-run, by the owner after T1 has merged (the factory app cannot dispatch workflows):

    gh workflow run factory-tickets.yml --ref main -f plan=specs/0001-factory-setup-runbook/plan.md
    gh workflow run factory-tickets.yml --ref main -f plan=specs/0002-local-factory-operation/plan.md

Run the second a minute after the first has finished. Expected for 0001: the run succeeds; its log says `T1 exists as #8` and `T2 exists as #10`; no new epic. Then one issue per marker and no more:

    gh issue list --state all --search '"factory:ticket specs/0001-factory-setup-runbook" in:body' --json number --jq length
    gh issue list --state all --search '"factory:epic specs/0001-factory-setup-runbook" in:body' --json number --jq length

Expected: `2` and `1` (they already do since the run after #9, which created #10; before it they printed `1` and `1`). For 0002: `10` and `1`. Every ticket has a status in the Project. The result is a comment on T1's issue.

V8, nothing secret, no email address, no claim. For `AGENTS.md` (T7), the strict scan:

    grep -nE -e "BEGIN [A-Z ]*PRIVATE KEY" -e "sk-ant-" -e "gh[pousr]_[A-Za-z0-9_]{20,}" -e "github_pat_" -e "[A-Za-z0-9._%+-]+@[A-Za-z0-9-]+\.[a-z]{2,}" AGENTS.md
    grep -niE -e "compliant" -e "certified" AGENTS.md

Expected: no output, exit 1, for each.

For the helper (T2 to T5), a scan that lets the code name what it checks for. The helper tests a key for its PEM header and parses SSH remote URLs, so the header text and `git@github.com` are in it by design; what must not be in it is a key's body, a token or any other address:

    grep -nE -A1 -e "BEGIN [A-Z ]*PRIVATE KEY" .github/scripts/factory-token.mjs | grep -cE "^[0-9]+-[A-Za-z0-9+/=]{40,}$"
    grep -noE -e "sk-ant-[A-Za-z0-9]" -e "gh[pousr]_[A-Za-z0-9_]{20,}" -e "github_pat_[A-Za-z0-9]" -e "[A-Za-z0-9._%+-]+@[A-Za-z0-9-]+\.[a-z]{2,}" .github/scripts/factory-token.mjs | grep -cvF -e "git@github.com"

Expected: `0` for each. The first counts a line of 40 or more base64 characters straight after a PEM header; the second counts token shapes and addresses other than the SSH user of a remote URL. The noreply address is built from its parts in a template, where `]` stands before the `@`, so it does not match and needs no exception. Both were tried on 2026-11-19 against mock files: a file with the header test, the URL pattern and the template gives `0` and `0`; a header followed by a body line gives `1`; a literal address gives `1`. The helper's test file is not scanned: it holds fake token strings its assertions look for; the reviewer reads it for a real key.

V9, scope and size (every ticket):

    git diff --name-only $BASE...HEAD
    node docs/spec/factory/.github/factory/size.mjs $BASE HEAD

Expected: only the paths in the ticket's `Touches`; `N changed lines (soft S, hard H)` with no "over the … limit" suffix, where S and H are the policy's limits on the branch: 200 and 400 before T8 has merged, 400 and 1000 after. Whichever applies, no ticket of this plan passes 200.

V10, the size limits (T8):

    node -e "const p=require('./docs/spec/factory/.github/factory/policy.json');process.exit(p.size.soft===400&&p.size.hard===1000?0:1)"
    grep -LE "(400|1000).*initial build|initial build.*(400|1000)" docs/spec/README.md docs/spec/agents/AGENTS.root.md docs/spec/factory/README.md docs/spec/agents/skills/yaf-commits-and-pull-requests/SKILL.md docs/spec/agents/skills/yaf-implementing-tickets/SKILL.md docs/spec/agents/skills/yaf-orchestrating-work/SKILL.md docs/spec/agents/skills/yaf-reviewing-changes/SKILL.md docs/spec/agents/skills/yaf-reviewing-specs-and-plans/SKILL.md docs/spec/agents/skills/yaf-writing-plans/SKILL.md docs/spec/agents/skills/yaf-writing-plans/references/plan-template.md

Expected: the first exits 0; before T8 it exits 1 (tried on 2026-11-19). The second lists the files that have no line naming 400 or 1000 together with "initial build": it prints nothing after T8, and all ten files before it (tried on 2026-11-19). The reviewer reads each changed line for the kept default, 200 and 400. Then V1 and V3: the kit's tests pass with the new limits, 0 fail, and the references are in step.

The `Verify` cell of each ticket holds a command only, because `create-tickets.mjs` renders the cell as a code block in the issue. The full verification of a ticket is the V-commands this section names for it.

V11, owner-addressed findings (T9):

    node --test "docs/spec/factory/.github/factory/tests/*.test.mjs"
    node -e "const s=require('./docs/spec/factory/.github/factory/doc-review.schema.json');const f=s.properties.findings.items;process.exit(f.properties.for&&f.properties.for.enum.join()==='owner'&&!f.required.includes('for')?0:1)"
    grep -c "report it as a Major finding addressed to them" docs/spec/factory/.github/factory/prompts/doc-review.md

Expected: the first exits 0 with `tests/post-review.test.mjs` among the files run. The second exits 0; before T9 it exits 1. The third prints `0`; before T9 `1` (tried on 2026-11-19, the last two). After the merge the proof is the next flagged plan: its document review passes with its deviations listed under "For the owner", and `factory/plan-gate` holds it until the owner approves.

V12, the reviewer session's environment (T10):

    grep -c "withoutToken(process.env)" .github/scripts/local-review.mjs
    node .github/scripts/local-review.mjs <pr-number> --dry-run

Expected: `1`; before T10 `0`. The dry run, by the orchestrator on any open pull request, prints a verdict as before and posts nothing. Then V2.

## Research

[`research.md`](./research.md): where the keys are and what was verified; the token procedure, the commit identity and what was checked for the helper in Node and git; the Claude Code permission rule syntax and its limits; why the ticket creation of plan 0001 stopped, read from the logs of both runs and the live issues; how the gates classify this plan and its tickets; open points; what reads and what states the size limits; how the document review's verdict becomes a check.

## Decision log

| Date | Decision | Reason | By |
|---|---|---|---|
| 2026-11-19 | **Key storage, option A.** Each GitHub App private key is a file in a private per-user folder outside every repository, readable by the user only: `chmod 600` on Linux and macOS, an ACL for the user on Windows. Environment variables hold the paths, never the key: `YAF_FACTORY_KEY_FILE` (app `yaf-factory`, ID 5834995) and `YAF_REVIEWER_KEY_FILE` (app `yaf-reviewer`, ID 5837737) | Local operation without a prompt on three operating systems. Rejected: the key itself in an environment variable (inherited by every child process, leaks into dumps and logs); a passwordless operating-system credential store (different code per system, still readable silently on Windows and Linux); a key manager's CLI (not passwordless). The owner explicitly accepts the security responsibility: any process running as the user, agents included, can read the file. It departs from the default of `18` §4: deviation 2 | Human owner, 2026-11-19, relayed by the orchestrator |
| 2026-11-19 | The helper keeps a `--key-cmd` flag, a command that prints the key, as the route to a credential store or a key manager later | It costs a few lines now and a later move needs no change to the helper | Proposed by the orchestrator; part of the option the owner chose, whose description included it |
| 2026-11-19 | The folder is `~/.wtf-corp/yaf-factory/` on all three systems; the files keep GitHub's download names; the variables are set at user level with full paths. On Windows the folder's ACL has inheritance removed. Tokens were minted from both files for both apps, each scoped to this repository | The owner set it up and verified it | Human owner, 2026-11-19, relayed by the orchestrator |
| 2026-11-19 | **One local session may hold both identities: accepted and recorded.** The orchestrator holds both identities: its session on the maintainer's machine mints the factory token and the reviewer token, and it runs the review of pull requests it opened itself. The owner's acceptance covers exactly that. The rule that remains is that the implementer never runs the review. Because one session can mint both, so a pull request in tier low or medium could pass `factory/review` and merge automatically with no human. The separation between the identity that opens a pull request and the one that passes its review holds by that rule only, until the reviews move back to CI | The owner's answer to the plan review's first finding: "Accept it, record it". The reviews cannot run in CI until the account behind `factory-review` works; a second app, with the checks pinned to it in the ruleset, is still better than one identity for both. Deviation 3; first row of the risks | Human owner, 2026-11-19, relayed by the orchestrator |
| 2026-11-19 | **Commits are authored as the app.** The helper sets `GIT_AUTHOR_NAME` and `GIT_COMMITTER_NAME` to `<app-slug>[bot]` and `GIT_AUTHOR_EMAIL` and `GIT_COMMITTER_EMAIL` to the bot account's noreply address, `<bot-user-id>+<app-slug>[bot]@users.noreply.github.com`. In T4, with tests | The owner's answer: "Yes, author as the app". ADR-0029 §8: agents authenticate as the app, "never as the human", and "agent work must not carry a person's identity" | Human owner, 2026-11-19, relayed by the orchestrator |
| 2026-11-19 | The bot user ids are constants in the helper's `APPS`, next to the App IDs: `358792446` for `yaf-factory[bot]`, `358867400` for `yaf-reviewer[bot]` | The owner left the choice to the plan: a run-time lookup, the policy, or an option. An account's id does not change; a lookup adds a call and a failure to every run; the policy has no such field and no reviewer entry, and an option is one more thing to get wrong. Both ids were read from `GET /users/<slug>%5Bbot%5D`, and the factory one equals the id in the address of the app's commits on `main` (research §2) | Planner |
| 2026-11-19 | `FACTORY_PROJECT_OWNER` and `FACTORY_PROJECT_NUMBER` are set by the user as environment variables; T7 says so, and the helper passes them on without fetching them | `status.mjs` and `project.mjs` need them. They are repository variables, and the factory app has neither the Variables nor the Actions permission (research 0001 §3, item 5), so its token is not expected to read them; widening the app for two fixed values is the wrong trade | Planner, on the plan reviewer's finding |
| 2026-11-19 | This reverses plan 0001's record (keys in a key manager, passed on standard input). Plan 0001 and its research are updated in this plan's pull request: C7 and its budget, research §5 and §6, the risks, the decision log, a fourth deviation. The key setup commands exist once, in `AGENTS.md` (T7); 0001's runbook points there | 0001's T2 writes the runbook from those files; they must describe what is in use, and two copies of the commands would drift | Human owner (the reversal); planner (the edits) |
| 2026-11-19 | **Through the factory.** Small tickets, reviewed locally, approved by the owner | The owner's decision. Every ticket here is tier high, so each waits for the owner | Human owner, 2026-11-19, relayed by the orchestrator |
| 2026-11-19 | **The size limits are raised for the initial build: `size.soft` 400, `size.hard` 1000, until `1.0.0`.** A new, independent ticket of this plan, T8 | In the owner's words: "lift the pr lines limit for the initial dev. when hard 400 is difficult to not exceed, and a change is reasonable"; the values and "Until 1.0" are the owner's choice. Deviation 6 | Human owner, 2026-11-19, relayed by the orchestrator |
| 2026-11-19 | The limits return to 200 and 400 at `1.0.0`: the release-preparation plan, the last one before the `1.0.0` pull request of `17` §3, carries a ticket that reverses T8. **Open point for that milestone** | Part of the owner's decision ("until 1.0"). Recorded here because nothing else would remind anyone | Human owner (the term); planner (where it is recorded) |
| 2026-11-19 | T8 changes the policy, the one kit test whose fixture the new limit overtakes, and the ten lines that state the numbers; it leaves the ADRs, `17`, `18` and the history record as they are | Policy and prose are one concern. The ADRs record the starting values, to which the limits return | Planner, on the orchestrator's direction |
| 2026-11-19 | In those ten lines the kit's stated defaults, 200 and 400, stay; each line adds that this repository uses 400 and 1000 during the initial build | The change is temporary and local: the kit's default does not change, only this repository's policy value. At `1.0.0` the added clause is removed and the lines are true again as they stand | The orchestrator's choice, answering the plan review |
| 2026-11-19 | **Findings addressed to the owner do not fail `factory/doc-review`; the plan gate carries them.** A new, independent ticket, T9 | The kit's prompt reports a security, scope or contract deviation as a Major for the owner, `verdictOf` fails on any Major, and the check is required: a flagged plan with such a deviation could never merge, however the owner answers. `factory/plan-gate` already demands a maintainer's approving review on a flagged plan, and that review is where the answer belongs | Human owner, 2026-11-19, relayed by the orchestrator |
| 2026-11-19 | T9's design: an optional field `for: "owner"` on a finding of the document review; the severity stays; `verdictOf` leaves such findings out of the count for `factory/doc-review` only; `renderSummary` lists them under "For the owner". `factory/review` is not changed | The smallest change that keeps who must answer apart from how grave a finding is. Code pull requests have `factory/human-approval` for tier high and no finding class for the owner | Planner, within the owner's decision |
| 2026-11-19 | The first version of this plan merged as #9. What came after, T9, T10, the tested command runner in T5 and the corrected first check of T7, is a plan update in a pull request of its own, which passes the plan gate again | The owner merged #9 while the answers to its document review were being written. ADR-0025: when the approach or the ticket list changes after the merge, the plan is updated through a pull request | Human owner (the merge); orchestrator (the update) |
| 2026-11-19 | T1 to T8 keep their keys and their issues; the update's merge creates issues for T9 and T10 only. An existing issue keeps the body it was created with. Where an issue's body and the row of the plan on `main` differ, the plan wins: the issue links to its row | `create-tickets.mjs` finds a ticket by its hidden marker and, when it exists, only logs "exists as #n": it edits neither body nor labels (read in the script, and seen in run 44615242471: "T1 exists as #8"). Rewriting bodies would be a change to the kit that no one asked for, and comments on the issue may already refer to the old text | Planner, on the orchestrator's direction |
| 2026-11-19 | The test step of T2 goes into the installed `.github/workflows/factory-gates.yml` only, not into the kit's copy under `docs/spec/factory/.github/workflows/` | It runs tests of `.github/scripts/`, this repository's own scripts, which a repository installing the kit does not have. The installed file already differs from the kit by design (provenance header, rewritten paths, job `references`); this is one more such difference, and the runbook of plan 0001 lists them | Planner, answering the document review |
| 2026-11-19 | Removing the token from the reviewer session's environment is in this plan, as T10. It was an open point | T4 puts the token into every wrapped command, the review included, and the fix is small: one exported function next to `childEnv` and one argument in `local-review.mjs` | Planner, on the orchestrator's recommendation |
| 2026-11-19 | This plan's estimates stay at or under 200 and its split stands after T8 | `factory/plan-gate` judges this pull request by the base branch's policy; and small tickets for key-handling code are wanted whatever the limit | Orchestrator's direction; planner |
| 2026-11-19 | No `spec.md`; the plan cites ADR-0029, the kit README and `18` §4 | Factory operation, like plan 0001: no behaviour of YAF and no requirement beyond what those documents hold. Listed as deviation 1; the owner accepts it by approving this plan's pull request, and by nothing else | Orchestrator's brief; planner |
| 2026-11-19 | Folder `0002-local-factory-operation` | The branch has this name. `18` §5 suggests `0002-results-and-errors`; that list is a suggestion, and milestone plans take the next free numbers | Human owner (branch) |
| 2026-11-19 | Seven tickets where the brief expected three (ten in the end: T8 and T9 by the owner's decisions, T10 from the review): the helper is four stacked tickets (T2 minting, T3 key loading, T4 the command's environment, T5 the command line); the deny rules (T6) are apart from the instructions (T7); the kit fix is T1, not last | The helper with its tests is about 505 lines by the estimates, and the command part alone passed 185 once the commit identity was added. A guard file and a text are two concerns. T1 first, because merging this plan runs the unfixed ticket script (risks) | Planner, from the slicing rules of `yaf-writing-plans` and the plan review |
| 2026-11-19 | T1 does not touch the sub-issue link | The brief asked to investigate a 404 there. The run's log and the live issues show the link was made; the 404 is the label lookup in `ensureLabel` (research §4) | Planner, from the evidence |
| 2026-11-19 | T1 is one ticket, "make re-running ticket creation safe": the lookup in `addItem` with its re-reads, the first status for an existing ticket, the README's count and note | One concern, a re-run that finishes. Without the first status, a ticket created by a run that then failed stays outside the status flow, because a re-run skips existing tickets; V7 needs both | Planner |
| 2026-11-19 | The two deny rules of T6 name this repository's key folder and script, and go into the kit's generic `docs/spec/factory/.claude/settings.json` | `.claude/settings.json` is a verbatim copy written by `sync-references.mjs`; there is no place for a rule of this repository alone, and adding one is a change to the generator, larger than the rules. Part of deviation 4 | Planner |
| 2026-11-19 | A second deny rule, for the helper's `--print` | The brief keeps `--print` for the human's own terminal; a rule makes the accident harder. Same limits as every Bash rule | Planner; for the owner to strike if unwanted |
| 2026-11-19 | The helper sets `GITHUB_REPOSITORY` for the command when it is unset | `status.mjs` and the other kit scripts need it (kit README, "Running the orchestrator"), and the helper already knows the repository | Planner |
| 2026-11-19 | The helper refuses a `git` command when `origin` is not an HTTPS remote | Its credential helper serves HTTPS only; over SSH the command would run as the person, silently. Refusing is simpler and safer than rewriting the remote for the command | Planner, on the plan reviewer's finding |
| 2026-11-19 | No built-in default key path; no fallback from one key source to another | File names carry a date; a silent fallback hides a misconfiguration | Planner |
| 2026-11-19 | `--key-cmd` runs through the shell and is a flag only | A command line needs a shell to be parsed as the person meant it; restricting it to a flag keeps it out of reach of a variable set elsewhere | Planner |
| 2026-11-19 | The helper's tests are in `.github/scripts/tests/` and run as a second step of `factory-self-test`; T2 adds the step | No job runs tests from `.github/scripts/` today. One step in an existing job; a ticket of its own would be two lines | Planner |
| 2026-11-19 | V8 for the helper scans for a key's body, token shapes and addresses other than `git@github.com`, not for the PEM header text | The helper must name the header it checks for and the SSH form of a remote URL; a scan that forbids them would make the implementer split strings to pass it | Planner, on the plan reviewer's finding |
| 2026-11-19 | Ticket titles use `ci:`; the `Type` column says `chore` | `ci` is the Conventional Commit type for this (#2 and #6 used it); the kit's Type column and `type:*` labels have no `ci` (research §5) | Planner |
| 2026-11-19 | The Claude Code sandbox is not turned on | It would enforce the key folder rule, and it changes how every local session runs. A decision for the owner, outside this plan | Planner |
| 2026-11-19 | **Open point for the owner:** make `factory-self-test` a required check | Research §6. Not needed for this plan | Open |

## Deviations from the specification

Six departures from the process and the specification. Each is for the human maintainer to accept or refuse by reviewing this plan's pull request. The second and the third are security-relevant, and each is a decision of the owner in its own right (decision log).

1. **No spec for a change that is not an initial-build milestone.** ADR-0023 and ADR-0025 put a merged `spec.md` before a plan, and allow a plan without one only for milestones M0–M15. This plan has none and cites ADR-0029, the kit README and `18` §4. Why: it is factory operation with no requirement of its own, and far too large to be trivial.
2. **The app keys are long-lived files on disk.** `18` §4, "Agent identity for local work", asks how a local session obtains a `yaf-factory` installation token "without a long-lived secret on disk", and gives as its default "a short-lived token minted from the app's key held in the operating system's credential store". Here each key is a file in `~/.wtf-corp/yaf-factory/`, readable by the user only, with its path in an environment variable. The token is still short-lived, scoped to one repository and never written to disk; the key is not. Why: the owner's decision of 2026-11-19, with the alternatives and the accepted cost in the decision log. What it costs: any process running as the user, agents included, can read the key; file permissions keep other users out, and the deny rule of T6 stops accidents only.
3. **One local session can act as the author and as the reviewer.** ADR-0029 rests the reviewer's independence on "fresh context, different instructions, different credentials and place of execution", ADR-0029 §8 gives the pull-request reviewer a job of its own, and the tier table lets tier low and medium merge automatically once `factory/review` has passed. With this plan, one helper on the maintainer's machine mints tokens for both apps from keys the same user holds, and the `AGENTS.md` condition that the reviewer app's key be kept where agents do not read it is withdrawn. The credentials are different in name and in the ruleset, and no longer in who can use them: a session that wrote a pull request could post its passing review, and in tier low or medium that pull request would merge with no human. What stands against it is a rule of procedure: the implementer never runs the review. The orchestrator does, for pull requests it opened itself, holding both identities; the owner's acceptance covers that. Why: the owner's decision of 2026-11-19, "Accept it, record it" (decision log). The owner accepts the risk for as long as the reviews run locally; it ends when they move back to CI. Tier-high pull requests and flagged plans are not affected: they need the owner's review.
4. **The helper is not part of the kit, and the kit's settings name this repository.** ADR-0029 §1 puts the factory's scripts in `.github/factory/` (here `docs/spec/factory/.github/factory/`), and its Enforcement table has their tests run in `ci.yml`. The helper and its tests are in `.github/scripts/`, and the tests run in job `factory-self-test` of `factory-gates.yml`, which is not a required check. Why: the helper holds this repository's App IDs and variable names and serves a temporary local review; `sync-references.mjs` and `local-review.mjs` are there for the same reason; `ci.yml` does not exist before M0. The path is under `.github/**`: tier high and code-owned, like the kit. The other way round, T6 writes this repository's key folder and script path into the kit's generic `docs/spec/factory/.claude/settings.json`, because `.claude/settings.json` is a verbatim copy of it and there is no other place for a local rule today. A repository that takes the kit gets two rules that match nothing there.
5. **T7's verification is not the docs build.** ADR-0025 (Tickets table, rule 3) names the docs build as the verification of a pure documentation ticket. T7 changes the root `AGENTS.md`, which is no part of the documentation site, and `dotnet docfx` cannot run before M14. It is verified by V3, V5 and V8.
6. **The size limits of the approved kit are raised for the initial build.** ADR-0029 §5 sets `size.soft = 200` and `size.hard = 400`; ADR-0025 (Tickets table, rule 1), ADR-0026 (rule 11), ADR-0027, `17` §2.2 and `18` §1 item 4 repeat the numbers. T8 sets the policy to 400 and 1000 until `1.0.0`. ADR-0029 §5 says of the two that "both numbers are policy values, tuned from the digest's measurements"; this change comes from the owner's judgement of the initial build, not from measurements, and it doubles the size at which a review is asked to justify itself. Why: the owner's decision of 2026-11-19 (decision log). The ADRs and the specification are not edited; the limits return to their values at `1.0.0`. What it costs is in the risks: larger pull requests, a harder review, and automatic merges of up to 1000 lines in tier low and medium.

## Tickets

All ten are tier high by path (research §5). T1, T2, T6 and T9 depend on nothing and share no path: they may run in parallel. T10 is stacked on T5. T8 depends on nothing either, and shares `docs/spec/factory/README.md` with T1, so those two run one after another. T3 is stacked on T2, T4 on T3 and T5 on T4; T10 on T5: all five change the one helper file and its one test file.

**Issues that already exist.** T1 to T8 were in the first version of this plan and keep their keys. When this update merges, `create-tickets.mjs` skips every ticket whose marker it finds and creates issues for the new rows only, T9 and T10; with the unfixed script a run may stop before it reaches them (risks). It never edits an existing issue: #12 (T1), #13 (T2), and any issue created from the first version before this update merges, keep the body they were created with. Two rows changed in this update: T5 (estimate 160; the failing tests now include `runCommand`) and T7 (what V5 prints before the ticket). Every issue carries the line "**Plan** `specs/0002-local-factory-operation/plan.md#Tn`": the implementer and the reviewers read the row of the plan on `main`, and where the issue's body and that row differ, the plan wins. The orchestrator says so in a comment on the issue of T5 and of T7 when it hands them out.

| Key | Title | Type | Area | Requirements | Depends on | Touches | Tier | Size | Failing test first | Verify |
|---|---|---|---|---|---|---|---|---|---|---|
| T1 | ci: make re-running ticket creation safe | chore | factory | L1 (ADR-0029 §3; kit README "How a ticket flows" 3) | — | `docs/spec/factory/.github/factory/project.mjs`, `docs/spec/factory/.github/factory/create-tickets.mjs`, `docs/spec/factory/.github/factory/tests/project.test.mjs`, `docs/spec/factory/README.md` | high | 130 | `tests/project.test.mjs`, test "addItem returns the existing item when the Project already holds the issue": fails because `addItem` rethrows the error of `gh project item-add`. Then "addItem reads the list again until the item shows", "addItem rethrows any other error" and the tests of the first-status rule. Full verification: V1, V3, V9, and V7 after the merge | `node --test "docs/spec/factory/.github/factory/tests/*.test.mjs"` |
| T2 | ci: add app token minting for local factory work | chore | factory | L2, L6 (ADR-0029 §8; kit README checklist 2; `18` §4) | — | `.github/scripts/factory-token.mjs`, `.github/scripts/tests/factory-token.test.mjs`, `.github/workflows/factory-gates.yml` | high | 135 | `.github/scripts/tests/factory-token.test.mjs`, test "buildJwt signs RS256 with the app id as issuer": fails because the module does not exist. Then "mintToken asks for a token scoped to the one repository" and "a failed call's error holds no key, JWT or token". Full verification: V1, V2, V3, V8, V9 | `test -f .github/scripts/tests/factory-token.test.mjs && node --test ".github/scripts/tests/*.test.mjs"` |
| T3 | ci: read the app key from a private file in the token helper | chore | factory | L3 (the owner's decision on key storage; `18` §4) | T2 | `.github/scripts/factory-token.mjs`, `.github/scripts/tests/factory-token.test.mjs` | high | 100 | Same test file, test "keyFileProblem refuses a key file readable by group or others": fails because the function does not exist. Then the tests of `loadKey`: the app's variable, `--key-file`, `--key-cmd`, both flags, no source, content that is no PEM. Full verification: V1, V2, V3, V8, V9 | `grep -q "export function loadKey" .github/scripts/factory-token.mjs && test -f .github/scripts/tests/factory-token.test.mjs && node --test ".github/scripts/tests/*.test.mjs"` |
| T4 | ci: build the app's environment for a wrapped command in the token helper | chore | factory | L4 (ADR-0029 §8; the owner's decision on the commit author) | T3 | `.github/scripts/factory-token.mjs`, `.github/scripts/tests/factory-token.test.mjs` | high | 110 | Same test file, test "childEnv makes the app the author of a commit": fails because the function does not exist. Then "childEnv overrides git's credential helper for the command only", "childEnv refuses an existing GIT_CONFIG_COUNT", "childEnv leaves other variables alone" and the tests of `repoFromRemote`. Full verification: V1, V2, V3, V8, V9 | `grep -q "export function childEnv" .github/scripts/factory-token.mjs && test -f .github/scripts/tests/factory-token.test.mjs && node --test ".github/scripts/tests/*.test.mjs"` |
| T5 | ci: run a command as a factory app with the token helper | chore | factory | L5 (ADR-0029 §8; kit README checklist 2, "Running the orchestrator") | T4 | `.github/scripts/factory-token.mjs`, `.github/scripts/tests/factory-token.test.mjs` | high | 160 | Same test file, test "the command line exits 2 with the usage text when no command is given": fails because the script has no command line. Then the tests of `parseArgs` "a git command is refused when origin is not HTTPS" and "runCommand returns the child's exit code". Full verification: V1, V2, V3, V8, V9, and V6 by the owner | `grep -q "export function parseArgs" .github/scripts/factory-token.mjs && test -f .github/scripts/tests/factory-token.test.mjs && node --test ".github/scripts/tests/*.test.mjs"` |
| T6 | ci: add deny rules for the app keys to the local agent settings | chore | factory | L7 (ADR-0029 §8; kit README checklist 7) | — | `docs/spec/factory/.claude/settings.json`, `.claude/settings.json` | high | 10 | V4 prints `0` for both files and exits 1, because neither rule exists. Full verification: V1, V3, V4, V9 | `grep -qF "Read(~/.wtf-corp/yaf-factory/**)" .claude/settings.json && grep -qF "factory-token.mjs *--print*" .claude/settings.json && node .github/scripts/sync-references.mjs --check` |
| T7 | docs: describe running the factory locally in AGENTS.md | docs | factory | L8 (ADR-0029 §8; kit README checklist 2, "Running the orchestrator"; the owner's decisions) | T5, T6 | `AGENTS.md` | high | 70 | V5's loop prints nine `missing:` lines, one per pattern, because the section does not exist, and its second command prints "the withdrawn condition is still there", because the sentence has not been replaced. Full verification: V3, V5, V8, V9 | `grep -q "^## Running the factory locally" AGENTS.md && grep -q "factory-token.mjs --app reviewer" AGENTS.md && grep -q "FACTORY_PROJECT_NUMBER" AGENTS.md` |
| T8 | ci: raise the size limits for the initial build | chore | factory | L9 (ADR-0029 §5; kit README "Tuning the policy"; the owner's decision on the size limits) | — | `docs/spec/factory/.github/factory/policy.json`, `docs/spec/factory/.github/factory/tests/delta.test.mjs`, `docs/spec/factory/README.md`, `docs/spec/README.md`, `docs/spec/agents/AGENTS.root.md`, `docs/spec/agents/skills/yaf-commits-and-pull-requests/SKILL.md`, `docs/spec/agents/skills/yaf-implementing-tickets/SKILL.md`, `docs/spec/agents/skills/yaf-orchestrating-work/SKILL.md`, `docs/spec/agents/skills/yaf-reviewing-changes/SKILL.md`, `docs/spec/agents/skills/yaf-reviewing-specs-and-plans/SKILL.md`, `docs/spec/agents/skills/yaf-writing-plans/SKILL.md`, `docs/spec/agents/skills/yaf-writing-plans/references/plan-template.md` | high | 45 | V10's first command exits 1, because the policy holds 200 and 400. With the policy changed, the kit test "ticket list problems a script can decide" fails, because its fixture's estimate of 320 is no longer over the soft limit; the fixture then takes its number from the policy. Full verification: V1, V3, V9, V10 | `node -e "const p=require('./docs/spec/factory/.github/factory/policy.json');process.exit(p.size.soft===400&&p.size.hard===1000?0:1)" && node --test "docs/spec/factory/.github/factory/tests/*.test.mjs"` |
| T9 | ci: keep findings for the owner from failing the document review | chore | factory | L10 (ADR-0023 "What counts as approval"; ADR-0029 §7; ADR-0025) | — | `docs/spec/factory/.github/factory/post-review.mjs`, `docs/spec/factory/.github/factory/doc-review.schema.json`, `docs/spec/factory/.github/factory/prompts/doc-review.md`, `docs/spec/factory/.github/factory/tests/post-review.test.mjs` | high | 95 | `tests/post-review.test.mjs`, test "a document review whose only Major findings are for the owner passes": fails because `verdictOf` counts every Major. Then "a Major without the marker still fails", "factory/review ignores the marker", "the summary lists owner findings apart" and "the schema allows the marker". Full verification: V1, V3, V9, V11 | `test -f docs/spec/factory/.github/factory/tests/post-review.test.mjs && node --test "docs/spec/factory/.github/factory/tests/*.test.mjs"` |
| T10 | ci: keep the app token out of the reviewer session's environment | chore | factory | L11 (ADR-0029 §8; kit README design decision 3) | T5 | `.github/scripts/factory-token.mjs`, `.github/scripts/tests/factory-token.test.mjs`, `.github/scripts/local-review.mjs` | high | 40 | `.github/scripts/tests/factory-token.test.mjs`, test "withoutToken removes what childEnv added": fails because the function does not exist. V12's first command prints `0`, because `local-review.mjs` passes its whole environment on. Full verification: V1, V2, V3, V8, V9, V12 | `grep -q "export function withoutToken" .github/scripts/factory-token.mjs && grep -q "withoutToken(process.env)" .github/scripts/local-review.mjs && node --test ".github/scripts/tests/*.test.mjs"` |
