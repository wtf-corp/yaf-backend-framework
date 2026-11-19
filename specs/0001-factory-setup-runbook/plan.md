# 0001 — Factory setup runbook: plan

**Spec** none: this plan has no `spec.md`. Its approved basis is ADR-0029 (`docs/spec/adr/0029-software-factory.md`) §1, §3, §4, §7, §8 and the kit README (`docs/spec/factory/README.md`), sections "Install at M0" and "Human setup checklist", both on `main` since #1 · **Status** Draft

Why there is no spec: the change adds one maintainer document and changes no behaviour. It has no requirement of its own to approve: what the factory is, and what its setup must achieve, is already decided in ADR-0029 and described in the kit README. The runbook records how that setup was carried out in this repository, so it can be repeated in another one. It is too large to be trivial (`factory/ticket` allows about 20 changed lines), so it needs tickets, and tickets need a merged plan. Having no spec is a deviation from the process and is listed as one below; it flags this plan for a maintainer's approving review.

## Approach

Add one file, `docs/maintainers/factory-setup.md`: a runbook a maintainer follows top to bottom to install the factory in a repository **by reference**, the way pull request #2 did here. It is generic, with placeholders for organisation, repository, team, app and logins, and gives this repository's values once as the worked example. It links to the files that hold the detail instead of repeating them, and it documents only what the kit README does not: the by-reference layout, the exact GitHub settings applied, how to act as the app locally, and what went wrong on the way.

The facts are in [`research.md`](./research.md), each with its provenance. The work is two stacked tickets on the one file: T1 creates it with the repository part (sections 1–3), T2 adds the GitHub part (sections 4–10). Each implementer writes from that file and the files it cites, and re-reads each cited file: where the repository and the research differ, the repository wins and the difference is a comment on the ticket.

Files read to decide: `AGENTS.md`, `CLAUDE.md`, `specs/AGENTS.md`, `.agents/README.md`, `.github/CODEOWNERS`, `.github/scripts/sync-references.mjs`, the seven `.github/workflows/factory-*.yml`, `docs/spec/factory/README.md`, `docs/spec/factory/.claude/settings.json`, and in `docs/spec/factory/.github/factory/`: `policy.json`, `classify.mjs`, `size.mjs`, `ticket.mjs`, `plan.mjs`, `gates.mjs`, `create-tickets.mjs`, `setup-github.sh`, `labels.json`; ADR-0025 and ADR-0029; `docs/spec/agents/AGENTS.nested.md` (sections `specs/` and `docs/`); `docs/spec/spec/17-ci-release-docs.md` §2.2, §5, §7 and `18-build-plan.md` §1, §2, §5.

### What the runbook contains

Ten numbered sections, in the order a maintainer works. The headings are fixed, because verification command V4 counts them. Each content item `C1…C10` is one section and is the coverage target of the test strategy. The last column is a budget in lines as `size.mjs` counts them: every added line, blank lines included.

