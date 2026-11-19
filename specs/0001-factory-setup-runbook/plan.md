# 0001 — Factory setup runbook: plan

**Spec** none: this plan has no `spec.md`. Its approved basis is ADR-0029 (`docs/spec/adr/0029-software-factory.md`) §1, §3, §4, §7, §8 and the kit README (`docs/spec/factory/README.md`), sections "Install at M0" and "Human setup checklist", both on `main` since #1 · **Status** Draft

Why there is no spec: the change adds one maintainer document and changes no behaviour. It has no requirement of its own to approve: what the factory is, and what its setup must achieve, is already decided in ADR-0029 and described in the kit README. The runbook records how that setup was carried out in this repository, so it can be repeated in another one. It is too large to be trivial (`factory/ticket` allows about 20 changed lines), so it needs tickets, and tickets need a merged plan. Having no spec is a deviation from the process and is listed as one below; it flags this plan for a maintainer's approving review.

## Approach

Add one file, `docs/maintainers/factory-setup.md`: a runbook a maintainer follows top to bottom to install the factory in a repository **by reference**, the way pull request #2 did here. It is generic, with placeholders for organisation, repository, team, app and logins, and gives this repository's values once as the worked example. It links to the files that hold the detail instead of repeating them, and it documents only what the kit README does not: the by-reference layout, the exact GitHub settings applied, how to act as the app locally, and what went wrong on the way.

The facts are in [`research.md`](./research.md), each with its provenance. The work is two stacked tickets on the one file: T1 creates it with the part done from the repository checkout (sections 1–4), T2 adds the GitHub settings (sections 5–10). Each implementer writes from that file and the files it cites, and re-reads each cited file: where the repository and the research differ, the repository wins and the difference is a comment on the ticket.

Files read to decide: `AGENTS.md`, `CLAUDE.md`, `specs/AGENTS.md`, `.agents/README.md`, `.github/CODEOWNERS`, `.github/scripts/sync-references.mjs`, the seven `.github/workflows/factory-*.yml`, `docs/spec/factory/README.md`, `docs/spec/factory/.claude/settings.json`, and in `docs/spec/factory/.github/factory/`: `policy.json`, `classify.mjs`, `size.mjs`, `ticket.mjs`, `plan.mjs`, `gates.mjs`, `create-tickets.mjs`, `setup-github.sh`, `labels.json`; ADR-0025 and ADR-0029; `docs/spec/agents/AGENTS.nested.md` (sections `specs/` and `docs/`); `docs/spec/spec/17-ci-release-docs.md` §2.2, §5, §7 and `18-build-plan.md` §1, §2, §4, §5; on `main` after #6, `AGENTS.md` ("Model reviews run locally (temporary)") and `.github/scripts/local-review.mjs`.

### What the runbook contains

Ten numbered sections, in the order a maintainer works. The headings are fixed, because verification command V4 counts them. Each content item `C1…C10` is one section and is the coverage target of the test strategy. The last column is a budget in lines as `size.mjs` counts them: every added line, blank lines included.

