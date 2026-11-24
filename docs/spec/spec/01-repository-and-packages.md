# 01 — Repository layout, packages and build

## 1. Repository

```
yaf/
├── AGENTS.md                      # agent entry point (see agents/AGENTS.root.md in this spec)
├── CLAUDE.md                      # one line: @AGENTS.md (same for other tools that need a pointer file)
├── README.md, LICENSE, SECURITY.md, CONTRIBUTING.md, CHANGELOG.md
├── YAF.slnx
├── global.json                    # SDK 11.0.x, rollForward latestFeature; test runner Microsoft.Testing.Platform
├── Directory.Build.props          # shared build settings (§4)
├── Directory.Build.targets
├── Directory.Packages.props       # central package management, transitive pinning
├── nuget.config                   # package source mapping, audit source
├── package.json, package-lock.json, .releaserc.json   # semantic-release, CI only (17-ci-release-docs.md)
├── build/release/                 # verify.sh, prepare.sh, publish.sh used by the release job
├── .editorconfig, .gitattributes, .gitignore, .gitleaks.toml
├── .config/dotnet-tools.json      # docfx, dotnet-ef, reportgenerator, cyclonedx, dotnet-stryker, yaf-compliance (tools/)
├── .agents/skills/                # Agent Skills (see agents/skills in this spec); tool-neutral rules
├── .claude/                       # the factory's Claude Code kit (ADR-0029; factory/ in this spec): agents/, settings.json, and skills/, a committed mirror of .agents/skills kept in step by a check (Claude Code reads skills only from there)
│   ├── settings.json              #   permission deny rules for agents (no merge, no approving review, no push to protected branches)
│   └── agents/                    #   one definition per role: spec-writer, spec-reviewer, planner, plan-reviewer,
│                                  #   implementer, code-reviewer, pr-reviewer
├── .github/
│   ├── CODEOWNERS, pull_request_template.md, dependabot.yml
│   ├── ISSUE_TEMPLATE/            #   ticket.yml, epic.yml (factory tickets); bug, feature, security
│   ├── workflows/                 #   ci.yml, audit.yml, nightly.yml, mutation.yml, docs.yml (17-ci-release-docs.md) and the
│   │                              #   factory workflows: factory-gates.yml, factory-pr-review.yml, factory-doc-review.yml,
│   │                              #   factory-automerge.yml, factory-tickets.yml, factory-status.yml, factory-digest.yml
│   └── factory/                   #   policy.json (risk tiers, size limits, roles, limits) with policy.schema.json; the gate
│                                  #   scripts (classify, size, delta, guards, ticket, create-tickets, human-approval: Node 22
│                                  #   modules without dependencies); labels.json, setup-github.sh; prompts/ for the CI reviewers
├── adr/                           # decision records
├── specs/                         # feature specs and plans (ADR-0024, ADR-0025) — one folder per change: spec.md, plan.md,
│                                  # optional research.md; 0000-framework/ holds this specification. The work breakdown lives in
│                                  # GitHub issues created from the plan's Tickets table, not in a file (ADR-0029)
├── docs/                          # DocFX site: articles/, api/ (generated), compliance/ (generated)
├── src/                           # one folder per package, folder name = package name without "YAF."
├── tests/                         # one test project per src project + cross-cutting suites
├── samples/conference/            # the reference solution (16-reference-sample.md)
└── templates/                     # dotnet new templates (14-dev-host-and-templates.md)
```

Each of `src/`, `tests/`, `samples/`, `templates/`, `docs/`, `adr/` and `specs/` has its own `AGENTS.md` (see `agents/`).

The `.claude/` folder, the `factory-*` workflows and `.github/factory/` are the software factory's kit. They are copied from `factory/` of this specification at M0 and installed by hand with the human owner (`18-build-plan.md`); from then on they are guard files, changed only through a pull request a human maintainer approves.

## 2. Packages

Folder `src/<Name>` builds package `YAF.<Name>`; the root namespace equals the package id. Namespaces follow folders.

### 2.1 Core (no third-party engine)

