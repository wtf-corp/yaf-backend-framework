# ADR-0006 — Repository structure and packaging

> **Status** Accepted
> **Date** 2026-11
> **Deciders** Framework maintainers

## Context

The prior design was consumed as a git submodule with project references. That made co-development easy, but an app received every project whether it used it or not, pulled the framework's build settings and package versions into its own build, could not detect a breaking change before compiling against it, and pinned a commit rather than a version. Vendor SDKs were separated only partly.

YAF must let an app take exactly the building blocks and adapters it needs (ADR-0003), and must tell it reliably when an upgrade breaks something.

## Drivers

- Pay only for what is used: no broker SDK in an app that does not use that broker.
- Breaking changes to the public surface are detected mechanically, before release.
- Simple upgrades: one version number for the whole framework.
- A new solution or service is created in one command and builds and passes its tests as generated.
- One repository in which framework, tests, sample, templates and docs change together.

## Options considered

| Option | For | Against |
|---|---|---|
| Git submodule with project references | Step-into debugging; change both sides at once | No selection per package; build settings leak; no API-compatibility gate; commit pins |
| One large NuGet package | Simple to reference | Every app gets every dependency |
| **One NuGet package per building block and per vendor adapter, lockstep version** | Selective; isolates vendors; validated API | More packages to publish; needs a source mode for co-development |
| Independent version per package | Fine-grained releases | Compatibility matrix between packages; hard for users and agents |

## Decision

### One repository

```
src/        one folder per package; folder <Name> builds package YAF.<Name>
tests/      one test project per src project, plus cross-cutting suites
samples/    the reference solution
templates/  dotnet new templates
docs/       the docs site and generated references
adr/, specs/, .agents/skills/, .github/
```

Each of `src/`, `tests/`, `samples/`, `templates/`, `docs/`, `adr/` has its own `AGENTS.md` (ADR-0028). Layout detail: `spec/01-repository-and-packages.md`.

### Packages

- **Core packages** hold contracts and engines that need no third-party engine: `YAF.Core`, `YAF.Domain`, `YAF.Application`, `YAF.Infrastructure`, `YAF.Persistence.EfCore`, `YAF.Persistence.EfCore.PostgreSql`, `YAF.Http`, `YAF.Http.Controllers`, `YAF.Http.MinimalApis`, `YAF.Privacy`, `YAF.Privacy.EfCore`, `YAF.Testing`, `YAF.Testing.Integration`.
- **Adapter packages** each isolate one vendor dependency: `YAF.Validation.FluentValidation`, `YAF.Messaging.RabbitMq`, `YAF.Messaging.AwsSqs`, `YAF.Storage.AwsS3`, `YAF.Cryptography.AwsKms`, `YAF.Http.Versioning`, `YAF.Markup.HtmlSanitizer`, `YAF.Aspire.Hosting`, and platform-specific persistence add-ons. There are no mediator adapters (ADR-0009).
- **Planned for a later release**, each as an added package: `YAF.Messaging.AzureServiceBus`, `YAF.Storage.AzureBlob`, `YAF.Cryptography.AzureKeyVault`, `YAF.Http.FastEndpoints`, `YAF.AI`; the privacy coordinator context and the incident register as additions to `YAF.Privacy`. The first release ships one adapter of each kind, so that each contract is proven by at least two implementations (the in-memory or built-in one and a real one) without the cost of maintaining every vendor from the start.
- **Test support is two packages.** `YAF.Testing` holds pure helpers and depends on `YAF.Application` only, so a domain test project does not pull in ASP.NET Core, EF Core and Testcontainers. `YAF.Testing.Integration` holds the web factory, the container fixtures, test authentication, the security test pack and the conformance suites.
- **A store for a package that may not reference persistence is its own package**: `YAF.Privacy.EfCore` holds the EF Core mappings of what `YAF.Privacy` keeps.
- Root namespace equals package id; namespaces follow folders.
- Dependencies point inwards: Core ← Domain ← Application ← Infrastructure / Http ← adapters.
- Implementations are `internal`, exposed through public interfaces and `Add*` / `Use*` / `Map*` extension methods. `InternalsVisibleTo` names only the package's own test project.
- The `Analyzers` project (generators and analyzers, `netstandard2.0`) is not a package: it ships inside `YAF.Core` as an analyzer asset, so every consumer gets it without a separate reference and in the same version.

