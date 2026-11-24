# 0007 — No NuGet lock files: plan

**Spec** none: this plan has no `spec.md`. Its basis is the owner's decision of 2026-11-24 (decision log), applied to `01` §4 (`docs/spec/spec/01-repository-and-packages.md`), `11` §8 control C-SC-1 (`docs/spec/spec/11-privacy-and-compliance.md`), `17` §2, §2.1, §4 (`docs/spec/spec/17-ci-release-docs.md`), `18` §2 (row M0), ADR-0003 (Enforcement) and ADR-0020 (GitHub Actions outline). The tickets amend those files; one of them adds ADR-0032 · **Status** Draft

## Approach

The owner decided on 2026-11-24 that NuGet lock files are no longer used: "since directory packages props are in place package lock files are not needed". Central package management stays as it is: `Directory.Packages.props` with `ManagePackageVersionsCentrally` and `CentralPackageTransitivePinningEnabled`, one version per package for every project, which is what the owner wanted for a core library whose packages ship together ("all the components will receive common versions anyway and this will help avoiding internal package mismatch"). What goes: the property `RestorePackagesWithLockFile` in `Directory.Build.props`, every committed `packages.lock.json`, and `--locked-mode` on restore. The trade-off is stated under "Risks and rollback" and in the ADR that T4 writes.

Lock files are wired in four places, and each gets one ticket, so that the tier-high parts stay small and the one the owner must push stands alone:

1. **CI (T1).** `.github/workflows/ci.yml`, job `build-test`, restores with `dotnet restore YAF.slnx --locked-mode`. It becomes `dotnet restore YAF.slnx`. The factory app has no Workflows permission, so **the owner pushes this branch** (`AGENTS.md`, "Unattended runs"), as for plan 0006 T25.
2. **Build (T2).** `Directory.Build.props` loses `RestorePackagesWithLockFile`, and every `packages.lock.json` tracked on `main` is deleted, in one change: NuGet keeps using a lock file that is present even without the property (research §1). On `main` today that is `tests/Skeleton.Tests/packages.lock.json`; open pull request #149 (plan 0006 T1, ticket #113) adds `src/Core/packages.lock.json` and `tests/Core.Tests/packages.lock.json` and deletes the Skeleton one. **T2 waits for #113 to close**, so that it removes whatever lock files #149 merged and #149 cannot bring them back afterwards. Its verify command checks that `git ls-files '*packages.lock.json'` is empty, whichever files that turns out to be.
3. **Factory policy (T3).** `policy.json` lists `**/packages.lock.json` in `size.exclude` and in `dependencyPatchFiles`. Both globs are removed, and `package-lock.json` (npm, for semantic-release, `17` §3) stays in both. Reason: the policy describes this repository, and the glob is orphaned by T2. A NuGet lock file that shows up again would silently switch locked restore back on for its project (research §1), so it should be counted by `factory/size` and not be excused as a dependency patch file. T3 runs after T2, so that the lock files T2 deletes are still left out of T2's size. The kit tests that use `src/Core/packages.lock.json` as their example of an excluded file switch to `package-lock.json`, and `size.test.mjs` gets a case showing that a NuGet lock file is counted.
4. **Specification and ADR (T4, T5).** `01` §4, `11` §8 C-SC-1, `17` §2, §2.1, §4 and `18` §2 (row M0) are amended with a dated note, the way #146 amended `03`. A new ADR-0032 records the decision (see "Why a new ADR" below). The skill `yaf-security` loses two checklist items that name lock files and `--locked-mode` (T5).

**Plan 0006 (T6).** Its V2, which every one of its tickets runs, starts with `dotnet restore YAF.slnx --locked-mode`, and the touches of T13 and T16 name `src/Core/packages.lock.json` and `tests/Core.Tests/packages.lock.json`. The planning skill (rule 12, "Deviations") prescribes a plan update pull request when a merged plan's ticket list changes. T6 is that pull request: it changes only `specs/0006-core/plan.md`, so it is a plan pull request, checked by the plan reviewer and passed by the plan gate (plan 0006 is flagged, so the owner approves it). T6 runs after T2. Details under "Plan 0006's open tickets".

