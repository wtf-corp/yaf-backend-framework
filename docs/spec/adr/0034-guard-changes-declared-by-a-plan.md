# ADR-0034 — Guard changes declared by a merged plan

> **Status** Proposed
> **Date** 2026-11
> **Deciders** maintainers
> **Supersedes** in part: 0029 §4 (the tier of declared guard changes); 0026 rule 3 and its Enforcement rows on the `guard-change` label and on `CODEOWNERS` for analyzer configuration; 0023 "What counts as approval" (the weakened-guard row) and the guard-file code-owner row of its Enforcement; 0027 the guard-file code-owner row of its Enforcement; 0025 (the seven keys of the `factory:delta` block and the reasons a plan is flagged). Amends, in the same pull request, `spec/17-ci-release-docs.md` §2.2 (`factory/guards`, the sensitive paths) and §6 (`CODEOWNERS`), and `spec/18-build-plan.md` rule 10

## Context

ADR-0029 §4 puts every pull request with "a guard file change" or "a new dependency" in tier high: it waits for a maintainer's approving review, and a relaxed analyzer setting also fails `factory/guards` until a maintainer applies `guard-change`. `.editorconfig` and `Directory.*.props` are sensitive paths there.

In plan 0013 the owner approved a plan whose tickets T1 and T2 declared new `.editorconfig` sections and new package pins. The pull requests that carried them, #388 (eight file-scoped `.editorconfig` sections) and #389 (seven `PackageVersion` lines in `Directory.Packages.props`), did only what the plan declared and were still tier high: the owner approved the same change twice, and applied `guard-change` to #388. Only a patch update of an existing package is exempt today (`classify.mjs`, `isPatchBumpOnly`).

On 2026-11-26 the owner chose to remove this double approval (idea 2 of `specs/0015-factory-throughput/plan.md`). On 2026-11-27 the owner agreed to the decision, with the `neverLowered` list below, on condition that a plan declaring `editorconfig` entries is always flagged (plan 0015, Q3, Q4 and decision log). Acceptance of this record is a code owner's approving review of its pull request.

This record supersedes parts of five accepted records, under rule 2 of the template, as plan 0013 T69 does for one row of ADR-0003:

- ADR-0029 §4: a guard file change and a new dependency are tier high (Decision 4).
- ADR-0026 rule 3 and its Enforcement: a changed guard needs a maintainer's `guard-change` label, and `CODEOWNERS` covers analyzer configuration (Decisions 4 and 8).
- ADR-0023 and ADR-0027: a weakened guard needs `guard-change` (ADR-0023, "What counts as approval"), and the rulesets require a code owner's review of the guard files (both Enforcement sections), which no longer holds for `.editorconfig` (Decision 8).
- ADR-0025: the `factory:delta` block has exactly seven keys, and its list of reasons a plan is flagged, repeated in ADR-0023, gains `editorconfig` entries (Decisions 2 and 7).

Every other part of these records stands.

The approved specification said the same, and is amended in the pull request that carries this record, as the owner chose on 2026-11-27: `spec/17-ci-release-docs.md` §2.2 (`factory/guards` passes on declared findings; the sensitive paths are always high except a declared change) and §6 (`.editorconfig` leaves the `CODEOWNERS` paths), and `spec/18-build-plan.md` rule 10 (`guard-change` is not needed for a declared change). Each amended line cites this record.

## Drivers

1. No change to an analyzer setting or a package pin reaches `main` without a human's approval.
2. The owner approves a guard change once, where it is decided: in the plan.
3. The security, privacy, factory and instruction paths among ADR-0029 §4's sensitive paths never get a relaxed analyzer without a review of the pull request itself.
4. A pull request cannot declare its own exception.

## Options considered

| Option | For | Against |
|---|---|---|
| A — As today: tier high, `guard-change` by a maintainer | Nothing changes | The owner approves the same change in the plan and again in the pull request |
| B — A `guard-change`-like label the gates apply when the diff matches | Reuses the label path | Only a human's label counts (ADR-0029); a bot's label would be a new kind of approval |
| C — Lower the tier of any guard file the plan names in `touches` | Simple to compute | Touches name files, not lines: the pull request could add anything to the file |
| D — Exact entries in the plan's `factory:delta`, read from the base branch (chosen) | The owner approved exactly these lines; the plan approval is the human act | New delta category and a parser for each file; `neverLowered` to keep up |

