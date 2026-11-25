# ADR-0030 — BenchmarkDotNet as the benchmark library

> **Status** Proposed
> **Date** 2026-11
> **Deciders** maintainers

## Context

ADR-0018 says that benchmarks exist and do not run in CI, and names no library for them. `spec/03-results-and-errors.md` §5 asks for a benchmark project, `tests/Benchmarks`, that shows a successful `Result<T>` round trip allocates nothing. M1 (`specs/0006-core/plan.md`, T24) creates that project. `spec/15-testing.md` §6 already lists `tests/Benchmarks` as "BenchmarkDotNet, informational", but no ADR names the package, and ADR-0003 says a new dependency needs a record. The owner chose BenchmarkDotNet and asked for an ADR that says why it is used (plan 0006, decision log Q2).

Verified against BenchmarkDotNet 0.15.8, the latest stable release on nuget.org on 2026-11-24: MIT licence, repository `github.com/dotnet/BenchmarkDotNet`. The licence was verified for the BenchmarkDotNet package only, not for its transitive packages. ADR-0003 names a CI licence check over all restored packages, which does not exist yet; until it does, the licences of the transitive packages are kept by review. Its `MemoryDiagnoser` adds an `Allocated` column, measured with `GC.GetAllocatedBytesForCurrentThread` (benchmarkdotnet.org, "Diagnosers"). The package brings a large graph: `Microsoft.CodeAnalysis.CSharp` 4.14.0, `Microsoft.Diagnostics.Runtime`, `Microsoft.Diagnostics.Tracing.TraceEvent`, `Iced`, `Perfolizer`, `CommandLineParser`, `Gee.External.Capstone`, `System.Management` and others (its nuspec).

## Drivers

- A benchmark result, allocations included, that a reader can trust and reproduce.
- No dependency in any package YAF ships (ADR-0003 allow-list).
- Permissive licence (ADR-0003 licence policy).
- Little code of YAF's own to maintain for something that is not shipped.

## Options considered

| Option | For | Against |
|---|---|---|
| A — BenchmarkDotNet in `tests/Benchmarks` | A widely used .NET benchmark tool; warm-up, statistics, process isolation and an allocation column out of the box; MIT; maintained under the `dotnet` organisation | A large transitive graph in the restore of one project; one more version to keep current |
| B — An own allocation harness (a console program around `GC.GetAllocatedBytesForCurrentThread`) | No dependency | Measures allocations only, no timing; warm-up, JIT tiering and noise are YAF's problem; one more tool to test and maintain; results nobody outside recognises; departs from `spec/15-testing.md` §6 |
| C — No benchmarks; the allocation unit test of `Core.Tests` only | Nothing to add | `spec/03-results-and-errors.md` §5 and ADR-0018 ask for benchmarks; no timing at all, and none of the hot paths later milestones add (M15) can be measured; departs from `spec/15-testing.md` §6 |

## Decision

1. `tests/Benchmarks` and later benchmark projects under `tests/` use BenchmarkDotNet, with `MemoryDiagnoser` where allocations matter.
2. BenchmarkDotNet is a test-only dependency. No project under `src/`, `samples/` or `templates/` references it, and no package YAF ships depends on it. Benchmark projects set `IsPackable` to `false`.
3. The version is set once, in `Directory.Packages.props`. It is subject to the dependency advisory checks of ADR-0020 like any other package.
4. Benchmarks are run by hand and do not run in CI (ADR-0018). A property that must hold on every change, such as "a successful `Result<T>` round trip allocates nothing", also has a unit test that runs in CI (plan 0006, T6).
5. This ADR does not add BenchmarkDotNet to the allow-list of ADR-0003, which governs packages; it records a test-only dependency.
6. While BenchmarkDotNet 0.15.8 does not recognise `net11.0` out of process, benchmarks run with its in-process toolchain. The benchmark classes live in a non-packable class library that the console project runs, so the analyzer rule CA1515 (public types of an application should be internal) needs no suppression (owner's decisions on #136, 2026-11-24).

## Consequences

**Positive**

- Benchmark numbers in a format other .NET developers read and can rerun.
- No harness code of YAF's own.

**Negative / costs**

- The restore of `tests/Benchmarks` pulls in a large graph, including an older `Microsoft.CodeAnalysis.CSharp` that transitive pinning raises to the version the analyzers pin (`specs/0006-core/research.md` §10).
- One more package version to keep current, and a change to it is tier high (`Directory.Packages.props` is a guard file).
- Benchmarks outside CI can drift unnoticed; the CI unit test covers only what it asserts.
- The in-process run gives up the process isolation listed for option A, until a stable BenchmarkDotNet release supports .NET 11 out of process.

## Enforcement

| Mechanism | What it checks |
|---|---|
| `Directory.Packages.props` as a guard file (factory policy) | Adding or changing the package version puts the pull request in tier high, for a maintainer's review |
| Architecture tests of ADR-0003's allow-list | Each built core assembly references only its allow-list row, so not BenchmarkDotNet |

The rest is kept by review: that no adapter project under `src/` and no `samples/` or `templates/` project references the package, that benchmark projects stay non-packable, and that the transitive packages carry permitted licences.

## Guidance for agents

- Write benchmarks only under `tests/`, with BenchmarkDotNet. Do not write an own timing or allocation harness.
- Never reference BenchmarkDotNet from `src/`, `samples/` or `templates/`, and never make a benchmark project packable.
- Do not add a benchmark to CI. When a benchmarked property must hold on every change, add a unit test for it as well.
- Use only BenchmarkDotNet APIs and arguments you checked in its documentation or source for the version in `Directory.Packages.props`.
- When another benchmark or profiling library seems needed, stop and ask: it needs its own ADR.

## Related

- ADR-0018 — testing strategy; benchmarks exist and do not run in CI
- ADR-0003 — dependency policy and allow-list
- ADR-0020 — dependency advisory checks
- `spec/15-testing.md` §6 — the test projects, `tests/Benchmarks` among them
- `specs/0008-m1-maintenance/plan.md` (decision log) — the in-process run and the review findings this record answers
- `spec/03-results-and-errors.md` §5; `specs/0006-core/plan.md` (decision log Q2, T23, T24); `specs/0006-core/research.md` §10