| Package | Depends on | Content |
|---|---|---|
| `YAF.Core` | — | Results, errors, typed id contracts, framework ids, classification attributes, `Guard`, `Optional<T>` |
| `YAF.Domain` | Core | Entities, aggregates, mementos, value objects, events, rules, specifications, state machines, privacy markers |
| `YAF.Application` | Domain | CQRS contracts, dispatcher and pipeline, validation, execution context, authorization model, capabilities, messaging/jobs/storage/crypto/markup-sanitizer **ports**, process managers |
| `YAF.Infrastructure` | Application | Engines that need no specific store or broker: outbox relay, inbox, job runner, cron trigger (expressions parsed by Cronos), in-memory transport, key ring, field protector, redaction, security events, audit chain sealer, telemetry, health checks |
| `YAF.Persistence.EfCore` | Infrastructure | `YafDbContext`, interceptors, conventions and converters for typed ids, smart enumerations and value objects, repository base with field protection, read-model query helper, store implementations (outbox, inbox, jobs, cron, idempotency, audit, process state), unit of work, migrator worker, specification translation |
| `YAF.Persistence.EfCore.PostgreSql` | Persistence.EfCore | Npgsql setup, row-level security, the three database roles, partitioning of append-only tables, `SKIP LOCKED` claims, `LISTEN/NOTIFY`, advisory locks and their lease-row fallback, snake_case naming |
| `YAF.Http` | Application | Authentication schemes, authorization requirement data derived from request markers, execution-context middleware, problem details mapping, rate limiting, security headers, OpenAPI transformers, version-set abstraction with the URL-segment convention, framework endpoints |
| `YAF.Http.Controllers` | Http | `YafControllerBase`, MVC conventions |
| `YAF.Http.MinimalApis` | Http | `IEndpoint` discovery, `ToHttpResult()`, route group conventions |
| `YAF.Privacy` | Application, Infrastructure | Retention and purge, legal hold, inventory, export and erasure operations over the generated exporters and erasers (`IDataSubjectOperations`) |
| `YAF.Privacy.EfCore` | Privacy, Persistence.EfCore | EF Core mappings and stores for what `YAF.Privacy` keeps (legal holds, retention state) |
| `YAF.Testing` | Application | Pure test helpers, usable from a domain test project: memento and result assertions, fakes, test clock, approval helper, architecture rules |
| `YAF.Testing.Integration` | Testing, Http, Persistence.EfCore.PostgreSql | Test host factory, Testcontainers fixtures, test auth with a signing key, security test pack, conformance suites, authorization inventory |
| `Analyzers` (project, not a package) | Roslyn only (`netstandard2.0`) | Source generators (typed ids, error codes, memento data classes, dispatcher registrations, exporters and erasers, data inventory) and analyzers; packed into `YAF.Core` under `analyzers/dotnet/cs`, so every consumer gets them without a separate reference. There is no `YAF.Analyzers` package |

Analyzer ids are `YAF` + the number of the spec file of the area + a three-digit sequence: `YAF2001` (domain and privacy markers, `02` and `11`), `YAF4001` (application), `YAF5001` (persistence), `YAF12001` (observability). An id is never reused.

### 2.2 Adapters (each isolates one third-party dependency)

| Package | Wraps |
|---|---|
| `YAF.Validation.FluentValidation` | FluentValidation validators as `IValidator<T>` |
| `YAF.Messaging.RabbitMq` | Exchanges and quorum queues |
| `YAF.Messaging.AwsSqs` | SNS topics + SQS queues |
| `YAF.Storage.AwsS3` | `IObjectStore`, claim check store |
| `YAF.Cryptography.AwsKms` | `IKeyProvider` for the key ring and per-subject keys |
| `YAF.Persistence.EfCore.PostgreSql.AwsRds` | IAM authentication token provider for Npgsql |
| `YAF.Http.Versioning` | `Asp.Versioning` behind the version-set abstraction of `YAF.Http` (header and media-type versioning, a document per version) |
| `YAF.Markup.HtmlSanitizer` | An HTML sanitizer library as `IMarkupSanitizer`, for members marked `[Markup]` (`04-application.md`) |
| `YAF.Aspire.Hosting` | AppHost extensions: PostgreSQL with migrator ordering, RabbitMQ, opt-in LocalStack, secret parameters |

