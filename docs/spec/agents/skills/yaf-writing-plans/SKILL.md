---
name: yaf-writing-plans
description: How to turn an approved YAF specification into plan.md — the approach, affected packages by layer, the public API, schema and error-code delta (also as the machine-readable factory:delta block), migration plan, test strategy, risks, and the Tickets table that slices the work into small, independent tickets. Load BEFORE writing any code for a non-trivial change; WHEN a spec has just been merged; WHEN starting a milestone of the initial build (M0–M15), which is planned straight from specs/0000-framework; WHEN asked to "plan", "break down", "slice" or "estimate" work; and WHEN the implementation has to deviate from a merged plan.
license: MIT
metadata:
  owner: yaf
  version: "1"
---

# Writing implementation plans

A plan says **how** an approved spec will be met and into which tickets the work is cut. The planner never judges its own plan: a separate plan reviewer checks it (`yaf-reviewing-specs-and-plans`), and it reaches `main` through a pull request of its own before any code is written. After the merge a workflow turns the Tickets table into GitHub issues, and from then on the issues are the source of truth for the work. Decision records: ADR-0025 (plans), ADR-0029 (tickets, tiers, limits).

## When to load

- The spec is merged to `main` and no plan exists yet.
- You are starting a milestone of the initial build.
- You are about to deviate from a merged plan.
- The plan reviewer returned findings.

Do not plan against a spec that is not on `main`. If it is not merged, go back (`yaf-writing-specs`).

## Initial build (milestones M0–M15)

`specs/0000-framework/` is the approved spec. The milestone's folder `specs/NNNN-<slug>/` holds `plan.md` (and `research.md` when needed) and **no `spec.md`**.

- In place of `R…` and `S…` ids, cite the specification by file and section: `05 §4`, and its acceptance criteria as `05 §11 item 3`. Every acceptance item of the milestone's sections is mapped to a ticket and a test.
- Fill the plan's **Decision log** with each open point you settled (the build plan lists them) and the reason.
- Fill **Deviations from the specification**: where the plan departs from a section, and why (a third-party API that differs, a contradiction between sections). A deviation flags the plan for the human owner; one that changes scope, security or a public contract is a question for them before you submit the plan.
- Do not plan anything the specification marks as a later release.

## Rules

1. **REQUIRED** — `plan.md` lives next to the spec in `specs/NNNN-<slug>/`. There is no `tasks.md` and no progress-log file: the plan ends with the **Tickets** table, and status, evidence and deviations live on the issues created from it.
2. **REQUIRED** — The approach names the alternatives rejected and why.
3. **REQUIRED** — Affected packages are listed by layer, in this order: Core → Domain → Application → Infrastructure → Persistence → Http → adapters → Testing → Analyzers → sample → templates → docs.
4. **REQUIRED** — The public API, schema and error-code delta is exact, in prose **and** in the machine-readable `factory:delta` block. Check `factory/delta` compares that block with the approved files a pull request changes; a tier-medium pull request merges automatically only when they are equal.
5. **REQUIRED** — A schema change follows expand/contract: this release's migrations work with the previous release's code.
6. **REQUIRED** — The test strategy maps every requirement and acceptance scenario to a ticket, a test level and a project (ADR-0018).
7. **REQUIRED** — Every row of the Tickets table has: key `T1…`, title, requirements or sections, depends on, `touches` (paths or areas), expected tier, size estimate, the **failing test to write first**, and a verification command with the expected result.
8. **REQUIRED** — One ticket is one concern, one pull request, and at most the soft size limit (`size.soft` in the policy file, 200 changed hand-written lines by default; this repository uses 400 during the initial build, until `1.0.0`). A ticket that cannot fit is split; the plan reviewer rejects one estimated above the limit.
9. **REQUIRED** — No ticket depends on a later one. Dependencies are stated, never implied by order.
10. **REQUIRED** — The plan goes to `main` in a pull request of its own (`docs(specs): NNNN <title> — plan`). The plan reviewer reports check `factory/doc-review`. The plan then merges automatically **unless it is flagged**: a public API change, a schema change, a new dependency, a deviation from the spec, or any ticket in tier high makes `factory/plan-gate` require a human maintainer's approving review. Never approve a pull request yourself; the `Status` line is informative.
11. **REQUIRED** — A new dependency is not planned in silently; it is raised with the human owner first (ADR-0003) and flags the plan.
12. **REQUIRED** — Deviating from a merged plan: comment on the ticket; when the approach, the delta or the ticket list changes, open a plan update pull request, which passes the plan gate again. A deviation that changes scope or requirements goes back to the human owner.
13. **RECOMMENDED** — Put what you verified about third-party behaviour in `research.md`, with sources and versions.
14. **RECOMMENDED** — Order tickets inside-out by layer, so each builds on tested code. Sample, templates and docs get tickets of their own in the same plan, depending on the code they describe.

## Steps

1. **Re-read the spec.** List requirements and scenarios; they are your coverage targets.
2. **Read the code.** Every package the change touches, the tests around it, the sample's use of it. Write down the files you read.
3. **Check what you do not know.** For third-party APIs, read the documentation or source for the version in `Directory.Packages.props`. Record findings in `research.md`. Do not plan around remembered API shapes.
4. **Choose the approach.** Prefer the smallest change that meets the requirements. Write two or three paragraphs; add the alternatives table.
5. **Fill the layer table.** A layer with no change says "none".
6. **Write the delta**, in prose and in the `factory:delta` block. If it is breaking, confirm the spec says so; if the spec does not, stop and ask.
7. **Write the migration plan** or "none".
8. **Slice the tickets** (next section) and fill the Tickets table from `references/plan-template.md`. For each, decide the failing test first; if you cannot name one, the ticket is not ready.
9. **Map scenarios to tickets and tests.**
10. **List risks**, each with mitigation and rollback.
11. **List the verification commands** for the whole change.
12. **Open a pull request with `plan.md`** (and `research.md`), nothing else, on branch `spec/NNNN-<slug>`. Do not brief the plan reviewer on why the plan is right.
13. **Answer the plan reviewer's findings** until `factory/doc-review` passes; after `limits.fixRounds` rounds (default 2) stop and let the pull request go to `needs-human`.
14. **Implementation starts after the merge.** `factory-tickets.yml` creates the issues; you create none by hand.

