# ADR-0025 — Implementation plans

> **Status** Accepted · **Date** 2026-11 · **Deciders** maintainers

## Context

An approved specification (ADR-0024) says what must be true. Before code is written, someone accountable needs to be able to see how it will be made true, in which packages, with which public API and schema changes, and in what order — and the factory (ADR-0029) needs the work cut into tickets small enough to implement, verify, review and merge one at a time, some of them in parallel.

## Drivers

- The approach must be checked before effort is spent on it, and seen by a human when it carries risk.
- Work must be divisible into units small enough to review properly and independent enough to hand to separate agents.
- What a plan will change in the public surface must be stated in a form a script can compare with what the code then changes.
- Progress and deviations must be visible to someone who was not in the session.
- A plan must be executable by an agent that has only the repository and the ticket.

## Options considered

1. **Plan in the conversation only.** Lost when the session ends; nothing to review against.
2. **One plan document with prose steps.** Reviewable, but steps are too coarse to verify and to resume.
3. **Two files: `plan.md` for the approach, `tasks.md` for ordered tasks with a progress log.** Everything is in the repository; but a second place tracks the same work once tickets exist, and agents working in parallel conflict on one log file.
4. **One file, `plan.md`, ending in a ticket list from which the tickets are created; after that the tickets are the source of truth.**

## Decision

Option 4. `plan.md` lives next to the spec in `specs/NNNN-<slug>/`. `research.md` is optional and holds findings the plan relies on (documentation checked, experiments run), with sources. There is no `tasks.md` and no progress log file.

For a milestone of the initial build the folder holds no `spec.md`: the plan names the files and sections of `specs/0000-framework/` it implements, and those sections' requirements and acceptance criteria take the place of `R…` and `S…` ids (cited as, for example, `05 §4` and `05 §11 item 3`). Such a plan also carries a **decision log** (open points settled, with the reason) and a **deviations from the specification** section; a deviation that changes scope, security or a public contract is a question for the human owner before the plan is submitted.

### `plan.md` — how

Rules:

1. Describe the approach in a few paragraphs and name the alternatives that were rejected and why.
2. List affected packages **by layer, in this order**: Core → Domain → Application → Infrastructure → Persistence → Http → adapters → Testing → Analyzers → sample → templates → docs.
3. State the public API delta exactly (added, changed, removed members; configuration keys; error codes), in prose **and** in the machine-readable `factory:delta` block.
4. A schema change follows expand/contract: this release's migrations work with the previous release's code.
5. Map the test strategy to the spec's acceptance scenarios: each scenario names the test level, the project and the ticket that will prove it.
6. List risks with a rollback for each.
7. List the verification commands that prove the whole change.
8. End with the **Tickets** table.

### The `factory:delta` block

A fenced block that declares everything the plan's tickets may add to the approved files. Check `factory/delta` compares it with what a pull request actually changes there (ADR-0029): a tier-medium pull request merges automatically only when every added line is declared.

````markdown
```factory:delta
{
  "publicApi": [
    "YAF.Domain.Specification<T>*"
  ],
  "schema": [],
  "errorCodes": ["core.specification.not_translatable"],
  "contracts": [],
  "configuration": [],
  "telemetry": [],
  "dependencies": []
}
```
````

The block is JSON with exactly these seven keys. An empty list means "none", an entry ending in `*` matches by prefix, and a missing key fails the plan review. Anything under `publicApi`, `schema` (migration names) or `dependencies` flags the plan, as does a breaking change, which is stated in the prose delta above and is always tier high (see Approval).

### The Tickets table

Rules:

1. One ticket is **one concern, one pull request**, estimated within the soft size limit (200 changed lines of hand-written code and tests; ADR-0029). A ticket with "and" in its title is two tickets.
2. Each ticket has: a key `T1…`; a title in Conventional Commit form; the requirement ids or sections it serves; the tickets it depends on; what it `touches` (paths or areas — the orchestrator uses this to decide what may run in parallel); the expected tier; a size estimate; **the failing test to write first**; the verification command with the expected result.
3. A ticket that cannot name its failing test is not ready; split it or go back to the approach. Pure documentation tickets name the docs build as their verification.
4. No ticket depends on a later one. Dependent tickets form a stack and merge in order.
5. Every requirement and every acceptance scenario is served by at least one ticket.
6. Tickets that touch a shared file (`Directory.Packages.props`, `PublicAPI.Unshipped.txt`, `errors.approved.txt`, a migrations folder) say so in `touches`; they will not run in parallel.

### Template

````markdown
# NNNN — <Title>: plan