There are no dispatcher adapters. The built-in source-generated dispatcher is the only engine; an app that wants another one implements `IDispatcher` itself.

### 2.3 Planned for a later release (not in 1.0)

| Package | Wraps |
|---|---|
| `YAF.Messaging.AzureServiceBus` | Topics and subscriptions; with it the Service Bus emulator in `YAF.Aspire.Hosting` |
| `YAF.Storage.AzureBlob` | `IObjectStore`, claim check store |
| `YAF.Cryptography.AzureKeyVault` | `IKeyProvider` |
| `YAF.Http.FastEndpoints` | Base endpoints and processors for FastEndpoints |
| `YAF.Privacy` additions | The privacy coordinator bounded context (data subject request aggregate and its orchestration across services), the incident register (`IIncidentRegister`), `DecisionRecord` |
| `YAF.AI` | Governed gateway over `Microsoft.Extensions.AI` (`13-ai.md`) |

The transport, object-store, key-provider and endpoint contracts and their conformance suites are written in 1.0 so that each of these is an added package, not a breaking change.

Rules:

- A core package MUST NOT reference a third-party engine. The allowed third-party dependencies of core packages are listed in ADR-0003 (Cronos in `YAF.Infrastructure` is the one library outside the platform and the ORM); adding one needs an ADR.
- Only an adapter package may reference its vendor SDK. An architecture test over the built assemblies enforces both rules (`tests/Architecture.Tests`).
- Implementations are `internal`, exposed through public interfaces and `Add*` / `Use*` / `Map*` extension methods. `InternalsVisibleTo` names only the package's own test project.

## 3. Dependency graph

```
Core ◄─ Domain ◄─ Application ◄─┬─ Infrastructure ◄─ Persistence.EfCore ◄─ Persistence.EfCore.PostgreSql
                                ├─ Http ◄─ Http.Controllers | Http.MinimalApis | Http.Versioning
                                ├─ Validation.* , Markup.*
                                └─ Messaging.* , Storage.* , Cryptography.*  (reference Infrastructure)
Privacy ─► Application, Infrastructure        Privacy.EfCore ─► Privacy, Persistence.EfCore
Testing ─► Application                        Testing.Integration ─► Testing, Http, Persistence.EfCore.PostgreSql
Analyzers: no references
```

A domain or application test project references `YAF.Testing` only, and so pulls in neither ASP.NET Core, EF Core nor Testcontainers.

## 4. Build settings

`Directory.Build.props` (root) sets for every project:

