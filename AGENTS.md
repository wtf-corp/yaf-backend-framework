# AGENTS.md

The instructions for AI coding agents in this repository are kept in the specification bundle, not copied here. Read the root instructions now, in full. They are this file's content:

@docs/spec/agents/AGENTS.root.md

(Path: `docs/spec/agents/AGENTS.root.md`. Tools that do not follow `@` imports: open that file.)

## Path map

The bundle is written for an installed layout where its files sit at the repository root. Here they stay in `docs/spec/` and are referenced from where tools look for them. Whenever an instruction, skill, role definition, prompt or command names a path in the left column, use the right column.

| Named in the bundle | In this repository |
|---|---|
| `specs/0000-framework/` (the approved specification of the initial build) | `docs/spec/`; the normative files are in `docs/spec/spec/` |
| `adr/` | `docs/spec/adr/`. New ADRs go there too |
| `.agents/skills/<name>/`, `.claude/skills/<name>/` | Pointer stubs. The skill itself is `docs/spec/agents/skills/<name>/` |
| `.agents/README.md` | `docs/spec/agents/README.md` |
| `.claude/agents/<role>.md` | A pointer stub with the front matter Claude Code needs. The definition is `docs/spec/factory/.claude/agents/<role>.md` |
| `.github/factory/` (policy, gate scripts, prompts, schemas, labels, setup script) | `docs/spec/factory/.github/factory/`, used in place. Example: `node docs/spec/factory/.github/factory/size.mjs <base> <head>` |
| `.github/factory/README.md` (the factory kit) | `docs/spec/factory/README.md` |
| `.github/workflows/factory-*.yml` | Real files in `.github/workflows/`, installed from `docs/spec/factory/.github/workflows/` |
| `node .github/factory/sync-skills.mjs` | `node .github/scripts/sync-references.mjs` |
| `specs/NNNN-<slug>/` (feature specs, plans, research) | `specs/NNNN-<slug>/`, unchanged |

## Nested instructions

The nested `AGENTS.md` files are sections of `docs/spec/agents/AGENTS.nested.md`. When you work in a tree below, read its section there before editing. A tree that does not exist yet gets its own pointer file when a milestone creates it.

| Working in | Section of `AGENTS.nested.md` |
|---|---|
| `src/` | `src/AGENTS.md` |
| `tests/` | `tests/AGENTS.md` |
| `samples/` | `samples/AGENTS.md` |
| `templates/` | `templates/AGENTS.md` |
| `docs/` (the DocFX site, not `docs/spec/`) | `docs/AGENTS.md` |
| `docs/spec/adr/` | `adr/AGENTS.md` |
| `specs/` | `specs/AGENTS.md` (also `specs/AGENTS.md` in this repository) |

## Running the factory locally

