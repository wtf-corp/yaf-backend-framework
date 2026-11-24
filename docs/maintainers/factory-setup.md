# Factory setup runbook

How to install the software factory (ADR-0029) in a repository by reference, the way this repository did it. Follow it top to bottom. It covers only what the [kit README](../spec/factory/README.md) does not: the by-reference layout, the settings as applied here, and what went wrong on the way. For anything that changes often (pinned SHAs, policy values, workflow steps) it links the file instead of repeating it.

## 1. What this sets up

The factory is the set of gates, workflows, role definitions and policy that lets agents work tickets while merging stays with checks and humans ([ADR-0029](../spec/adr/0029-software-factory.md)). The install is done by hand, by a human maintainer with an agent assisting; the factory does not install its own gates.

Prerequisites: `gh` logged in as the maintainer, `jq`, Node 22 or later, and owner rights in the organisation (for the app, the team and the Project).

The runbook is generic. It uses these placeholders; the right column is this repository, the worked example.

| Placeholder | This repository |
|---|---|
| `<org>` / `<repo>` | `wtf-corp` / `yaf-backend-framework` |
| `<team>` | `admins` (code owner `@wtf-corp/admins`) |
| `<maintainer-login>` | `mika666` |
| `<app-slug>` / bot login | `yaf-factory` / `yaf-factory[bot]` |
| `<app-id>` | `5834995`, an identifier shown on the app's settings page, not a secret |
| `<reviewer-app-slug>` / `<reviewer-app-id>` | `yaf-reviewer` / `5837737`, needed only while the model reviews run locally (see [`AGENTS.md`](../../AGENTS.md)) |
| `<project-number>` | `1` (Project "YAF factory") |
| `<bundle>` | `docs/spec` |

Agents may not change GitHub settings: the deny rules of [`.claude/settings.json`](../spec/factory/.claude/settings.json) block `gh secret`, `gh variable set`, `gh label`, `gh repo edit` and `gh api` on rulesets and environments. An agent assisting the setup hands each such command to the human, who runs it in their own terminal.

## 2. The kit by reference

The kit README's "Install at M0" step 1 copies the kit to the repository root. This repository does not: the bundle stays in `<bundle>` and the root holds references to it.

- [`AGENTS.md`](../../AGENTS.md) imports `@docs/spec/agents/AGENTS.root.md` and adds the path map from the bundle's installed layout to this repository, the table of nested instruction sections and the rule for changing the bundle.
- [`CLAUDE.md`](../../CLAUDE.md) is one line: `@AGENTS.md`.
- [`specs/AGENTS.md`](../../specs/AGENTS.md) and [`.agents/README.md`](../../.agents/README.md) are pointer files into `docs/spec/agents/`.

[`.github/scripts/sync-references.mjs`](../../.github/scripts/sync-references.mjs) (Node 22+, no dependencies) writes the files tools look for in fixed places:

| Written to | Content |
|---|---|
| `.claude/agents/<name>.md` | The front matter of `docs/spec/factory/.claude/agents/<name>.md` and a pointer to it |
| `.claude/skills/<name>/SKILL.md`, `.agents/skills/<name>/SKILL.md` | The front matter of `docs/spec/agents/skills/<name>/SKILL.md` and a pointer to it |
| `.claude/settings.json`, `.github/pull_request_template.md`, `.github/ISSUE_TEMPLATE/ticket.yml`, `.github/ISSUE_TEMPLATE/epic.yml` | Verbatim copies: these formats have no include and the tool reads one fixed path |

Run `node .github/scripts/sync-references.mjs` after changing a role definition, a skill, the settings or a template, and commit the result. `--check` prints `58 reference(s) to docs/spec/ are in step` and exits 0 when all are in step, and exits 1 on a missing, changed or extra file; `--root <dir>` works on another checkout. The script owns `.claude/agents`, `.claude/skills` and `.agents/skills` entirely: a file there it did not generate is removed on write.

CI job `references` in [`factory-gates.yml`](../../.github/workflows/factory-gates.yml) replaces the kit's `skills-mirror` job. It checks out the base commit at the root and the pull request head in `pr/`, and runs the base branch's script with `--check --root pr`; only while the base has no script (the installing pull request) does it use the head's copy. It holds no secret.