| Item | Ticket | Section heading | Content | Basis | Research | Lines (about) |
|---|---|---|---|---|---|---|
| C1 | T1 | `## 1. What this sets up` | Purpose, who runs it, prerequisites (`gh`, `jq`, Node 22+, organisation owner rights), the placeholder table with this repository's values (the App ID included: it is an identifier, not a secret), and the rule that agents hand settings commands to the human | ADR-0029 §1; README "Install at M0" intro | §1, §3 intro | 19 |
| C2 | T1 | `## 2. The kit by reference` | The bundle stays in `docs/spec/`; `AGENTS.md` path map, `CLAUDE.md`, pointer files; `sync-references.mjs` (what it generates, `--check`), CI job `references`; the workflows as real copies and their four changes, pointing at the files for the pinned SHAs; the table mapping the README's install steps to this layout | README "Install at M0" 1–3, design decision 1 | §2 | 35 |
| C3 | T1 | `## 3. Policy and code owners` | CODEOWNERS with `/docs/spec/`; the additive policy changes; why the dot-folder patterns are needed; the kit's test command and expected count | README "Install at M0" 4–5; ADR-0029 §4 | §2 | 21 |
| C4 | T1 | `## 4. Labels, Project and variables` | `gh auth refresh -s project`; `setup-github.sh` and what it creates | README "Human setup checklist" intro; ADR-0029 §3 | §3.1 | 12 |
| C5 | T2 | `## 5. Repository settings and the main ruleset` | Auto-merge, squash only, delete branch on merge; team access; the ruleset JSON in full as it stands here (research §4), the command that applies it, the check that exactly one ruleset applies to `main`; the `integration_id` note: here the two review checks are pinned to the reviewer app, and a repository whose reviews run in CI pins all ten to GitHub Actions | README checklist 1, 5; ADR-0029 §4, §7 | §3.2–3.4, §4 | 60 |
| C6 | T2 | `## 6. The app, environments and secrets` | The app, its permissions, selected repositories, `FACTORY_APP_ID`, `bots.factory`; `factory-merge` and `factory-review`; the dedicated API key and the rejected subscription token; setting secrets interactively. The CI review is presented as the intended setup, the one to port. A short paragraph gives this repository's current state: the reviews run locally through a second app (`yaf-reviewer`) while `FACTORY_REVIEW_IN_CI` is not `true`; it points to `AGENTS.md` and `.github/scripts/local-review.mjs` and repeats neither | README checklist 2–4; ADR-0029 §8 | §3.5–3.8 | 33 |
| C7 | T2 | `## 7. Acting as the app locally` | Why; the token procedure as numbered steps with the endpoints and the `GIT_CONFIG_*` values; where the key is: a file in a private per-user folder outside every repository (default `~/.wtf-corp/yaf-factory/`), readable by the user only, its path in a user-level environment variable (`YAF_FACTORY_KEY_FILE`, `YAF_REVIEWER_KEY_FILE`), never the key itself in a variable. The setup commands for each operating system are not repeated here: the section points to `AGENTS.md`, "Running the factory locally" (plan 0002's T7), which holds the one copy, or names plan 0002 while that section does not exist; a fresh key is generated if one is lost or may have been read; the token is never written to disk and is printed only on request, in the human's own terminal. The section says in one line that this departs from the default of `18` §4 and who accepted it (deviation 4). It links `.github/scripts/factory-token.mjs` if that file is on `main` when T2 is written (plan 0002); otherwise it links no script and names plan 0002 | README checklist 2; ADR-0029 §8; `18` §4 "Agent identity for local work" | §5 | 24 |
| C8 | T2 | `## 8. Prove the gates` | The kit README's seven trials as a checklist, with expected results and a column for the pull request that proved each; none is marked as passed: trial 1 (#3) is recorded as it happened and as to repeat (research §5). The size trial keeps both halves: `size-accepted` applied by the app does not help, applied by a human it does. The section says that this list is the README's, and links `17` §7 for the full gate acceptance criteria, which are M0's exit criteria and are not repeated here | README "Install at M0" 7 | §5 | 16 |
| C9 | T2 | `## 9. Gotchas` | Ticket-or-trivial rule; the single-maintainer approval problem; Git Bash and `gh api /path`; `integration_id`; additive policy edits; a disabled ruleset releases every armed auto-merge, so cancel auto-merge or pause before a break-glass; rulesets stack, so check for exactly one | ADR-0029 §3, §4, §6, §7; the owner's setup notes | §6 | 15 |
| C10 | T2 | `## 10. Still to do here` | Moving the model reviews back to CI (the steps are in `AGENTS.md`; one line with a link); repeating trial 1; `ci.yml` and its checks in the ruleset; a ruleset for `next`; the merge queue. One line says that the README's "Verify before relying on it" list stays open, with a link; one line says that checklist items 6 (the human-only labels are protected by process) and 7 (local settings and their gaps) are left to the README, with a link | README checklist 5–7; `17` §2 | §7, §5 | 10 |

Sums, with four lines for the title and introduction in T1 and two changed introduction lines in T2: T1 about 92 lines, estimated at 105; T2 about 160, estimated at 170. C7 went from 22 to 24 lines with the plan update of 2026-11-19: the key paragraph and the line on the `18` §4 deviation came in, the standard-input and clipboard text went out, and the per-system setup commands stay in `AGENTS.md`; the estimate of 170 keeps its headroom. Section 4 moved from T2 to T1 when the GitHub part grew by the reviewer app, the key handling and two gotchas, so that both tickets keep headroom under 200. A paragraph is one line; nothing is hard-wrapped. The first plan review counted a single ticket at 200 to 215 lines once blank lines are included, which is why the work is split.

Rules for the text:

- No secret, key material, token, API key or email address, and no file that holds one. The App IDs (5834995 for the factory app, 5837737 for the reviewer app) are identifiers, not secrets: each appears once, as the worked example.
- The CI review is written as the setup to port; the local review is written as this repository's temporary state, by pointer. If `AGENTS.md` no longer has that section when T2 is written, the paragraph and the pointer are dropped and the ruleset JSON is taken from the then-current state, with a comment on the ticket.
- Every [owner] and [assumed] fact from the research keeps that standing in the runbook: "as applied here" for the first, "confirm against the live setting" for the second. All seven trials are listed as to do, none as passed.
- Present tense, no marketing, and never a statement that YAF or a repository is "compliant" or "certified" (hard rule 9).
- Links to files in this repository are relative, so V5 can check them.

### Alternatives rejected

| Alternative | Why not |
|---|---|
| Add the by-reference notes to the kit README (`docs/spec/factory/README.md`) | The kit README describes the kit for any repository, in the installed layout. This repository's layout and settings are not part of the kit. The file is also under `docs/spec/**`: tier high and code-owned, where a plain docs file is tier low |
| An ADR | An ADR records a decision and its reasons; `adr/AGENTS.md` rule 6 sends how-to material to the docs. No decision changes here |
| A section in the root `README.md` | About 230 lines of maintainer procedure in a file whose readers are users of the packages |
| Commit the token script and the ruleset JSON as files under `.github/` and keep the runbook short | That is a change to guard paths (tier high) and adds a script that handles the app's key; out of scope for a documentation change. It is plan 0002 (`specs/0002-local-factory-operation/plan.md`) |
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
| The runbook exists with sections 1–4 | C1–C4 | T1 | none (documentation check) | none | V1, V4 |
| The runbook has all ten sections | C5–C10 | T2 | none (documentation check) | none | V1, V4 |
| The change is the runbook only, within the size limit | ADR-0029 §5 | T1, T2 | none (documentation check) | none | V2, V3 |
| The ruleset JSON parses and requires exactly the policy's checks plus `references` | C5; ADR-0029 §7 | T2 | none (documentation check) | none | V6 |
| Every relative link resolves | C2, C3, C6, C8, C10 | T1, T2 | none (documentation check) | none | V5 |
| No secret, key, token or email address; no claim of being compliant or certified | hard rules 3 and 9 | T1, T2 | none (documentation check) | none | V7, V8 |
| Each fact agrees with its source | C1–C10 | T1, T2 | review | none | The reviewer reads `research.md` and the cited files |

## Risks and rollback

| Risk | Likelihood | Mitigation | Rollback |
|---|---|---|---|
| The runbook states a setting that differs from the live one (how the merge methods were set; the ruleset JSON is the file actually applied) | Medium | Such values are marked [assumed] in the research and "confirm against the live setting" in the runbook; T2 asks for tier high, so its pull request cannot merge until the owner approves it, and that review is where the owner compares the text with the live settings | Revert the pull request; documentation only, nothing depends on it |
| A secret, key, token or email address reaches the file | Low | V7; the text rules above; the research holds none to copy | Revert, and rotate whatever leaked: a revert does not remove it from history |
| The runbook goes stale when the setup changes (`ci.yml` checks added to the ruleset, a second maintainer, new action pins) | High over time | It links to files for anything that changes often (SHAs, policy, workflows) and ends with "Still to do here"; the plan that makes such a change updates the runbook | Revert or correct in a follow-up docs ticket |
| A ticket grows beyond its estimate | Low | Two tickets estimated at 105 and 170 against a soft limit of 200, with budgets per section that count blank lines; the token script is described, not included | A ticket that passes 200 says why in its pull request; the implementer stops and comments on the ticket before going further, and does not grow it |
| The model reviews do not run in CI: the account behind `factory-review` does not work yet, so `factory/review` and `factory/doc-review` are posted by the reviewer app from a local run (`AGENTS.md` on `main`, #6). This plan's pull request and both tickets depend on that stopgap, and the runbook describes a state that is meant to end | Certain until the reviews move back | The runbook presents CI review as the setup to port and the local review as temporary, by pointer to `AGENTS.md` and `local-review.mjs`; V5 fails on a pointer to a file that is gone, and the text rules say what T2 does then. No check is skipped, bypassed or made optional | Correct the paragraph and the ruleset JSON in a follow-up docs ticket when the reviews move back |
| An app key leaks or is lost | Medium: each key is a file on disk that any process running as the user, agents included, can read (deviation 4) | The folder is outside every repository and readable by the user only; variables hold paths, never the key; the runbook says what the permissions do not stop and points to `AGENTS.md` for the setup commands of each operating system. Plan 0002 adds a deny rule for agents' reads of the folder, which stops accidents only | Delete the key on the app's settings page and generate a fresh one; update `FACTORY_APP_PRIVATE_KEY` for the factory app |
| T1 is rejected or delayed, holding T2 | Low | T2 is stacked on T1 and merges after it (ADR-0029 §5); T1 is the smaller and simpler of the two | T2 waits; if T1 is abandoned, a plan update merges the two parts or re-cuts them |
| A maintainer rebuilds the token script from the description and gets it wrong | Medium until plan 0002 puts a reviewed script in the repository | Every step cites the GitHub or git documentation it rests on, with the exact endpoints, claims and `GIT_CONFIG_*` values; the token lives one hour and is scoped to one repository, and cannot exceed the app's permissions | Revoke the token, or the app's private key if it may have been exposed, and generate a new key |
| A reader takes the trial list for proof that the gates work | Low | No trial is marked as passed: trial 1 is recorded as merged during a break-glass and as to repeat; the rest are a checklist with empty result columns | Correct the wording in a follow-up docs ticket |
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

Expected: `N changed lines (soft S, hard H)` with no "over the … limit" suffix, where S and H are the policy's limits on the branch (200 and 400 when this plan merged; plan 0002's T8 raises them for the initial build); N near the estimate (T1 105, T2 170) and in any case at most 200.

V4, the sections:

    grep -c "^## [0-9]" docs/maintainers/factory-setup.md

Expected: `4` after T1, `10` after T2.

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

[`research.md`](./research.md): the verified facts of the setup with their provenance, the ruleset JSON as applied, the app-token procedure with the GitHub and git documentation it rests on (the script is not in the repository until plan 0002 adds it), the trial list, the gotchas, how the gates classify this plan and its tickets, and the open points.

## Decision log

| Date | Decision | Reason | By |
|---|---|---|---|
| 2026-11-19 | No `spec.md`; the plan cites ADR-0029 and the kit README as its approved basis | Documentation only: no behaviour, public API, schema or CI change, so there is no requirement to approve beyond what those two documents already hold | Proposed by the planner from the task brief. It is listed as a deviation; the owner approves it by approving this flagged plan's pull request, and by nothing else |
| 2026-11-19 | Folder `0001-factory-setup-runbook`, and the plan stays standalone | The branch has this name. `18` §5 suggests `0001-repository-skeleton` for M0; that list is a suggestion. The runbook is not folded into M0's plan, which takes the next free number | Human owner (branch); coordinator, answering the plan review |
| 2026-11-19 | The file is `docs/maintainers/factory-setup.md` | Named in the brief; see deviations for what it departs from | Human owner (path); planner (the note for M14) |
| 2026-11-19 | Two stacked tickets: T1 the part done from the checkout (sections 1–4), T2 the GitHub settings (sections 5–10) | One ticket had no headroom under the soft limit once blank lines are counted (plan review, round 1). Both touch the one file, so T2 depends on T1. Section 4 moved to T1 when the GitHub part grew (pull-request review of the plan) | Planner, on the plan reviewer's findings |
| 2026-11-19 | **Superseded on 2026-11-19 by the last row of this table.** `18` §4 "Agent identity for local work" is settled by its default: a short-lived installation token minted from the app's key; the key is held in a key manager, passed to the script through standard input, and never stored as a file. A fresh key is generated if one is lost | It is the default of `18` §4 (key in the operating system's credential store, no long-lived secret on disk). The owner moved both app keys out of the filesystem. Fetching the key with the key manager's CLI instead of the clipboard is a possible later improvement | Human owner, 2026-11-19, relayed by the coordinator |
| 2026-11-19 | The runbook presents CI review as the setup to port and the local review (reviewer app, `FACTORY_REVIEW_IN_CI`) as this repository's current, temporary state, by pointer | `AGENTS.md` on `main` (#6) is the record of the stopgap and of how it ends; a second copy would go stale | Coordinator's direction; planner |
| 2026-11-19 | The token script stays out of this plan's files: research §5 and the runbook describe the steps, with the exact helper string. Since the plan update of 2026-11-19 the runbook links the reviewed script of plan 0002 once that is on `main` (C7) | The owner's decision ("keep the script out"). Rejected: committing it in `research.md` (no code review, and later edits need no human); a pinned permalink | Human owner, relayed by the coordinator after plan review round 2 |
| 2026-11-19 | The script's long-term home is a reviewed, tier-high file, `.github/scripts/factory-token.mjs`, added by plan 0002. (This row was an open point until the owner's decision of 2026-11-19) | It handles the app's private key and belongs under code review and code ownership; that is a guard-path change, out of scope for a documentation plan | Human owner, relayed by the coordinator |
| 2026-11-19 | T2 asks for tier high | The classifier computes low for a docs file, and then nothing makes anyone compare the stated settings with the live ones. With the request, T2's pull request waits for the owner. A label may raise a tier (ADR-0029 §4); it makes this plan flagged for a second reason | Planner, on the plan reviewer's finding |
| 2026-11-19 | Pinned action SHAs are not repeated in the runbook | They change with every update; the workflow files are the record | Planner |
| 2026-11-19 | Verification is V1–V9, not the docs build | See deviations | Planner |
| 2026-11-19 | Coverage targets are content items `C1…C10`, not `R…`/`S…` ids | There is no spec to take ids from; the items are derived from the cited sections | Planner |
| 2026-11-19 | Ticket titles without a scope: `docs: …` | The file belongs to no package; `docs(docs)` adds nothing. #1 and #2 are unscoped too | Planner |
| 2026-11-19 | C8 lists the kit README's seven trials only and links `17` §7 | `17` §7 holds the full gate acceptance criteria, which are M0's exit criteria; repeating them here would make a second copy to keep in step | Planner, on the plan reviewer's finding |
| 2026-11-19 | Plan update: each app key is a file in a private per-user folder outside every repository, default `~/.wtf-corp/yaf-factory/` on Windows, Linux and macOS, readable by the user only. User-level environment variables `YAF_FACTORY_KEY_FILE` and `YAF_REVIEWER_KEY_FILE` hold the paths, never the key. This reverses the earlier row (key manager, standard input) and departs from the default of `18` §4: deviation 4 | Local operation must work without a prompt on three operating systems. Rejected: the key itself in an environment variable (inherited by every child process, leaks into dumps and logs); a passwordless operating-system credential store (different code per system, still readable silently on Windows and Linux); a key manager's CLI (not passwordless). The owner accepts the security responsibility: any process running as the user can read the file. The full record is in plan 0002's decision log | Human owner, 2026-11-19, relayed by the coordinator |

