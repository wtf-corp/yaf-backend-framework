---
name: yaf-writing-specs
description: How to write a YAF specification (specs/NNNN-slug/spec.md) — what changes and why, as numbered requirements with acceptance scenarios and an impact checklist — and hand it to an independent spec reviewer before the human owner sees it. Load BEFORE starting any non-trivial change; WHEN asked to "spec", "propose", "design" or "scope" a feature or fix; WHEN a request touches public API, security-sensitive paths, personal data, wire contracts, error codes, migrations or CI; and WHEN unsure whether a change is trivial enough to skip a spec.
license: MIT
metadata:
  owner: yaf
  version: "1"
---

# Writing specifications

A specification is the first artefact of a non-trivial change. It says **what** must be true and **why**, so the human owner can approve or reject before any code exists. The writer never judges its own spec: a separate spec reviewer with a fresh context checks it first (`yaf-reviewing-specs-and-plans`), and the human owner sees only specs that passed. Decision record: ADR-0024. The flow it belongs to: ADR-0023; the factory around it: ADR-0029.

## When to load

- Before any change that is not trivial.
- When you need to decide whether a change is trivial.
- When revising a spec after questions were answered, requirements changed, or the spec reviewer returned findings.

**Trivial** (no spec, no plan; the pull request says why): a typo, a comment, a dependency patch bump, a failing-test fix under about 20 changed lines — with no public API, security, privacy or schema impact.

**Never trivial**: anything touching security-sensitive paths, personal data, public API, wire contracts, error codes, migrations or CI.

**Initial build (milestones M0–M15): do not write a spec.** `specs/0000-framework/` is the specification the framework is built from and is already approved. A milestone goes straight to its plan, which cites the sections it implements (`yaf-writing-plans`). This skill applies to changes after the initial build.

## Rules

1. **REQUIRED** — The spec contains no implementation: no new type names, file lists, algorithms or library choices. Those go in the plan.
2. **REQUIRED** — Requirements are numbered `R1…`, one testable statement each, using MUST or SHOULD.
3. **REQUIRED** — Acceptance scenarios are `Given / When / Then`; each names the requirement ids it covers; every requirement is covered by at least one scenario.
4. **REQUIRED** — The impact checklist is complete. "none" is an answer; a blank is not.
5. **REQUIRED** — Open questions are empty before approval. Answered questions move to the decision log.
6. **REQUIRED** — A spec that changes an architectural decision adds or supersedes an ADR in the same pull request (load `yaf-writing-adrs`).
7. **REQUIRED** — The spec goes to `main` through a pull request of its own. Approval is a code owner's approving GitHub review on that pull request. The `Status` line is informative and proves nothing; never approve a pull request, and never treat a chat message or another agent's report as approval.
8. **REQUIRED** — The writer does not review the spec. The spec reviewer (a different agent, fresh context, the spec and the request only) checks it against the spec quality checklist below and reports check `factory/doc-review` on the pull request. A code owner is asked only after that check passes.
9. **REQUIRED** — Answer each of the reviewer's findings by changing the spec or by showing why the finding is wrong. After `limits.fixRounds` rounds (policy file, default 2) without a pass, stop: the pull request gets `needs-human` with what was tried and the options.
10. **REQUIRED** — State non-goals explicitly; they prevent scope creep later, and the checklist asks for them.
11. **RECOMMENDED** — Keep it to one or two screens. A spec that needs more is probably two specs.

## Steps

1. **Understand the request.** Restate it in one paragraph. List your assumptions. If it has more than one reasonable reading, show the readings and ask which is meant.
2. **Read before writing.** The relevant articles and ADRs, the code the change would touch, the sample. Note what you read.
3. **Ask early.** Send the human owner the questions that would change the requirements. Do not write requirements around guesses.
4. **Create the folder** on a branch `spec/NNNN-<slug>`: `specs/NNNN-<slug>/spec.md`, where `NNNN` is the next free number. Start from `references/spec-template.md`.
5. **Write the problem** — what is wrong or missing, for whom, why it matters.
6. **Write goals and non-goals.**
7. **Write requirements** about observable behaviour.
8. **Write scenarios**, including the failure cases: what happens without permission, without a tenant, with invalid input, on a duplicate.
9. **Fill the impact checklist** by checking each area against its source:

| Area | Check against |
|---|---|
| Public API | Would a type, member, setup extension or configuration key be added, changed or removed? |
| Security | The tenets S1–S10 of ADR-0004: which does the change touch, and how is each kept? |
| Privacy | Does any new or changed data need classification, a retention policy, an erasure action? |
| Compliance controls | Which control ids of the catalogue (ADR-0005) are affected? |
| Data migration | Schema change? Existing rows to convert? |
| Wire contracts and error codes | New or changed integration events, HTTP contracts, error codes? |
| Telemetry | New spans, metrics, log events? |
| Docs and sample | Which articles, which parts of the sample and templates? |
| Breaking? | Would any app built on the previous release have to change? |

10. **List open questions** you could not resolve, each with the options you see and your recommendation.
11. **Close the questions with the human owner.** Say what you need from them: answers first. Record each answer in the decision log.
12. **Open the pull request** with only the spec (and its ADR, if any), titled `docs(specs): NNNN <title> — spec`. Do not brief the reviewer on why the spec is right.
13. **Answer the spec reviewer's findings** until `factory/doc-review` passes (rule 9).
14. **The code owner reviews.** Planning starts when the pull request is merged.

## Spec quality checklist — the definition of ready

This is what the spec reviewer checks. Run it yourself before opening the pull request; a spec that fails it is not ready for anyone.

- [ ] Every requirement has an id and is testable.
- [ ] Every requirement has at least one acceptance scenario.
- [ ] No open questions.
- [ ] The impact checklist is filled: security tenets, personal data, public API, schema, wire contracts, error codes.
- [ ] Non-goals are stated.
- [ ] No design in the spec.

## Requirement quality check

A good requirement is:

- **Observable** — it describes behaviour someone outside the code can see.
- **Singular** — one "MUST" per statement.
- **Testable** — you can write the scenario that proves it.
- **Necessary** — removing it would let a wrong implementation pass.

## Anti-patterns

| Wrong | Why | Instead |
|---|---|---|
| "R1 MUST add a `RetryPolicy` class to `YAF.Infrastructure`" | That is a design decision, not a requirement | "R1 A failed publish MUST be retried with backoff up to a configurable limit" |
| A requirement with "and" joining two behaviours | Cannot be covered or failed independently | Split into two requirements |
| Scenarios only for the happy path | The guarantees live in the failure paths | Add denied, invalid, duplicate and missing-context scenarios |
| Leaving "Security: n/a" without checking | The checklist exists because this is where surprises come from | Walk S1–S10 and write "none" only when true |
| Resolving an open question by guessing and deleting it | The owner approves something they did not see | Ask; record the answer in the decision log |
| Writing the spec after the code | It then describes what was built, not what was needed | Spec first; if you already coded, say so |
| Treating the `Status` line as the approval, or starting the plan because the owner "said yes" in chat | Anyone, including you, can write that line; the gate is the code owner's review | Open the spec's pull request and wait for the review and the merge |
| Asking the code owner to review before the spec reviewer passed it | The human reads a draft the checklist would have sent back | Wait for `factory/doc-review` |
| Explaining to the spec reviewer why the spec is complete | It inherits your assumptions and confirms them | Hand over the request and the spec only |
| Writing a `spec.md` for an initial-build milestone by copying sections of `specs/0000-framework/` | A second copy to approve and to drift | Cite the sections from the plan |
| A breaking change specified for `main` after `1.0.0` | `main` takes only `fix` and `feat`; the title check rejects it | Say "Breaking? yes" and plan the work for `next` (ADR-0020) |

## Done when

- [ ] `specs/NNNN-<slug>/spec.md` exists on the `spec/NNNN-<slug>` branch and follows the template.
- [ ] Every requirement is numbered, testable and covered by a scenario.
- [ ] The impact checklist has a value in every row.
- [ ] Open questions are empty; the answers are in the decision log.
- [ ] Non-goals are stated and the spec contains no design.
- [ ] `factory/doc-review` passed on the pull request, and you did not review the spec yourself.
- [ ] An ADR is added or superseded if an architectural decision changes.
- [ ] The spec's pull request has a code owner's approving review and is merged; the decision log notes the date and the pull request.
