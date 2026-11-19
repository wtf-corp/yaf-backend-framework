# ADR-0001 — Record architecture decisions

> **Status** Accepted
> **Date** 2026-11
> **Deciders** Framework maintainers

## Context

YAF is built and maintained largely by AI coding agents working with human maintainers. An agent starts every session without memory of earlier ones; a human joins months after a choice was made. Both need to know not only what the code does but which alternatives were rejected and why, or they will reopen settled questions and "fix" deliberate constraints (a refused request without a permission marker, a repository that does not save).

Code, tests and the docs site say what is. They do not say why.

## Drivers

- Decisions must survive the loss of conversational context.
- A reader must find the reason for a constraint in under a minute.
- The record must be cheap to write, or it will not be written.
- Agents may draft; accountability for a decision stays with a human.

## Options considered

| Option | For | Against |
|---|---|---|
| ADR files in the repository | Versioned with the code; reviewed in the same pull request; readable by any agent | Needs discipline to keep short |
| Wiki or external documents | Easy to edit | Drifts from the code; invisible to agents working in a checkout; no review gate |
| Commit messages and pull request descriptions only | No extra artefact | Not discoverable by topic; reasons scattered |
| Long design documents | Thorough | Go stale; nobody reads them before a small change |

## Decision

Architecture decisions are recorded as ADR files in `adr/`, following `0000-adr-template.md`.

**Numbering and naming.** `NNNN-kebab-title.md`, four digits, strictly increasing, never reused. `adr/README.md` lists every ADR with its status and one line of summary; it is updated in the same pull request.

**Statuses.** `Proposed` → `Accepted` → (`Superseded by NNNN` | `Deprecated`). An Accepted ADR is immutable apart from its status line; a change of mind is a new ADR that supersedes it.

**When an ADR is required.** A pull request that does any of the following contains a new ADR, or cites the Accepted ADR that already covers it:

| Trigger | Example |
|---|---|
| A new dependency in a core package, or a change to the dependency allow-list | Adding a JSON schema library to `YAF.Infrastructure` (ADR-0003) |
| A new or changed shape of a public contract that apps build on | `Result<T>`, `IRequestMiddleware`, the envelope, the memento base interfaces |
| A change to a security or privacy control, or a new unsafe option | Relaxing token validation, a new elevation path (ADR-0004, ADR-0005) |
| A new package, or moving a responsibility between packages | Splitting `YAF.Infrastructure` (ADR-0006) |
| A change to the development process | Review rules, the spec or plan format (ADR-0023 to ADR-0028) |
| A choice between real alternatives that someone will later question | Polling outbox versus change data capture |

Not required: a bug fix, an internal refactoring that keeps contracts, a new option with a safe default inside an existing decision, documentation.

**Relation to other artefacts.** The ADR holds the decision and its reasons. The spec for a feature (ADR-0024) holds requirements and acceptance criteria. The plan (ADR-0025) holds the steps. The docs site (ADR-0021) explains use. An ADR links to them; it does not repeat them.

**Who decides.** An agent may draft an ADR and must do so when a trigger applies, with status `Proposed`. Only a human maintainer accepts an ADR: the proof is a code owner's approving review on the pull request that carries it, which branch protection requires for `adr/`. The `Status` line records the outcome; it is not the proof, because whoever can edit the file can write it. An agent never implements a decision whose ADR is still `Proposed`, except in a branch whose pull request contains that ADR and is waiting for the human's acceptance.

## Consequences

**Positive**

- A constraint can be traced to its reason; agents stop re-litigating.
- Decisions are reviewed like code, in the pull request that introduces them.
- The "Guidance for agents" section of each ADR gives nested `AGENTS.md` files and skills a source to quote (ADR-0028).

**Negative / costs**

- One more file for a significant change.
- Immutable records accumulate; superseded ADRs stay in the folder and must be marked clearly.
- Deciding whether a trigger applies is a judgment; the pull request template asks the question explicitly.

## Enforcement

| Mechanism | What it checks |
|---|---|
| CI script `adr-lint` | File name pattern, required sections present, status is a known value, numbers unique and contiguous, every ADR listed in `adr/README.md`, a `Superseded by` target exists and names this ADR in `Supersedes` |
| CI gate on diff | A pull request that changes an Accepted ADR beyond its status line fails |
| Pull request template | A checkbox: "ADR trigger applies: yes (ADR-NNNN) / no" |
| CODEOWNERS and branch protection | `adr/` requires a code owner's approving review; that review, by a human maintainer, is what accepts an ADR |
| Dependency allow-list test (ADR-0003) | A new core dependency fails the build until the allow-list, and therefore an ADR, is updated |

## Guidance for agents

- Before changing behaviour in an area, read the ADRs listed for that area in the nearest `AGENTS.md`.
- When a trigger in the table applies, draft the ADR first, with status `Proposed`, and stop for human acceptance before implementing.
- Never edit an Accepted ADR's content. Propose a superseding ADR and say what changes.
- Never set a status to `Accepted` yourself, and never treat a `Status` line as proof of acceptance: check that the ADR is on `main`.
- If the code contradicts an Accepted ADR, report it; do not silently pick one. The ADR wins until a human says otherwise.
- Keep the ADR to the decision and its reasons. Put contracts in the spec and steps in the plan.

## Related

- ADR-0023 — agentic development process (where ADRs sit in the flow)
- ADR-0024, ADR-0025 — specifications and plans
- ADR-0028 — agent instructions and skills
- `adr/0000-adr-template.md`