## Deviations from the specification

Four departures from the process and the specification. Each is for the human maintainer to accept or refuse by reviewing this plan's pull request. The first three concern how the document is specified, placed and verified and change nothing the runbook tells a maintainer to do. The fourth, added by the plan update of 2026-11-19, is security-relevant and changes section 7 of the runbook.

1. **No spec for a change that is not an initial-build milestone.** ADR-0023 and ADR-0025 put a merged `spec.md` before a plan, and allow a plan without one only for milestones M0–M15. This plan has none and cites ADR-0029 and the kit README instead. Why: the change is one maintainer document with no requirement of its own, and it is too large to be trivial.
2. **`docs/maintainers/` is outside the documentation site's layout.** `17` §5 lists the folders of the DocFX site (`articles/`, `api/`, `compliance/`, `reference/`); `maintainers/` is not one of them. The rules of `docs/AGENTS.md` are written for articles of that site, and this plan treats them as not applying to a maintainer document: in particular rule 2 (code is included from the sample or a compiled test, not pasted; the runbook has shell commands and JSON inline), rule 4 (types are linked with `xref`), rule 7 (a new article is added to `toc.yml`) and rule 8 (verify with the docs build). Rules 3, 5 and 6 on plain wording and on claims are kept. There is no `docfx.json` and no `toc.yml` until M14 in any case. Why: the runbook is for maintainers of the repository, not for users of the packages. The plan that creates the site excludes the folder or adds it to `toc.yml` on purpose.
3. **Verification is not the docs build.** ADR-0025 (Tickets table, rule 3) names the docs build as the verification of a pure documentation ticket. T1 and T2 use V1–V9 instead. Why: `dotnet docfx docs/docfx.json` cannot run before M14.
4. **The app keys are long-lived files on disk.** `18` §4, "Agent identity for local work", asks how a local session obtains an installation token "without a long-lived secret on disk" and gives as its default "a short-lived token minted from the app's key held in the operating system's credential store". Here each key is a file in a private per-user folder outside every repository, readable by the user only, with its path in an environment variable. The token is still short-lived and never written to disk; the key is not. Why: the owner's decision of 2026-11-19 (decision log), made so that local operation needs no prompt on Windows, Linux and macOS. What it costs: any process running as the user, agents included, can read the key; the file permissions keep other users out, not the user's own processes. The same deviation is declared in plan 0002, which adds the script that reads the files.

