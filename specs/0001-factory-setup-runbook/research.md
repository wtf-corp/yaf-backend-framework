# 0001 — Factory setup runbook: research

What the plan in [`plan.md`](./plan.md) relies on. The implementers of T1 and T2 write `docs/maintainers/factory-setup.md` from this file and the files it cites. Nothing here is a secret: no key material, no token, no API key, no email address. The App ID is an identifier, not a secret. Keep it that way in the runbook.

Each fact carries its provenance:

- **[repo]** read in this repository on 2026-11-19, at commit `2d5ac1b` (pull request #2); the local-review stopgap was read on `main` at `a3adfe0` (pull request #6). The implementer re-reads the cited file and quotes the file, not this summary, where they differ.
- **[owner]** reported by the human owner from setting this repository up on 2026-11-19. An agent cannot re-check these: the deny rules of `.claude/settings.json` block `gh api *rulesets*`, `gh api *environments*`, `gh secret *`, `gh variable set *`, `gh label *` and `gh repo edit *`.
- **[docs]** third-party behaviour checked against the vendor's documentation on 2026-11-19; the source is given.
- **[assumed]** not stated by either; the runbook must mark it as something to confirm.

## 1. Placeholders and the worked example

The runbook is generic. It uses these placeholders and gives this repository's values once, as the worked example.

| Placeholder | This repository | Provenance |
|---|---|---|
| `<org>` | `wtf-corp` | [repo] `.github/CODEOWNERS` |
| `<repo>` | `yaf-backend-framework` | [repo] |
| `<team>` | `admins` (CODEOWNERS entry `@wtf-corp/admins`) | [repo] `.github/CODEOWNERS`, `policy.json` `humans.team` |
| `<maintainer-login>` | `mika666` | [repo] `policy.json` `humans.logins` |
| `<app-slug>` / bot login | `yaf-factory` / `yaf-factory[bot]` | [repo] `policy.json` `bots.factory`; [owner] |
| `<app-id>` | `5834995`. An identifier, not a secret: it is shown on the app's settings page, stored in variable `FACTORY_APP_ID`, and is what the token procedure of section 5 uses as `iss` | [owner] |
| `<reviewer-app-slug>` / `<reviewer-app-id>` | `yaf-reviewer` / `5837737`. Also an identifier, not a secret. Only this repository's temporary local review needs it (section 3, item 8) | [owner]; [repo] `AGENTS.md` |
| `<project-number>` | `1` (Project "YAF factory") | [owner] |
| `<bundle>` | `docs/spec` | [repo] `AGENTS.md` path map |

## 2. In the repository: the kit installed by reference [repo]

The kit README's "Install at M0" step 1 says to copy the contents of `factory/` to the repository root. This repository did not copy: the bundle stays in `docs/spec/` and the root holds references to it.

**Instruction files**

- `AGENTS.md` imports `@docs/spec/agents/AGENTS.root.md` and holds the path map from the bundle's installed layout to this repository, the table of nested instruction sections, and the rule for changing the bundle.
- `CLAUDE.md` is one line: `@AGENTS.md`.
- `specs/AGENTS.md` and `.agents/README.md` are pointer files into `docs/spec/agents/`.

**Generated references: `.github/scripts/sync-references.mjs`** (Node 22+, no dependencies)

| Written to | Content |
|---|---|
| `.claude/agents/<name>.md` | The front matter of `docs/spec/factory/.claude/agents/<name>.md` plus a pointer to it (Claude Code reads tools, model, isolation and skills from the front matter) |
| `.claude/skills/<name>/SKILL.md`, `.agents/skills/<name>/SKILL.md` | The front matter of `docs/spec/agents/skills/<name>/SKILL.md` plus a pointer to it |
| `.claude/settings.json`, `.github/pull_request_template.md`, `.github/ISSUE_TEMPLATE/ticket.yml`, `.github/ISSUE_TEMPLATE/epic.yml` | Verbatim copies: these formats have no include mechanism and the tool reads one fixed path |

- `node .github/scripts/sync-references.mjs` writes them; `--check` exits 1 on a missing, changed or extra file; `--root <dir>` works on another checkout. It owns `.claude/agents`, `.claude/skills` and `.agents/skills` entirely: a file there it did not generate is reported as extra and removed on write.
- Run on 2026-11-19: `node .github/scripts/sync-references.mjs --check` → `58 reference(s) to docs/spec/ are in step`.
- CI job `references` in `.github/workflows/factory-gates.yml` replaces the kit's `skills-mirror` job. It checks out the base commit at the root and the pull request head in `pr/`, and runs the **base branch's** script with `--check --root pr`. It falls back to `pr/.github/scripts/sync-references.mjs` only while the base has no script, which is the installing pull request. The job holds no secret.

**Workflows are real copies**

GitHub runs only real files in `.github/workflows/`, and Windows has no symlinks without Developer Mode, so `.github/workflows/factory-*.yml` (seven files) are copies of `docs/spec/factory/.github/workflows/`, changed in four ways:

1. Script paths rewritten from `.github/factory/` to `docs/spec/factory/.github/factory/`.
2. A provenance header: "Installed from docs/spec/factory/.github/workflows/<file>: …".
3. Third-party actions pinned by commit SHA, with the version as a trailing comment. The pins on 2026-11-19 were `actions/checkout` v4.4.0, `actions/setup-node` v4.4.0, `actions/setup-dotnet` v4.3.1, `actions/create-github-app-token` v1.12.0 and `anthropics/claude-code-action` v1.0.283. The runbook points at the files for the SHAs instead of repeating them: they change with every update.
4. In `factory-gates.yml`, job `test-evidence` skips until `global.json` exists (M0 adds the solution).

Since pull request #6 the two review workflows run only when repository variable `FACTORY_REVIEW_IN_CI` is `true` (section 3, item 8). Which workflow uses which environment: `factory-automerge.yml`, `factory-tickets.yml` and `factory-status.yml` use `factory-merge` (variables `FACTORY_APP_ID`, `FACTORY_PROJECT_OWNER`, `FACTORY_PROJECT_NUMBER`, `FACTORY_PAUSED`; secret `FACTORY_APP_PRIVATE_KEY`). `factory-pr-review.yml` and `factory-doc-review.yml` use `factory-review` (secret `ANTHROPIC_API_KEY`), pass `github_token: ${{ github.token }}` and `allowed_bots: yaf-factory` to the action.

**Code owners:** `.github/CODEOWNERS` is installed from the kit with the team set to `@<org>/<team>` and with `/docs/spec/` added, because the bundle (specification, ADRs, agent guidance, the kit with its live policy and scripts) lives there. Its header says the team must hold humans only and needs write access, or GitHub ignores the entries.

**Policy, used in place:** `docs/spec/factory/.github/factory/policy.json`. The changes against the kit are additive only. Replacing entries broke the kit's own tests [owner].

| Key | Change |
|---|---|
| `humans` | `team` = `<team>`, `logins` = [`<maintainer-login>`] |
| `bots.factory` | must equal the app's bot login, `<app-slug>[bot]` |
| `size.exclude` | + `.agents/skills/**` (generated stubs; the kit already excludes `.claude/skills/**`) |
| `docPaths.spec` | + `docs/spec/spec/**`, `docs/spec/adr/**` |
| `sensitivePaths` | + `docs/spec/**`, `docs/spec/factory/.github/**`, `docs/spec/factory/.claude/**` |

Why the two dot-folder patterns: `lib.mjs` matches with Node's `path.posix.matchesGlob`, whose `**` does not match a path segment that starts with a dot. Checked on 2026-11-19 with Node v24.21.0:

    node -e "const p=require('path').posix;const f='docs/spec/factory/.github/factory/policy.json';console.log(p.matchesGlob(f,'docs/spec/**'),p.matchesGlob(f,'docs/spec/factory/.github/**'))"
    → false true

Without the explicit patterns the policy file itself is classified tier low, because `docs/**` is in `tiers.low.paths`.

**The kit's tests:** `node --test "docs/spec/factory/.github/factory/tests/*.test.mjs"` → 31 tests, 31 pass (run on 2026-11-19). `factory-gates.yml` runs them as job `factory-self-test`.

**What the kit README's install steps become here**

| Kit README, "Install at M0" | Here |
|---|---|
| 1. Copy `factory/` to the root | Keep the bundle in `docs/spec/`; add the path map to `AGENTS.md`; copy only the workflows and CODEOWNERS |
| 2. `CLAUDE.md` = `@AGENTS.md` | Same |
| 3. `node .github/factory/sync-skills.mjs` | `node .github/scripts/sync-references.mjs` |
| 4. Edit `policy.json` and CODEOWNERS | In place under `docs/spec/factory/.github/factory/`; additive |
| 5. Script tests | Same command with the `docs/spec/factory/` prefix |
| 6. Human setup checklist | Section 3 below |
| 7. Prove the gates | Section 5 below |

## 3. GitHub setup, done by a human [owner]

The agent's `.claude/settings.json` denies `gh secret`, `gh variable set`, rulesets, environments, `gh label` and `gh repo edit` [repo]. An agent assisting the setup hands these commands to the human. The runbook says so at the top of the section.

1. **Labels, Project, variables.** `gh auth refresh -h github.com -s project`, then `bash docs/spec/factory/.github/factory/setup-github.sh` from the repository root. The script needs `gh` and `jq`, is idempotent, and created: 18 labels (from `labels.json`), the Project "YAF factory" (#1) with Status options Backlog / Ready / In progress / In review / Done, linked to the repository, and the variables `FACTORY_PROJECT_OWNER`, `FACTORY_PROJECT_NUMBER` and `FACTORY_PAUSED=false` [repo: `setup-github.sh`, `labels.json`].
2. **The `main` ruleset**, applied with `gh api -X POST repos/<org>/<repo>/rulesets --input ruleset-main.json`. JSON in section 4. Then check that it is the only one: `gh api repos/<org>/<repo>/rules/branches/main` must show rules from exactly one ruleset (section 6).
3. **Repository settings.** `gh repo edit --enable-auto-merge`. The repository merges by squash only and deletes branches on merge. Flags per the `gh` manual [docs: https://cli.github.com/manual/gh_repo_edit]: `--enable-squash-merge`, `--enable-merge-commit=false`, `--enable-rebase-merge=false`, `--delete-branch-on-merge`, `--enable-auto-merge`. Whether the owner set the merge methods with these flags or in the web settings is not recorded [assumed]; both give the same result.
4. **Team access.** Team `<team>` gets access to the repository. Maintain is recommended; Admin works but is broader than needed.
5. **The GitHub App** (`<app-slug>`). Permissions as in the kit README's checklist, item 2: Contents, Pull requests, Issues read and write; Projects read and write (organisation); Checks and Metadata read. Not Administration, Workflows, Secrets, Environments or Members. Installed on selected repositories only. Its App ID is stored as variable `FACTORY_APP_ID`. Its bot login must equal `bots.factory` in the policy. Not a member of the maintainers team; on no bypass list.
6. **Environment `factory-merge`.** Deployment branches: custom branch policies allowing only `main`. Secret `FACTORY_APP_PRIVATE_KEY` (the content of the app's private key, pasted at the prompt). Recommended: untick "Allow administrators to bypass configured protection rules".
7. **Environment `factory-review`.** Secret `ANTHROPIC_API_KEY`: a dedicated key, ideally in its own Console workspace with a monthly spending limit.
   - Considered and rejected: a Claude subscription token (`claude setup-token`, passed as action input `claude_code_oauth_token`). It shares the subscriber's usage limits, it is tied to a personal account, and it makes the installed workflows differ from the kit. The input exists [docs: https://github.com/anthropics/claude-code-action/blob/main/docs/usage.md, "Claude Code OAuth token (alternative to anthropic_api_key)"].
   - Secrets are set interactively: `gh secret set <NAME> --env <ENV> -R <org>/<repo>`, then paste at the prompt. Never from a file left on disk, never on the command line.
8. **This repository's temporary state: the model reviews run locally.** The intended setup, and the one to port, is item 7: the two review workflows run in CI with the key in `factory-review`. Here that account did not work yet, so since pull request #6 [repo: `AGENTS.md`, "Model reviews run locally (temporary)"; `.github/scripts/local-review.mjs`]:
   - a second GitHub App, `yaf-reviewer` (App ID 5837737), with Checks write, Pull requests write and Contents read, posts `factory/review` and `factory/doc-review` [owner];
   - the ruleset pins those two checks to that app and leaves the other eight on GitHub Actions (section 4);
   - the CI review jobs are off unless repository variable `FACTORY_REVIEW_IN_CI` is `true`;
   - the orchestrator runs `node .github/scripts/local-review.mjs <pr-number>` with a token of the reviewer app. The factory app cannot write checks, and that stays so: the identity that opens pull requests must not be able to pass its own review.

   The runbook presents this as this repository's current state, in a few lines, and points to `AGENTS.md` and `.github/scripts/local-review.mjs` instead of repeating them. Both go away when the reviews move back to CI (section 7).

## 4. The `main` ruleset

Endpoint and field names [docs: https://docs.github.com/en/rest/repos/rules, "Create a repository ruleset", API version 2022-11-28]: `name` and `enforcement` are required; `conditions.ref_name.include` accepts `~DEFAULT_BRANCH`; rule types `deletion` and `non_fast_forward` take no parameters; `pull_request` requires `dismiss_stale_reviews_on_push`, `require_code_owner_review`, `require_last_push_approval`, `required_approving_review_count` (0–10) and `required_review_thread_resolution`, and accepts `allowed_merge_methods` (`merge`, `squash`, `rebase`); `required_status_checks` requires `required_status_checks` (items `context`, optional `integration_id`) and `strict_required_status_checks_policy`.

What the owner applied [owner]: target the default branch, no bypass actors; rules `deletion` and `non_fast_forward`; `pull_request` with 0 required approvals, code-owner review required, stale reviews dismissed, squash as the only merge method; `required_status_checks`, strict, with the nine names of `policy.json` `checks` plus `references`. Eight are pinned to `integration_id` 15368 (GitHub Actions). `factory/review` and `factory/doc-review` were first pinned to 15368 too and are now pinned to 5837737, the reviewer app, for as long as the reviews run locally (section 3, item 8).

The file below is the ruleset as it stands on 2026-11-19 (ruleset id 27648742) [owner]: the file the owner applied with `gh api -X POST repos/<org>/<repo>/rulesets --input ruleset-main.json`, reflowed to fewer lines, with the two review checks re-pinned. The runbook shows it as applied here and says in one line that a repository whose reviews run in CI uses 15368 for all ten.

```json
{
  "name": "main: factory gates",
  "target": "branch",
  "enforcement": "active",
  "bypass_actors": [],
  "conditions": { "ref_name": { "include": ["~DEFAULT_BRANCH"], "exclude": [] } },
  "rules": [
    { "type": "deletion" },
    { "type": "non_fast_forward" },
    { "type": "pull_request", "parameters": {
        "required_approving_review_count": 0,
        "require_code_owner_review": true,
        "dismiss_stale_reviews_on_push": true,
        "require_last_push_approval": false,
        "required_review_thread_resolution": false,
        "allowed_merge_methods": ["squash"] } },
    { "type": "required_status_checks", "parameters": {
        "strict_required_status_checks_policy": true,
        "do_not_enforce_on_create": false,
        "required_status_checks": [
          { "context": "factory/tier", "integration_id": 15368 },
          { "context": "factory/ticket", "integration_id": 15368 },
          { "context": "factory/size", "integration_id": 15368 },
          { "context": "factory/delta", "integration_id": 15368 },
          { "context": "factory/guards", "integration_id": 15368 },
          { "context": "factory/review", "integration_id": 5837737 },
          { "context": "factory/doc-review", "integration_id": 5837737 },
          { "context": "factory/plan-gate", "integration_id": 15368 },
          { "context": "factory/human-approval", "integration_id": 15368 },
          { "context": "references", "integration_id": 15368 } ] } }
  ]
}
```

This layout is 32 lines, 34 with its fence; the runbook uses it as is to stay within its size.

Notes for the runbook:

- A required check pinned to an `integration_id` must be reported by that app: 15368 is GitHub Actions, 5837737 is this repository's reviewer app. The gates report their `factory/*` check runs with `github.token` [repo: `factory-gates.yml` grants `checks: write` to the `gates` job], so they should qualify for 15368. The kit README's open point "Check runs created with the workflow token satisfy a required status check" is not settled by trial 1: pull request #3 merged while the ruleset was disabled (section 5).
- `references` is a job name, reported by Actions itself.
- "The author cannot approve" is GitHub's own behaviour, not a ruleset field.
- The kit README also asks for the build, test, security and title jobs of `ci.yml`: not yet, see section 7.

## 5. Acting as the app, and the gate trials [owner]

**Why.** Gate trials need pull requests authored by the app, so that a human's review counts: an author cannot approve their own pull request. The kit README says the same for agents ("authenticate locally with a token of this app, never with your login").

**How.** A dependency-free Node script (Node 22+, about 85 lines) mints an installation token and runs a command with it. The private key comes in on standard input (`--key -`), for example in PowerShell: `Get-Clipboard -Raw | node app-token.mjs --app-id <app-id> --key - -- gh pr create …`, and the clipboard is cleared afterwards. The script is **not in the repository**, by the owner's decision of 2026-11-19: not here, not in the runbook. The runbook describes the steps below and links nothing; a maintainer who needs the script writes it from them. What the owner's script did [owner], with the API facts each [docs]:

1. Build a JWT signed RS256 with the app's private key: header `alg: RS256`; claims `iat` (60 seconds in the past, for clock drift), `exp` (at most 10 minutes ahead; the owner's script used 9), `iss` (the App ID; the client ID also works and is what the documentation recommends). Sent as `Authorization: Bearer <jwt>`. Source: https://docs.github.com/en/apps/creating-github-apps/authenticating-with-a-github-app/generating-a-json-web-token-jwt-for-a-github-app. Node's built-in `crypto` signs RS256, so no dependency is needed.
2. `GET /repos/{owner}/{repo}/installation` with the JWT → the installation `id`.
3. `POST /app/installations/{installation_id}/access_tokens` with the JWT and body `{ "repositories": ["<repo>"] }` → `token`, `expires_at`. The token lives one hour and cannot exceed the app's permissions; `repositories` narrows it to the one repository. Source for 2 and 3: https://docs.github.com/en/rest/apps/apps (API version 2022-11-28). Do not assume a token length: the page announces a new token format.
4. Run the wrapped command as a child process, without a shell, with `GH_TOKEN` and `GITHUB_TOKEN` set to the token and `GIT_TERMINAL_PROMPT=0`. `gh` then acts as `<app-slug>[bot]`.
5. For `git`, override the credential helper for that process only, through the environment:
   - `GIT_CONFIG_COUNT=2`
   - `GIT_CONFIG_KEY_0=credential.helper`, `GIT_CONFIG_VALUE_0=` (empty: resets the helper list, so the user's credential manager is not asked)
   - `GIT_CONFIG_KEY_1=credential.helper`, `GIT_CONFIG_VALUE_1=` a helper that answers `username=x-access-token` and `password=$GH_TOKEN`

   The pairs are zero-indexed, override configuration files, and are overridden by `git -c` [docs: https://git-scm.com/docs/git-config, "ENVIRONMENT"]. An empty `credential.helper` resets the list, and a value starting with `!` is a shell snippet [docs: https://git-scm.com/docs/gitcredentials, "Configuration Options" and "Custom Helpers"]. The helper string used is `!f() { echo username=x-access-token; echo "password=$GH_TOKEN"; }; f` [owner: `git ls-remote` and `git push` authenticated as the app with it].
6. **Where the key is.** The app keys (factory and reviewer) are held in a key manager or the operating system's credential store, never as files: the owner moved both out of the filesystem on 2026-11-19, and no key file is left on disk [owner]. The key is passed to the script at run time through standard input and is not written anywhere. If a key is lost, a fresh one is generated on the app's settings page and the old one deleted; nothing depends on keeping a copy. This is the default of `18` §4, "Agent identity for local work": "a short-lived token minted from the app's key held in the operating system's credential store", with no long-lived secret on disk [repo: `docs/spec/spec/18-build-plan.md`]. Passing the key through the clipboard exposes it to whatever reads the clipboard until it is cleared; fetching it straight from the key manager's CLI is a possible later improvement.
7. The token is never written to disk and expires by itself. It is printed only when the script is run with `--print`, which is meant for the human's own terminal: never in CI, a log or an agent session.

**The trial list.** These seven are the kit README's "Install at M0" step 7. The README's "Verify before relying on it" is a different list: assumptions about GitHub, the action and the kit's own logic that the kit could not test. The runbook does not work through it; it says that the list stays open and links it. The full gate acceptance criteria are `17` §7 (`docs/spec/spec/17-ci-release-docs.md`), which are M0's exit criteria; the runbook links them and does not repeat them.

| Trial | Expected | State on 2026-11-19 |
|---|---|---|
| A trivial docs pull request | Tier low, merges by itself | **To repeat.** Pull request #3, opened by the app, passed every deterministic gate (tier low, trivial accepted, auto-merge armed) and failed `factory/review` on its first model API call. It then merged unreviewed, when the ruleset was disabled for a break-glass merge and its armed auto-merge fired (section 6). That proves nothing about the gates |
| A file under `src/Http/Authentication/` | Tier high, waits for a human; the human's approval satisfies `factory/human-approval`, a bot's does not | On the list |
| A pull request over 400 changed lines | `factory/size` fails; `size-accepted` applied by the app does not help; applied by a human maintainer it does, and the tier stays high | On the list |
| `tier:low` put on a tier-high pull request | Still high | On the list |
| A pull request that weakens `policy.json` and touches a sensitive path | Judged by the base branch's policy | On the list |
| `FACTORY_PAUSED=true` | Auto-merge stops being enabled | On the list |
| Delta trials: a declared public API line, then an undeclared one | Medium merges by itself; undeclared fails `factory/delta` and is tier high | Once the first plan exists |

No trial counts as passed. The runbook presents the table as a checklist with a column for the pull request that proved each, and does not claim results the notes do not hold.

## 6. Gotchas [owner, unless marked]

- **Every non-spec, non-plan pull request needs a ticket or must be trivial.** `factory/ticket` passes for a code pull request only when it closes exactly one open ticket created from a plan on the base branch, or carries a `Trivial because:` line, is not tier high and has at most 20 changed lines [repo: `ticket.mjs`]. So the first real change after the install needs a merged plan first; this runbook's own plan is that case.
- **A single maintainer login cannot approve its own pull requests.** With one human login, the maintainer's own pull requests to code-owned or tier-high paths cannot be approved. The work goes through the app (section 5), or a second human login is added to the team and to `humans.logins`.
- **Git Bash rewrites `gh api /path`** as a Windows path. Drop the leading slash: `gh api repos/<org>/<repo>/…`.
- **`integration_id`** pins a required check to one app (15368 is GitHub Actions); a check of the same name reported by another app does not satisfy it.
- **Disabling a ruleset for a break-glass merge releases every open pull request that has auto-merge armed.** Pull request #3 merged unreviewed that way. Before a break-glass, cancel auto-merge on the open pull requests or set `FACTORY_PAUSED=true`, and remember that pausing does not disarm auto-merge already enabled (kit README, "Running the orchestrator").
- **Rulesets stack.** An old default ruleset (1 required approval, Copilot review) was still active next to the factory ruleset, so one approval blocked every auto-merge. `gh api repos/<org>/<repo>/rules/branches/main` must show rules from exactly one ruleset.
- **App keys are not kept as files:** section 5, step 6.
- **`**` and dot-folders** in policy globs: section 2.
- **Replacing policy entries** instead of adding to them broke the kit's tests: section 2.

## 7. Still to do in this repository [owner]

- Moving the model reviews back to CI: fix the account behind `factory-review`, set `FACTORY_REVIEW_IN_CI=true`, point `factory/review` and `factory/doc-review` in the ruleset back at GitHub Actions (15368), delete `local-review.mjs` and the `AGENTS.md` section [repo: `AGENTS.md`].
- Repeating trial 1, then trials 2–7.
- `ci.yml` in M0, and adding its checks (build, test, security, title) to the `main` ruleset.
- A ruleset for `next` after `1.0.0`.
- Optionally a merge queue.

## 8. Facts about the gates the plan depends on [repo]

Checked by running the kit's own code against the plan's paths on 2026-11-19:

- `prKind(['specs/0001-factory-setup-runbook/plan.md', 'specs/0001-factory-setup-runbook/research.md'])` → `plan`. The plan's pull request is a plan pull request.
- `classify({ files: ['docs/maintainers/factory-setup.md'], title: 'docs: …' })` → tier `low` (T2 asks for `high` by label: `gates.mjs` counts a `tier:*` label applied by anyone but the gates job as a request to raise, and `create-tickets.mjs` labels the issue from the Tier column), reason "no sensitive path, no delta, low-risk type". `docs/maintainers/**` matches `docs/**` in `tiers.low.paths` and no entry of `sensitivePaths` (`docs/spec/**` does not cover it). It is not in CODEOWNERS.
- `size.exclude` does not cover `docs/maintainers/**`: every line of the runbook counts towards `size.soft` (200) and `size.hard` (400). `size.mjs` sums the added and deleted lines of `git diff --numstat`, so blank lines count.
- `plan.mjs` `ticketProblems` fails `factory/plan-gate` for an estimate above `size.soft`, for a missing verification command and for empty `touches`; a `docs` ticket may omit the failing test. `hasDeviations` flags a plan whose "Deviations" section holds anything but `none`.
- `create-tickets.mjs` strips one backtick from each end of the `Verify` cell and puts the rest into a code block in the issue, and `plan.mjs` splits table cells on an unescaped `|`: a `Verify` cell holds a command only, with no prose and no pipe.

## 9. Not available at planning time, and open points

- There is no docs build yet: no `docs/docfx.json`, no `toc.yml`. The DocFX site is created in M14 (`18` §2); `17` §5 lists its folders (`articles/`, `api/`, `compliance/`, `reference/`). ADR-0025 names the docs build as the verification of a pure documentation ticket; the plan substitutes checks that run today.
- The results of all seven trials (trial 1 has to be repeated).
- **Open point: the token script's long-term home.** A reviewed, tier-high file under `.github/`, in a plan of its own. Until then it is in no repository file: section 5 holds the description only.