| Item | Ticket | Section heading | Content | Basis | Research | Lines (about) |
|---|---|---|---|---|---|---|
| C1 | T1 | `## 1. What this sets up` | Purpose, who runs it, prerequisites (`gh`, `jq`, Node 22+, organisation owner rights), the placeholder table with this repository's values (the App ID included: it is an identifier, not a secret), and the rule that agents hand settings commands to the human | ADR-0029 §1; README "Install at M0" intro | §1, §3 intro | 19 |
| C2 | T1 | `## 2. The kit by reference` | The bundle stays in `docs/spec/`; `AGENTS.md` path map, `CLAUDE.md`, pointer files; `sync-references.mjs` (what it generates, `--check`), CI job `references`; the workflows as real copies and their four changes, pointing at the files for the pinned SHAs; the table mapping the README's install steps to this layout | README "Install at M0" 1–3, design decision 1 | §2 | 35 |
| C3 | T1 | `## 3. Policy and code owners` | CODEOWNERS with `/docs/spec/`; the additive policy changes; why the dot-folder patterns are needed; the kit's test command and expected count | README "Install at M0" 4–5; ADR-0029 §4 | §2 | 21 |
| C4 | T2 | `## 4. Labels, Project and variables` | `gh auth refresh -s project`; `setup-github.sh` and what it creates | README "Human setup checklist" intro; ADR-0029 §3 | §3.1 | 12 |
| C5 | T2 | `## 5. Repository settings and the main ruleset` | Auto-merge, squash only, delete branch on merge; team access; the ruleset JSON in full as applied (research §4), the command that applies it, the `integration_id` note | README checklist 1, 5; ADR-0029 §4, §7 | §3.2–3.4, §4 | 56 |
| C6 | T2 | `## 6. The app, environments and secrets` | The app, its permissions, selected repositories, `FACTORY_APP_ID`, `bots.factory`; `factory-merge` and `factory-review`; the dedicated API key and the rejected subscription token; setting secrets interactively | README checklist 2–4; ADR-0029 §8 | §3.5–3.7 | 28 |
| C7 | T2 | `## 7. Acting as the app locally` | Why; the token procedure as numbered steps with the endpoints and the `GIT_CONFIG_*` values; handling of the key and the token (never written to disk; printed only on request, in the human's own terminal). The section links to no script: none is in the repository | README checklist 2; ADR-0029 §8 | §5 | 20 |
| C8 | T2 | `## 8. Prove the gates` | The kit README's seven trials as a checklist, with expected results and a column for the pull request that proved each; only trial 1 (#3) is filled, with its state as recorded in research §5. The size trial keeps both halves: `size-accepted` applied by the app does not help, applied by a human it does. The section says that this list is the README's, and links `17` §7 for the full gate acceptance criteria, which are M0's exit criteria and are not repeated here | README "Install at M0" 7 | §5 | 16 |
| C9 | T2 | `## 9. Gotchas` | Ticket-or-trivial rule; the single-maintainer approval problem; Git Bash and `gh api /path`; `integration_id`; additive policy edits | ADR-0029 §3, §4, §7; the owner's setup notes | §6 | 12 |
| C10 | T2 | `## 10. Still to do here` | Getting the model review in `factory-review` to pass; `ci.yml` and its checks in the ruleset; a ruleset for `next`; the merge queue. One line says that the README's "Verify before relying on it" list stays open, with a link; one line says that checklist items 6 (the human-only labels are protected by process) and 7 (local settings and their gaps) are left to the README, with a link | README checklist 5–7; `17` §2 | §7, §5 | 9 |

Sums, with four lines for the title and introduction in T1 and two changed introduction lines in T2: T1 about 80 lines, estimated at 90; T2 about 155, estimated at 160. A paragraph is one line; nothing is hard-wrapped. The first plan review counted a single ticket at 200 to 215 lines once blank lines are included, which is why the work is split.

Rules for the text:

- No secret, key material, token, API key or email address, and no file that holds one. The App ID (5834995 here) is an identifier, not a secret: it appears once, as the worked example of `<app-id>` and of variable `FACTORY_APP_ID`.
- Every [owner] and [assumed] fact from the research keeps that standing in the runbook: "as applied here" for the first, "confirm against the live setting" for the second. Trials 2–7 are listed as to do, not as passed.
- Present tense, no marketing, and never a statement that YAF or a repository is "compliant" or "certified" (hard rule 9).
- Links to files in this repository are relative, so V5 can check them.

### Alternatives rejected

| Alternative | Why not |
|---|---|
| Add the by-reference notes to the kit README (`docs/spec/factory/README.md`) | The kit README describes the kit for any repository, in the installed layout. This repository's layout and settings are not part of the kit. The file is also under `docs/spec/**`: tier high and code-owned, where a plain docs file is tier low |
| An ADR | An ADR records a decision and its reasons; `adr/AGENTS.md` rule 6 sends how-to material to the docs. No decision changes here |
| A section in the root `README.md` | About 230 lines of maintainer procedure in a file whose readers are users of the packages |
| Commit the token script and the ruleset JSON as files under `.github/` and keep the runbook short | That is a change to guard paths (tier high) and adds a script that handles the app's key; out of scope for a documentation change. It can be a later plan of its own |
| Include the token script in the runbook, or keep it verbatim in `research.md` | Refused by the owner: `plan.md` and `research.md` are not code-owned and get no code review, so a script that handles the app's key would enter the repository unreviewed and could later be edited without a human. The runbook would also pass `size.soft` |
| Link the script by a pinned permalink | Refused by the owner with the option above: the script would still sit in a file nobody reviewed as code |
| One ticket for the whole runbook | `size.mjs` counts blank lines, so the file comes to 200 to 215 lines: no headroom under `size.soft` (200). ADR-0029 §5 has dependent tickets stacked instead |
| Three or more tickets | The two parts are each well under the soft limit; a further cut would split the GitHub steps mid-procedure |

## Affected packages

| Layer | Package / area | Change |
|---|---|---|
| Core | none | none |
| Domain | none | none |
| Application | none | none |
| Infrastructure | none | none |
| Persistence | none | none |
| Http | none | none |
| Adapters | none | none |
| Testing (`Testing`, `Testing.Integration`) | none | none |
| Analyzers | none | none |
| Sample | none | none |
| Templates | none | none |
| Docs | `docs/maintainers/factory-setup.md` | New file (T1), extended (T2). Not part of the DocFX site, which does not exist yet (see deviations) |

No file under `docs/spec/`, `.github/`, `.claude/` or `.agents/` changes, and no workflow, policy value or instruction file.

## Public API, schema and error-code delta

- **Added** none
- **Changed** none
- **Removed** none

Breaking: no.
New dependency: no.

```factory:delta
{ "publicApi": [], "schema": [], "errorCodes": [], "contracts": [], "configuration": [], "telemetry": [], "dependencies": [] }
```

## Schema and migration plan

none

## Test strategy

There is no executable behaviour to test and no test project is involved. Each content item is proved by the verification commands below and by the pull-request reviewer comparing the runbook with `research.md` and the cited files.

| Scenario | Requirements | Ticket | Level (unit / integration / architecture / conformance) | Test project | Notes |
|---|---|---|---|---|---|
| The runbook exists with sections 1–3 | C1–C3 | T1 | none (documentation check) | none | V1, V4 |
| The runbook has all ten sections | C4–C10 | T2 | none (documentation check) | none | V1, V4 |
| The change is the runbook only, within the size limit | ADR-0029 §5 | T1, T2 | none (documentation check) | none | V2, V3 |
| The ruleset JSON parses and requires exactly the policy's checks plus `references` | C5; ADR-0029 §7 | T2 | none (documentation check) | none | V6 |
| Every relative link resolves | C2, C3, C7, C8 | T1, T2 | none (documentation check) | none | V5 |
| No secret, key, token or email address; no claim of being compliant or certified | hard rules 3 and 9 | T1, T2 | none (documentation check) | none | V7, V8 |
| Each fact agrees with its source | C1–C10 | T1, T2 | review | none | The reviewer reads `research.md` and the cited files |

## Risks and rollback

| Risk | Likelihood | Mitigation | Rollback |
|---|---|---|---|
| The runbook states a setting that differs from the live one (how the merge methods were set; the ruleset JSON is the file actually applied) | Medium | Such values are marked [assumed] in the research and "confirm against the live setting" in the runbook; T2 asks for tier high, so its pull request cannot merge until the owner approves it, and that review is where the owner compares the text with the live settings | Revert the pull request; documentation only, nothing depends on it |
| A secret, key, token or email address reaches the file | Low | V7; the text rules above; the research holds none to copy | Revert, and rotate whatever leaked: a revert does not remove it from history |
| The runbook goes stale when the setup changes (`ci.yml` checks added to the ruleset, a second maintainer, new action pins) | High over time | It links to files for anything that changes often (SHAs, policy, workflows) and ends with "Still to do here"; the plan that makes such a change updates the runbook | Revert or correct in a follow-up docs ticket |
| A ticket grows beyond its estimate | Low | Two tickets estimated at 90 and 160 against a soft limit of 200, with budgets per section that count blank lines; the token script is described, not included | A ticket that passes 200 says why in its pull request; the implementer stops and comments on the ticket before going further, and does not grow it |
| The model review in environment `factory-review` fails: on pull request #3 `factory/review` failed on its first model API call (research §5). The same call backs `factory/doc-review` on this plan's pull request and `factory/review` on T1 and T2, and all are required checks | Certain until fixed | A precondition, not something this plan works around: the owner fixes the key or account setup in `factory-review` before this plan's pull request can merge. No check is skipped, bypassed or made optional to get past it | Nothing to roll back: the pull requests wait, unmerged |
| T1 is rejected or delayed, holding T2 | Low | T2 is stacked on T1 and merges after it (ADR-0029 §5); T1 is the smaller and simpler of the two | T2 waits; if T1 is abandoned, a plan update merges the two parts or re-cuts them |
| A maintainer rebuilds the token script from the description and gets it wrong | Medium | Every step cites the GitHub or git documentation it rests on, with the exact endpoints, claims and `GIT_CONFIG_*` values; the token lives one hour and is scoped to one repository, and cannot exceed the app's permissions | Revoke the token, or the app's private key if it may have been exposed, and generate a new key |
| A reader takes the trial list for proof that the gates work | Low | Trial 1 is recorded as it stands; the rest are a checklist with empty result columns | Correct the wording in a follow-up docs ticket |
| The DocFX site of M14 picks up `docs/maintainers/` and fails on it, or publishes it | Low | Decision log: the plan that creates the site excludes the folder or adds it to `toc.yml` on purpose | Exclude the folder in `docfx.json` |

## Verification commands

Run in Git Bash from the repository root, on the ticket's branch. There is no solution yet, so `dotnet build` and `dotnet test` do not apply, and there is no docs build (see deviations). `BASE` is `origin/main`; for T2 while T1 has not merged, it is T1's branch.

V1, the check that fails first:

    test -f docs/maintainers/factory-setup.md                                # T1
    test "$(grep -c "^## [0-9]" docs/maintainers/factory-setup.md)" = 10     # T2

Expected: exit 1 before the ticket, exit 0 after.

V2, scope:

    git diff --name-only $BASE...HEAD

Expected: exactly one line, `docs/maintainers/factory-setup.md`.

V3, size:

    node docs/spec/factory/.github/factory/size.mjs $BASE HEAD

Expected: `N changed lines (soft 200, hard 400)` with no "over the … limit" suffix; N near the estimate (T1 90, T2 160) and in any case at most 200.

V4, the sections:

    grep -c "^## [0-9]" docs/maintainers/factory-setup.md

Expected: `3` after T1, `10` after T2.

V5, the relative links (T1 and T2):

    node -e '
    const fs = require("fs"), path = require("path");
    const file = "docs/maintainers/factory-setup.md";
    const text = fs.readFileSync(file, "utf8");
    const dead = [...text.matchAll(/\]\((?!https?:|#)([^)#\s]+)/g)].map((m) => m[1]).filter((l) => !fs.existsSync(path.join(path.dirname(file), l)));
    if (dead.length) throw new Error("dead links: " + dead.join());
    console.log("links OK");
    '

Expected: `links OK`, exit 0.

V6, the ruleset JSON (T2):

    node -e '
    const fs = require("fs");
    const text = fs.readFileSync("docs/maintainers/factory-setup.md", "utf8");
    const block = text.match(/```json\r?\n([\s\S]*?)\r?\n```/);
    if (!block) throw new Error("no json block");
    const rule = JSON.parse(block[1]).rules.find((r) => r.type === "required_status_checks");
    const got = rule.parameters.required_status_checks.map((c) => c.context).sort();
    const policy = JSON.parse(fs.readFileSync("docs/spec/factory/.github/factory/policy.json", "utf8"));
    const want = [...policy.checks, "references"].sort();
    if (got.join() !== want.join()) throw new Error("required checks differ from policy.checks plus references: " + got.join());
    console.log("ruleset JSON OK");
    '

Expected: `ruleset JSON OK`, exit 0. The first `json` block in the file must be the ruleset.

V7, nothing secret and no email address:

    grep -nE -e "BEGIN [A-Z ]*PRIVATE KEY" -e "sk-ant-" -e "gh[pousr]_[A-Za-z0-9_]{20,}" -e "github_pat_" -e "[A-Za-z0-9._%+-]+@[A-Za-z0-9-]+\.[a-z]{2,}" docs/maintainers/factory-setup.md

Expected: no output, exit 1.

V8, wording:

    grep -niE -e "compliant" -e "certified" docs/maintainers/factory-setup.md

Expected: no output, exit 1.

V9, nothing else moved:

    node .github/scripts/sync-references.mjs --check
    node --test "docs/spec/factory/.github/factory/tests/*.test.mjs"

Expected: exit 0 for each.

The `Verify` cell of each ticket holds a command only, because `create-tickets.mjs` renders the cell as a code block in the issue. That command is V1 and V4 for the ticket and must exit 0; the full verification is V1–V9 here.

V4 to V8 were tried on 2026-11-19 against a mock file built from the headings and the JSON of `research.md` §4: they pass on it, and V5 and V7 fail when a dead link or an email address is added.

## Research

[`research.md`](./research.md): the verified facts of the setup with their provenance, the ruleset JSON as applied, the app-token procedure with the GitHub and git documentation it rests on (the script itself is not in the repository), the trial list, the gotchas, how the gates classify this plan and its tickets, and the open points.

## Decision log

| Date | Decision | Reason | By |
|---|---|---|---|
| 2026-11-19 | No `spec.md`; the plan cites ADR-0029 and the kit README as its approved basis | Documentation only: no behaviour, public API, schema or CI change, so there is no requirement to approve beyond what those two documents already hold | Proposed by the planner from the task brief. It is listed as a deviation; the owner approves it by approving this flagged plan's pull request, and by nothing else |
| 2026-11-19 | Folder `0001-factory-setup-runbook`, and the plan stays standalone | The branch has this name. `18` §5 suggests `0001-repository-skeleton` for M0; that list is a suggestion. The runbook is not folded into M0's plan, which takes the next free number | Human owner (branch); coordinator, answering the plan review |
| 2026-11-19 | The file is `docs/maintainers/factory-setup.md` | Named in the brief; see deviations for what it departs from | Human owner (path); planner (the note for M14) |
| 2026-11-19 | Two stacked tickets, T1 the repository part and T2 the GitHub part | One ticket had no headroom under the soft limit once blank lines are counted (plan review, round 1). Both touch the one file, so T2 depends on T1 | Planner, on the plan reviewer's finding |
| 2026-11-19 | The token script stays out of the repository: research §5 and the runbook describe the steps, with the exact helper string, and link nothing | The owner's decision ("keep the script out"). Rejected: committing it in `research.md` (no code review, and later edits need no human); a pinned permalink | Human owner, relayed by the coordinator after plan review round 2 |
| 2026-11-19 | **Open point:** the script's long-term home: a reviewed, tier-high file under `.github/`, in a plan of its own | It handles the app's private key and belongs under code review and code ownership; that is a guard-path change, out of scope for a documentation plan | Open; the human owner decides |
| 2026-11-19 | T2 asks for tier high | The classifier computes low for a docs file, and then nothing makes anyone compare the stated settings with the live ones. With the request, T2's pull request waits for the owner. A label may raise a tier (ADR-0029 §4); it makes this plan flagged for a second reason | Planner, on the plan reviewer's finding |
| 2026-11-19 | Pinned action SHAs are not repeated in the runbook | They change with every update; the workflow files are the record | Planner |
| 2026-11-19 | Verification is V1–V9, not the docs build | See deviations | Planner |
| 2026-11-19 | Coverage targets are content items `C1…C10`, not `R…`/`S…` ids | There is no spec to take ids from; the items are derived from the cited sections | Planner |
| 2026-11-19 | Ticket titles without a scope: `docs: …` | The file belongs to no package; `docs(docs)` adds nothing. #1 and #2 are unscoped too | Planner |
| 2026-11-19 | C8 lists the kit README's seven trials only and links `17` §7 | `17` §7 holds the full gate acceptance criteria, which are M0's exit criteria; repeating them here would make a second copy to keep in step | Planner, on the plan reviewer's finding |

## Deviations from the specification

Three departures from the process and the specification. Each is for the human maintainer to accept or refuse by reviewing this plan's pull request; none changes behaviour, security or a public contract.

1. **No spec for a change that is not an initial-build milestone.** ADR-0023 and ADR-0025 put a merged `spec.md` before a plan, and allow a plan without one only for milestones M0–M15. This plan has none and cites ADR-0029 and the kit README instead. Why: the change is one maintainer document with no requirement of its own, and it is too large to be trivial.
2. **`docs/maintainers/` is outside the documentation site's layout.** `17` §5 lists the folders of the DocFX site (`articles/`, `api/`, `compliance/`, `reference/`); `maintainers/` is not one of them. The rules of `docs/AGENTS.md` are written for articles of that site, and this plan treats them as not applying to a maintainer document: in particular rule 2 (code is included from the sample or a compiled test, not pasted; the runbook has shell commands and JSON inline), rule 4 (types are linked with `xref`), rule 7 (a new article is added to `toc.yml`) and rule 8 (verify with the docs build). Rules 3, 5 and 6 on plain wording and on claims are kept. There is no `docfx.json` and no `toc.yml` until M14 in any case. Why: the runbook is for maintainers of the repository, not for users of the packages. The plan that creates the site excludes the folder or adds it to `toc.yml` on purpose.
3. **Verification is not the docs build.** ADR-0025 (Tickets table, rule 3) names the docs build as the verification of a pure documentation ticket. T1 and T2 use V1–V9 instead. Why: `dotnet docfx docs/docfx.json` cannot run before M14.

## Tickets

T2's tier is a request to raise: `classify.mjs` computes low for this path. The orchestrator applies `tier:high` to T2's pull request when it opens it, so `factory/human-approval` waits for the owner.

| Key | Title | Type | Area | Requirements | Depends on | Touches | Tier | Size | Failing test first | Verify |
|---|---|---|---|---|---|---|---|---|---|---|
| T1 | docs: add the factory setup runbook with the repository part | docs | docs | C1–C3 (ADR-0029 §1, §4; kit README "Install at M0" 1–5) | — | `docs/maintainers/factory-setup.md` | low | 90 | V1 for T1 exits 1, because the runbook does not exist. Full verification with expected results: V1–V5 and V7–V9 under "Verification commands" in the plan | `test -f docs/maintainers/factory-setup.md && test "$(grep -c "^## [0-9]" docs/maintainers/factory-setup.md)" = 3` |
| T2 | docs: add the GitHub setup to the factory setup runbook | docs | docs | C4–C10 (ADR-0029 §3, §4, §7, §8; kit README "Install at M0" 7, "Human setup checklist") | T1 | `docs/maintainers/factory-setup.md` | high (requested) | 160 | V1 for T2 exits 1, because the runbook has three sections, not ten; V6 fails, because there is no ruleset JSON. Full verification with expected results: V1–V9 under "Verification commands" in the plan | `test "$(grep -c "^## [0-9]" docs/maintainers/factory-setup.md)" = 10 && grep -q "factory/human-approval" docs/maintainers/factory-setup.md` |
