# Nested `AGENTS.md` files — desired content

The root file is loaded for every task, so it stays short. Each subtree adds only what an agent needs **when working there**. Each section below is the full content of one file. Nested files add or tighten; they never loosen a hard rule of the root.

---

## `src/AGENTS.md`

```markdown
# src — framework packages

Folder `src/<Name>` builds package `YAF.<Name>`; root namespace = package id; namespaces follow folders.

## Before you edit
- Find the package's layer in `adr/0006-*` and check the allowed references (`tests/Architecture.Tests` enforces them).
- Load the skill for the area (root AGENTS.md, skills index) and `yaf-public-api-change` if anything `public` changes.

## Rules for this tree
1. Everything `public` has XML docs that say what it is for, what the caller must do first, and what a failure looks like. CS1591 is an error.
2. Implementations are `internal sealed`, exposed through an interface and an `Add*`/`Use*`/`Map*` extension. `InternalsVisibleTo` names only the package's own test project.
3. New public members go to `PublicAPI.Unshipped.txt` (the analyzer tells you). Removing or changing a shipped member is breaking.
4. No third-party package in a core package beyond the ADR-0003 allow-list. Vendor SDK types never appear in a public signature outside their adapter.
5. Target the frameworks set in `Directory.Build.props` (currently `net11.0`, C# 15). Do not add or drop a target framework or add a polyfill package: ADR-0003 decides when a new .NET is added (a minor release) and when one is dropped (a major). With more than one target, `#if` on the target framework is allowed only where a newer API is used, in a file named `*.NetNN.cs`. (`Analyzers` is the one exception: Roslyn requires `netstandard2.0`.)
6. No static mutable state, no service locator, no `DateTime.Now/UtcNow`, no `Guid.NewGuid()`, no `ILogger.Log*` extension calls (see `BannedSymbols.txt`).
7. An option that weakens a control is named `Allow…`/`Disable…`, defaults to `false`, and is registered in `UnsafeOptionsRegistry`.
8. A new span, metric or log event is added to `TelemetryCatalog`; a new option to its options class with XML docs (the references are generated from both).
9. Framework code logs names, ids and error codes — never payloads, results or values.
10. Security-sensitive paths need a human reviewer from CODEOWNERS: `Http/Authentication`, `Http/Authorization`, `Infrastructure/Cryptography`, `Persistence.EfCore*/Tenancy`, `Persistence.EfCore/Audit`, `Privacy/`, `Analyzers/`. A pull request touching one is tier high and never merges automatically. The guard files (approved files, `BannedSymbols.txt`, analyzer configuration, architecture tests, CI workflows, `.claude/`, `.github/factory/`) are code-owned too; weakening one needs the `guard-change` label applied by a human maintainer.

## Per-package notes
- `Core`: trim/AOT compatible (`IsAotCompatible`, `IsTrimmable`); no reflection. Native AOT is a non-goal for the other packages while persistence depends on EF Core; still prefer the generators to reflection over user types.
- `Analyzers`: a project packed into `YAF.Core`, not a package; `netstandard2.0`, Roslyn only, incremental generators. Every diagnostic has an id `YAF` + the number of the area's spec file + a three-digit sequence (`YAF2001`, `YAF12001`), listed in `docs/articles/analyzers.md`, and a test.
- `Messaging.*`: must pass the conformance suite in `tests/Conformance.*` — write to the suite, not to the sample.
- `Http.Controllers`, `Http.MinimalApis`: behaviour that differs between the two styles is a bug; `tests/Http.Parity.Tests` decides.
- `Testing` holds pure helpers and references `Application` only; anything that needs a host, a container or ASP.NET Core goes to `Testing.Integration`.

## Verify
`dotnet build YAF.slnx` · `dotnet test --project tests/<Name>.Tests` · `dotnet test --project tests/Architecture.Tests` · `dotnet pack src/<Name> -c Release` (package validation).
```

---

## `tests/AGENTS.md`

```markdown
# tests

Load `yaf-testing` first.