## Tickets

T2's tier is a request to raise: `classify.mjs` computes low for this path. `create-tickets.mjs` labels the issue `tier:high` but nothing carries the label to the pull request, so T2's row says it where the issue body repeats it: the orchestrator applies `tier:high` to the pull request when it opens it, and `factory/human-approval` then waits for the owner.

| Key | Title | Type | Area | Requirements | Depends on | Touches | Tier | Size | Failing test first | Verify |
|---|---|---|---|---|---|---|---|---|---|---|
| T1 | docs: add the factory setup runbook with the repository part | docs | docs | C1–C4 (ADR-0029 §1, §3, §4; kit README "Install at M0" 1–5, "Human setup checklist" intro) | — | `docs/maintainers/factory-setup.md` | low | 105 | V1 for T1 exits 1, because the runbook does not exist. Full verification with expected results: V1–V5 and V7–V9 under "Verification commands" in the plan | `test -f docs/maintainers/factory-setup.md && test "$(grep -c "^## [0-9]" docs/maintainers/factory-setup.md)" = 4` |
| T2 | docs: add the GitHub settings to the factory setup runbook | docs | docs | C5–C10 (ADR-0029 §4, §6, §7, §8; kit README "Install at M0" 7, "Human setup checklist" 1–7; `18` §4) | T1 | `docs/maintainers/factory-setup.md` | high (requested) | 170 | The pull request carries `tier:high`, applied when it is opened: the classifier computes low, and the owner's review is where the text is compared with the live settings. V1 for T2 exits 1, because the runbook has four sections, not ten; V6 fails, because there is no ruleset JSON. Full verification with expected results: V1–V9 under "Verification commands" in the plan | `test "$(grep -c "^## [0-9]" docs/maintainers/factory-setup.md)" = 10 && grep -q "factory/human-approval" docs/maintainers/factory-setup.md` |
