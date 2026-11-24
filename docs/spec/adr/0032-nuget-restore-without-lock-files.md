# ADR-0032 — NuGet restore without lock files

> **Status** Proposed
> **Date** 2026-11
> **Deciders** maintainers

## Context

`Directory.Packages.props` uses central package management (`ManagePackageVersionsCentrally`) with `CentralPackageTransitivePinningEnabled`: one version per package for every project, and a transitive package that has a `PackageVersion` entry is resolved at that version. The specification also asked for NuGet lock files: `RestorePackagesWithLockFile` in `Directory.Build.props`, a committed `packages.lock.json` per project, and `--locked-mode` on every restore in CI (`spec/01-repository-and-packages.md` §4, `spec/17-ci-release-docs.md` §2, §2.1, §4). ADR-0003 names lock files in its Enforcement table, and ADR-0020's workflow outline restores with `--locked-mode`.

What lock files gave: a locked restore failed when the resolved graph changed without a change to the declared dependencies (a better matching version published, an SDK with other implicit references), and the `contentHash` in the lock file made restore fail with `NU1403` when a package differed from the one restored before. What they did not give: YAF's projects under `src/` are libraries, and an app that installs them resolves its own graph; a library's lock file "has little say" over it (`specs/0007-no-package-lock-files/research.md` §1).

On 2026-11-24 the owner decided that lock files are no longer used, since `Directory.Packages.props` is in place, after the cost below was stated (`specs/0007-no-package-lock-files/plan.md`, decision log).

## Drivers

1. One place for package versions.
2. No files that change with every dependency update and that YAF's consumers never see.
3. Keep the dependency checks that do not need lock files.

## Options considered

1. **Lock files with `--locked-mode`**, as the specification had it. Detects silent changes of the transitive graph and checks content hashes. Not chosen: the owner decided against it, knowing the cost; every dependency change rewrites lock files that consumers never see.
2. **Lock files without locked mode.** Keeps the content-hash check. Not chosen: the files and their churn stay, and the owner asked for them to go.
3. **No lock files; central package management with transitive pinning** (chosen). Versions live in one file; a transitive package whose version matters is pinned there.

## Decision

1. No `packages.lock.json` is committed, `RestorePackagesWithLockFile` is not set, and no restore uses `--locked-mode` or `--use-lock-file`, anywhere in the repository. Property and files go together: NuGet keeps using a lock file that is present even without the property.
2. Package versions live only in `Directory.Packages.props`, with `ManagePackageVersionsCentrally` and `CentralPackageTransitivePinningEnabled` set.
3. A transitive package whose version matters (a security fix, a change of behaviour) gets a `PackageVersion` entry, which transitive pinning then applies.
4. This replaces "lock files" in ADR-0003's Enforcement row and `dotnet restore --locked-mode` in ADR-0020's workflow outline. Every other decision of ADR-0003 and ADR-0020 stands; neither record is edited.

## Consequences

- A dependency update changes one file, `Directory.Packages.props`.
- **Given up: the content-hash check.** A package that differs from the one restored before (another feed, a tampered global packages folder) is no longer stopped by `NU1403`.
- **Given up: detection of transitive changes.** A transitive package without a `PackageVersion` entry resolves by NuGet's rules. When a better matching version is published, when the SDK feature band changes the implicit references (`global.json` rolls forward to the latest feature band), or when package pruning changes, CI builds the new graph and nothing fails.
- Control C-SC-1 (`spec/11-privacy-and-compliance.md` §8) is weaker than the specification first promised; its mechanism now names only what remains.
- What remains: NuGet audit with `NuGetAuditMode` `all` on every restore, the daily `audit.yml` and the release audit (`spec/17-ci-release-docs.md` §2.1); dependency review on every pull request; one package source, nuget.org, with package source mapping `*` (`nuget.config`); signed-package verification on restore, on by default on Windows and on Linux, where CI runs, and off by default on macOS (research §3).
- Rollback: restore the property, regenerate lock files with `dotnet restore --use-lock-file`, put `--locked-mode` back in CI, in a new ADR and specification change.

## Enforcement

Nothing mechanical forbids a lock file or the property; this decision is kept by review only. Once the factory policy no longer excludes `**/packages.lock.json` (plan 0007, T3), `factory/size` counts a committed NuGet lock file as hand-written lines, so one that reappears shows in the size of its pull request. `Directory.Build.props`, where the property would go, is a guard file of the factory policy.

## Guidance for agents

- Never add `RestorePackagesWithLockFile`, `--use-lock-file` or `--locked-mode`, and never commit a `packages.lock.json`. If a restore writes one, find out why; do not commit it.
- Pin a transitive package whose version matters in `Directory.Packages.props` instead.
- Do not suppress or work around a NuGet audit or signature failure; stop and ask.
- When locked restore seems needed again, stop and ask: it needs a new ADR.

## Related

- ADR-0003 — dependency policy; its Enforcement row names lock files
- ADR-0020 — release automation; its outline restores with `--locked-mode`
- `spec/01-repository-and-packages.md` §4; `spec/11-privacy-and-compliance.md` §8, C-SC-1; `spec/17-ci-release-docs.md` §2, §2.1, §4; `spec/18-build-plan.md` §2
- `specs/0007-no-package-lock-files/plan.md` (decision log 2026-11-24), `specs/0007-no-package-lock-files/research.md`
