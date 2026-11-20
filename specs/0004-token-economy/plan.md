# 0004 — Token economy: plan

**Spec** none: this plan has no `spec.md`. Its basis is the owner's decision of 2026-11-20 (decision log), applied to ADR-0027 (`docs/spec/adr/0027-review.md`) decision 4, ADR-0029 (`docs/spec/adr/0029-software-factory.md`) §2, §6 and the roles table, the kit README (`docs/spec/factory/README.md`) "Agent definitions" and "Tuning the policy", the skill `yaf-orchestrating-work` (step 5.3, rules 5 and 10), and the `AGENTS.md` section "Model reviews run locally (temporary)" · **Status** Draft

Why there is no spec: the change tunes how the factory spends model usage. It adds no behaviour to YAF and no requirement of its own. Plans 0001 to 0003 took the same route. Having no spec is a deviation and is listed as one.

## Approach

On 2026-11-19 two 5-hour usage limits were hit in one day while plans 0002 and 0003 ran. The causes: a Fable implementer on every tier-high ticket (all of them, since the factory's own files are tier high), Fable reviews, two reviews per ticket, fix rounds for Minor findings, three implementers at once, and very long orchestrator contexts. The owner chose four measures; this plan puts each where the factory reads it, with the smallest change.

1. **Models (T1).** `policy.json` `roles` is what the orchestrator passes when it spawns an agent and what `scope.mjs` gives `local-review.mjs`. Every role gets `"model": "opus"`; `roles.implementer` loses `modelByTier`, so `implementer-high` is no longer spawned. The kit definitions whose `model:` default is `fable` (`planner`, `plan-reviewer`, `pr-reviewer`) get `opus`, so that an agent spawned without the override does not fall back to Fable; the kit README asks to keep the two in step. `implementer-high` keeps `fable` and is left alone: nothing spawns it, and plan 0003's open #42 changes that file. Fable stays available for one task at a time: a writer of design, research or architecture work (spec writer, planner, ADR writer) may propose it, and it is used only after the owner says yes. The policy cannot hold that rule: `policy.schema.json` `$defs.role` allows `agent`, `model` and `modelByTier` only, with `additionalProperties: false`, and has no note field. So the rule is prose in `AGENTS.md` (T2). The skills are not changed: every agent loads `AGENTS.md`, and the nearest `AGENTS.md` wins over a skill.
2. **One review per ticket (T2).** While the reviews run locally, the local `factory/review` (the required check, posted by `local-review.mjs`) is the review. The skill's step 5.3, the separate `code-reviewer`, is skipped: after steps 5.1 (re-run the verification) and 5.2 (size and `touches`) the orchestrator opens the pull request. `inflight.mjs` `worktreeState` (merged with #57) moves a worktree to `open-pr` only after a `factory:local-review` marker with `ready`; under the override the orchestrator writes that marker from the result of steps 5.1 and 5.2, and `AGENTS.md` says that it then records those steps, not a code review. No script changes. This is an override in `AGENTS.md`, like the section on local reviews.
3. **Fix rounds only for Blocker or Major (T2).** The review prompt already has this rule: `prompts/pr-review.md`, "Verdict": `ready` "only when no pass failed and there is no Blocker or Major", and `yaf-reviewing-changes` "Severity" passes a Minor as "author's call, recorded". What cost rounds was the orchestrator sending Minors back, and the local code reviewer's revert-and-run campaigns (`code-reviewer.md`, "Acceptance"), which measure 2 removes. So no prompt or skill change: `AGENTS.md` tells the orchestrator to start a fix round only on `changes-required`, to record Minors and Nits on the ticket and to batch them into follow-up issues, and to brief any reviewer it starts with the ticket's scenarios and the diff, with no mutation or probe campaigns.
4. **Lean orchestration (T1, T2).** `limits.parallelImplementers` goes from 3 to 2 in the policy, not in prose only: rule 5 of the skill and the kit README read the number from there, and a prose limit beside a policy of 3 would be two answers. Short reports and a fresh session or `/compact` per batch are behaviour, so they are prose in `AGENTS.md`; the handover is the Project, the tickets and their marker comments, which plan 0003 already made the run's record.

Files read to decide: `AGENTS.md`, `docs/spec/agents/AGENTS.root.md`, `docs/spec/factory/README.md`, ADR-0027, ADR-0029, `docs/spec/factory/.github/factory/policy.json`, `policy.schema.json`, `scope.mjs`, `inflight.mjs`, `markers.mjs`, `plan.mjs`, `create-tickets.mjs`, `prompts/pr-review.md`, `.github/scripts/local-review.mjs`, the skills `yaf-orchestrating-work`, `yaf-reviewing-changes`, `yaf-writing-plans`, the kit agent definitions, and plans 0002 and 0003.

### Alternatives rejected

| Alternative | Why not |
|---|---|
| A note or `askFirst` field in the policy for Fable | The schema has none and forbids extra properties; a schema change for one sentence, and nothing would read it |
| The Fable rule also in `yaf-writing-specs`, `yaf-writing-plans`, `yaf-writing-adrs` | Three more guard files for one rule `AGENTS.md` already gives every agent |
| Change the review prompt or `yaf-reviewing-changes` for measure 3 | The prompt's verdict rule already lets Minors pass; the change is the orchestrator's |
| Change `inflight.mjs` to skip the `local-review` state | A script change, tests and a ticket, for a temporary override; the marker written from steps 5.1 and 5.2 needs none |
| The parallel limit as prose only | The skill reads `limits.parallelImplementers`; two numbers would disagree |
| Edit the kit skill `yaf-orchestrating-work` instead of overriding it | The override is local and temporary like the local reviews; the kit stays as decided |
| Delete `implementer-high` or set it to `opus` | Unused once `modelByTier` is gone; #42 changes the same file |

## Affected packages

| Layer | Package / area | Change |
|---|---|---|
| Core … Analyzers | none | none |
| Sample | none | none |
| Templates | none | none |
| Docs | factory (guard files) | `docs/spec/factory/.github/factory/policy.json`, three kit agent definitions and their generated references (T1); `AGENTS.md` section "Token economy" (T2) |

## Public API, schema and error-code delta

None. Breaking: no. New dependency: no.

```factory:delta
{ "publicApi": [], "schema": [], "errorCodes": [], "contracts": [], "configuration": [], "telemetry": [], "dependencies": [] }
```

## Schema and migration plan

None.

## What T2 writes

A section `## Token economy` in `AGENTS.md`, after "Model reviews run locally (temporary)". It overrides the bundle (ADR-0027 decision 4, ADR-0029, the kit README, `yaf-orchestrating-work`) on these points only:

- **Why.** The owner's words and date, quoted from the decision log; the two limits hit on 2026-11-19 and their causes.
- **Models.** Every role runs on Opus (`policy.json` `roles`). `implementer-high` is not spawned. Fable: only a spec writer, planner or ADR writer, for one task, proposed with a one-line reason (by the orchestrator, or by the writer in its report before it starts the work), and used only after the owner's yes in their own message; the pull request body says "Model: Fable, agreed by the owner on <date>". Never for an implementer or a reviewer. The policy has no field for this, which is why it is here.
- **One review per ticket.** While the reviews run locally, step 5.3 of `yaf-orchestrating-work` is skipped; the local `factory/review` is the review. For a worktree in state `local-review` the orchestrator does steps 5.1 and 5.2 and writes the `factory:local-review` marker with their result; that marker then records those steps, not a code review. What it costs: no reviewer runs the tests with and without the change; the CI test jobs and the report-only `factory/test-evidence` are the executed evidence.
- **Fix rounds.** Only a `changes-required` verdict (a Blocker, a Major or a failed pass) starts a fix round. Minors and Nits go in one comment on the ticket; at the end of a batch the orchestrator opens one follow-up issue per plan listing them, without the `factory` label, for the owner to triage. A reviewer the orchestrator briefs checks the ticket's scenarios and the diff, with no mutation or probe campaigns.
- **Lean orchestration.** At most two implementers (`limits.parallelImplementers`). Agent reports are at most 15 lines: branch, head commit, commands with exit codes and counts, size, deviations. A fresh session, or `/compact`, after each batch of tickets; the Project, the tickets and their marker comments are the handover, nothing in the session.
- **When it ends.** When the reviews move back to CI, the owner decides whether the second review returns; until then this section stands.

## Plan 0003's open tickets

Each follows this decision without a plan update. The orchestrator adds one line to each brief: "`AGENTS.md`, Token economy, overrides the skill's step 5.3, the models and the parallel limit."

- **#42** (0003 T11, allowlist): unchanged. It edits `implementer-high`, which is unused now; harmless.
- **#44** (0003 T13, skill): unchanged; the kit skill keeps the full process, including the code reviewer in step 5, and `AGENTS.md` overrides it. It must not name a model or the number 3.
- **#45** (0003 T14, `/factory-run`): the command defers to the skill's step 5 and the parallel limit of the policy; it does not spawn `code-reviewer` or `implementer-high` by name, and names no model.
- **#46** (0003 T15, `AGENTS.md`): runs after T2 (same file); its subsection must not repeat or contradict "Token economy".
- **#39** (0003 T8, pull request #63) changes `policy.json`: **T1 waits for it**. `Depends on` cannot name it, so the orchestrator labels T1 `blocked` with a comment naming #39, and removes the label when #39 has closed.

## Test strategy

| What must be true | Ticket | Level | Check |
|---|---|---|---|
| Every role is `opus`, no `modelByTier`, `parallelImplementers` is 2; the kit tests still pass | T1 | check | T1's verify command; V1 |
| The three definitions default to `opus`; the references are in step | T1 | check | T1's verify command; V1 |
| `AGENTS.md` has the section with the points above | T2 | documentation check | T2's verify command; the reviewer reads it against "What T2 writes" |

## Risks and rollback

| Risk | Likelihood | Mitigation | Rollback |
|---|---|---|---|
| Opus misses what Fable caught on tier-high work | Medium | The owner still approves every tier-high pull request; a writer can ask for Fable on design work | Set the role back in the policy |
| One review lets through what the local code reviewer would have found by running the tests | Medium | CI test jobs and `factory/test-evidence`; the prompt's pass 1 still asks why each test fails without the change | Remove the override from `AGENTS.md` |
| Batched Minors are never fixed | Medium | One follow-up issue per plan, triaged by the owner | none needed |
| The orchestrator's `ready` marker is read as a code review | Low | `AGENTS.md` says what it records | none needed |

## Verification commands

Git Bash, repository root, on the ticket's branch; `BASE` is `origin/main`. V1, every ticket:

    node --test "docs/spec/factory/.github/factory/tests/*.test.mjs"
    node --test ".github/scripts/tests/*.test.mjs"
    node .github/scripts/sync-references.mjs --check
    git diff --name-only $BASE...HEAD
    node docs/spec/factory/.github/factory/size.mjs $BASE HEAD

Expected: exit 0 and 0 failed for the first two; the references are in step; only the ticket's `Touches`; no "over the … limit" suffix.

## Research

None needed: the plan relies on no third-party behaviour.

## Decision log

| Date | Decision | Reason | By |
|---|---|---|---|
| 2026-11-20 | **"fable is strong, but burns the tokens way too quickly. so basically let's run the implementation on Opus, while design, research, architecture, etc may prompt for a stronger model if this seems sensible"**, documented with the reason in place. All four measures: models, one review per ticket, fix rounds only for Blocker or Major, lean orchestration | Two 5-hour usage limits hit in one day while running plans 0002 and 0003 | Human owner, relayed by the orchestrator |
| 2026-11-20 | The Fable rule lives in `AGENTS.md`, not the policy or the skills | The schema has no field for it; `AGENTS.md` reaches every agent | Planner |
| 2026-11-20 | The parallel limit is the policy value 2, not prose | The skill reads the policy; one number | Planner |
| 2026-11-20 | Measure 3 is `AGENTS.md` only; the prompt and `yaf-reviewing-changes` are unchanged | Their verdict rule already passes Minors | Planner |
| 2026-11-20 | Under the override the orchestrator writes the `factory:local-review` marker from steps 5.1 and 5.2; `inflight.mjs` is unchanged | No script change for a temporary override | Planner |

## Deviations from the specification

Three departures, for the owner to accept or refuse by reviewing this plan's pull request.

1. **No spec** for a change that is not an initial-build milestone (ADR-0023, ADR-0025). It is factory tuning with no requirement of its own, as plans 0001 to 0003 were.
2. **One review instead of two.** ADR-0027 decision 4 asks for "two independent agent reviews … a local one before the pull request is opened, one in CI", and "Neither replaces the other"; ADR-0029's roles table and `yaf-orchestrating-work` step 5.3 put the `code-reviewer` before the pull request. T2 skips it while the reviews run locally. What it costs: the reviewer that ran the tests with and without the change is gone; executed evidence comes from CI's test jobs and the report-only `factory/test-evidence`.
3. **The kit's model and parallel defaults change.** The policy's roles (Fable for planner, plan reviewer, pull-request reviewer, and for tier-high implementers through `modelByTier`), the kit README's "default: tier high on the stronger model", the three definitions' `model:`, and `limits.parallelImplementers = 3` (ADR-0029 §6, `17` §2.2) become Opus everywhere and 2. The kit README allows both as policy values ("Tuning the policy"); the values the ADRs name are departed from.

## Tickets

Both are tier high by path (guard files) and wait for the owner. They share no file and may run in parallel; T1 also waits for #39 (above).

| Key | Title | Type | Area | Requirements | Depends on | Touches | Tier | Size | Failing test first | Verify |
|---|---|---|---|---|---|---|---|---|---|---|
| T1 | ci: run every factory role on Opus with two implementers at once | chore | factory | Decision log 2026-11-20, measures 1 and 4 | — | `docs/spec/factory/.github/factory/policy.json`, `docs/spec/factory/.claude/agents/planner.md`, `docs/spec/factory/.claude/agents/plan-reviewer.md`, `docs/spec/factory/.claude/agents/pr-reviewer.md`, `.claude/agents/planner.md`, `.claude/agents/plan-reviewer.md`, `.claude/agents/pr-reviewer.md` | high | 15 | The policy check of Verify exits 1 and prints `planner planReviewer prReviewer implementer 3`. Blocked until #39 has closed | `node -e "const p=require('./docs/spec/factory/.github/factory/policy.json');const r=p.roles,bad=Object.keys(r).filter(k=>r[k].model!=='opus'),tier=Object.keys(r).filter(k=>r[k].modelByTier);console.log(bad.concat(tier).join(' '),p.limits.parallelImplementers);process.exit(bad.length+tier.length===0&&p.limits.parallelImplementers===2?0:1)"` exits 0 and prints ` 2`; `grep -c "^model: opus" docs/spec/factory/.claude/agents/planner.md docs/spec/factory/.claude/agents/plan-reviewer.md docs/spec/factory/.claude/agents/pr-reviewer.md` prints 1 for each; V1 |
| T2 | docs: describe the token economy in AGENTS.md | docs | factory | Decision log 2026-11-20, measures 1 to 4; "What T2 writes" | — | `AGENTS.md` | high | 45 | The loop of Verify prints nothing, because the section does not exist | `for s in "^## Token economy" "Fable" "step 5.3" "Blocker or Major" "/compact" "2026-11-20"; do grep -q -e "$s" AGENTS.md && echo "ok: $s"; done` prints six `ok:` lines; V1 |
