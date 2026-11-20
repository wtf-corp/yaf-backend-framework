# 0005 — Repository skeleton (M0): research

What the plan relies on that is not in the specification, how it was checked, and when. Everything was checked on 2026-11-20 on the owner's Windows machine. Nothing here is a secret. Probes ran in a scratch folder outside the repository, and nothing from them was committed.

## 1. Toolchain

| Fact | How checked | Result |
|---|---|---|
| SDKs installed on this machine | `dotnet --list-sdks` | `10.0.401`, `11.0.100` |
| Active SDK | `dotnet --info` | `11.0.100`, commit `297acf930f`, MSBuild `18.12.2-1.26529.118`. The installed workloads (MAUI and others) carry preview 6 manifests from Visual Studio. The build does not use them |
| .NET 11 is released, and which SDK goes with it | Microsoft's release metadata, `https://builds.dotnet.microsoft.com/dotnet/release-metadata/11.0/releases.json` | `latest-release` `11.0.0` of 2026-11-10, `latest-sdk` `11.0.100`, `release-type` `sts`, `support-phase` `active`. For SDK `11.0.100` the `csharp-version` is `15.0` |
| NuGet in that SDK | `dotnet nuget --version` | `8.0.0-rc.53118+297acf930…` (the SDK's own copy) |
| What the CI runner can get | `actions/setup-dotnet` with `global-json-file`, already used by job `test-evidence` in `.github/workflows/factory-gates.yml` (pinned at v4.3.1), installs from the same release metadata. Latest release of the action: v6.1.0 of 2026-11-03 (GitHub API) | **Not run on a runner yet.** The first run of `ci.yml` (T6) is the proof. If v4.3.1 cannot install `11.0.100`, T6 pins a newer release by SHA |

`global.json` as planned: `"sdk": { "version": "11.0.100", "rollForward": "latestFeature" }` and `"test": { "runner": "Microsoft.Testing.Platform" }` (`01` §1).

## 2. Probe: a solution with one test, on SDK 11.0.100

Root `Directory.Build.props` with the settings of `01` §4 (`net11.0`, `LangVersion` `latest`, `Nullable` and `ImplicitUsings`, `TreatWarningsAsErrors`, `AnalysisLevel` `latest`, `AnalysisMode` `All`, `EnforceCodeStyleInBuild`, `Deterministic`, `RestorePackagesWithLockFile`, `NuGetAudit*`, `Version` `0.0.0`). `Directory.Packages.props` with `ManagePackageVersionsCentrally` and `CentralPackageTransitivePinningEnabled`. A solution made by `dotnet new sln` (the result is `YAF.slnx`) and one xUnit v3 test project (`OutputType` `Exe`).

| Step | Result |
|---|---|
| `dotnet build YAF.slnx -c Release` | 0 warnings, 0 errors, once the item in the next row was settled |
| A test named `Build_Placeholder_Passes` | **Fails the build with `CA1707`** ("Remove the underscores from member name"). `AnalysisMode` `All` turns the rule on, and the naming convention `Method_Scenario_Expected` (`AGENTS.md`, `tests/AGENTS.md` rule 2) breaks it. Settled by `dotnet_diagnostic.CA1707.severity = none` in an `.editorconfig` section for `tests/`. That entry is the first in the "short documented suppression list" of `01` §4 |
| xUnit v3 needs `using Xunit;` | Without it `Fact` is not found. A global `<Using Include="Xunit" />` in the test project works too |
| `dotnet test --solution YAF.slnx -c Release --no-build` | `total: 1, succeeded: 1`, exit 0 |
| `dotnet test <path to .csproj> -c Release` (the form `test-evidence.sh` uses) | Passes, exit 0: the MTP runner accepts the project path as a plain argument |
| `dotnet restore YAF.slnx --locked-mode` after the first restore | exit 0. The lock file is `tests/<project>/packages.lock.json` |
| A `nuget.config` with one `nuget.org` source, `packageSourceMapping` `*` → `nuget.org`, `auditSources` → `nuget.org` | Restore exits 0 with no warning |
| An empty solution (`<Solution />`), no project | `dotnet build`: 1 warning, "Unable to find a project to restore!", so the build is not warning-free. `dotnet test --solution`: "The solution configuration '\|' is invalid", exit 1. **So the solution and the placeholder test project come in one ticket (T2)** |

## 3. Probe: library build settings and the banned list

`src/Directory.Build.props` imports the root file and adds `GenerateDocumentationFile`, `CS1591` and `CS1573` in `WarningsAsErrors`, `IsPackable`, `EnablePackageValidation`, `IncludeSymbols` and `snupkg`, `Microsoft.CodeAnalysis.BannedApiAnalyzers` and `Microsoft.CodeAnalysis.PublicApiAnalyzers` (both `PrivateAssets="all"`), and `BannedSymbols.txt` as `AdditionalFiles`. A probe project under `src/` with a public class that returns `DateTime.UtcNow`:

| Diagnostic | Seen |
|---|---|
| `CS1591` missing XML comment | yes, as error |
| `RS0016` symbol not in the declared public API | yes, as error. The analyzer picks up `PublicAPI.Shipped.txt` and `PublicAPI.Unshipped.txt` in the project folder by itself |
| `RS0030` banned symbol (`DateTime.UtcNow`; `Environment.GetEnvironmentVariable(string)` in a second run) | yes, as error |

With the public API declared and the docs written, an entry for a type the project does not reference (`M:Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.IgnoreQueryFilters``1(System.Linq.IQueryable{``0})`) and one written without a parameter list (`M:Microsoft.Extensions.Logging.LoggerExtensions.LogInformation`) caused no diagnostic. So entries for packages that come later can be in the list from M0 on. **Not checked:** whether an entry without a parameter list bans every overload once `Microsoft.Extensions.Logging.Abstractions` is referenced. T4 checks it with a probe that references the package. If it does not, T4 lists the overloads.

Some items of the list in `01` §4 cannot be written as a symbol. A symbol ban removes a member everywhere, so it cannot depend on how an argument was built or what it is for:

| Item | Enforced instead by | Checked |
|---|---|---|
| `Random` for security purposes | `CA5394` (insecure randomness), on under `AnalysisMode` `All` | Rule id from memory. T4 confirms it with the probe |
| `Aes` in ECB mode | `CA5358` (unsafe cipher modes) | Same |
| Raw SQL built by interpolation into `FromSqlRaw` / `ExecuteSqlRaw` | EF Core's own analyzer for interpolated raw SQL. It arrives with EF Core in M3 | **Not checked.** M3's plan verifies the rule id against the EF Core version it pins |
| `IgnoreQueryFilters` **outside** `YAF.Persistence.EfCore` | The entry is in the list. M3 exempts its own package (a separate list, or no `AdditionalFiles` entry for that project) | For M3 |

## 4. Package versions on the feed

Read from `https://api.nuget.org/v3-flatcontainer/<id>/index.json` on 2026-11-20. These are the versions found that day, not pins. The implementer takes the current stable version at implementation time (`18` §1 item 8) and records it in `Directory.Packages.props`.

| Package | Latest stable | Licence | Used by |
|---|---|---|---|
| `xunit.v3` | 4.0.3 (depends on `xunit.v3.mtp-v2` 4.0.3, which means Microsoft.Testing.Platform v2) | Apache-2.0 | T2. Probed: builds and runs on SDK 11.0.100 |
| `Microsoft.CodeAnalysis.BannedApiAnalyzers` | 5.6.0 | MIT | T4. Probed |
| `Microsoft.CodeAnalysis.PublicApiAnalyzers` | 5.6.0 (a 5.11.0 prerelease exists) | MIT | T4. Probed |
| `Microsoft.Sbom.Targets` | 4.1.13 | MIT | Not M0: SBOMs are M14 (`18` §2) |

## 5. Secret scan

- `gitleaks/gitleaks-action` needs a licence key in `GITLEAKS_LICENSE` for a repository that an organisation owns. The key is free, but it is one more secret, and pull requests from forks get no secrets (`17` §2 rules). Source: the action's README, `https://github.com/gitleaks/gitleaks-action`, read 2026-11-20 (it mentions `@v3`).
- The gitleaks CLI is MIT and needs no key. Latest release `v8.30.1` of 2026-03-21 (GitHub API). It ships a checksums file with each release.
- The plan's recommendation (open question Q5): download the CLI release pinned by version, check its SHA-256 against a value written in the workflow, and run it. Whether a scan of the full history finds false positives in the current tree is **not checked** here: gitleaks is not installed on this machine. T6's first run shows it.

## 6. Merge queue and strict status checks

- The repository is public and owned by an organisation (`gh api repos/<org>/<repo>`: `visibility` `public`, `allow_auto_merge` `true`).
- GitHub's merge queue page says workflows that report required checks must also run on `merge_group`, or the required check is never reported and the merge fails. Source: `https://docs.github.com/en/repositories/configuring-branches-and-merges-in-your-repository/configuring-pull-request-merges/managing-a-merge-queue`, read 2026-11-20. The page did not say which plans have the queue. **Not checked** for this repository.
- The `main` ruleset (`rules/branches/main`, read 2026-11-20 with the factory app's token) has the rule types `deletion`, `non_fast_forward`, `pull_request`, `required_status_checks`. The ruleset's required checks and its `integration_id` pins are as runbook section 5 records them: `factory/review` and `factory/doc-review` are pinned to the reviewer app.
- Consequence for a queue: `factory-gates.yml` answers `merge_group` by reporting the factory checks as passed **from GitHub Actions**. The two review checks are pinned to the reviewer app while the reviews run locally, so on a queue commit nobody would report them, and the queue would wait forever. Inferred from the pins and the workflow. Not tried.

## 7. What plans 0001–0004 left for this plan

| Open point | Where | How this plan settles it |
|---|---|---|
| `dotnet build` and `dotnet test` on the allow list | Plan 0003, decision log ("Open point … for M0's plan") | T1, with the owner's yes (Q3) |
| Strict policy or merge queue, and who updates a branch that is behind | Plan 0003, decision log and risks | Q4. The text follows in T11 |
| A failed build as a fix round | Plan 0003, decision log | No change in M0: a failed `ci.yml` job is `gate-failed` and goes to the owner, as the skill says for any failed gate (decision log) |
| `ci.yml` and its checks in the ruleset | Runbook section 10 | T6, then owner step O1, then T11 updates the runbook |
| Trials 2 to 7 | Runbook sections 8 and 10 | Owner step O2, and Q6 for trial 2 |

## 8. Pull request history used as evidence

`gh pr list --state all` and `gh pr view <n> --json reviews` with the factory app's token, 2026-11-20:

- #1 the specification, licence and readme; #2 the kit installed by reference (both merged by the owner).
- #29, a tier-low docs ticket that merged by itself as `app/yaf-factory`: trial 1.
- About thirty tier-high pull requests (#23–#28, #30, #47–#66, #71–#79) merged. Their reviews were checked for #73 only: `APPROVED` by the owner, then merged by the automatic merge. Every one of them merged either by the owner or by the automatic merge, which arms a tier-high pull request only after an approval (`AGENTS.md`, "Unattended runs").
- Plan pull requests #4, #9, #26 (flagged) and #20, #67 merged after the owner's approving review. No plan has yet merged **without** a human review, so "an unflagged plan merges after `factory/doc-review`" has not been seen.
- No spec pull request exists yet.
- Issues #8, #10, #12–#18, #31–#46, #68–#70 were created from merged plans by `factory-tickets.yml`.