**Why a new ADR, and not an edit of ADR-0003 and ADR-0020.** `adr/AGENTS.md` rule 3 asks for an ADR for "a change to a security or privacy control", and lock files are a listed mechanism of control C-SC-1 ("Dependency integrity and vulnerability gate"). `yaf-writing-adrs` rule 7 and ADR-0001 forbid rewriting an accepted ADR to say something different: ADR-0003's Enforcement row "NuGet audit, lock files, package source mapping (ADR-0020)" and the two `dotnet restore --locked-mode` comments in ADR-0020's outline cannot be edited. Superseding them is wrong too: their decisions (the dependency allow-list, automatic releases) do not change, and ADR-0031 shows that superseding means restating the whole record, which would copy two long ADRs to change one row and two comments. So ADR-0032, `Proposed`, records the one decision and says which mechanism of ADR-0003 and which steps of ADR-0020's outline it replaces. ADR-0003 and ADR-0020 stay as they are, status lines included: the status vocabulary has no "partly superseded". `adr/README.md` lists ADR-0032 with that note. The ADR goes in the same pull request as the specification change that needs it (`yaf-writing-adrs` rule 8), which is T4.

Files read to decide: `AGENTS.md` (with the path map), `docs/spec/agents/AGENTS.root.md`, `docs/spec/agents/AGENTS.nested.md` (`specs/`, `adr/`), `specs/AGENTS.md`, the skills `yaf-writing-plans` (with its template), `yaf-writing-adrs`, `yaf-commits-and-pull-requests`, `yaf-security`, `docs/spec/factory/.claude/agents/planner.md`, `01` §4, `11` §8, `17` §2, §2.1, §2.2, §3, §4, `18` §1, §2, ADR-0001, ADR-0003, ADR-0020, ADR-0029, ADR-0031, `docs/spec/adr/README.md`, `docs/spec/factory/README.md` ("Tiers"), `policy.json`, `classify.mjs`, `size.mjs`, `plan.mjs`, `ticket.mjs`, `gates.mjs`, `create-tickets.mjs`, `tests/classify.test.mjs`, `tests/size.test.mjs`, `.github/scripts/validate-instructions.mjs`, `.github/scripts/sync-references.mjs`, `specs/0004-token-economy/plan.md`, `specs/0005-repository-skeleton/plan.md`, `specs/0006-core/plan.md`, and on `main`: `Directory.Build.props`, `Directory.Packages.props`, `nuget.config`, `global.json`, `.github/workflows/ci.yml`, `tests/Skeleton.Tests/`. Open pull requests #148 and #149 (their file lists) and the issues of plan 0006.

A search for `packages.lock`, `locked-mode`, `RestorePackagesWithLockFile`, `lock file` and `lock-file` over the repository found these places, and nothing else:

| Place | What it says | Ticket |
|---|---|---|
| `.github/workflows/ci.yml` | `dotnet restore YAF.slnx --locked-mode` | T1 |
| `Directory.Build.props` | `RestorePackagesWithLockFile` | T2 |
| `tests/Skeleton.Tests/packages.lock.json` (and after #149 `src/Core/`, `tests/Core.Tests/`) | lock files | T2 |
| `policy.json` `size.exclude`, `dependencyPatchFiles`; `tests/size.test.mjs`, `tests/classify.test.mjs` | `**/packages.lock.json`, `src/Core/packages.lock.json` | T3 |
| `01` §4 | row `RestorePackagesWithLockFile`, "CI restores with `--locked-mode`" | T4 |
| `11` §8, C-SC-1 | mechanism "Lock files, audit as error, source mapping, dependency review" | T4 |
| `17` §2, §2.1, §4 | `--locked-mode` in `build-test`, `audit.yml` and the `release` job | T4 |
| `18` §2, row M0 | "CPM, lock files" | T4 |
| ADR-0003 Enforcement, ADR-0020 outline | lock files, `--locked-mode` | T4 (ADR-0032; the two stay unedited) |
| `yaf-security` | "lock files updated"; "`dotnet restore --locked-mode` succeeds" | T5 |
| `specs/0006-core/plan.md` | V2; touches of T13, T16; T13's text | T6 |
| `17` §2.2 (`factory/size`), ADR-0029 §5 (size), `yaf-writing-plans` (size), kit README ("Tiers") | "lock files" as a kind of excluded or patch file | none: `package-lock.json` still is one |
| ADR-0026, `yaf-implementing-tickets` | never delete a lock file to make a check pass | none: still true for `package-lock.json` |
| ADR-0020, `17` §3 | npm lock file for semantic-release | none: not NuGet |
| ADR-0028 | lock file of third-party skills | none: not NuGet |
| `specs/0005-repository-skeleton/plan.md`, `research.md`; plan 0006's T1 row and its decision log | what was planned and merged | none: the record of what was done |

### Alternatives rejected

| Alternative | Why not |
|---|---|
| Keep lock files and `--locked-mode` | The owner decided against it, knowing the cost (decision log) |
| Keep lock files, drop only `--locked-mode` | The content-hash check stays, but every dependency change rewrites lock files that YAF's consumers never see (research §1: a library's lock file "has little say"), and the owner asked for the files to go |
| Remove the property only, or the files only | NuGet keeps using a present lock file without the property, and writes new ones with the property (research §1) |
| Do it inside #149 (plan 0006 T1) | The owner asked for a plan of its own; #149's ticket is about the core package |
| Edit ADR-0003's Enforcement row and ADR-0020's outline in place | `yaf-writing-adrs` rule 7, ADR-0001: an accepted ADR is changed only in its status line |
| Supersede ADR-0003 and ADR-0020 with revised copies | Their decisions stand; two long records copied for one row and two comments |
| Keep `**/packages.lock.json` in the policy, harmless since no such file exists | A file that would silently switch locked restore back on would then be left out of the size and excused as a patch file; the glob is orphaned by T2 |
| A check that fails when a `packages.lock.json` is committed | A new guard for a file NuGet no longer writes; review and the size count (T3) show it. The ADR says that only review enforces it |
| T1 and T2 as one ticket | The owner has to push a branch that changes `.github/workflows/`; that part stays separate and small |
| A note on issues #125 and #128 instead of a plan update | `create-tickets.mjs` never edits an existing issue, but V2 is read from the plan, and the skill asks for a plan update when the ticket list changes |

## Affected packages