On a developer's machine every factory action goes through the token helper, `.github/scripts/factory-token.mjs`. It runs one command as a GitHub App, with a short-lived token for this repository. The owner's decisions behind this section are in the decision log of [plan 0002](specs/0002-local-factory-operation/plan.md#decision-log).

**Identities.**

- Implementer and orchestrator act as `yaf-factory`: `node .github/scripts/factory-token.mjs --app factory -- <gh or git command>`. That includes `git commit`, so that author and committer are the app.
- The local review posts as `yaf-reviewer`: `node .github/scripts/factory-token.mjs --app reviewer -- node .github/scripts/local-review.mjs <pr-number>`.
- `origin` must be an HTTPS remote. Over SSH `git` would use the person's key, so the helper refuses to run it. Switch with `git remote set-url origin https://github.com/<owner>/<name>.git`.
- Never the user's login for a factory action: committing or pushing a ticket's branch, opening or editing a pull request, commenting, labelling, setting status, posting a check.
- `--print` writes the token instead of running a command. It is for the human's own terminal only: never in an agent session, a log or CI.

**What keeps author and reviewer apart** is a rule of procedure: the implementer never runs the review. It never runs the helper with `--app reviewer` and never runs `local-review.mjs`. The orchestrator holds both identities: it opens the pull request as the factory app and runs the review of that same pull request as the reviewer app, in a fresh reviewer session that it does not brief. The rule does not stop the orchestrator's session, which can mint both tokens: a pull request in tier low or medium could pass `factory/review` and merge with no human. The owner accepted this on 2026-11-19, until the reviews are back in CI (plan 0002, decision log).

**Order per pull request.**

1. The orchestrator opens the pull request as the factory app.
2. The gates run in CI.
3. The orchestrator runs the local review as the reviewer app, and again after every push.
4. Tier low and medium merge by themselves. A tier-high pull request and a flagged plan wait for the owner's approving review.

**Key setup.** This is the one copy of these commands. The two private keys are files in `~/.wtf-corp/yaf-factory/` (on Windows `~` is `%USERPROFILE%`), outside every repository, under GitHub's download names `yaf-factory.<date>.private-key.pem` and `yaf-reviewer.<date>.private-key.pem`. `YAF_FACTORY_KEY_FILE` and `YAF_REVIEWER_KEY_FILE` hold the full paths, never the key.

Linux and macOS (the helper refuses a key file that group or others can read):

```bash
mkdir -p ~/.wtf-corp/yaf-factory && chmod 700 ~/.wtf-corp/yaf-factory
chmod 600 ~/.wtf-corp/yaf-factory/*.pem
# in the shell profile:
export YAF_FACTORY_KEY_FILE="$HOME/.wtf-corp/yaf-factory/yaf-factory.<date>.private-key.pem"
export YAF_REVIEWER_KEY_FILE="$HOME/.wtf-corp/yaf-factory/yaf-reviewer.<date>.private-key.pem"
```

Windows, in PowerShell (the helper cannot check the ACL there and only warns):

```powershell
$dir = "$env:USERPROFILE\.wtf-corp\yaf-factory"
New-Item -ItemType Directory -Force $dir
icacls $dir /inheritance:r /grant:r "$($env:USERNAME):(OI)(CI)F"
[Environment]::SetEnvironmentVariable('YAF_FACTORY_KEY_FILE', "$dir\yaf-factory.<date>.private-key.pem", 'User')
[Environment]::SetEnvironmentVariable('YAF_REVIEWER_KEY_FILE', "$dir\yaf-reviewer.<date>.private-key.pem", 'User')
```

Confirm with `icacls <dir>` that only the entries you expect are left. The command adds none for SYSTEM or Administrators: add `/grant:r "*S-1-5-18:(OI)(CI)F"` (SYSTEM) and `/grant:r "*S-1-5-32-544:(OI)(CI)F"` (Administrators) if you want them; the SIDs work in every display language. On Windows a user-level variable reaches only processes started afterwards: restart the terminal and Claude Code.

**Project variables.** Set `FACTORY_PROJECT_OWNER` and `FACTORY_PROJECT_NUMBER` as user-level environment variables too, with the values of the repository variables of the same names. `status.mjs` needs them, and the app's token cannot read them.

**What the setup does not give.**

- Any process running as the user, agents included, can read the key files. The two deny rules in `.claude/settings.json` (reading the key folder, running the helper with `--print`) stop accidents, not a determined agent. A key that may have been read is deleted on the app's settings page and replaced. Keys as files depart from the default of `18` §4 (`docs/spec/spec/18-build-plan.md`) by the owner's decision (plan 0002, decision log).
- The helper's check of `git` commands looks only at the first word of the wrapped command and at whether `origin` of its working directory is HTTPS. It does not cover:
  - a `git` started by another program the helper runs: `gh`, or a `node` script such as `local-review.mjs` with its own `git fetch`;
  - an HTTPS `origin` on a host other than github.com; the credential helper it sets answers any host (#58);
  - `git -C <dir>`, `--git-dir` or `GIT_DIR` pointing at another repository (the orchestrator's `git -C <worktree>` stays within this repository, whose worktrees share its remotes);
  - a push to a remote other than `origin`;
  - `git -c` overrides.

**Check when setting up a machine,** by its user in their own terminal (no agent runs it):

```bash
node .github/scripts/factory-token.mjs --app factory -- gh api repos/<owner>/<name> --jq .full_name   # prints the repository
node .github/scripts/factory-token.mjs --app factory -- gh api user                # must fail: an app's token cannot read /user
node .github/scripts/factory-token.mjs --app reviewer -- git ls-remote origin HEAD  # one commit id, no credential prompt
node .github/scripts/factory-token.mjs --app factory -- git var GIT_AUTHOR_IDENT    # yaf-factory[bot] and its noreply address
```

## Model reviews run locally (temporary)

Until the `factory-review` environment has a working API account, the two model reviews do not run in CI. `.github/workflows/factory-pr-review.yml` and `factory-doc-review.yml` are switched off by repository variable `FACTORY_REVIEW_IN_CI`. This overrides what the bundle (ADR-0027, ADR-0029, the kit README, `yaf-orchestrating-work`) says about the CI reviewer, and only that. The review itself, its prompt, schema, model, read-only tools, checks and fix rounds are unchanged.

- **Who runs it.** The orchestrator, after it opens a pull request and after every push to one, once the gates have run. Never the implementer of that pull request, never in the implementer's worktree.
- **How.** `node .github/scripts/factory-token.mjs --app reviewer -- node .github/scripts/local-review.mjs <pr-number>`: the helper runs it as the **reviewer app** (`yaf-reviewer`). It checks out the base and head in a temporary directory, runs a fresh `claude -p` reviewer there with the base branch's prompt and schema, and posts `factory/review` or `factory/doc-review` (and the other one as not applicable). `--dry-run` prints the verdict and posts nothing.
- **Which identity.** Only the reviewer app: the main ruleset accepts these two checks from it alone. The factory app cannot write checks, and that stays so: the identity that opens pull requests must not be able to pass its own review. Never post them with a personal token.
- **What it costs in trust.** In CI the reviewer ran on a clean runner from the base branch. Locally it runs on the maintainer's machine: its reads are limited to the temporary checkout by permission rules. Author and reviewer are kept apart by a rule of procedure: the implementer never runs the review. It never runs the helper with `--app reviewer` and never runs `local-review.mjs`. The owner accepted on 2026-11-19 that the separation rests on that rule alone until the reviews move back to CI ([plan 0002](specs/0002-local-factory-operation/plan.md#decision-log)). Treat it as a stopgap.

This should move back to CI: fix the account, set `FACTORY_REVIEW_IN_CI=true`, point the two checks in the ruleset back at GitHub Actions (`integration_id` 15368), delete `local-review.mjs`, the `reviewer` entry of `APPS` in `factory-token.mjs` (the entry, not the helper) and this section.

## Token economy

This overrides the bundle (ADR-0027 decision 4, ADR-0029, the kit README, `yaf-orchestrating-work`) on the points below, and only those. The plan is [plan 0004](specs/0004-token-economy/plan.md).

**Why.** On 2026-11-19 two 5-hour usage limits were hit in one day while plans 0002 and 0003 ran. The causes: a Fable implementer on every tier-high ticket, Fable reviews, two reviews per ticket, fix rounds for Minor findings, three implementers at once and very long orchestrator contexts. The owner decided on 2026-11-20: "fable is strong, but burns the tokens way too quickly. so basically let's run the implementation on Opus, while design, research, architecture, etc may prompt for a stronger model if this seems sensible" ([plan 0004](specs/0004-token-economy/plan.md#decision-log), decision log).

**Models.**

- Every role runs on Opus (`policy.json` `roles`). `implementer-high` is not spawned.
- Fable only for a spec writer, planner or ADR writer, for one task. The orchestrator, or the writer in its report before it starts the work, proposes it with a one-line reason. It is used only after the owner says yes in their own message. The pull request body then says "Model: Fable, agreed by the owner on <date>".
- Never Fable for an implementer or a reviewer.
- The policy has no field for this rule, which is why it is here.

**One review per ticket.** While the reviews run locally, step 5.3 of `yaf-orchestrating-work` (the `code-reviewer`) is skipped. The local `factory/review` is the review. For a worktree in state `local-review` the orchestrator does steps 5.1 and 5.2 and writes the `factory:local-review` marker with their result. That marker then records those steps, not a code review. What it costs: no reviewer runs the tests with and without the change. The CI test jobs and the report-only `factory/test-evidence` are the executed evidence.

**Fix rounds.** Only a `changes-required` verdict (a Blocker or Major finding, or a failed pass) starts a fix round. Minors and Nits go in one comment on the ticket. At the end of a batch the orchestrator opens one follow-up issue per plan that lists them, without the `factory` label, for the owner to triage. A reviewer the orchestrator briefs checks the ticket's scenarios and the diff, with no mutation or probe campaigns.

**Lean orchestration.**

- At most two implementers at once (`limits.parallelImplementers`).
- Agent reports are at most 15 lines: branch, head commit, commands with exit codes and counts, size, deviations.
- A fresh session, or `/compact`, after each batch of tickets. The Project, the tickets and their marker comments are the handover, nothing in the session.

**When it ends.** When the reviews move back to CI, the owner decides whether the second review returns. Until then this section stands.

## Changing the bundle

Everything under `docs/spec/` is the approved specification and the factory's guard files: tier high and code-owned. After changing a role definition, a skill, `.claude/settings.json` or a GitHub template in the bundle, run `node .github/scripts/sync-references.mjs` and commit the regenerated references. CI check `references` fails when they are out of step.