1. Test first: write it, run it, watch it fail for the expected reason, then implement. Note the failing run as evidence on the ticket's pull request.
2. One behaviour per test; name `Method_Scenario_Expected`; Arrange / Act / Assert separated by blank lines; no logic (loops, conditionals) in a test body.
3. Assert on error codes (`ShouldFailWith(Errors.X)`), never on messages.
4. Real PostgreSQL and brokers through the shared collection fixtures of `YAF.Testing.Integration`. No in-memory database provider. Isolate by a new tenant (`factory.NewTenant()`), not by deleting data. Domain and application test projects reference `YAF.Testing` only.
5. Fakes from `YAF.Testing` or hand-written for ports we own; a mocking library only for third-party interfaces. Test tokens are signed with the test key of `YAF.Testing.Integration`; there is no unsigned-token switch.
6. Time is `FakeTimeProvider`; never `Task.Delay` to wait for background work — use `WaitForIdleAsync`, `RunDueAsync`, `Eventually.Async`.
7. Test data is synthetic and obviously fake (`attendee-0001@example.test`). Keys are generated in the test.
8. A test that proves a security tenet or a control carries `[Control("S…")]` / `[Control("C-…")]`. Never remove such a tag or test without an approved spec.
9. `*.approved.*` files change only deliberately: explain the diff in the pull request. Never commit a `*.received.*` file.
10. Categories: put Docker-dependent tests in `Docker`, broker tests in `Broker:<name>`, template tests in `Templates`, end-to-end in `E2E`.
11. Do not make a failing test pass by weakening it. If the test is wrong, say why in the pull request and fix the test in its own commit.
```

---

## `samples/AGENTS.md`

```markdown
# samples/conference — the reference system

This code is copied by users and included verbatim in the documentation. It must show the recommended way, with no shortcuts.

1. Use only public YAF APIs. If the sample needs something internal, the framework is missing an extension point — raise it, do not work around it.
2. Every pattern in the README's concept map has a file and a test; adding a framework feature adds a row, a use of it, and a test here.
3. Moving or renaming a file may break a docs include; run the docs build.
4. Domain language comes from `docs/<context>/ubiquitous-language.md`. Update it when you add a term.
5. Contexts talk only through contracts in `src/Contracts`; a service never references another service's projects (architecture test).
6. Development stand-ins (`Dev*`) are registered only under `IsDevelopment()`.
7. All data is synthetic. No real names, emails, companies or domains other than `example.test`.
8. A shortcut that cannot be avoided gets a `// SAMPLE-ONLY:` comment and an entry in `docs/adr/`.
9. Keep the `.http` request files working; they are run in CI in order.
10. After a model change: add an EF migration in that service's Infrastructure project and check `has-pending-model-changes`.

Run: `dotnet run --project samples/conference/src/AppHost`. Tests: `dotnet test --solution samples/conference/Conference.slnx`.
```

---

## `templates/AGENTS.md`

```markdown
# templates — dotnet new

Templates are real projects with placeholder names (`TemplateApp`, `TemplateService`, `TemplateItem`) that build and pass their tests as they are (`Templates.slnx`).

1. Change the sample first, then mirror the change here and in `docs/articles/quick-start.md`, in the same pull request.
2. Never use a placeholder name as part of another identifier (the engine replaces substrings).
3. After changing the template's model, regenerate its initial migration (instantiate → `dotnet ef migrations add InitialCreate` → copy back with placeholders).
4. Generated output must contain no secret: development keys are generated at instantiation and written only to git-ignored files.
5. The generated `AGENTS.md` and `.agents/skills` come from `templates/solution/agents/` — keep them in step with this repository's service-facing skills.
6. Verify: `dotnet test --project tests/Templates.Tests` (instantiates each `--http` style, with and without `--aws`, builds, tests). The default output runs on PostgreSQL and RabbitMQ with no account.
```

---

## `docs/AGENTS.md`

```markdown
# docs — DocFX site