**Spec** ./spec.md (merged in #nnn) — or, for an initial-build milestone, the sections of `specs/0000-framework/` · **Status** Draft | Approved (informative)

## Approach

…

### Alternatives rejected

| Alternative | Why not |
|---|---|

## Affected packages

| Layer | Package / area | Change |
|---|---|---|
| Core | | |
| Domain | | |
| Application | | |
| Infrastructure | | |
| Persistence | | |
| Http | | |
| Adapters | | |
| Testing | | |
| Analyzers | | |
| Sample | | |
| Templates | | |
| Docs | | |

## Public API delta

- Added: …
- Changed: …
- Removed: …

```factory:delta
{ "publicApi": [], "schema": [], "errorCodes": [], "contracts": [], "configuration": [], "telemetry": [], "dependencies": [] }
```

## Schema and migration plan

none / expand step, contract step, compatibility with the previous release

## Test strategy

| Scenario | Level | Test project | Ticket | Notes |
|---|---|---|---|---|
| S1 | integration | tests/… | T2 | |

## Risks and rollback

| Risk | Likelihood | Mitigation | Rollback |
|---|---|---|---|

## Verification commands

    dotnet build YAF.slnx -c Release
    dotnet test YAF.slnx
    …

## Decision log (initial-build milestones; optional otherwise)

| Date | Decision | Reason | By |
|---|---|---|---|

## Deviations from the specification (initial-build milestones)

none / section, what differs, why

## Tickets

| Key | Title | Requirements | Depends on | Touches | Tier | Size | Failing test first | Verify |
|---|---|---|---|---|---|---|---|---|
| T1 | feat(domain): specification base type | R1, R2 | — | src/Domain/Specifications/** | medium | 120 | `SpecificationTests.And_combines_both` — fails: type missing | `dotnet test tests/Domain.Tests --filter-query …` → passes |
| T2 | … | | T1 | | | | | |
````

### From plan to tickets

When the plan's pull request merges, workflow `factory-tickets.yml` creates one issue per row, idempotently (each issue carries the hidden marker `<!-- factory:ticket specs/NNNN-slug#T3 -->`), under the milestone's or spec's `epic` issue, with dependencies as "blocked by" links, and adds them to the Project in `Backlog` (ADR-0029). The issue body repeats the row and links `specs/NNNN-slug/plan.md#T3`.

From then on **the tickets are the source of truth** for the work: status, evidence, questions and deviations are on the issue and its pull request. `plan.md` stays the record of the approach and of what was declared.

### Plan quality checklist

The plan reviewer (ADR-0029), a separate agent with a fresh context that receives the spec (or the cited sections) and the plan only, checks:

- the approach is stated and the rejected alternatives are named;
- the public API, schema and error-code delta is declared, and the `factory:delta` block is complete and agrees with the prose;
- every requirement and scenario is mapped to a ticket and a test level;
- every ticket has one concern, a failing test to write first, a verification command, an estimate within the soft size limit, declared dependencies and `touches`;
- no ticket depends on a later one, and tickets that could run in parallel do not overlap in `touches`;
- every risk has a rollback;
- for an initial-build milestone, every deviation from `specs/0000-framework/` is listed.

Its verdict is check `factory/doc-review` on the plan's pull request, with findings as review comments. "Changes required" sends the plan back to the planner, for at most `limits.fixRounds` rounds; after that the pull request gets `needs-human`.

### Approval

`plan.md` is submitted in a pull request of its own (title `docs(specs): …`, releases nothing).

- It merges **automatically** once `factory/doc-review` has passed, unless it is flagged.
- A plan is **flagged** when it declares a public API change, a schema change, a new dependency, a deviation from the spec, or any ticket in tier high. Check `factory/plan-gate` then requires an approving review by a human maintainer before the merge. `plan.md` is not under `CODEOWNERS`; the check is what demands the human.
- Work on tickets starts after the merge, on branches cut from a `main` that contains the plan. The `Status` line in `plan.md` is informative only (ADR-0023).

### Deviation rule

- A deviation from a merged plan is first a **comment on the ticket**: what differs and why.
- When it changes the approach, the declared delta, or the ticket list, the plan is updated in a **plan update pull request**, which passes the plan review and the plan gate again; `factory-tickets.yml` then adds or updates the affected issues. The code that deviates does not merge before the plan update does: `factory/delta` and `factory/ticket` read the plan on the base branch.
- A deviation that changes scope or requirements goes back to the human owner; the affected tickets get `needs-human` and wait.

## Consequences

- Planning surfaces the public API and migration questions while they are still cheap, and in a form a script can hold the implementation to.
- The ticket list lets work be resumed, or split between agents, without re-deriving it.
- A plan costs one small pull request and one agent review before any code. Plans that declare nothing risky do not wait for a person; a plan reviewer agent is then the only judge of the approach, and a poor approach in such a plan is first seen by a human, if at all, in its results.
- A wrongly declared delta or an under-estimated ticket shows up as a failed check or an oversized pull request and sends work to the human; plans therefore need more care than a prose plan did.
- The record of how the work went is on GitHub, not in the repository.
- Plans go stale if the deviation rule is ignored; the checks read the merged plan, and the pull-request reviewer compares plan and diff (ADR-0027).

## Enforcement

- `factory/doc-review` and `factory/plan-gate` are required checks on a plan's pull request.
- A CI check parses `plan.md` in changed `specs/` folders: the `factory:delta` block is present with every key, the Tickets table has every column filled, keys are unique, dependencies point at earlier keys.
- `factory/ticket` requires that a code pull request closes one ticket whose `plan.md` is **on the base branch**. A plan that exists only on the feature branch fails the check.
- `factory/delta` compares the pull request's changes to the approved files with the merged plan's `factory:delta` block.
- `factory/size` and the pull-request reviewer hold tickets to the size limits (ADR-0029).
- Package validation and the approved public API files show an API delta the plan did not declare.

## Guidance for agents

- As planner: load the `yaf-writing-plans` skill before planning. Read the code the plan touches before proposing an approach; cite the files you read.
- Check third-party behaviour in documentation or source and put what you found in `research.md`. Do not plan around remembered API shapes.
- Prefer more, smaller tickets. Slice vertically where you can, so each ticket leaves the repository green and useful.
- Declare the delta honestly and completely. An omission does not hide the change; it sends every ticket that touches it to the human.
- Do not review your own plan, and do not start a ticket for a plan that is not merged. Do not set the plan's status to `Approved` to unblock yourself; nothing reads it.
- As plan reviewer: load `yaf-reviewing-specs-and-plans`. Judge from the spec and the plan only. Report findings; do not rewrite the plan.
- When reality disagrees with the plan, say so on the ticket first, and update the plan through its own pull request when the approach, the delta or the tickets change.

## Related

ADR-0018 (testing strategy), ADR-0023, ADR-0024, ADR-0026, ADR-0027, ADR-0029.