Template: `references/plan-template.md`.

## Slicing tickets

Small pull requests are the point: a reviewer finds defects in 200 lines and misses them in 2 000.

| Signal | Action |
|---|---|
| The title contains "and" | Split |
| Estimate above the soft limit | Split by layer, by scenario, or into "add the type" then "use it" |
| More than one concern (behaviour plus a refactoring, code plus a dependency bump) | One ticket each |
| No failing test can be named | Clarify the requirement, or it is a docs ticket — verify with the docs build |
| Needs a decision you cannot make | Stop and ask before listing it |
| Touches a sensitive path (ADR-0029) together with ordinary code | Split, so the tier-high part is as small as it can be and the rest can merge automatically |
| A is needed before B compiles | B `depends on` A. The implementer stacks B's branch on A's, and they merge in order |
| Depends on nothing and shares no `touches` with another ticket | Can run in parallel |

`touches` is what the orchestrator uses to decide what may run at the same time. Be honest and specific: name the folders or files. Shared files count as overlap and make tickets run one after another — `Directory.Packages.props`, `PublicAPI.Unshipped.txt`, `errors.approved.txt`, a migration folder. Where two tickets would both add public API to one package, say so in `touches`.

The **tier** column is your expectation (`low`, `medium`, `high`); `classify.mjs` computes the real one from the diff, and nothing lowers it below that floor. Size estimates count changed hand-written lines; generated code, lock files, approved files, public API files, migration designer files and snapshots do not count.

## The `factory:delta` block

A fenced block in `plan.md`, read by `delta.mjs`. It lists what the tickets together may add to the approved files; anything a pull request adds beyond it is an undeclared delta and makes that pull request tier high.

````markdown
```factory:delta
{
  "publicApi": ["YAF.Messaging.IOutboxParking*"],
  "schema": ["AddOutboxParkedAt"],
  "errorCodes": ["core.outbox.not_parked"],
  "contracts": [],
  "configuration": ["Outbox:OnParked"],
  "telemetry": [],
  "dependencies": []
}
```
````

Write `[]` for an empty list. A plan whose block is entirely empty promises tier low for every ticket outside the sensitive paths.

## Deviations

When implementation shows the plan is wrong:

1. The implementer stops the ticket and comments on it: what was found, the options.
2. Does this change **scope or requirements**? If yes, the ticket gets `needs-human`. If no, continue.
3. If the approach, the delta or the ticket list changes, update `plan.md` in a pull request of its own; it goes through the plan reviewer and the plan gate again, and the workflow reconciles the issues.
4. If only the way one ticket is done changes, the comment on the ticket is the record.

## Anti-patterns

| Wrong | Why | Instead |
|---|---|---|
| One ticket: "implement the feature" | Cannot be reviewed, parallelised or merged safely | Tickets of one concern, each with its test |
| A 900-line ticket "because it belongs together" | The size check fails it and a human must look | Split along the layers or the scenarios |
| Tickets ordered API-first, domain last | Outer layers get written against code that does not exist | Core → Domain → … → docs, with dependencies stated |
| "Add tests" as the final ticket | Tests written after the code confirm what was built, not what was required | Every ticket starts with its failing test |
| Vague `touches` ("various") | The orchestrator cannot tell what may run in parallel; worktrees collide | Name folders and shared files |
| Leaving the delta block empty to stay in tier low | The first pull request with a new public member becomes tier high as an undeclared delta | Declare it; a declared delta is what lets tier medium merge automatically |
| Planning to suppress an analyzer or edit an approved file "to make it pass" | That weakens a guard | Plan the real fix; if none exists, ask |
| Assuming a library has a method you remember | Memory of APIs is unreliable | Check and cite in `research.md` |
| Carrying on after reality diverged from the plan | The reviewer compares diff with plan and finds unexplained differences | Comment on the ticket; update the plan when the approach changes |
| Leaving the sample, templates and docs for "a follow-up" | The release ships without them | Tickets in the same plan |
| Putting the plan and the first code in one pull request | The plan is then gated together with what it was meant to gate; `factory/ticket` rejects it | Plan pull request first, code after the merge |
| Creating the issues by hand, or writing a `tasks.md` | Two sources of truth | The workflow creates them from the merged plan |
| Reviewing your own plan, or briefing the plan reviewer | The writer never judges its own work | Hand over the spec and the plan only |
| Setting `Status: Approved` to get going | Nothing reads it | Wait for the merge |
| Planning a breaking change onto `main` after `1.0.0` | The title check rejects `!` there | Target `next` |

## Done when

- [ ] `plan.md` follows the template; every section has content or "none".
- [ ] The `factory:delta` block matches the prose delta exactly.
- [ ] Every requirement and acceptance scenario is mapped to a ticket and a test.
- [ ] Every ticket has one concern, its failing test, a verification command, an estimate within the soft limit, `depends on` and `touches`; none depends on a later one.
- [ ] Third-party assumptions are checked and recorded.
- [ ] Initial-build milestone: decision log and deviations sections are filled; no `spec.md` was written.
- [ ] `factory/doc-review` passed; the pull request is merged (after a human maintainer's review when flagged); the issues exist.