The workflows cannot be references: GitHub runs only real files in `.github/workflows/`, and Windows has no symlinks without Developer Mode. The seven [`factory-*.yml`](../../.github/workflows/) are copies of `docs/spec/factory/.github/workflows/`, changed in four ways:

1. Script paths rewritten from `.github/factory/` to `docs/spec/factory/.github/factory/`.
2. A header line: `Installed from docs/spec/factory/.github/workflows/<file>: …`.
3. Third-party actions pinned by commit SHA, with the version as a trailing comment. The workflow files are the record of the pins.
4. In `factory-gates.yml`, job `test-evidence` does nothing until `global.json` exists (M0 adds the solution).

Temporarily, the review jobs of `factory-pr-review.yml` and `factory-doc-review.yml` also run only when variable `FACTORY_REVIEW_IN_CI` is `true`: see "Model reviews run locally (temporary)" in [`AGENTS.md`](../../AGENTS.md).

What the kit README's install steps become here:

| Kit README, "Install at M0" | Here |
|---|---|
| 1. Copy `factory/` to the root | Keep the bundle in `docs/spec/`, add the path map to `AGENTS.md`, copy only the workflows and CODEOWNERS |
| 2. `CLAUDE.md` = `@AGENTS.md` | Same |
| 3. `node .github/factory/sync-skills.mjs` | `node .github/scripts/sync-references.mjs` |
| 4. Edit `policy.json` and CODEOWNERS | Section 3 |
| 5. Run the script tests | Section 3 |
| 6. Human setup checklist | Sections 4 to 7 |
| 7. Prove the gates | Section 8 |

## 3. Policy and code owners

[`.github/CODEOWNERS`](../../.github/CODEOWNERS) is installed from the kit with the team set to `@<org>/<team>` and with `/docs/spec/` added, because the specification, the ADRs, the agent guidance and the kit with its live policy and scripts live there. The team must hold humans only and needs write access to the repository, or GitHub ignores the entries.

The policy is used in place: [`docs/spec/factory/.github/factory/policy.json`](../spec/factory/.github/factory/policy.json). Change it by adding entries only; as found here, replacing the kit's entries broke the kit's own tests. The changes as applied here:

| Key | Change |
|---|---|
| `humans` | `team` = `<team>`, `logins` = [`<maintainer-login>`] |
| `bots.factory` | Must equal the app's bot login, `<app-slug>[bot]` (unchanged here: the kit's value already matched) |
| `size.exclude` | + `.agents/skills/**` (generated stubs; the kit already excludes `.claude/skills/**`) |
| `docPaths.spec` | + `docs/spec/spec/**`, `docs/spec/adr/**` |
| `sensitivePaths` | + `docs/spec/**`, `docs/spec/factory/.github/**`, `docs/spec/factory/.claude/**` |

The two dot-folder patterns are needed because the scripts match with Node's `path.posix.matchesGlob`, whose `**` does not match a path segment that starts with a dot. Without them the policy file itself is tier low, because `docs/**` is in `tiers.low.paths`. To see it:

    node -e "const p=require('path').posix;const f='docs/spec/factory/.github/factory/policy.json';console.log(p.matchesGlob(f,'docs/spec/**'),p.matchesGlob(f,'docs/spec/factory/.github/**'))"

Expected output: `false true`.

Then run the kit's tests, which `factory-gates.yml` also runs as job `factory-self-test`:

    node --test "docs/spec/factory/.github/factory/tests/*.test.mjs"

Expected: every test passes, none fails or is skipped (45 tests on 2026-11-20).

## 4. Labels, Project and variables

The first GitHub step is scripted. `setup-github.sh` uses the Projects API, so give the maintainer's `gh` login that scope first, then run the script from the repository root:

    gh auth refresh -h github.com -s project
    bash docs/spec/factory/.github/factory/setup-github.sh

[`setup-github.sh`](../spec/factory/.github/factory/setup-github.sh) needs `gh` and `jq` and is idempotent. It creates:

- the 18 labels of [`labels.json`](../spec/factory/.github/factory/labels.json);
- the Project "YAF factory", linked to the repository, with the Status options Backlog / Ready / In progress / In review / Done;
- the repository variables `FACTORY_PROJECT_OWNER`, `FACTORY_PROJECT_NUMBER` and `FACTORY_PAUSED=false` (an existing `FACTORY_PAUSED` is kept).

