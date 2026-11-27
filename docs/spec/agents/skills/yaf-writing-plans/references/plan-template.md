# NNNN — <Title>: plan

**Spec** ./spec.md (merged in #nnn) · **Status** Draft

<!-- Initial-build milestone: replace the spec link with the files and sections of specs/0000-framework this plan implements, e.g. "05 §1–5, 08 §1–2". -->
<!-- Status is informative. The plan is checked by the plan reviewer (factory/doc-review) and merges automatically unless it is flagged (factory/plan-gate), in which case a human maintainer approves it. -->

## Approach

<!-- A few paragraphs: how the requirements will be met. Name the files you read to decide. -->

### Alternatives rejected

| Alternative | Why not |
|---|---|
| | |

## Affected packages

<!-- Every row filled; "none" where a layer does not change. -->

| Layer | Package / area | Change |
|---|---|---|
| Core | | |
| Domain | | |
| Application | | |
| Infrastructure | | |
| Persistence | | |
| Http | | |
| Adapters | | |
| Testing (`Testing`, `Testing.Integration`) | | |
| Analyzers | | |
| Sample | | |
| Templates | | |
| Docs | | |

## Public API, schema and error-code delta

<!-- Exact members, configuration keys, error codes, migrations, contracts. "none" if nothing changes. -->

- **Added** …
- **Changed** …
- **Removed** …

Breaking: no / yes — matches the spec's impact checklist.
New dependency: no / yes — agreed with the human owner on yyyy-MM-dd.

<!-- The same delta, machine-readable. factory/delta compares it with the approved files each pull request changes. JSON with these eight keys; [] for an empty list; an entry ending in * matches by prefix. A new package pin is "Id@Version"; an .editorconfig setting is "[<repository/relative/path>] <setting>" (ADR-0034); either flags the plan. -->

```factory:delta
{ "publicApi": [], "schema": [], "errorCodes": [], "contracts": [], "configuration": [], "telemetry": [], "dependencies": [], "editorconfig": [] }
```

## Schema and migration plan

<!-- "none", or: expand step, contract step (which later release), compatibility with the previous release's code. -->

## Test strategy

| Scenario | Requirements | Ticket | Level (unit / integration / architecture / conformance) | Test project | Notes |
|---|---|---|---|---|---|
| S1 | R1 | T1 | | | |

## Risks and rollback

| Risk | Likelihood | Mitigation | Rollback |
|---|---|---|---|
| | | | |

## Verification commands

    dotnet build YAF.slnx -c Release
    dotnet test YAF.slnx

<!-- Add the specific commands that prove the whole change: a filtered test run, the docs build, a pending-model-changes check, package validation. State the expected result of each. -->

<!-- A milestone plan only (delete otherwise): its exit criteria and the checks that prove them, read by exit-check.mjs at the milestone's end. Each criterion names one or more check ids; "expect" is the exit code, 0 by default. A plan with this block is always flagged for the owner. -->

```factory:exit
{
  "criteria": [
    {"text":"<exit criterion, as the build plan states it> (Exit 1)","checks":["V2","E1"]}
  ],
  "checks": [
    {"id":"V2","title":"restore, build and all tests","run":"dotnet restore YAF.slnx && dotnet build YAF.slnx -c Release --no-restore && dotnet test --solution YAF.slnx -c Release --no-build","expect":0},
    {"id":"E1","title":"exit test <Name>Tests","run":"dotnet test --solution YAF.slnx -c Release --no-build --filter-query \"/*/*/<Name>Tests/*\"","expect":0}
  ]
}
```

## Research

<!-- Link ./research.md when third-party behaviour was checked. -->

## Decision log

<!-- Required for an initial-build milestone: each open point settled, with the reason. Optional otherwise. -->

| Date | Decision | Reason | By |
|---|---|---|---|
| | | | |

## Deviations from the specification

<!-- Initial-build milestones only. "none", or: section, what differs, why. Any entry flags the plan for a human maintainer. Scope, security and public-contract deviations are agreed with the human owner first. -->

## Tickets

<!--
One row per ticket; one ticket = one concern = one pull request, within the soft size limit (200 changed hand-written lines by default; this repository uses 400 during the initial build, until `1.0.0`).
After this plan merges, factory-tickets.yml creates one issue per row. Do not create issues by hand.
Key:        T1, T2 … stable; never renumber after the plan is merged.
Requires:   requirement ids, or sections of specs/0000-framework (05 §4; 05 §11 item 3).
Depends on: keys of earlier tickets only, or "—".
Touches:    folders or files, including shared ones (Directory.Packages.props, PublicAPI.Unshipped.txt, errors.approved.txt, a migration folder).
Tier:       expected low | medium | high; the classifier computes the real one.
Size:       estimated changed hand-written lines.
-->

| Key | Title | Requirements | Depends on | Touches | Tier | Size | Failing test first | Verify |
|---|---|---|---|---|---|---|---|---|
| T1 | | R1 | — | `src/…`, `tests/…` | low | 120 | `<TestClass>.<Test_name>` — fails because … | `dotnet test tests/… --filter-query …` → passes |
| T2 | | R1, R2 | T1 | | medium | 180 | | |
