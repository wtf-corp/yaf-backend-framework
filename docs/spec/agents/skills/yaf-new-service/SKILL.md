---
name: yaf-new-service
description: Creating a new service (bounded context) on YAF, from the dotnet new templates or by hand - project layout and references, composition-root order, Aspire AppHost wiring, the required extension points, secrets for local runs, and the path from first aggregate to first endpoint with tests. Load BEFORE running dotnet new yaf-solution / yaf-service / yaf-aggregate / yaf-usecase / yaf-event, creating service projects manually, editing a service's Program.cs or module registration, adding a service to the AppHost, or when a new service refuses to start.
license: MIT
metadata:
  owner: yaf
  version: "1"
---

# Creating a YAF service

A service is one bounded context: Domain, Application, Infrastructure, Migrator, Api, a Contracts project and three test projects. Generate it; do not assemble it from memory.

## When to load

- Starting a solution or adding a service, aggregate, use case or integration event with the templates.
- Wiring a service into the AppHost, or choosing its HTTP style or transport.
- The service fails at startup (missing extension point, key, topology, pipeline order).

## Which template?

| I need… | Run |
|---|---|
| A new solution with AppHost, service defaults, CI, `AGENTS.md`, skills | `dotnet new yaf-solution -n Conference` |
| A new bounded context with one aggregate, a command, a query, migration, tests | `dotnet new yaf-service -n Catalog --aggregate Session --http minimal` |
| Another aggregate in a service | `dotnet new yaf-aggregate -n Speaker` (in the service folder) |
| Another command or query with endpoint and tests | `dotnet new yaf-usecase -n PublishSession --kind command` |
| An integration event with publisher and consumer | `dotnet new yaf-event -n SessionPublished` |

Options: `--http minimal|controllers`, `--transport`, `--aws` (LocalStack wiring for SNS/SQS, S3 and KMS; needs an account token), `--aggregate <Name>`, `--no-privacy`.

## Layout and references

```
src/Services/Catalog/
├── Domain/            → YAF.Domain only
├── Application/       → Domain, YAF.Application (+ YAF.Privacy unless --no-privacy)
├── Infrastructure/    → Application, YAF.Persistence.EfCore.PostgreSql, transport/storage adapters (+ YAF.Privacy.EfCore unless --no-privacy)
├── Migrator/          → Infrastructure
└── Api/               → Application, Infrastructure, YAF.Http.<Style>, ServiceDefaults
src/Contracts/Conference.Contracts.Catalog/   → YAF.Core only
tests/Catalog/         Domain.Tests/ Application.Tests/   → YAF.Testing
                       Api.IntegrationTests/              → YAF.Testing.Integration
```

Optional adapters a service adds when it needs them: `YAF.Markup.HtmlSanitizer` (a `[Markup]` member), `YAF.Http.Versioning` (more than URL-segment versioning), `YAF.Validation.FluentValidation`.

## Rules

1. **REQUIRED** Use the templates. If you must create a project by hand, copy the generated shape exactly and run the service's architecture tests.
2. **REQUIRED** Set `YafServiceCode` (for example `catalog`) in the service's props: it prefixes error codes and names. Never change it after release.
3. **REQUIRED** References point inwards only (table above). Domain references no package besides `YAF.Domain`.
4. **REQUIRED** One module method per layer (`AddCatalogApplication()`, `AddCatalogInfrastructure()`); the API's `Program.cs` only composes.
5. **REQUIRED** Keep the composition-root order shown below; `app.UseYafHttp()` is one call.
6. **REQUIRED** Register real implementations of the required extension points before any non-Development deployment: `IPrivilegeSource`, and `IApiKeyStore` when API keys are enabled. The generated `DevPrivilegeSource` / `DevApiKeyStore` are `[DevelopmentOnly]` and the service refuses to start elsewhere without replacements.
7. **REQUIRED** The service context derives from `YafDbContext`, opts into the framework tables it needs (`IOutboxDbContext`, `IInboxDbContext`, `IJobsDbContext`, `IAuditDbContext`, …) and the initial migration enables row-level security on every tenant table.
8. **REQUIRED** Migrations are applied by the service's Migrator; the AppHost starts the API after the migrator completes.
9. **REQUIRED** No secrets in `appsettings*.json`. Local keys come from AppHost parameters (`AddYafDevSecrets()`) or user secrets. Local callers use signed tokens from the development issuer (`dotnet user-jwts create`); there is no setting that accepts unsigned tokens.
10. **REQUIRED** Wire the service into the AppHost with `AddYafService<…>(name, s => s.WithDatabase(…).WithMessaging(…).WithObjectStorage(…).WithYafSecrets())`.
11. **REQUIRED** Keep the generated test suites: architecture rules, error catalogue approval, field id approval (`fields.approved.txt`), pipeline approval, authorization inventory, cross-tenant requests, row-level security assert, log-leak test.
12. **REQUIRED** The service ships its committed `openapi/v1.json`, `Requests/*.http` files and a README (run, secrets, what it owns).
13. **RECOMMENDED** Minimal APIs for a new service unless the team has a reason for another style; one style per service.
14. **RECOMMENDED** Write the context's ubiquitous language (`docs/<context>/ubiquitous-language.md`) before the first aggregate.
15. **RECOMMENDED** Keep the generated defaults: RabbitMQ in the AppHost (no account needed), the in-memory transport in tests. Opt into LocalStack (`--aws`, `UseLocalStack`) only when the service deploys to AWS and the team has the token.

## How to: from nothing to a running endpoint

```bash
dotnet new yaf-solution -n Conference && cd Conference
dotnet new yaf-service -n Catalog --aggregate Session --http minimal
dotnet build Conference.slnx
dotnet test --solution Conference.slnx            # generated tests pass as generated
dotnet run --project src/AppHost                  # PostgreSQL, RabbitMQ, migrator, API, dashboard
```

