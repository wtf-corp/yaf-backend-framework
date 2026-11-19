# ADR-0024 — Specifications

> **Status** Accepted · **Date** 2026-11 · **Deciders** maintainers

## Context

ADR-0023 makes a specification the first artefact of every non-trivial change. This record fixes what a specification contains, where it lives and when it counts as approved.

Without a fixed shape, specs drift into design documents, mix "what" with "how", and leave their impact on security, privacy and contracts to be discovered in review. And a spec is the one artefact a human always approves (ADR-0023), so what reaches that human should already be complete: checking that it is complete is work for a second agent, not for the person.

## Drivers

- The human owner must be able to approve or reject in minutes, and must only be shown specs that already pass a quality check.
- The agent that writes a spec does not judge it.
- Requirements must be individually addressable, so plans, tests and review findings can point at them.
- Impact on the framework's guarantees must be stated up front, not found later.
- The format must be plain markdown that any agent can write and read.

## Options considered

1. **Free-form issue text.** No structure to check against; requirements are implicit.
2. **A heavyweight requirements template** (user stories, personas, estimates). Mostly noise for a framework.
3. **A short fixed template: problem, numbered requirements, acceptance scenarios, impact checklist; reviewed by a separate agent against a fixed checklist before a human approves.** Enough to review and to test against.

## Decision

Option 3. A specification says **what** changes and **why**. It contains no implementation: no type names that do not exist yet, no file lists, no algorithms. Those belong to the plan (ADR-0025).

### Location and naming

`specs/NNNN-<slug>/spec.md`, where `NNNN` is the next free number and `<slug>` is a short kebab-case name. The spec is written on a branch `spec/NNNN-<slug>` and reaches `main` through a pull request of its own, titled `docs(specs): …`.

`specs/0000-framework/` is the specification the framework was first built from. It is approved as committed; the milestones of the initial build reference its sections and write no `spec.md` of their own (ADR-0023). Everything below applies to changes after the initial build.

### Status

| Status | Meaning |
|---|---|
| `Draft` | Being written; open questions may exist |
| `Approved` | Its pull request passed the spec review, was approved by a code owner and merged; planning may start |
| `Implemented` | All pull requests for it are merged |
| `Abandoned` | Will not be done; the reason is in the decision log |

The status line is informative: it tells a reader where the spec stands. The approval itself is the code owner's review on the pull request that merged the spec, and that is what checks rely on.

### Rules

1. Requirements are numbered `R1`, `R2`, … and use MUST or SHOULD. One requirement, one statement that can be tested.
2. Every acceptance scenario is `Given / When / Then` and names the requirement ids it covers. Every requirement is covered by at least one scenario.
3. The impact checklist is filled in completely; "none" is an answer, a blank is not.
4. **Open questions must be empty before approval.** Each answered question moves to the decision log with its answer.
5. A spec that changes an architectural decision adds or supersedes an ADR in the same pull request (ADR-0001).
6. A change to an approved spec is made in the file and noted in the decision log with the date. Because code owners review every pull request that touches a `spec.md`, a change that alters a requirement is re-approved by the review of the pull request that carries it, after the spec review has run on it again.
7. The spec is written by one agent (`spec-writer`) and reviewed by another (`spec-reviewer`) before a human sees it (below).
8. Approval is a code owner's approving review on the spec's pull request, given after the spec review passed, followed by the merge. The decision log notes the date and the pull request; that note is a record, not the approval.

### Spec review and the quality checklist

The spec reviewer is a separate agent with a fresh context (ADR-0029). It receives the spec and the ADRs it cites, not the writer's conversation. It checks:

- every requirement has an id, uses MUST or SHOULD, and is one testable statement;
- every requirement is covered by at least one acceptance scenario, and every scenario is `Given / When / Then`;
- there are no open questions;
- the impact checklist is filled in completely, and agrees with the requirements (a requirement about personal data with "Privacy: none" is a finding);
- goals and non-goals are stated;
- the spec contains no design: no type names that do not exist yet, no file lists, no algorithms;
- a spec that changes an architectural decision carries its ADR.