| Layer | Package / area | Change |
|---|---|---|
| Core … Analyzers | none | none |
| Testing (`Testing`, `Testing.Integration`) | none | none. `tests/Skeleton.Tests` (or `tests/Core.Tests` after #149) loses its lock file (T2) |
| Sample | none | none (no sample yet) |
| Templates | none | none (no templates yet) |
| Docs | `docs/spec/` (specification, ADRs, a skill) | `01`, `11`, `17`, `18` amended; ADR-0032 and its `README.md` row (T4); `yaf-security` (T5) |
| Build and CI (not a layer) | `Directory.Build.props`, lock files, `.github/workflows/ci.yml`, `docs/spec/factory/.github/factory/policy.json` and two kit tests | T1, T2, T3 |
| Plans (not a layer) | `specs/0006-core/plan.md` | V2, touches of T13 and T16, T13's text, a decision-log row (T6) |

## Public API, schema and error-code delta

None. No public type, configuration key, error code, contract or telemetry name changes; no migration. Restoring without a lock file does not change what is built or packed: the same `Directory.Packages.props` versions resolve.

Breaking: no.
New dependency: no.

```factory:delta
{ "publicApi": [], "schema": [], "errorCodes": [], "contracts": [], "configuration": [], "telemetry": [], "dependencies": [] }
```

## Schema and migration plan

None.

## What T4 writes

**ADR-0032**, `docs/spec/adr/0032-nuget-restore-without-lock-files.md`, status `Proposed`, the eight sections of the template, about one page:

- **Context.** `Directory.Packages.props` with central package management and transitive pinning pins one version per package for every project. YAF's packages are libraries; an app that installs them resolves its own graph, and YAF's lock files never reach it (research §1). What lock files gave: `--locked-mode` failed a restore whose graph changed without a change to the declared dependencies, and the `contentHash` check (`NU1403`) failed a package that differed from the one restored before. The owner's decision and its date, citing this plan.
- **Drivers.** One place for versions; no files that change with every dependency update and that consumers never see; keep the checks that do not need lock files.
- **Options.** (1) lock files with `--locked-mode`, as `01` §4 had it; (2) lock files without locked mode; (3) **no lock files, central package management with transitive pinning** (chosen). Each with its reason, as in "Alternatives rejected".
- **Decision.** No `packages.lock.json`, no `RestorePackagesWithLockFile`, no `--locked-mode`, anywhere in the repository. Package versions live only in `Directory.Packages.props`. A transitive package whose version matters (a security fix, a behaviour change) gets a `PackageVersion` entry, which transitive pinning then applies. It replaces "lock files" in ADR-0003's Enforcement row and `dotnet restore --locked-mode` in ADR-0020's outline; the other decisions of both records stand.
- **Consequences.** Simpler dependency updates, one file per change. Costs, stated as such: no content-hash check of restored packages; a change in the resolved transitive graph (a lower matching version published, an SDK feature band with other implicit references, package pruning) passes CI silently. What remains: NuGet audit with `NuGetAuditMode` `all`, the daily audit, dependency review, one source with package source mapping, and signed-package verification on Windows and Linux (research §3).
- **Enforcement.** Nothing mechanical forbids a lock file: review only, and `factory/size` counts one (T3). Said plainly.
- **Guidance for agents.** Never add `RestorePackagesWithLockFile`, `--use-lock-file` or `--locked-mode`; never commit a `packages.lock.json`; pin a transitive package in `Directory.Packages.props` instead.
- **Related.** ADR-0003, ADR-0020, `01` §4, `11` C-SC-1, `17` §2, this plan.

`docs/spec/adr/README.md`, table "Engineering": a row for 0032, "Restore NuGet packages without lock files; replaces the lock-file mechanism named in 0003 and 0020 (Proposed)".

**Specification**, each place with a note `> **Amended <date>**, by the owner's decision of 2026-11-24 (`specs/0007-no-package-lock-files/plan.md`, decision log; ADR-0032): …`, as #146 did:

- `01` §4: the row `RestorePackagesWithLockFile` is removed. Below the table: no NuGet lock files; versions are pinned in `Directory.Packages.props` (central package management, transitive pinning).
- `11`, control C-SC-1: mechanism "Pinned versions in `Directory.Packages.props`, audit as error, source mapping, dependency review". The control's name stays.
- `17` §2: `dotnet restore YAF.slnx` in `build-test`; §2.1: `dotnet restore -p:AuditPipeline=true` in the row of `audit.yml`; §4: `dotnet restore -p:AuditPipeline=true -p:NuGetAuditLevel=high` in the `release` job.
- `18` §2, row M0: "CPM" without "lock files".

The notes say in words what was removed ("the locked restore", "the lock-file property"), so that no `--locked-mode` and no `RestorePackagesWithLockFile` is left in `docs/spec/spec/`.

## Plan 0006's open tickets

- **#113** (0006 T1, pull request #149) is not changed and is not held: it merges with its lock files, and T2 removes them. T2 is labelled `blocked` with a comment naming #113 until #113 has closed; `Depends on` cannot name a ticket of another plan.
- **#125** (0006 T13) and **#128** (0006 T16) name lock files in their touches, and T13's text asks for the lock files to be restored again and committed. T6 removes both. The orchestrator labels #125 and #128 `blocked` with a comment naming T6's issue, and removes the label when T6 has merged. The issue bodies keep the stale paths (`create-tickets.mjs` never edits an existing issue); that is harmless, since V1 checks that a diff stays within the touches, not that every listed path changes.
- **#137** (0006 T25) also changes `.github/workflows/ci.yml`: it and T1 run one after the other, never at the same time. Both are pushed by the owner.
- **#135** (0006 T23, pull request #148) adds ADR-0030 and changes `docs/spec/adr/README.md`. T4 is labelled `blocked` until #135 has closed, and takes the number 0032 (0030 is #148's, 0031 is on `main`).
- Every other ticket of plan 0006 runs V2. Until T6 merges, V2 still carries `--locked-mode`, which passes on a tree without lock files and checks nothing (research §5).

What T6 changes in `specs/0006-core/plan.md`: V2 becomes `dotnet restore YAF.slnx && dotnet build YAF.slnx -c Release --no-restore && dotnet test --solution YAF.slnx -c Release --no-build`; the touches of T13 lose `src/Core/packages.lock.json` and `tests/Core.Tests/packages.lock.json`, those of T16 lose `src/Core/packages.lock.json`; in "What the tickets contain", T13 loses "the lock files of `src/Core` and `tests/Core.Tests` are restored again and committed with any change" and T1 keeps its sentence about lock files (it records what #149 merged); a decision-log row dated the day of the change cites this plan. The T1 row, the earlier decision-log rows and the delta do not change.

## Test strategy

There are no requirements `R…`; what must be true after the plan:

| What must be true | Ticket | Level | Check |
|---|---|---|---|
| CI restores without `--locked-mode`, and `build-test` passes | T1 | CI | T1's verify; the `build-test` run on T1's pull request |
| No `packages.lock.json` is tracked; `RestorePackagesWithLockFile` is gone; a restore writes no lock file; build and tests pass | T2 | build | T2's verify; V2 |
| The policy no longer excludes or excuses a NuGet lock file; `package-lock.json` still is; the kit tests pass | T3 | unit | `size.test.mjs` "a NuGet lock file is counted" (new, fails first); the whole kit test run |
| ADR-0032 exists as `Proposed` and is listed; the specification names no lock file and no `--locked-mode`; ADR-0003 and ADR-0020 are unchanged | T4 | documentation check | T4's verify greps; the doc reviewer reads it against "What T4 writes" |
| The security skill names neither | T5 | documentation check | T5's verify greps; `sync-references.mjs --check` |
| Plan 0006 is verified without lock files; it still parses | T6 | documentation check | T6's verify; `validate-instructions.mjs` |

## Risks and rollback

| Risk | Likelihood | Mitigation | Rollback |
|---|---|---|---|
| **The resolved transitive graph changes and nothing fails.** A transitive package without a `PackageVersion` entry resolves by NuGet's rules; it moves when a lower matching version is published, when the SDK feature band changes the implicit references (`global.json` has `rollForward` `latestFeature`), or when package pruning changes. Before, `--locked-mode` failed CI in each case; now the build uses the new graph | Low on nuget.org (versions cannot be deleted); medium for SDK changes | Transitive pinning applies to every package listed in `Directory.Packages.props`; ADR-0032 tells agents to list a transitive package whose version matters; NuGet audit (`all`) reports advisories in transitive packages; dependency review on every pull request. Accepted by the owner | Revert T1 and T2 (restore lock files with `dotnet restore --use-lock-file`, set the property, `--locked-mode` in CI), and a new ADR and specification change |
| **No content-hash check of restored packages.** A package that differs from the one restored before (another feed, a tampered global packages folder) is no longer stopped by `NU1403` | Low | One source, nuget.org, with source mapping `*` (`nuget.config`); signed-package verification on Windows and on Linux, where CI runs (research §3). Not covered: macOS, where verification is off by default. Accepted by the owner | As above |
| Control C-SC-1 is weaker than the specification promised | Certain | T4 rewrites the control's mechanism to what remains, with a dated note; no wording claims more | As above |
| A `packages.lock.json` is committed again and silently switches locked restore back on for its project | Low | NuGet writes none without the property; T3 makes it count in `factory/size` and not pass as a patch file; review. ADR-0032 says that only review enforces it | Delete the file |
| #149 merges lock files after T2 | Low | T2 is held until #113 closes | Run T2's change again |
| Plan 0006 tickets are verified against lock files that no longer exist | Medium | T6; #125 and #128 held until it merges; V2 with `--locked-mode` passes without lock files meanwhile (research §5) | — |
| Conflicts with #148 (`docs/spec/adr/README.md`) and #137 (`ci.yml`) | Medium | T4 held until #135 closes; T1 and #137 one after the other | — |

## Verification commands

Git Bash, repository root, on the ticket's branch; `BASE` is `origin/main`.

V1, every ticket: `git diff --name-only $BASE...HEAD` lists only the ticket's touches; `node docs/spec/factory/.github/factory/size.mjs $BASE HEAD` stays at the estimate or below and within 400; `node .github/scripts/sync-references.mjs --check` and `node .github/scripts/validate-instructions.mjs` exit 0. A ticket that changes a skill or role definition runs `node .github/scripts/sync-references.mjs` first and commits what it regenerates (T5 changes no front matter, so nothing is expected).

V2, T2 and T3, expected 0 warnings, 0 errors, all tests passing, and no lock file written:

    dotnet restore YAF.slnx && dotnet build YAF.slnx -c Release --no-restore && dotnet test --solution YAF.slnx -c Release --no-build
    test -z "$(git status --porcelain -- '*packages.lock.json')" && test -z "$(git ls-files '*packages.lock.json')"

V3, the kit tests, T3: `node --test "docs/spec/factory/.github/factory/tests/*.test.mjs"` and `node --test ".github/scripts/tests/*.test.mjs"` → 0 failed.

The whole plan, on `main` after the last ticket: V2; V3; `grep -rn -e "--locked-mode" -e "RestorePackagesWithLockFile" -e "packages\.lock\.json" --exclude-dir=.git --exclude-dir=.claude . | grep -v -e "^./specs/000[5-7]" -e "^./docs/spec/factory/.github/factory/tests/size.test.mjs" -e "^./docs/spec/adr/0003-" -e "^./docs/spec/adr/0020-" -e "^./docs/spec/adr/0032-"` prints nothing (the remaining hits are the record in plans 0005 to 0007, T3's test case, the two unedited ADRs and the ADR that replaces them); the latest `ci.yml` run on `main` is green.

## Research

[`research.md`](./research.md): when NuGet uses a lock file (presence opts in), what locked mode and the content hash checked, signed-package verification per platform, what transitive pinning pins, the probe on SDK 11.0.100, and GitHub's dependency graph for NuGet.

## Decision log

| Date | Decision | Reason | By |
|---|---|---|---|
| 2026-11-24 | **Stop using NuGet lock files.** The owner first asked: "with the core library more components may be added, please maybe change the package lock into directory packages props. if this core lib is to be delivered via nuget, all the components will receive common versions anyway and this will help avoiding internal package mismatch", then decided: "since directory packages props are in place package lock files are not needed". The orchestrator pointed out what is given up: the content-hash verification of restored packages and `--locked-mode` detection of silent changes in the transitive graph. The owner confirmed that the lock files are removed, and that this is done as a plan of its own, not inside the open #149. Central package management with `CentralPackageTransitivePinningEnabled` stays | Owner's decision in an attended session | Human owner, relayed by the orchestrator, 2026-11-24 |
| 2026-11-24 | Folder `0007-no-package-lock-files` | Next free number | Planner |
| 2026-11-24 | A new ADR-0032; ADR-0003 and ADR-0020 are neither edited nor superseded | Approach, "Why a new ADR" | Planner |
| 2026-11-24 | The ADR and the specification change are one ticket (T4); the skill is another (T5) | `yaf-writing-adrs` rule 8 puts the ADR with the change that needs it; T4 stays a specification pull request, read by the doc reviewer, while a skill change makes a code pull request | Planner |
| 2026-11-24 | `**/packages.lock.json` leaves `size.exclude` and `dependencyPatchFiles`; `package-lock.json` stays | Approach, item 3 | Planner |
| 2026-11-24 | T3 after T2 | The lock files T2 deletes are not counted in T2's size | Planner |
| 2026-11-24 | T1 and T2 do not depend on each other | A restore with `--locked-mode` passes on a tree without lock files (research §5) | Planner |
| 2026-11-24 | Plan 0006 is brought in step by a plan update pull request (T6) after T2; #125 and #128 wait for it; plan 0006's T1 row and earlier decision-log rows stay | Skill rule 12 and "Deviations"; a merged ticket's row is the record of what it did | Planner |
| 2026-11-24 | The generic "lock files" wording of `17` §2.2, ADR-0026, ADR-0029, the kit README and the skills `yaf-writing-plans` and `yaf-implementing-tickets` stays | `package-lock.json` is still a lock file, excluded from the size and never deleted to pass a check | Planner |
| 2026-11-24 | `18` §2's row M0 is amended | `18` is the recipe for rebuilding YAF; left as it is, a rebuild would add lock files in M0 | Planner |
| 2026-11-24 | Plans 0005 and 0006's history are not edited | They record what was planned and merged | Planner |

## Deviations from the specification

Two departures, for the owner to accept or refuse by reviewing this plan's pull request.

1. **No `spec.md`** for a change that is not an initial-build milestone (ADR-0023, ADR-0025). The change removes one build mechanism by the owner's decision and adds no behaviour to YAF; plans 0001 to 0004 took the same route.
2. **The specification's locked restore is dropped.** `01` §4 (`RestorePackagesWithLockFile`, CI restores with `--locked-mode`), `11` §8 C-SC-1 ("Lock files"), `17` §2, §2.1, §4 (`--locked-mode` in `build-test`, `audit.yml` and the `release` job), `18` §2 (M0: "lock files"), ADR-0003's Enforcement row and ADR-0020's outline. This weakens a supply-chain control on purpose, as decided by the owner on 2026-11-24 with the cost stated (decision log, "Risks and rollback"). T4 amends the specification and records the decision in ADR-0032, which a code owner accepts by approving T4's pull request.

## Tickets

T1, T2 and T4 can start at once, each subject to its hold: T2 waits for #113 (pull request #149), T4 for #135 (pull request #148), and T1 does not run alongside plan 0006's #137 (same file). T3 and T6 follow T2; T5 follows T4. Every ticket is tier high (guard files, specification, sensitive paths) except T6, a plan pull request whose plan gate asks the owner because plan 0006 is flagged. **T1 changes `.github/workflows/`: the owner pushes its branch.** T6 is written by a planner, not an implementer, since it edits a plan.

| Key | Title | Type | Area | Requirements | Depends on | Touches | Tier | Size | Failing test first | Verify |
|---|---|---|---|---|---|---|---|---|---|---|
| T1 | ci: restore packages without locked mode in the build job | chore | ci | Decision log 2026-11-24; `17` §2 (`build-test`) as amended by T4 | — | `.github/workflows/ci.yml` | high | 1 | `grep -q -- "--locked-mode" .github/workflows/ci.yml` exits 0: the build job still restores in locked mode. **The owner pushes this branch.** Not at the same time as #137 | `! grep -q -- "--locked-mode" .github/workflows/ci.yml && grep -q "run: dotnet restore YAF.slnx *$" .github/workflows/ci.yml` exits 0; job `build-test` passes on the pull request; V1 |
| T2 | build: stop using NuGet lock files | chore | build | Decision log 2026-11-24; research §1 (property and files together) | — | `Directory.Build.props`, `src/Core/packages.lock.json` (removed), `tests/Core.Tests/packages.lock.json` (removed), `tests/Skeleton.Tests/packages.lock.json` (removed, if #149 left it) | high | 1 | `test -z "$(git ls-files '*packages.lock.json')"` exits 1, and `grep -c RestorePackagesWithLockFile Directory.Build.props` prints 1. Blocked until #113 has closed. Every `packages.lock.json` tracked on `main` at that time is removed, whichever the touches name | `test -z "$(git ls-files '*packages.lock.json')" && ! grep -q RestorePackagesWithLockFile Directory.Build.props` exits 0; V2 → 0 warnings, all tests passed, no lock file written; V1 |
| T3 | ci: drop the NuGet lock file globs from the factory policy | chore | factory | Decision log 2026-11-24; Approach, item 3 | T2 | `docs/spec/factory/.github/factory/policy.json`, `docs/spec/factory/.github/factory/tests/size.test.mjs`, `docs/spec/factory/.github/factory/tests/classify.test.mjs` | high | 15 | `size.test.mjs` "a NuGet lock file is counted" (`900\t0\tsrc/Core/packages.lock.json` gives `lines` 900 and nothing excluded) fails: the glob excludes it | `node -e "const p=require('./docs/spec/factory/.github/factory/policy.json');const g=[...p.size.exclude,...p.dependencyPatchFiles];process.exit(g.filter(x=>x.includes('packages.lock')).length===0&&g.filter(x=>x==='package-lock.json').length===2?0:1)"` exits 0; V3 → 0 failed; V1 |
| T4 | docs(specs): drop NuGet lock files from the specification | docs | specs | Decision log 2026-11-24; "What T4 writes"; `adr/AGENTS.md` rule 3; `yaf-writing-adrs` rules 7, 8, 9 | — | `docs/spec/adr/0032-nuget-restore-without-lock-files.md`, `docs/spec/adr/README.md`, `docs/spec/spec/01-repository-and-packages.md`, `docs/spec/spec/11-privacy-and-compliance.md`, `docs/spec/spec/17-ci-release-docs.md`, `docs/spec/spec/18-build-plan.md` | high | 120 | `test -f docs/spec/adr/0032-nuget-restore-without-lock-files.md` exits 1, and `grep -rl -e "--locked-mode" -e "RestorePackagesWithLockFile" docs/spec/spec` lists `01` and `17`. Blocked until #135 has closed | `f=docs/spec/adr/0032-nuget-restore-without-lock-files.md; d=docs/spec/spec; grep -q "^# ADR-0032" $f && grep -q "Proposed" $f && grep -q "^## Enforcement" $f && grep -q "0032-nuget-restore-without-lock-files.md" docs/spec/adr/README.md && ! grep -rq -e "--locked-mode" -e "RestorePackagesWithLockFile" $d && ! grep -q "CPM, lock files" $d/18-build-plan.md && ! grep -q "Lock files, audit" $d/11-privacy-and-compliance.md && test -z "$(grep -L 0007-no-package-lock-files $d/01-repository-and-packages.md $d/11-privacy-and-compliance.md $d/17-ci-release-docs.md $d/18-build-plan.md)" && git diff --quiet $BASE -- docs/spec/adr/0003-technology-stack-and-dependency-policy.md docs/spec/adr/0020-versioning-and-release-automation.md` exits 0; V1 |
| T5 | docs(agents): drop the locked restore from the security skill | docs | agents | Decision log 2026-11-24; ADR-0032 | T4 | `docs/spec/agents/skills/yaf-security/SKILL.md` | high | 4 | `grep -c -e "--locked-mode" -e "lock files updated" docs/spec/agents/skills/yaf-security/SKILL.md` prints 2 | `! grep -q -e "--locked-mode" -e "lock files" docs/spec/agents/skills/yaf-security/SKILL.md` exits 0; `node .github/scripts/sync-references.mjs --check` exits 0; V1 |
| T6 | docs(specs): verify plan 0006 without NuGet lock files | docs | specs | Decision log 2026-11-24; "Plan 0006's open tickets"; `yaf-writing-plans` rule 12 | T2 | `specs/0006-core/plan.md` | low | 12 | `grep -c -e "--locked-mode" specs/0006-core/plan.md` prints 1, and the T13 and T16 rows name `packages.lock.json` | `p=specs/0006-core/plan.md; ! grep -q -e "--locked-mode" $p && ! grep -qE "^. T1[36] .*packages\.lock\.json" $p && ! grep -q "are restored again and committed" $p && grep -q "0007-no-package-lock-files" $p` exits 0; `node .github/scripts/validate-instructions.mjs` exits 0 (plan 0006 still parses); V1 |