This runs from a clean clone with Docker and no account. LocalStack is opt-in: set the AppHost's `UseLocalStack` parameter (or generate with `--aws`) and supply the `localstack-auth-token` secret parameter.

Then, test first at each step:

1. Domain: shape the aggregate (`yaf-domain-modeling`).
2. Application: command/query with markers and validator (`yaf-use-cases`); add permissions to `Capabilities`.
3. Infrastructure: `[MementoData]` partial data class, configuration, repository, migration with RLS (`yaf-persistence`).
4. Api: endpoint (`yaf-http-endpoints`); its permission comes from the request's marker; accept the OpenAPI and authorization inventory diffs.
5. Run the `.http` file against the AppHost; look at the trace in the dashboard.

Composition root:

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();                                  // OpenTelemetry, health, discovery, resilience
builder.AddYafHttp(http => http
    .AddAuthentication()
    .AddApiDocs()
    .AddRateLimiting()
    .AddCors());
builder.AddCatalogApplication();                               // AddYafApplication, handlers, options, capabilities
builder.AddCatalogInfrastructure();                            // AddYafPostgres<CatalogDbContext>, repositories, AddYafMessaging, jobs

if (builder.Environment.IsDevelopment())
    builder.Services.AddCatalogDevelopmentStandIns();          // DevPrivilegeSource, DevApiKeyStore

var app = builder.Build();

app.UseYafHttp();                                              // fixed order: headers → errors → limits → CORS → authn → context → rate limit → authz
app.MapYafEndpoints();                                         // /errors, /capabilities, /notifications
app.MapEndpoints<Program>();                                   // or MapControllers()
app.MapDefaultEndpoints();                                     // /health/*

app.Run();
```

AppHost:

```csharp
var pg = builder.AddPostgres("pg").WithDataVolume();
var broker = builder.AddYafRabbitMq("rabbit");                    // default: no account needed

builder.AddYafService<Projects.Catalog_Api>("catalog", s => s
    .WithDatabase(pg, "catalog-db", migrator: typeof(Projects.Catalog_Migrator))
    .WithMessaging(broker)
    .WithYafSecrets());
```

With LocalStack switched on, the broker resource is instead:

```csharp
var aws = builder.AddYafLocalStack("aws")                      // needs the localstack-auth-token parameter
    .WithTopic("conference-catalog-events")
    .WithQueue("registrations", subscribeTo: ["conference-catalog-events"], deadLetter: true);
```

## Startup failure → cause

| Message mentions | Fix |
|---|---|
| `IPrivilegeSource` / `IApiKeyStore` not registered | Register your implementation (or the dev stand-ins in Development) |
| Encryption / redaction key missing | AppHost secret parameters or user secrets; never a config-file value |
| `ApplyYafModel` not called | Call it last in `OnModelCreating` |
| Pending migrations | Run the migrator; add the missing migration |
| Topology missing | `Provision` in Development, or create it with IaC; check names in `Messaging` |
| Transport `X` not registered | Reference `YAF.Messaging.X` |
| `[Markup]` member but no `IMarkupSanitizer` | Reference `YAF.Markup.HtmlSanitizer` and register it |
| Purpose not in `IPurposeCatalog` | Add the purpose to the catalogue, or fix the name on the member |
| Duplicate error code across assemblies | Pin one with `[ErrorCode]` or rename; update `errors.approved.txt` |
| LocalStack auth token missing | Supply the secret parameter, or switch `UseLocalStack` off (RabbitMQ) |
| Pipeline pieces missing or misordered | Use `app.UseYafHttp()` instead of individual `Use*` calls |
| Unsafe option outside Development | Remove it from the deployed configuration |
| Undeclared request type | Add the access marker |

## Anti-patterns

| Wrong | Why | Instead |
|---|---|---|
| Hand-copying projects from another service | Drifts from the supported shape; stale wiring | Templates |
| Shipping with `DevPrivilegeSource` | Grants everything | Real `IPrivilegeSource`; startup refuses otherwise |
| One "Shared" project referenced by all services' domains | Couples bounded contexts | Contracts projects for events; duplicate small value objects |
| Calling another service's database or repository | Breaks the context boundary | Integration events or its API |
| `Database.Migrate()` at API startup | Races, needs DDL rights | Migrator project |
| Deleting the generated security tests "to simplify" | Removes the controls | Keep; extend |
| Changing `YafServiceCode` later | Every error code changes | Choose once |
| Putting handlers in the Api project | Use cases unreachable from jobs and consumers | Application project |

## Verify

```bash
dotnet build <Solution>.slnx
dotnet test --solution <Solution>.slnx
dotnet run --project src/Services/<Svc>/Api -- --yaf-check
dotnet ef migrations has-pending-model-changes --project src/Services/<Svc>/Infrastructure
dotnet run --project src/AppHost        # then run src/Services/<Svc>/Api/Requests/*.http
```

- Approved files of the new service are committed (`errors`, `pipeline`, authorization inventory, `openapi/v1.json`).
- The service's README explains run steps and secrets.

## References

- `docs/articles/getting-started.md` — the quick start this skill condenses.
- `docs/articles/templates.md` — template options and what each generates.
- `adr/0019-local-development-host.md` — AppHost shape, RabbitMQ as the default broker, LocalStack as the opt-in.
- `samples/conference/` — two services (Catalog, Registrations) wired end to end; `README.md` maps every concept to a file.
- Skills: `yaf-domain-modeling`, `yaf-use-cases`, `yaf-persistence`, `yaf-http-endpoints`, `yaf-messaging`, `yaf-testing`.
