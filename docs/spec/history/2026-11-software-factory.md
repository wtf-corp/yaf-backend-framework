# The software factory, November 2026

This file records how the development process of the specification was turned into a software factory: what was asked for, what a short study of current practice found, what was decided, and what was written. It is history, not a rule. ADR-0029, ADR-0023 … 0028, `spec/17` and the kit in `factory/` are what bind.

It follows `2026-11-specification-review.md`, and changes some of what that record describes: the two approvals before code, `tasks.md`, `review.md` and "every merge by a human" no longer hold.

## 1. What was asked for

After the specification review, and before the specification moves to its own repository, the owner asked for the process to be "as much an automated software factory as possible", with a human in the loop, and named six properties:

1. Independent agents in worktrees where parallel work pays off.
2. Spec writing and spec reviewing by different agents, with a quality gate.
3. Code writing and automatic code review by different agents.
4. An automatic pull-request reviewer, a different agent again; tasks classified by risk, so that small work that touches neither security nor compliance merges automatically and the rest waits for a human.
5. Tickets created once a spec is confirmed, worked one by one with statuses, labels and references that commits and pull requests point to.
6. Small pieces of work and small pull requests in preference to large ones.

## 2. What the study found

There is no authoritative standard. The sources were practitioner and vendor articles, one research roadmap, and the documentation of the tools; they agree on the following.

| Finding | Where it went |
|---|---|
| Tier by blast radius with a deterministic floor: risk comes from a policy file and the paths touched; an agent may raise it, never lower it; authentication, data protection, migrations and irreversible operations always go to a human | Risk tiers, `policy.json`, `classify.mjs` |
| The component that writes never decides the result is good; reviewers get a fresh context and only the artefacts; several agents repeating one assumption add no safety | Separate roles, each reviewer with its own question |
| The orchestrator delegates and never edits; a workflow, not the agent, merges; fix loops are capped | Orchestrator skill, `factory-automerge.yml`, `limits.fixRounds` |
| One worktree per ticket is the usual isolation for parallel agents | `isolation: worktree` on the implementer |
| With heavy use of AI, pull requests grew by about 150 % and review time by about 90 % in one telemetry study; older review data, cited second-hand, puts effective review at 200–400 lines | Size limits 200 and 400 |
| Start automatic merge narrow and widen on measured results | Limits are policy values; a daily digest measures |
| The review action's own security notes: the model credential stays in the agent's environment | The CI reviewer has read-only tools and cannot execute the pull request's code |
| Claude Code reads skills only from `.claude/skills/` | A committed mirror of `.agents/skills/`, kept in step by a check |

The figures were not checked against the primary reports; they informed the starting values and nothing else.

## 3. Decisions

The option taken is in bold.

| # | Question | Options | Reason |
|---|---|---|---|
| 1 | Where agents run | **Hybrid: authoring locally, gating in GitHub Actions** · Everything in CI · Everything local | The gates work the same whoever wrote the change, and are out of the author's reach; authoring uses the local machine and can be steered |
| 2 | Where tickets live | **GitHub Issues and one Project** · Issues with status labels only · An external tracker | Same platform as code, checks and reviews; nothing to integrate |
| 3 | Source of truth for the work breakdown | **Tickets replace `tasks.md`** · Keep both, generated | No file to keep in step, and no conflicts between parallel agents appending to one log |
| 4 | Width of automatic merge | **Three tiers: low and medium merge automatically, high needs a human** · Only low · Everything outside the sensitive list | During the initial build most work is new features; without the medium tier nearly everything would wait for the owner. Medium requires that the change equals what an approved plan declared |
| 5 | Gates before code | **A human on every spec; on a plan only when it is flagged** · A human on both · A human on the spec only | A plan that schedules routine work need not wait; one that introduces public API, a schema change, a dependency, a deviation or a high-tier ticket does |
| 6 | Independence of reviewers | **One vendor by default, the agent selectable per role** (the owner asked for a mixture of the first two options) · A second vendor for the CI review · A second vendor for the high tier | Simple to start; another vendor can be set for any role in the policy |
| 7 | Size of pull requests | **Soft limit 200 lines, hard limit 400, both adjustable** · Hard limit only · Guidance without a check | Sizes creep without a check; the numbers are to be tuned from experience |
| 8 | Autonomy | **Bounded: three parallel implementers, two fix rounds, stop at the end of a milestone, a pause switch, a daily digest**, with room to raise the limits · One at a time · Six in parallel across milestones | Fast enough for sixteen milestones while the factory itself is new |
| 9 | What to produce | **Tool-neutral rules and a ready Claude Code kit** · Rules only | The factory cannot safely build its own gates, so they are written beforehand and installed by hand at M0 |

