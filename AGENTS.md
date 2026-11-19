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

## Model reviews run locally (temporary)

Until the `factory-review` environment has a working API account, the two model reviews do not run in CI. `.github/workflows/factory-pr-review.yml` and `factory-doc-review.yml` are switched off by repository variable `FACTORY_REVIEW_IN_CI`. This overrides what the bundle (ADR-0027, ADR-0029, the kit README, `yaf-orchestrating-work`) says about the CI reviewer, and only that. The review itself, its prompt, schema, model, read-only tools, checks and fix rounds are unchanged.

- **Who runs it.** The orchestrator, after it opens a pull request and after every push to one, once the gates have run. Never the implementer of that pull request, never in the implementer's worktree.
- **How.** `node .github/scripts/local-review.mjs <pr-number>` with `GH_TOKEN` set to an installation token of the **reviewer app** (`yaf-reviewer`). It checks out the base and head in a temporary directory, runs a fresh `claude -p` reviewer there with the base branch's prompt and schema, and posts `factory/review` or `factory/doc-review` (and the other one as not applicable). `--dry-run` prints the verdict and posts nothing.
- **Which identity.** Only the reviewer app: the main ruleset accepts these two checks from it alone. The factory app cannot write checks, and that stays so: the identity that opens pull requests must not be able to pass its own review. Never post them with a personal token.
- **What it costs in trust.** In CI the reviewer ran on a clean runner from the base branch. Locally it runs on the maintainer's machine: its reads are limited to the temporary checkout by permission rules, and the reviewer app's key must be kept where agents do not read it. Treat it as a stopgap.

This should move back to CI: fix the account, set `FACTORY_REVIEW_IN_CI=true`, point the two checks in the ruleset back at GitHub Actions (`integration_id` 15368), delete `local-review.mjs` and this section.

## Changing the bundle

Everything under `docs/spec/` is the approved specification and the factory's guard files: tier high and code-owned. After changing a role definition, a skill, `.claude/settings.json` or a GitHub template in the bundle, run `node .github/scripts/sync-references.mjs` and commit the regenerated references. CI check `references` fails when they are out of step.