Its verdict — "pass" or "changes required" — is check `factory/doc-review` on the spec's pull request, with findings as review comments. "Changes required" sends the spec back to the writer, for at most `limits.fixRounds` rounds (ADR-0029); after that the pull request gets `needs-human`. The reviewer does not rewrite the spec, and a pass is not an approval: the code owner approves.

### Template

```markdown
# NNNN — <Title>

**Status** Draft | Approved | Implemented | Abandoned (informative; approval is the code owner's review)
**Owner** <human owner> · **Author** <agent or person> · **Date** yyyy-MM-dd

## Problem

What is wrong or missing today, for whom, and why it matters. Two or three paragraphs.

## Goals

- …

## Non-goals

- …

## Requirements

- **R1** The framework MUST …
- **R2** A service SHOULD be able to …

## Acceptance scenarios

### S1 — <name> (R1, R2)

- **Given** …
- **When** …
- **Then** …

## Impact checklist

| Area | Impact |
|---|---|
| Public API (types, members, setup extensions, configuration keys) | none / describe |
| Security — tenets S1–S10 of ADR-0004 touched | none / S… and how |
| Privacy — classification, retention, erasure | none / describe |
| Compliance controls affected (ADR-0005) | none / control ids |
| Data migration | none / describe |
| Wire contracts and error codes | none / describe |
| Telemetry (spans, metrics, log events) | none / describe |
| Docs and sample | none / which articles, which sample parts |
| Breaking? | no / yes — what breaks and for whom |

## Open questions

- (must be empty before approval)

## Out of scope

- …

## Decision log

| Date | Decision | By |
|---|---|---|
| yyyy-MM-dd | … | … |
```

## Consequences

- Writing a spec takes real time for a real change; that time is taken from the implementation, where confusion costs more.
- A spec costs two agents before it costs a human: the human reads specs that are already complete and consistent, and decides only whether they say the right thing. The spec reviewer cannot judge that; a well-formed spec for the wrong thing passes it.
- Because specs hold no implementation, they stay true longer than design documents do.
- The impact checklist makes security and privacy review start before code exists.

## Enforcement

- A CI check parses `spec.md` in changed `specs/` folders: the status is one of the four values; `## Open questions` is empty when the pull request is not a draft; every `R<n>` appears in at least one scenario heading; every checklist row has a value.
- `factory/doc-review` is a required check on a spec's pull request (ADR-0029).
- Code owners of `specs/**/spec.md`, `specs/0000-framework/**` and `adr/**` are the maintainers, and the rulesets require their review, so a spec cannot reach `main` without a human approval. No check reads the status line as proof.
- A spec whose checklist marks "Breaking? yes" is implemented by pull requests with `!` in the title, which after `1.0.0` target `next`, not `main` (ADR-0020).

## Guidance for agents

- As spec writer: load the `yaf-writing-specs` skill before drafting. As spec reviewer: load `yaf-reviewing-specs-and-plans`, judge from the spec and the ADRs only, report findings, and do not rewrite. Never both for the same spec.
- Ask the human owner the questions you have **before** writing requirements around your guesses. List what you could not resolve under Open questions.
- Write requirements about observable behaviour. "MUST refuse a request without a tenant with `404`" is a requirement; "MUST add a `TenantGuard` class" is a plan.
- Fill the impact checklist by reading the relevant ADRs, not from memory.
- Open the spec as its own pull request; the spec review runs on it, and then it waits for a code owner's review. A passing `factory/doc-review` is not approval. You may set the status line to `Approved` in that pull request once the owner has said the content is right; the line approves nothing, the review does. Never approve a pull request.
- During the initial build, do not write a spec for a milestone; cite `specs/0000-framework/` by file and section in the plan.

## Related

ADR-0001, ADR-0004, ADR-0005, ADR-0020, ADR-0023, ADR-0025, ADR-0029.
