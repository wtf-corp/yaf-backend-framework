# ADR-0033 — ArchUnitNET as a dependency of YAF.Testing

> **Status** Proposed
> **Date** 2026-11
> **Deciders** maintainers
> **Supersedes** in part: 0003, the row `YAF.Testing` of its allow-list for core packages

## Context

ADR-0018 chooses ArchUnitNET for architecture rules and puts "the architecture rules" in `YAF.Testing`. `spec/15-testing.md` §3 lists ArchUnitNET among the dependencies of `YAF.Testing`, and §3.1 gives it `YafArchitectureRules.For(assemblies)`, the ready-made rule set an app runs over its own assemblies. ADR-0003's allow-list says the opposite: `YAF.Testing` takes "nothing beyond `YAF.Application`'s row", and "adding to this table requires a superseding ADR". The conflict came up when M3 planned `YafArchitectureRules` (`specs/0013-thin-slice/plan.md`, decision log Q3). On 2026-11-26 the owner chose to add the library under this ADR.

Verified on nuget.org (`https://api.nuget.org/v3-flatcontainer/tngtech.archunitnet/0.13.5/tngtech.archunitnet.nuspec`) on 2026-11-27: package `TngTech.ArchUnitNET` 0.13.5, licence Apache-2.0, targets `netstandard2.0` and `net462`, dependencies `CycleDetection` 2.0.0, `JetBrains.Annotations` 2026.2.0, `Mono.Cecil` 0.11.6, `Newtonsoft.Json` 13.0.4 and `System.ValueTuple` 4.6.2, each MIT by its own nuspec. It references no test framework; `TngTech.ArchUnitNET.xUnitV3`, already pinned for `tests/Architecture.Tests`, is the separate xUnit binding.

## Drivers

1. One rule engine for YAF's own architecture tests and the rules apps run (ADR-0018).
2. `YAF.Testing` stays free of a test framework, ASP.NET Core, EF Core and Docker (ADR-0018, `spec/15-testing.md` §3).
3. Permissive licences only (ADR-0003, licence policy).
4. Few transitive packages in every domain and application test project.

## Options considered

| Option | For | Against |
|---|---|---|
| **A — `TngTech.ArchUnitNET` in `YAF.Testing`** (chosen) | Follows ADR-0018 and `spec/15-testing.md`; the same engine as `tests/Architecture.Tests`; rules over types, members and dependencies without writing a reader of IL | Five transitive packages in every project that references `YAF.Testing`; a 0.x library whose API may change in a minor version |
| B — own rules on `System.Reflection` | No dependency; the rules M3 needs (references between layers, vendor packages, `YAF.Testing` outside tests) are about assembly references | A new row in ADR-0003's "own implementation first" table; an amendment of `spec/15-testing.md` §3; later rules over types and members would need an own engine |
| C — leave `YafArchitectureRules` to M15 | Nothing to decide now | Apps and the sample go without the rule set until then; the conflict stays open |

## Decision

1. ADR-0003's allow-list row for `YAF.Testing` reads: **`YAF.Application`'s row, plus `TngTech.ArchUnitNET`**. Its helpers still do not depend on a test framework; `TngTech.ArchUnitNET.xUnitV3` and the other test-framework bindings stay out of `YAF.Testing`.
2. `YAF.Testing` uses ArchUnitNET only to implement `YafArchitectureRules` (`spec/15-testing.md` §3.1, §4). Its public API exposes no ArchUnitNET type: `YafArchitectureRuleSet` reports violations as text (`specs/0013-thin-slice/plan.md`, declared delta), so a later change of engine is not a breaking change.
3. The version is pinned in `Directory.Packages.props`; the transitive packages are not pinned unless a security fix requires it (ADR-0032).
4. Every other row and rule of ADR-0003 stands, and ADR-0003 is not edited beyond its status line.

## Consequences

**Positive**

- Apps check their own layering with the rules YAF checks itself with, from the package their domain tests already reference.
- No second rule engine to write and test.

**Negative / costs**

- Every domain and application test project that references `YAF.Testing` restores `TngTech.ArchUnitNET`, `Mono.Cecil`, `Newtonsoft.Json`, `CycleDetection`, `JetBrains.Annotations` and `System.ValueTuple`, and may meet a version conflict with its own `Newtonsoft.Json`.
- ArchUnitNET is below 1.0: an update may need changes in `YafArchitectureRules`.
- Two packages of one library are pinned (the core and the xUnit binding) and move together.

## Enforcement

| Mechanism | What it checks |
|---|---|
| `tests/Architecture.Tests`, `DependencyTests.Testing_ReferencesOnlyAllowList` | `YAF.Testing` references only its allow-list; ArchUnitNET is added to that list by the ticket that adds the reference (plan 0013, T70) |
| Architecture tests of ADR-0018 | `YAF.Testing` references no ASP.NET Core, EF Core, Testcontainers or test-framework assembly |
| Public API analyzer, `src/Testing/PublicAPI.*.txt` | A public member that exposes an ArchUnitNET type shows in the reviewed API files; review rejects it (decision 2) |
| CI licence check, NuGet audit (ADR-0003, ADR-0032) | Licences and known vulnerabilities of ArchUnitNET and its transitive packages |

## Guidance for agents

- Reference `TngTech.ArchUnitNET` from `src/Testing` only, never from another package under `src/`.
- Never reference `TngTech.ArchUnitNET.xUnitV3` or any test framework from `YAF.Testing`.
- Do not return, accept or derive from an ArchUnitNET type in a public member of `YAF.Testing`.
- When a rule seems to need a further library in `YAF.Testing`, stop and ask: it needs a new ADR.

## Related

- ADR-0003 — dependency policy; its row `YAF.Testing` is superseded by this ADR
- ADR-0018 — testing strategy; chooses ArchUnitNET
- ADR-0032 — restore without lock files; pinning of transitive packages
- `spec/15-testing.md` §3, §3.1, §4
- `specs/0013-thin-slice/plan.md` (decision log Q3, T69, T70), `specs/0013-thin-slice/research.md` §2