1. `docs/reference/` and `docs/compliance/` are generated. Never edit them by hand; change the source (options classes, `TelemetryCatalog`, error catalogues, attributes, `[Control]` tags) and run `dotnet tool run yaf-compliance generate`.
2. Code in articles is included from the sample or a compiled test file (`[!code-csharp[](path#region)]`). Do not paste code inline; the quick start is the one exception and has its own compile test.
3. Write for a developer who has not read the code: what it is for, how to set it up, the rules, how to test it. Present tense, no marketing.
4. Link types with `<xref:…>`; the build fails on a dead link, which is the point.
5. Never write that YAF or an application "is compliant" with a regulation or standard. Describe the control and the evidence.
6. Regulatory clause identifiers appear only if verified against the official text; otherwise mark them unverified.
7. A new article is added to `toc.yml`. A renamed article leaves a redirect.
8. Verify: `dotnet docfx docs/docfx.json --warningsAsErrors`.
```

---

## `adr/AGENTS.md`

```markdown
# adr — decision records

Load `yaf-writing-adrs`.

1. An accepted ADR is not edited, except its status line. A changed decision is a new ADR that supersedes the old one.
2. Agents draft ADRs with status `Proposed`; only a human sets `Accepted`.
3. An ADR is needed for: a dependency in a core package, a new package, a public contract shape, a change to a security or privacy control, a change to the development process.
4. File name `NNNN-kebab-title.md`, next free number; add it to `adr/README.md`.
5. Every ADR has **Enforcement** (what keeps it true) and **Guidance for agents**. A decision nothing enforces is a wish — say so explicitly if that is the case.
6. Keep it to the decision and its reasons. How-to material belongs in `docs/articles` or a skill.
```

---

## `specs/AGENTS.md`

```markdown
# specs — feature specifications and plans

Load `yaf-writing-specs` / `yaf-writing-plans` to write, `yaf-reviewing-specs-and-plans` to review. Never both for the same document.

1. One folder per change: `specs/NNNN-slug/` with `spec.md`, `plan.md`, optional `research.md`. No `tasks.md`, no progress log, no `review.md`: the work breakdown is GitHub issues, the review record is the pull request (ADR-0029).
2. `specs/0000-framework/` is the specification the framework is built from; it is approved as committed. A milestone of the initial build (M0–M15) has a folder with `plan.md` only, citing its sections; do not copy requirements into a `spec.md`.
3. `spec.md` says what and why — no class names, no file paths. `plan.md` says how, declares the public API, schema and error-code delta in its `factory:delta` block, and ends with the Tickets table.
4. A ticket in the table is one concern and one pull request within the soft size limit, with its dependencies, the paths it touches, the failing test to write first and the verification command.
5. A spec with open questions is not ready. Do not start `plan.md` before the spec is merged, nor code before the plan is merged and its tickets exist. The spec and the plan each go through a pull request of their own (`docs(specs): …`) and a reviewer agent's check (`factory/doc-review`).
6. A spec is approved by a code owner's review. A plan merges after its review check, and needs a human maintainer's review when it is flagged (public API change, schema change, new dependency, deviation from the spec, a tier-high ticket). The `Status:` line is informative only: never treat it, or set it, as approval.
7. Once merged, a spec changes only through a pull request the code owners review, noted in its decision log. A deviation from the plan is a comment on the ticket; when it changes the approach it is a plan update pull request, which passes the plan gate again.
8. Requirements are numbered `R1…`, tickets `T1…` in the plan and by issue number afterwards; tests and review findings refer to them.
9. Never put secrets, real personal data or customer names in these files or in issues.
10. When the last ticket merges, set the spec's status to `Implemented` and leave the folder; it is the record.
```

---

## Pointer files

`CLAUDE.md`, `GEMINI.md`, `.github/copilot-instructions.md` and similar, where a tool needs one, contain a single import or pointer line to `AGENTS.md` (for example `@AGENTS.md`) and nothing else. Rules live in one place.

The agent definitions in `.claude/agents/` follow the same principle: each names its role, its tools and the skill to load, and states no rule of its own.