It does not configure the rulesets, the app, the environments and their secrets, or the team.

## 5. Repository settings and the main ruleset

From here on every step is a GitHub setting: the human runs it (section 1).

Repository settings, as applied here: auto-merge on, squash the only merge method, head branches deleted on merge. With `gh` (flags per the [`gh repo edit` manual](https://cli.github.com/manual/gh_repo_edit)):

    gh repo edit <org>/<repo> --enable-auto-merge --enable-squash-merge --enable-merge-commit=false --enable-rebase-merge=false --delete-branch-on-merge

Whether this repository set the merge methods with these flags or in the web settings is not recorded; both give the same result. Confirm against the live setting.

Team access: team `<team>` gets access to the repository, at least write, or the CODEOWNERS entries are ignored. Maintain is enough; Admin works but is broader than needed.

The ruleset for `main`, as it stands here (ruleset id `27648742`). Save it as `ruleset-main.json` outside the repository and apply it with `gh api -X POST repos/<org>/<repo>/rulesets --input ruleset-main.json`. Field names per GitHub's [REST reference for rulesets](https://docs.github.com/en/rest/repos/rules) (API version 2022-11-28).

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
        "strict_required_status_checks_policy": false,
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
          { "context": "references", "integration_id": 15368 },
          { "context": "build-test", "integration_id": 15368 },
          { "context": "pr-title", "integration_id": 15368 },
          { "context": "security", "integration_id": 15368 },
          { "context": "process", "integration_id": 15368 } ] } }
  ]
}
```

- **The rules** are as read back from `repos/<org>/<repo>/rules/branches/main` on 2026-11-20, and the required checks again on 2026-11-24. The read-back does not show `name`, `enforcement`, `bypass_actors` or `conditions`: those are as the owner applied them; confirm against the live setting. It also shows fields the file does not set, with the values GitHub keeps: `required_reviewers` empty, `dismissal_restriction` off, `require_extra_approval_for_unattributed_changes` true.
- **Required checks** are the nine names of `checks` in [`policy.json`](../spec/factory/.github/factory/policy.json), plus `references` and the four jobs of `ci.yml` that the kit README asks for: `build-test`, `pr-title`, `security`, `process`. The owner added those four on 2026-11-24 ([plan 0005](../../specs/0005-repository-skeleton/plan.md), O1).
- **Strict is off, and there is no merge queue.** The owner applied the ruleset with "require branches to be up to date before merging" on, and turned it off on 2026-11-19 (#56). With it on, every merge put the other open pull requests behind `main`, and updating a branch is a push: the gates and the model review run again and an approval is dismissed. A merge queue would stall while the reviews run locally: nobody reports `factory/review` and `factory/doc-review` on a queue commit. The owner decided on 2026-11-20 to keep both off until the reviews are back in CI, then to revisit ([plan 0005](../../specs/0005-repository-skeleton/plan.md), Q4 and deviation 7). The kit README asks for one of the two.
- **`integration_id`** pins a required check to the app that must report it. 15368 is GitHub Actions, which reports the gates' check runs and job `references`. Here `factory/review` and `factory/doc-review` are pinned to 5837737, the reviewer app, while the model reviews run locally (section 6). A repository whose reviews run in CI pins all fourteen to 15368.
- "The author cannot approve" is GitHub's own behaviour, not a ruleset field.

Then check that exactly one ruleset applies to `main` (rulesets stack, section 9):

    gh api repos/<org>/<repo>/rules/branches/main --jq '[.[].ruleset_id] | unique'

Expected: one id. Here `[27648742]`.

## 6. The app, environments and secrets

The GitHub App `<app-slug>` (kit README, "Human setup checklist" item 2), as applied here:

- Permissions: Contents, Pull requests and Issues read and write; Projects read and write (organisation); Checks and Metadata read. Not Administration, Workflows, Secrets, Environments or Members.
- Installed on selected repositories only: `<repo>`.
- Its App ID in variable `FACTORY_APP_ID`. Its bot login, `<app-slug>[bot]`, must equal `bots.factory` in the policy.
- Not a member of `<team>`, on no bypass list.

| Environment | Deployment branches | Secret | Used by |
|---|---|---|---|
| `factory-merge` | `main` only (a custom branch policy) | `FACTORY_APP_PRIVATE_KEY`: the content of the app's private key | `factory-automerge.yml`, `factory-tickets.yml`, `factory-status.yml` |
| `factory-review` | — | `ANTHROPIC_API_KEY`: a key created for this purpose, ideally in its own Console workspace with a monthly spending limit | `factory-pr-review.yml`, `factory-doc-review.yml` |

Recommended for `factory-merge`: untick "Allow administrators to bypass configured protection rules". Considered and rejected for `factory-review`: a Claude subscription token (`claude setup-token`, action input `claude_code_oauth_token`). It shares the subscriber's usage limits, is tied to a personal account, and makes the installed workflows differ from the kit.

Set each secret interactively and paste the value at the prompt: `gh secret set <NAME> --env <ENV> -R <org>/<repo>`. Never from a file left on disk, never on the command line.

The model reviews in CI, with the key in `factory-review`, are the setup to port. **This repository's current state:** the account behind `factory-review` does not work yet, so the two review jobs run only when repository variable `FACTORY_REVIEW_IN_CI` is `true`, and the reviews run locally. A second app, `<reviewer-app-slug>` (Checks write, Pull requests write, Contents read, as applied here), posts `factory/review` and `factory/doc-review`; the ruleset accepts those two from it alone (section 5). How it runs and how it ends: "Model reviews run locally (temporary)" in [`AGENTS.md`](../../AGENTS.md) and [`local-review.mjs`](../../.github/scripts/local-review.mjs).

## 7. Acting as the app locally

Agents act as the app, never as the maintainer: a pull request the maintainer authored cannot get the maintainer's approval, and the audit trail must show who did what (kit README, checklist item 2). Here every factory action on a developer's machine goes through the token helper, [`.github/scripts/factory-token.mjs`](../../.github/scripts/factory-token.mjs). How to call it, which role uses which app, and the checks for a new machine are in "Running the factory locally" in [`AGENTS.md`](../../AGENTS.md).

What the helper does, to port it:

1. Builds a JWT signed RS256 with the app's private key: `iat` 60 seconds back, `exp` at most 10 minutes ahead (the helper uses 9), `iss` the App ID ([GitHub: generating a JWT](https://docs.github.com/en/apps/creating-github-apps/authenticating-with-a-github-app/generating-a-json-web-token-jwt-for-a-github-app)).
2. `GET /repos/{owner}/{repo}/installation` with the JWT: the installation `id`.
3. `POST /app/installations/{installation_id}/access_tokens` with body `{ "repositories": ["<repo>"] }`: a token that lives one hour, reaches only that repository and cannot exceed the app's permissions ([GitHub: REST API for apps](https://docs.github.com/en/rest/apps/apps)).
4. Runs the command without a shell, with `GH_TOKEN` and `GITHUB_TOKEN` set to the token, `GIT_TERMINAL_PROMPT=0`, and the app's bot account as git author and committer.
5. For `git`, replaces the credential helpers for that process only: `GIT_CONFIG_COUNT=2`; `GIT_CONFIG_KEY_0=credential.helper` with an empty `GIT_CONFIG_VALUE_0`, which resets the list; `GIT_CONFIG_KEY_1=credential.helper` with `GIT_CONFIG_VALUE_1=!f() { echo username=x-access-token; echo "password=$GH_TOKEN"; }; f` ([git-config, "ENVIRONMENT"](https://git-scm.com/docs/git-config); [gitcredentials](https://git-scm.com/docs/gitcredentials)).

Where the keys are: each app key is a file in a private per-user folder outside every repository, by default `~/.wtf-corp/yaf-factory/`, readable by the user only. User-level environment variables hold the paths, never the key: `YAF_FACTORY_KEY_FILE` and `YAF_REVIEWER_KEY_FILE`. The setup commands for each operating system are written once, in `AGENTS.md`, and not repeated here. If a key is lost, or may have been read by something it was not meant for, generate a fresh one on the app's settings page, delete the old one, and for the factory app update `FACTORY_APP_PRIVATE_KEY`. The token is never written to disk; it is printed only with `--print`, in the human's own terminal.

Keys as files depart from the default of `18` §4 ([`18-build-plan.md`](../spec/spec/18-build-plan.md), a key in the operating system's credential store): the owner accepted that on 2026-11-19, knowing that any process running as the user can read the files ([plan 0001](../../specs/0001-factory-setup-runbook/plan.md), deviation 4).

## 8. Prove the gates

The kit README's seven trials ("Install at M0" step 7), with throwaway pull requests. This list is the README's; the full gate acceptance criteria are M0's exit criteria in [`17` §7](../spec/spec/17-ci-release-docs.md) and are not repeated here.

| # | Trial | Expected | Proved by |
|---|---|---|---|
| 1 | A docs change | Tier low, merges by itself | #29 on 2026-11-20: a tier-low docs ticket, merged by itself as `app/yaf-factory`. #3 before it merged during a break-glass and proved nothing (section 9) |
| 2 | A `feat` with a public API line declared in a merged plan; then the same with an undeclared line | Tier medium, merges by itself; undeclared: `factory/delta` fails and the tier is high | |
| 3 | A change under `src/Http/Authentication/` | Tier high, waits; a bot's approval does not satisfy `factory/human-approval`, a human maintainer's does | |
| 4 | A change over `size.hard` (1000 here during the initial build, soft 400; the kit's default is 400) | `factory/size` fails; `size-accepted` applied by the app does not help, applied by a human maintainer it does, and the tier stays high | |
| 5 | `tier:low` on a tier-high pull request | Still high | |
| 6 | A pull request that shrinks `sensitivePaths` in `policy.json` and touches a sensitive path | Judged by the base branch's policy | |
| 7 | `FACTORY_PAUSED=true` | Nothing more gets auto-merge; one already armed still merges | |

## 9. Gotchas

- **Ticket or trivial.** `factory/ticket` passes a code pull request only when it closes exactly one open ticket from a plan on the base branch, or says `Trivial because:`, is not tier high and has at most 20 changed lines. The first real change after the install needs a merged plan; this runbook's plan was that case.
- **One maintainer login cannot approve its own pull requests.** Work through the app (section 7), or add a second human login to `<team>` and to `humans.logins`.
- **Git Bash rewrites `gh api /path`** as a Windows path. Drop the leading slash: `gh api repos/<org>/<repo>/…`.
- **`integration_id`**: a check of the same name reported by another app does not satisfy a pinned check (section 5).
- **Policy edits are additive** (section 3).
- **A break-glass releases every armed auto-merge.** Disabling the ruleset lets every open pull request with auto-merge armed merge; #3 merged unreviewed that way. Before a break-glass, disable auto-merge on the open pull requests (`gh pr merge <n> --disable-auto`). `FACTORY_PAUSED=true` stops new auto-merges and disarms none.
- **Rulesets stack.** An old default ruleset (one required approval, Copilot review) stayed active beside the factory ruleset here, and its approval rule blocked every auto-merge. Check for exactly one (section 5).
- **The app cannot push workflow changes.** It has no Workflows permission, on purpose, so a ticket that touches `.github/workflows/` is committed by its implementer and pushed by the owner.
- **Strict checks put pull requests behind each other** (section 5).
- **Agent worktrees are isolated and locked.** Each implementer gets `.claude/worktrees/agent-<id>`; its harness refuses git commands aimed at another worktree, and Claude Code holds a `git worktree lock` on it while the agent runs. To remove one by hand after the agent has ended: `git worktree unlock <path>`, then `git worktree remove <path>`.

## 10. Still to do here

- Move the model reviews back to CI: the steps end the section "Model reviews run locally (temporary)" in [`AGENTS.md`](../../AGENTS.md).
- Trials 2 to 7 (section 8).
- Strict back on or a merge queue, once the reviews are back in CI (section 5).
- A ruleset for `next` after `1.0.0`.
- The kit README's ["Verify before relying on it"](../spec/factory/README.md#verify-before-relying-on-it) list stays open.
- Checklist items 6 (the human-only labels are protected by process) and 7 (local settings and their gaps) are left to the [kit README](../spec/factory/README.md#human-setup-checklist).