Keeping `.editorconfig` in `CODEOWNERS` was also considered: a declared section would still wait for a code owner's review, so the rule would help only `Directory.Packages.props` (plan 0015, Q3).

## Decision

A change to `.editorconfig` or `Directory.Packages.props` whose every added line the merged plan declares exactly is approved by the owner's approval of that plan:

1. **Declared files.** `policy.declaredGuards.files` lists `.editorconfig` and `Directory.Packages.props`. No other guard file is covered.
2. **`.editorconfig`.** The `factory:delta` block has the category `editorconfig`, one entry per setting: the section header, a space, and the setting line exactly as the file will hold it, for example `"[src/Application/IRequest\\{TResult\\}.cs] dotnet_diagnostic.CA1040.severity = <level>"`. The change is declared when, with comments and blank lines ignored, every section of the base is unchanged in the head, every new section names one file, every setting in a new section is a declared entry, and the named file matches none of `policy.declaredGuards.neverLowered`. A section names one file when its header is a repository-relative path with at least one `/`, no unescaped glob character (`*`, `?`, `[`, `{`), no `..` segment, and no `\` except as the escape of a character. A leading `/` is removed. The basis is the EditorConfig specification (https://spec.editorconfig.org): a section header that contains a `/` is matched relative to the directory of the `.editorconfig` file, here the repository root, with or without a leading `/`; a header without a `/`, such as `[Principal.cs]`, matches that file name in every directory and is not declared; `\` escapes the next character. That path, with the escapes and a leading `/` removed, is what `neverLowered` is matched against, without regard to case, so `src/application/context/Principal.cs` is refused like `src/Application/Context/Principal.cs`. T6 verifies with tests that the analyzer configuration of Roslyn, which the build uses, matches headers in this way before the rule takes effect. This is stricter than the "one file" condition of plan 0015 (Approach, "Idea 2") and loosens nothing.
3. **`Directory.Packages.props`.** A `dependencies` entry may carry its version, `Id@Version`. The change is declared when every `PackageVersion` of the base is unchanged in the head, every new one has exactly the attributes `Include` and `Version`, matching a declared `Id@Version`, and no child (any other attribute, such as `Condition`, or a child makes it undeclared), and the rest of the file, with comments and `PackageVersion` elements taken out, is unchanged. An entry without a version flags the plan, as before, and lowers nothing.
4. **Tier.** A declared change is tier medium for that file, and its guard findings in that file are reported as declared, not counted by `factory/guards`. Any other reason for tier high in the pull request (another sensitive path, an undeclared delta, oversize, `needs-human`) keeps it high. A change that is not declared exactly is tier high, as ADR-0029 §4 says.
5. **Declarations come from the base.** The declared entries are read only from the plan named by the `Plan:` line, as it is on the base branch. A pull request that edits the plan and the guard file together declares nothing and stays tier high.
6. **`neverLowered`.** A section naming a file under one of these paths keeps the pull request tier high: `src/Http/Authentication/**`, `src/Application/Authorization/**`, `src/Application/Context/**`, `src/Persistence.EfCore*/Tenancy/**`, `src/Infrastructure/Cryptography/**`, `src/Persistence.EfCore/Protection/**`, `src/Persistence.EfCore*/Audit/**`, `src/Privacy*/**`, `src/Infrastructure/Redaction/**`, `.github/**`, `.claude/**`, `.agents/**`, `AGENTS.md`, `**/AGENTS.md`, `CLAUDE.md`, `docs/spec/**`. From ADR-0029 §4's sensitive paths it takes the security and privacy paths (authentication and authorization, execution context and tenancy, field protection and keys, audit, privacy, redaction) and the factory and instruction paths, as the owner accepted (plan 0015, Q4). It leaves out analyzers and generators, migrations, the guard files, release and build configuration, `specs/**/spec.md` and `adr/**`: a section naming a file there can be declared. The list is a policy value; narrowing or widening it is a tier-high change of the policy file.
7. **The owner always sees the plan.** `dependencies` and `editorconfig` are both in `policy.delta.flaggingCategories`, so a plan or plan update that declares any such entry is flagged and needs the owner's approving review (`factory/plan-gate`).
8. **`CODEOWNERS`.** `.editorconfig` leaves `CODEOWNERS`, as `Directory.Packages.props` is already outside it: an undeclared change to either is tier high and needs a maintainer's approval through `factory/human-approval`.

Everything else in ADR-0029, ADR-0026, ADR-0023, ADR-0027 and ADR-0025, and the dependency rules of ADR-0003, stand.

## Consequences

**Positive**

- A pull request that applies what the approved plan declared, such as #389, merges at tier medium without a second approval.
- The owner's approval stays the one human act, made where the decision is visible: in the plan, with every line spelled out.

**Negative / costs**

- The approval of a plan now carries more weight: a declared entry the owner did not read closely merges without another look.
- Plans spell out exact `.editorconfig` lines and package versions; a version that changes after the plan merges needs a plan update.
- Two parsers (`.editorconfig` sections, `PackageVersion` elements) and the `neverLowered` list are new code and policy to maintain.
- `.editorconfig` no longer gets a code-owner review request on an undeclared change; `factory/human-approval` is the only human gate there.
- A change like #388 stays tier high when one of its sections names a `neverLowered` file (`src/Application/Context/Principal.cs`).

## Enforcement

| Mechanism | What it checks |
|---|---|
| `declared.mjs` and its tests (`tests/declared.test.mjs`, plan 0015 T6) | Sections and pins match the declaration exactly; base sections and pins unchanged; one file per section by the path rule of Decision 2 (test `editorconfigVerdict_SectionWithoutSlash_IsNotDeclared`: `[Principal.cs]`, a `..` segment and a `\` separator are not declared; a leading `/` matches as without it; T6's ticket #401 carries this test and the rule, comment of 2026-11-27); `neverLowered` files refused, without regard to case; header matching checked against Roslyn |
| `classify.mjs` and `gates.mjs` `declaredGuardFiles` (T7) | Tier medium only for declared files and only with no other high reason; declarations read from the base plan (test `declaredGuardFiles_DeclarationOnlyInTheHeadPlan_IsNone`) |
| `guards.mjs` `guardVerdict` (T7) | Findings in a declared file are reported, not counted |
| `policy.json` `declaredGuards`, `delta.flaggingCategories` (T7) | The covered files and `neverLowered`; a plan with `editorconfig` or `dependencies` entries is flagged (test `planFlags_EditorconfigEntry_IsFlagged`) |
| `factory/plan-gate` | A flagged plan waits for the owner's approving review |
| Gate jobs read policy and scripts from the base branch (ADR-0029) | A pull request cannot change the rule that judges it |

Until T6 and T7 merge, these changes are tier high as before.

## Guidance for agents

- Planner: declare every `.editorconfig` setting as `"[<section>] <setting line>"` and every new package as `Id@Version`, exactly as the file will hold them. Expect the plan to be flagged.
- Implementer: add exactly the declared lines, no more; do not reorder or reword existing sections or pins. Never edit the plan in the same pull request.
- Never declare a section for a file under a `neverLowered` path to lower a tier; such a change is tier high and needs the owner.
- When the change needs a line the plan does not declare, stop and report: the plan is updated first.

## Related

- Superseded in part: ADR-0029 §4 — risk tiers; ADR-0026 — coding rules and guards; ADR-0023 — approval; ADR-0027 — review; ADR-0025 — plans and the `factory:delta` block. ADR-0003 — dependency policy, unchanged
- `specs/0015-factory-throughput/plan.md` — Approach "Idea 2", Q3, Q4, decision log; tickets T6–T9
- `specs/0013-thin-slice/plan.md` T1, T2; pull requests #388, #389
