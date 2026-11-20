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
| 6. Human setup checklist | Section 4 and the GitHub settings |
| 7. Prove the gates | After the GitHub settings |

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