## 4. Design points settled while writing

- **"An agent never approves" stayed literally true.** The reviewing agent reports a status check; it never submits an approving review. Branch rules ask for the checks and for a code owner on the code-owned paths; check `factory/human-approval` supplies the human for tier high elsewhere.
- **`review.md` went with `tasks.md`.** The review record is the pull request: findings as review comments, one summary comment, the check.
- **The CI reviewer cannot run the pull request's code.** Executed evidence comes from jobs without secrets: build and test, and the report-only `factory/test-evidence`, which restores the non-test files to the base version and expects the new tests to fail. Only the local code reviewer runs commands itself.
- **Gate scripts and the policy are read from the base branch**, through a second checkout. The workflow file itself still comes from the pull request under the `pull_request` event; changes under `.github/**` are tier high and code-owned, and organisation-level required workflows are named as the stronger control.
- **Two environments.** `factory-review` holds the model API key; `factory-merge` holds the app's private key, and the workflows that use it never run on a pull request's own event.
- **Agents have their own identity**, GitHub App `yaf-factory`: a human cannot approve a pull request opened under that human's name.
- **A plan update is its own pull request** and merges before the code that deviates, because the gates read the plan from the base branch.
- **The declared delta is a subset check.** A plan covers several tickets, so every line a pull request adds to an approved file must match a declared entry; a removed line is always tier high.
- **`Directory.Packages.props` is sensitive but not code-owned**, with patch-only updates exempted, so a dependency patch can still be tier low.
- **Who is a human maintainer** is an explicit list of logins in the policy, because the workflow token cannot read team membership.
- **Another vendor per role is declared, not wired.** The workflows stop with a message when a role is not set to Claude; the kit's README says where to add the step.
- **Trivial changes** skip the ticket as well as the spec and plan, and are still classified, sized and reviewed.
- **Most plans of the initial build are flagged**, since they declare public API. The automatic path for plans matters mainly afterwards.
- **The templates do not generate the kit in 1.0**; a team that wants it copies it.

## 5. What was written

- **New:** ADR-0029; skills `yaf-orchestrating-work` and `yaf-reviewing-specs-and-plans`; the kit in `factory/` (49 files: seven agent definitions, Claude Code settings, seven workflows, the policy and its schema, the gate scripts and their tests, two review prompts, issue and pull-request templates, CODEOWNERS, labels and a setup script, a README with the install steps and the owner's setup checklist).
- **Rewritten or changed:** ADR-0020 and 0023 … 0028; `spec/01`, `spec/17`, `spec/18`; `README.md`; the three files under `agents/`; skills `yaf-writing-specs`, `yaf-writing-plans`, `yaf-reviewing-changes`, `yaf-commits-and-pull-requests`.
- **Renamed:** skill `yaf-implementing-tasks` to `yaf-implementing-tickets`.
- **Removed:** the `tasks.md` and `review.md` templates.

## 6. What has and has not been tested

- The gate scripts have 31 unit tests, which pass on Node 24: the tier floor cannot be lowered by a label, a sensitive path forces tier high, an oversize change forces tier high, an undeclared delta fails tier medium, size exclusions are respected, and the plan parser, guard, ticket, approval, verdict and status logic behave as written. Every script passes a syntax check.
- The workflow files and issue templates were not parsed by a YAML tool.
- **Nothing has run against GitHub or Claude Code.** No workflow has executed, no agent definition has been loaded, no ticket has been created.

## 7. To verify at M0, before any other milestone

The kit's README carries the full list. The points most likely to need a change:

- Whether the repository's plan offers the merge queue, rulesets, sub-issues and issue dependencies; the build plan gives a fallback for each.
- The inputs and outputs of the review action at the version in use, and that a pull request opened by the app triggers the review.
- That the checks hold against the gated party, by trial pull requests: a label cannot lower a tier; low and medium merge on their own; high waits for a human maintainer who is not the author; an oversize change fails; an undeclared delta fails; a change to the policy is judged by the policy on the base branch.
- That permission rules in the Claude Code settings behave as intended; their documented gaps mean the app token's scopes and the branch rules are the real control.
- Local identity: how the orchestrator obtains a short-lived token for the app.