| Setting | Value |
|---|---|
| `TargetFramework(s)` | `net11.0` for every project (`src`, tests, samples, templates), set once in `Directory.Build.props`. When a new .NET ships, a minor release adds it there as a further target; a target is dropped in the next major after Microsoft ends support for it (ADR-0003). CI builds and tests every target. The one exception is `Analyzers`: `netstandard2.0`, as Roslyn requires. `#if` on the target framework is allowed only where a newer API is used, and only in files named `*.NetNN.cs` (for example `Clock.Net12.cs`) |
| `LangVersion` | `latest` (C# 15 on `net11.0`); current language features are used freely |
| `Nullable`, `ImplicitUsings` | `enable` |
| `TreatWarningsAsErrors` | `true`; NuGet advisories NU1901–NU1904 stay warnings in local and pull-request builds and are errors when `-p:AuditPipeline=true`, which the daily `audit.yml` and the `release` job set (`17-ci-release-docs.md` §2.1) |
| `AnalysisLevel` / `AnalysisMode` | `latest` / `All`, with a short documented suppression list in `.editorconfig` |
| `EnforceCodeStyleInBuild` | `true` |
| `Deterministic`, `ContinuousIntegrationBuild` (CI), `EmbedUntrackedSources`, `PublishRepositoryUrl` | `true` |
| `NuGetAudit` / `NuGetAuditMode` / `NuGetAuditLevel` | `true` / `all` / `low` |
| `Version` | `0.0.0` in source; set by the release (`17-ci-release-docs.md`). Prereleases `1.0.0-alpha.N` / `-beta.N` until `1.0.0` |

No NuGet lock files are committed. Package versions are pinned in `Directory.Packages.props`, with central package management (`ManagePackageVersionsCentrally`) and transitive pinning (`CentralPackageTransitivePinningEnabled`).

> **Amended 2026-11-24**, by the owner's decision of 2026-11-24 (`specs/0007-no-package-lock-files/plan.md`, decision log; ADR-0032): the row of the lock-file property, with the locked restore in CI, is removed, and the paragraph above is added.

`src/Directory.Build.props` adds: `GenerateDocumentationFile`, CS1591 and CS1573 as errors, `IsPackable`, `EnablePackageValidation` with `PackageValidationBaselineVersion` (the last released version), `IncludeSymbols` + `snupkg`, `GenerateSBOM`, `IsAotCompatible` and `IsTrimmable` for `YAF.Core` only (native AOT is a non-goal for the other packages while persistence depends on EF Core; specifications and validators compile expression trees), and `Microsoft.CodeAnalysis.PublicApiAnalyzers` with `PublicAPI.Shipped.txt` / `PublicAPI.Unshipped.txt` per project.

`BannedSymbols.txt` (Microsoft.CodeAnalysis.BannedApiAnalyzers), applied to `src` and to the templates:

- `System.DateTime.Now`, `System.DateTime.UtcNow`, `System.DateTimeOffset.Now`, `System.DateTimeOffset.UtcNow` → use `TimeProvider`.
- `System.Guid.NewGuid()` → use `Guid.CreateVersion7()` or a typed id's `New()`.
- `System.Console.*` in library code; `System.Environment.GetEnvironmentVariable` outside composition roots.
- `MD5`, `SHA1`, `DES`, `TripleDES`, `RC2`, `Aes` in ECB mode, `Random` for security purposes.
- `Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.IgnoreQueryFilters` outside `YAF.Persistence.EfCore` (use the audited waiver).
- `Microsoft.Extensions.Logging.LoggerExtensions.Log*` in `src` (use `[LoggerMessage]`).
- Raw SQL built by interpolation into `FromSqlRaw` / `ExecuteSqlRaw`.
- `UseInMemoryDatabase` (tests run on PostgreSQL).

SBOMs: each package embeds an SPDX manifest (`GenerateSBOM`); the release additionally publishes one CycloneDX document for the repository (`17-ci-release-docs.md` §4).

`nuget.config`: a single `nuget.org` source, `packageSourceMapping` with `*` mapped to it, and `auditSources` pointing at the vulnerability index.

## 5. Coding conventions

- File-scoped namespaces; one public type per file; file name = type name; partial files `Type.Aspect.cs`.
- Private fields `_camelCase`, private static fields (readonly or not) `s_camelCase`, constants PascalCase; async methods end in `Async` and take a `CancellationToken` as the last parameter; no `async void`; `ConfigureAwait(false)` in library code.
- Setup extensions: `Add<Thing>` on `IServiceCollection` / `IHostApplicationBuilder`, `Use<Thing>` on the request pipeline, `Map<Thing>` on endpoints. Each `Add*` is idempotent.
- Options: one options class per feature, bound from a documented section, validated with a source-generated `IValidateOptions<T>` and `ValidateOnStart()` (`10-configuration.md`).
- Logging: `[LoggerMessage]` partial methods only, in a nested or sibling `Log` class; event ids are stable and listed in the package's article.
- Public API: XML docs say what the member is for, what the caller must do first, what it returns on failure. `<example>` on entry points.
- No mapper libraries. Mapping is explicit code next to the type it maps to.

## 6. Acceptance criteria

- `dotnet build YAF.slnx -c Release` succeeds with zero warnings on a clean clone with only the SDK and Docker installed.
- `tests/Architecture.Tests` proves: the dependency graph of §3 (including that `YAF.Testing` references nothing beyond `YAF.Application`, and that `YAF.Http` references neither an HTML sanitizer nor `Asp.Versioning`); no vendor SDK outside its adapter; no public type in an `Internal` namespace; every `Add*` method idempotent (called twice, one registration).
- Every packable project produces a package that passes package validation against its baseline.