### Versioning

All packages share **one version** and are released together (ADR-0020). A release of one is a release of all; an app references the same version of every YAF package. Source builds carry `0.0.0`. Until `1.0.0` only prereleases are published.

### Public surface control

| Mechanism | Purpose |
|---|---|
| `PublicAPI.Shipped.txt` / `PublicAPI.Unshipped.txt` per project (public API analyzers) | Every public API change is an explicit, reviewable line |
| `EnablePackageValidation` with `PackageValidationBaselineVersion` | Binary and source compatibility against the last release; a break fails unless the change is marked breaking |
| XML docs mandatory in `src` (CS1591, CS1573 as errors) | The API reference is complete |
| SBOM, symbols (`snupkg`), deterministic builds, source link | Traceable artefacts |

### Templates

`YAF.Templates` provides `dotnet new` templates for a solution (Aspire AppHost, service defaults, build props, agent files) and for a service (domain, application, infrastructure, migrator, API, tests) wired into an existing solution. They are plain compilable code with placeholder names, tested end to end in CI: generate, build with warnings as errors, run the generated tests.

### Source mode for co-development

For work that spans the framework and an app, the app can temporarily consume a local build: `dotnet pack` to a local feed, or a documented `YafSourceDir` MSBuild switch that replaces package references with project references. This is a development convenience; a merged app always references released packages.

## Consequences

**Positive**

- An app's dependency set matches what it uses; a vendor's SDK arrives only with its adapter.
- Upgrades are a single version bump with a compatibility check behind it.
- Framework build settings no longer leak into apps.
- Templates give agents and humans a correct starting point.

**Negative / costs**

- Over twenty packages to pack, validate and publish per release in 1.0, more as the planned adapters arrive.
- An app that needs an adapter planned for later (Azure, FastEndpoints) writes it against the public contract or waits.
- Debugging into the framework needs symbols or source mode.
- A change spanning framework and app takes two steps (release, then upgrade) unless source mode is used.
- Lockstep versions mean a package may get a new version without a change of its own.

## Enforcement

| Mechanism | What it checks |
|---|---|
| `tests/Architecture.Tests` | The dependency graph, including `YAF.Testing` → `YAF.Application` only; vendor SDKs only in their adapters; no public type in an `Internal` namespace; every `Add*` idempotent |
| Public API analyzers | Undeclared public API changes fail the build |
| Package validation in CI | Breaking changes against the baseline |
| Template end-to-end test | Generated solution and service build and pass their tests |
| CI pack job | Every packable project produces a valid package with SBOM and symbols |
| `InternalsVisibleTo` test | Only the matching test assembly is named |

## Guidance for agents

- Put new code in the package its dependencies allow. If it needs a vendor SDK, it belongs in that vendor's adapter, or in a new adapter package (which needs an ADR).
- Never add a project reference that breaks the inward direction, and never reference an adapter from a core package.
- Put a test helper that needs a web host, a database or a container in `YAF.Testing.Integration`, never in `YAF.Testing`.
- Do not create a package from the "planned for a later release" list as part of other work; each needs its own approved spec.
- A new public type or member goes into `PublicAPI.Unshipped.txt` and gets XML docs in the same change. Do not edit `PublicAPI.Shipped.txt` by hand.
- Do not suppress a package validation error. A real break needs a breaking-change commit (ADR-0020) and usually an ADR.
- Keep implementations `internal`; expose an interface and an `Add*` method.
- Never set a package version in a project file, and never give one package a different version.
- When a pattern or the setup of a service changes, change the templates and the sample in the same pull request.
- Do not add `InternalsVisibleTo` for anything but the package's own test project.

## Related

- ADR-0003 — dependency policy
- ADR-0019 — local development host and templates
- ADR-0020 — versioning and release automation
- ADR-0021 — documentation site
- `spec/01-repository-and-packages.md`
