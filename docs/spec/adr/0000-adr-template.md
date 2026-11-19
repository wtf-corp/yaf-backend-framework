# ADR-NNNN — Title in the imperative or as a noun phrase

> **Status** Proposed | Accepted | Superseded by NNNN | Deprecated
> **Date** YYYY-MM
> **Deciders** names or roles of the humans who accepted it
> **Supersedes** NNNN (omit when none)

File name: `NNNN-kebab-title.md`, four-digit number, next free number, never reused.

## Context

What situation forces a decision. Facts only: the constraint, the problem, what exists today. Three to ten lines. A reader who was not there must understand why the question came up.

## Drivers

- The requirements and forces the decision must satisfy, one per bullet.
- Order them by weight; the first ones decide when options tie.

## Options considered

| Option | For | Against |
|---|---|---|
| A — short name | … | … |
| B — short name | … | … |

At least two real options. "Do nothing" counts when it was a real candidate.

## Decision

What was chosen, stated so that it can be checked: types, packages, rules, defaults. Write what is true once the decision is implemented, in the present tense. Link the spec file that carries the detail (`spec/NN-….md`); the ADR records the decision and its reasons, the spec records the contract.

## Consequences

**Positive**

- …

**Negative / costs**

- … (every decision has some; an empty list means the analysis is not finished)

## Enforcement

What keeps the decision true without a human remembering it. One row per mechanism.

| Mechanism | What it checks |
|---|---|
| Analyzer `YAFnnnn` | … |
| Architecture test | … |
| Startup check | … |
| CI gate | … |

A decision with no enforcement row must say why it can only be reviewed.

## Guidance for agents

Short, imperative bullets an AI coding agent follows when it works in this area. They are also the checklist a reviewer uses.

- Do …
- Never …
- When … , stop and ask.

## Related

- ADR-NNNN — how it relates
- `spec/NN-….md`

---

## Rules for ADRs

1. An ADR is **immutable once Accepted**, except for its `Status` line. Typos and broken links may be fixed; the decision, its reasons and its guidance may not.
2. A decision is changed by a **new ADR** that supersedes the old one. The old one gets `Superseded by NNNN`; the new one names it in `Supersedes` and explains what changed and why.
3. `Proposed` ADRs may be edited freely. Only a human moves an ADR to `Accepted` (ADR-0001).
4. `Deprecated` means the decision no longer applies and nothing replaces it.
5. Keep an ADR between 60 and 140 lines. Longer material belongs in the spec or the docs site.
