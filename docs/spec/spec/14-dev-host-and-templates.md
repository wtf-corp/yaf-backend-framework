# 14 — Development host (Aspire) and templates

## 1. Aspire as the development host

Every solution built on YAF — the reference sample and anything the templates generate — has an **Aspire AppHost** that starts the whole system locally with one command and shows logs, traces and metrics in the Aspire dashboard. Aspire is development-time and test-time only; production deployment is outside this specification.

```bash
dotnet run --project samples/conference/src/AppHost
```

What the AppHost starts:

| Resource | How | Notes |
|---|---|---|
| PostgreSQL | `AddPostgres("pg").WithDataVolume()` + one database per service | Admin UI optional |
| Migrator per service | project resource | Services `WaitForCompletion(migrator)`; a failed migration stops the start |
| **RabbitMQ** | `AddYafRabbitMq("rabbit")` (from `YAF.Aspire.Hosting`, over Aspire's `AddRabbitMQ`) | **The default broker for local runs**: a real broker, no account, works offline. Exchanges and queues are provisioned by the services (`TopologyMode.Provision` in Development) |
| Object storage emulator | `AddYafObjectStorageEmulator("storage")` | A local S3-compatible container, used with the default setup; `YAF.Storage.AwsS3` talks to it through its endpoint setting |
| LocalStack (opt-in) | `AddYafLocalStack("aws")` (from `YAF.Aspire.Hosting`) | Started only when the AppHost parameter `UseLocalStack` is `true`: SNS topics, SQS queues, S3 buckets and KMS keys for teams that deploy to AWS. Resources are declared in the AppHost (`.WithTopic(…)`, `.WithQueue(…, subscribeTo: …)`, `.WithBucket(…)`, `.WithKey(…)`) and provisioned on start; services get `Aws:LocalStack:*` and resource names injected. Replaces RabbitMQ and the storage emulator for that run |
| Services | project resources with `WithReference` to their database and messaging resources | Each API on its own ports from its launch profile |
| OpenTelemetry | the dashboard's OTLP endpoint | From service defaults |

- **The default needs no account.** A fresh clone, and a solution just generated from the templates, start with PostgreSQL, RabbitMQ and the storage emulator: `Messaging:Transport=RabbitMq` is passed to every service. `InMemory` is valid only for a single-service run (§2) and for tests.
- **LocalStack is opt-in and requires an account auth token** (its images are no longer anonymous). Setting the AppHost parameter `UseLocalStack=true` switches every service to `Messaging:Transport=AwsSqs`, S3 and KMS on LocalStack, without code changes. The AppHost then takes the token as the secret parameter `localstack-auth-token`; when it is missing the AppHost fails at start with a message that says so and names the default setup as the alternative. CI runs the AWS conformance and end-to-end jobs only when the token secret is present, and reports them as skipped (never as passed) otherwise, for example on forks.
- Secrets are AppHost **parameters** (`AddParameter(name, secret: true)`): `encryption-key`, `lookup-key`, `redaction-hmac-key`, `api-key-pepper`, and `localstack-auth-token` when LocalStack is on. `AddYafDevSecrets()` generates the key parameters into AppHost user secrets on first run so a clone starts without manual key generation; it refuses to run outside Development.
- Aspire and its hosting integrations are pinned in `Directory.Packages.props`. The AppHost project shape and package names have changed between Aspire major versions; the implementer follows the current stable Aspire guidance and records the choice in ADR-0019. Community integrations (LocalStack) may lag behind Aspire; `YAF.Aspire.Hosting` wraps the LocalStack container itself (`AddContainer` + health check + init script) so it does not depend on one.
- **Development identity.** No option switches token validation off. The AppHost starts a small development issuer (`AddYafDevIssuer("idp")`): it holds a signing key generated into AppHost user secrets, publishes OpenID Connect discovery and key documents, and mints signed tokens for the development users listed in its configuration. Services get it injected as their JWT provider (`Auth:Jwt:Authority`), so they validate signatures exactly as they do in production. A service run on its own uses `dotnet user-jwts`, whose signing key and issuer the JWT bearer handler picks up from user secrets in Development. The issuer resource refuses to start outside Development.
- **Later release.** The Azure Service Bus emulator joins the AppHost together with `YAF.Messaging.AzureServiceBus`; the `WithMessaging` call below is the extension point it will use.

`YAF.Aspire.Hosting` API (AppHost side):

```csharp
var pg = builder.AddPostgres("pg");
var idp = builder.AddYafDevIssuer("idp");
var useLocalStack = builder.AddParameter("UseLocalStack", "false");

// default: no account needed
var rabbit = builder.AddYafRabbitMq("rabbit");
var storage = builder.AddYafObjectStorageEmulator("storage").WithBucket("conference-exports");

// opt-in: AWS services on LocalStack (needs the account token)
var aws = builder.AddYafLocalStack("aws", enabledBy: useLocalStack)
    .WithTopic("conference-catalog-events")
    .WithQueue("registrations", subscribeTo: ["conference-catalog-events"], deadLetter: true)
    .WithBucket("conference-exports")
    .WithKey("conference-fields");

builder.AddYafService<Projects.Catalog_Api>("catalog", s => s
    .WithDatabase(pg, "catalog-db", migrator: typeof(Projects.Catalog_Migrator))
    .WithMessaging(rabbit, orWhenEnabled: aws)       // one call whichever broker runs
    .WithObjectStorage(storage, orWhenEnabled: aws)
    .WithIdentity(idp)
    .WithYafSecrets());
```

End-to-end tests of the sample use `Aspire.Hosting.Testing` to start this AppHost and call the services over HTTP (`15-testing.md` §6).

## 2. Running one service without the AppHost

A service runs on its own with PostgreSQL only: in-memory transport, local-disk object store (`Storage:Provider=FileSystem`, Development only), keys from user secrets, tokens from `dotnet user-jwts`. The service README lists the four `dotnet user-secrets set` commands and the `dotnet user-jwts create` command for a development user. This path exists so a contributor without Docker Compose-style orchestration can still work, and it is what `WebApplicationFactory`-based integration tests use.

## 3. Templates (`dotnet new`)

Package `YAF.Templates`, sources under `templates/`. Templates are **real, compiling projects** with placeholder names (`TemplateApp`, `TemplateService`, `TemplateItem`), listed in a separate `Templates.slnx` that CI builds and tests, so a template can never ship broken.

| Template | Short name | Generates |
|---|---|---|
| Solution | `yaf-solution` | `.slnx`, `Directory.Build.props` / `.Packages.props` (YAF package versions), `global.json`, `nuget.config`, `.editorconfig`, `BannedSymbols.txt`, `AGENTS.md` (root + nested), `.agents/skills` (the service-facing skills), `adr/` with the template and ADR-0001, `specs/`, CI workflows, `src/AppHost`, `src/ServiceDefaults`, `README.md`, `SECURITY.md` |
| Service | `yaf-service` | Domain, Application, Infrastructure, Migrator, Api, Contracts projects and three test projects for one bounded context with one aggregate, one command, one query, the initial migration (with RLS), `.http` request files, and the AppHost wiring |
| Aggregate | `yaf-aggregate` | An aggregate folder (aggregate, id, memento, errors, a behaviour, tests), its `[MementoData]` partial data class declaration (the properties are generated), configuration and repository |
| Use case | `yaf-usecase` | Command or query folder with handler, validator, tests, and an endpoint in the service's HTTP style |
| Integration event | `yaf-event` | Contract record, publisher handler, consumer handler, tests |

Options: `--http controllers|minimal` (service), `--aws` (solution: the AppHost is generated with LocalStack switched on and the service projects reference the AWS adapters; without it the solution uses RabbitMQ and the storage emulator), `--aggregate <Name>`, `--no-privacy`. Options for a later HTTP style, a later transport or AI features are added with the packages they belong to.

Rules:

- Generated code builds with warnings as errors and its tests pass **as generated** (CI: instantiate each template with each `--http` option, build, test).
- A generated service declares the permission of an endpoint once, on its request type; no generated endpoint repeats it as an attribute.
- A generated service contains development stand-ins for the required extension points (`DevPrivilegeSource` granting every permission in one tenant, `DevApiKeyStore` with one generated key whose secret is written only to a git-ignored `.http` environment file). Its `.http` files take a signed bearer token for the development user from the same git-ignored environment file, filled by the development issuer or by `dotnet user-jwts`. They are registered **only in Development**; elsewhere the service refuses to start until real implementations are registered.
- Post-actions: add projects to the solution, add the service to the AppHost, restore. Editing existing files (AppHost, solution) is done by the template engine's supported mechanisms or by a small `yaf` dotnet tool (`tools/Yaf.Cli`) with the commands `yaf add service|aggregate|usecase|event`; the tool computes all edits before writing and refuses on any unexpected file state.
- A change to how a service is set up is made in the sample, the templates and the quick-start article in the same pull request.

## 4. Acceptance criteria

- On a clean machine with the SDK and Docker, with no account and no token: `dotnet new yaf-solution -n Acme && cd Acme && dotnet new yaf-service -n Library --aggregate Book && dotnet run --project src/AppHost` starts everything; the generated `.http` file creates and reads an item; traces for the request show in the dashboard.
- Template CI matrix (2 HTTP styles, with and without `--aws`) is green; generated solutions pass their own architecture and security tests. The `--aws` rows run when the LocalStack token is available and are reported as skipped otherwise.
- The sample AppHost starts with RabbitMQ by default and with LocalStack by setting `UseLocalStack=true` and the token; nothing else changes.
- A token signed with a key other than the development issuer's is refused by a service started from the AppHost; the development issuer does not start outside Development.
