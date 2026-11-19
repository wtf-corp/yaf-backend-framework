---
name: yaf-writing-adrs
description: When and how to write an architecture decision record in adr/ — the sections Context, Drivers, Options considered, Decision, Consequences, Enforcement, Guidance for agents, Related, and how to supersede an existing record. Load WHEN a change introduces, reverses or constrains an architectural choice (a new dependency, a new package or layer rule, a persistence, messaging, security, privacy or release decision); WHEN a spec's impact checklist says a decision changes; WHEN asked "why is it done this way" and no record answers it; and BEFORE editing any file in adr/.
license: MIT
metadata:
  owner: yaf
  version: "1"
---

# Writing ADRs

An ADR records one decision: the situation, the options, what was chosen and what follows. It is how a later reader — person or agent — learns why the framework is the way it is. Decision record: ADR-0001. Template: `adr/0000-adr-template.md`.

## When to load

- A spec or plan changes an architectural decision.
- You need a new dependency (ADR-0003 requires a record).
- You are about to edit, supersede or add a file in `adr/`.

## Does this need an ADR?

| Needs one | Does not |
|---|---|
| A new third-party dependency in a core package, or a new adapter package | A patch or minor version bump |
| A new or changed rule about layers, packages or what may reference what | An implementation detail inside one package |
| A change to a guarantee: security tenet, tenant isolation, privacy handling, delivery semantics, wire format | A bug fix that restores the documented behaviour |
| A choice between real alternatives that someone will ask about later | A choice with one sensible option |
| A change to the process: release, review, testing strategy, agent instructions | A wording fix in a document |
| Reversing or narrowing an existing ADR | — |

When unsure, ask: "would a competent newcomer be surprised by this, or want to undo it?" If yes, write it down.

## Rules

1. **REQUIRED** — One decision per ADR.
2. **REQUIRED** — File name `NNNN-<kebab-title>.md`, `NNNN` the next free number. Numbers are never reused.
3. **REQUIRED** — The sections below, in this order, all present.
4. **REQUIRED** — At least two options considered, each with the reason it was or was not chosen. "Do nothing" counts when it was a real option.
5. **REQUIRED** — Consequences include the costs, not only the benefits.
6. **REQUIRED** — Enforcement names the mechanism: analyzer id, architecture test, startup check, CI gate. If the only enforcement is review, say so.
7. **REQUIRED** — An accepted ADR is not rewritten to say something different. A changed decision is a **new** ADR that supersedes the old one.
8. **REQUIRED** — The ADR lands in the same pull request as the spec or change that needs it, and `adr/README.md` lists it.
9. **REQUIRED** — Only the maintainers accept an ADR. An agent drafts with status `Proposed`.
10. **RECOMMENDED** — One to two pages. Link to the spec for detail.
11. **RECOMMENDED** — Write for a reader who was not there: no "as discussed", no session references.

## Template

```markdown
# ADR-NNNN — <Title: the decision, as a noun phrase>

> **Status** Proposed | Accepted | Superseded by ADR-MMMM | Deprecated · **Date** yyyy-MM · **Deciders** maintainers

## Context

The situation and the problem. Facts, constraints, what forced a decision now.

## Drivers

- What a good answer must achieve, in priority order.

## Options considered

1. **<Option>** — what it is; why chosen or not.
2. **<Option>** — …

## Decision

What we do. Specific enough to check code against. Rules as a numbered list or a table.

## Consequences

What becomes easier, what becomes harder, what we now have to maintain, what we gave up.

## Enforcement

How the decision is kept: analyzers (ids), architecture tests, startup checks, CI gates, approved files. What is left to review.

## Guidance for agents

Imperative, short: what to do and not do when working in the area this decision governs, and when to stop and ask.

## Related

ADR numbers, spec folders, articles.
```

## Steps

1. **Check the existing records.** Read `adr/README.md` and the ADRs in the area. Is this decision already made? Does yours conflict with one?
2. **State the decision in one sentence.** If you cannot, it is more than one ADR or not yet a decision.
3. **Write Context and Drivers** from facts you verified.
4. **Write the options honestly**, including the ones you did not choose and the strongest argument for each.
5. **Write the Decision** as rules someone can check.
6. **Write Consequences**, costs included.
7. **Write Enforcement.** If a rule could be enforced mechanically and is not, say so and propose the mechanism.
8. **Write Guidance for agents.**
9. **Update `adr/README.md`**; add the ADR to the Related sections of the records it touches.
10. **If superseding**: see below.

## Superseding

- New ADR: Context explains what changed since the old decision. Related lists the old ADR.
- Old ADR: change **only** its status line to `Superseded by ADR-NNNN` and add nothing else. Its text stays as the record of what was decided then.
- Update references that pointed at the old ADR for the rule (skills, `AGENTS.md`, articles) to point at the new one.
- Small corrections that do not change the decision (a typo, a broken link, a renamed type) are edited in place.

## Anti-patterns

| Wrong | Why | Instead |
|---|---|---|
| Editing an accepted ADR so it now says the opposite | The history of why is lost; references to it become false | New ADR that supersedes |
| One option "considered" | It records a preference, not a decision | Real alternatives with reasons |
| Consequences that are all benefits | Nobody can weigh the decision later | Name the costs |
| "Enforced by code review" for a rule a test could check | It will erode | Add the analyzer or test, name it |
| An ADR that restates the specification | Two places to keep true | Record the decision and link the spec |
| Writing the ADR after the release | It rationalises instead of deciding | Same pull request as the change |
| Agent sets status `Accepted` | Acceptance is the maintainers' | `Proposed`; they change it |
| A design document with ten decisions | Cannot be superseded piecewise | One decision each |

## Done when

- [ ] The file is `adr/NNNN-<title>.md` with all eight sections.
- [ ] Two or more options, with reasons.
- [ ] Decision checkable; costs stated; enforcement named.
- [ ] `adr/README.md` and related ADRs updated; superseded ADR's status line changed, nothing else.
- [ ] Status is `Proposed` until the maintainers accept it.
