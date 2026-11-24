# 0007 — No NuGet lock files: research

What the plan relies on about NuGet, checked on 2026-11-24 against the documentation below and with a probe on .NET SDK 11.0.100 (the version `global.json` pins).

## 1. When NuGet uses a lock file

Source: Microsoft Learn, "NuGet PackageReference in project files", section "Locking dependencies" (page dated 2026-02-12, <https://learn.microsoft.com/en-us/nuget/consume-packages/package-references-in-project-files#locking-dependencies>).

- `RestorePackagesWithLockFile` set to `true` makes restore write `packages.lock.json` in the project folder.
- "Once a project has `packages.lock.json` file in its root directory, the lock file is always used with restore even if the property `RestorePackagesWithLockFile` is not set." **So the property and the files go in one change.** Removing only the property leaves every committed lock file in force; removing only the files lets the next restore write them again.
- With a lock file present, restore checks whether the declared dependencies changed. If not, it restores exactly the packages in the lock file and does not re-evaluate the graph. If they changed, it re-evaluates and rewrites the lock file.
- `--locked-mode` (`RestoreLockedMode`): "restore will either restore the exact packages as listed in the lock file or fail if you updated the defined package dependencies for the project after lock file was created."
- What a lock file guards against, in the page's words: a newly published version that matches the range better ("Day 2: Version 4.0.0 gets published. NuGet will now find the exact match"), a version removed from a feed (nuget.org does not allow deletion), floating versions, and SDK-added implicit references that change with the SDK version.
- "If your project is a library project that you do not ship or a common code project on which other projects depend, you **should not** check in the lock file … the lock file of a common code project has little say over the packages resolved for projects that depend on it." YAF's projects under `src/` are libraries: an app that installs `YAF.Core` resolves its own graph, whatever YAF's lock files say.

## 2. Content-hash check

Source: Microsoft Learn, "NuGet Error NU1403" (<https://learn.microsoft.com/en-us/nuget/reference/errors-and-warnings/nu1403>).

"The content of the package in the global packages folder is different from the value in the lock file. The package on the source may have changed, or multiple sources may have different instances of the same package id and version." The check needs the `contentHash` of a lock file; without lock files restore makes no such comparison.

## 3. What still checks packages on restore

Source: Microsoft Learn, "NuGet signed-package verification" (<https://learn.microsoft.com/en-us/dotnet/core/tools/nuget-signed-package-verification>).

- Windows: verification "is always enabled during package restore operations".
- Linux: "Starting with .NET 8 SDK, verification is enabled by default", opt out with `DOTNET_NUGET_SIGNATURE_VERIFICATION=false`. CI runs on `ubuntu-latest` with SDK 11.
- macOS: disabled by default, and the page recommends leaving it so.

nuget.org signs every package with its repository signature, so on Windows and Linux a package altered after nuget.org signed it fails restore. The signature does not show that a package is the same one restored last time; the lock file's hash did. `nuget.config` keeps one source, nuget.org, with package source mapping `*`, and NuGet audit (`NuGetAuditMode` `all`) still reports advisories of transitive packages. Neither depends on lock files.

## 4. Central package management and transitive pinning

Source: Microsoft Learn, "Central Package Management", section "Transitive Pinning" (<https://learn.microsoft.com/en-us/nuget/consume-packages/central-package-management#transitive-pinning>).

`CentralPackageTransitivePinningEnabled` "promotes a transitive dependency to a top-level dependency implicitly on your behalf when necessary": a transitive package **that has a `PackageVersion` entry** in `Directory.Packages.props` is resolved at that version (no downgrade, else `NU1109`). A transitive package without an entry is resolved by NuGet's normal rules (lowest applicable version, nearest wins). That is the part of the graph a lock file fixed and central package management does not.

## 5. Probe on SDK 11.0.100

A copy of `main` at `7de9a32` (`git archive`), outside the repository, Git Bash on Windows:

| Step | Result |
|---|---|
| `dotnet restore YAF.slnx --locked-mode`, tree unchanged | exit 0 |
| `RestorePackagesWithLockFile` removed from `Directory.Build.props`, `tests/Skeleton.Tests/packages.lock.json` deleted, `obj/` deleted, then `dotnet restore YAF.slnx --locked-mode` | exit 0; no lock file written |
| Same tree, `dotnet restore YAF.slnx`, `dotnet build YAF.slnx -c Release --no-restore`, `dotnet test --solution YAF.slnx -c Release --no-build` | exit 0 each; 0 warnings, 0 errors; 1 test passed; no `packages.lock.json` written |

So a restore with `--locked-mode` on a tree without lock files still succeeds: removing the flag from CI (T1) and removing the lock files (T2) can merge in either order. It also means that a stale `--locked-mode` in a verification command fails nothing; it only checks nothing, which is why T6 removes it from plan 0006.

## 6. GitHub's dependency graph

Source: GitHub Docs, "Dependency graph supported package ecosystems" (<https://docs.github.com/en/code-security/supply-chain-security/understanding-your-software-supply-chain/dependency-graph-supported-package-ecosystems>). The NuGet row lists `.csproj`, `.vbproj`, `.nuspec`, `.vcxproj`, `.fsproj` and `packages.config`, not `packages.lock.json`, and marks static transitive analysis as not supported. Dependency review (`17` §2.1) does not read the lock files today, so removing them changes nothing there.
