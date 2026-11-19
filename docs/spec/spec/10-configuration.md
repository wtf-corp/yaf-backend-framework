# 10 — Configuration

## 1. Rules

1. **Typed options are the only access path.** Application and infrastructure code injects `IOptions<T>` / `IOptionsMonitor<T>`. `IConfiguration` is used only in composition roots and `Add*` extensions. Analyzer `YAF10001` flags `IConfiguration` injected elsewhere; `Environment.GetEnvironmentVariable` is a banned API outside composition roots.
2. **Every options class is validated at startup.** Registration goes through one helper:

```csharp
services.AddYafOptions<OutboxOptions>("Outbox");
// = AddOptions<T>().BindConfiguration(section).Validate with the source-generated validator .ValidateOnStart()
```

   Each options class has a `[OptionsValidator] partial class <Name>Validator : IValidateOptions<T>` using data annotations, `[ValidateObjectMembers]`, `[ValidateEnumeratedItems]`, plus hand-written cross-field checks. Async checks that need I/O (does the topic exist, is the key usable) are `IStartupCheck`s, not validators.
   `interface IStartupCheck { string Name { get; } ValueTask<StartupCheckResult> RunAsync(CancellationToken ct); }` with `StartupCheckResult.Ok()` / `.Failed(message, configurationKey?)`.
3. **All failures are reported together.** `StartupChecks` runs after the container is built and before the server listens: options validation and every `IStartupCheck`. The host logs each failure with its configuration key and exits non-zero. There is no degraded start.
4. **Secrets are never defaults and never in committed files.** A member that holds a secret is marked `[SecretData]`; a test in the template asserts that no `[SecretData]` option has a non-null default and that committed `appsettings*.json` files contain no value for one. Secrets come from user secrets (Development), environment variables, or a vault provider added by the app.
5. **Precedence** is the host default: `appsettings.json` < `appsettings.{Environment}.json` < user secrets (Development) < environment variables < command line. The environment is selected only by `DOTNET_ENVIRONMENT` / `ASPNETCORE_ENVIRONMENT`.
6. **Reloadable vs fixed.** Options are fixed for the process unless documented as reloadable (rate limits, cron schedules, log levels, feature flags, sampling). Reloadable options use `IOptionsMonitor<T>`; a reload that fails validation is rejected and logged, the previous value stays.
7. **Pluggable providers use keyed services.** `Messaging:Transport`, `Storage:Provider`, `Encryption:KeyProvider` name a key; each adapter registers itself under its key; a missing registration is a startup failure naming the package to reference.
8. **Configuration binding and options validation are source generated** (`EnableConfigurationBindingGenerator`): no reflection at startup, and binding errors surface at build time.
9. **No option weakens authentication.** There is no setting that accepts unsigned tokens or skips token validation; local runs use signed development tokens (`09-security.md` §3).

## 2. Configuration sections

Every section is documented in the package's article and in `docs/articles/configuration-reference.md`, which is **generated** from the options classes (name, type, default, required, secret, reloadable, description from XML docs). A CI step fails when the generated file differs from the committed one.

| Section | Options class | Package |
|---|---|---|
| `Yaf:Service` | `ServiceOptions` (`App`, `Name`, `Code`, `Version`) | Application |
| `Auth:Jwt`, `Auth:ApiKey`, `Auth:StepUp` | `JwtAuthOptions`, `ApiKeyAuthOptions`, `StepUpOptions` | Http |
| `Tenancy` | `TenantResolutionOptions` | Http |
| `Http`, `Cors`, `RateLimiting`, `ApiDocs` | `HttpOptions`, `CorsOptions`, `RateLimitingOptions`, `ApiDocsOptions` | Http |
| `Encryption` (`KeyProvider`, `Keys:<purpose>:<id>`, `Current:<purpose>`, `AllowEphemeralKey`, `SubjectKeyCacheSeconds`); purposes: `encryption`, `lookup`, `redaction`, `api-key-pepper`, `messaging`, `audit` | `EncryptionOptions` | Infrastructure |
| `Redaction` (`Hmac:KeyId`, `Hmac:Key`) | `RedactionOptions` | Infrastructure |
| `Messaging` (`Transport`, `Topology`, `ClaimCheckThreshold`, `ShutdownGrace`, `MaxConcurrency`), `Messaging:AwsSqs`, `Messaging:RabbitMq` | `MessagingOptions` + per-transport | Infrastructure + adapters |
| `Outbox` (incl. `OnParked` per event type), `Inbox`, `Idempotency` | `OutboxOptions`, `InboxOptions`, `IdempotencyOptions` | Infrastructure |
| `Jobs`, `Cron` | `JobsOptions`, `CronOptions` | Infrastructure |
| `Persistence` (`RowLevelSecurity`, `CommandTimeout`, `Paging:MaxLimit`) | `PersistenceOptions` | Persistence.EfCore |
| `Audit` (`Anchor`, `VerifySchedule`, `SealLagThreshold`) | `AuditOptions` | Persistence.EfCore |
| `Privacy` (`IpTruncation`, `Retention`) | `PrivacyOptions` | Privacy |
| `Storage`, `Storage:AwsS3` | `StorageOptions` | Infrastructure + adapters |
| `Aws:LocalStack` (`Enabled`, `Endpoint`, `Region`) | `LocalStackOptions` | AWS adapters |
| `Telemetry` (`Sampling`, `LogBuffering`, `CaptureMessagePayloads`) | `TelemetryOptions` | Infrastructure |
| `Security` (`AcknowledgedUnsafe`, `OutboundUrlPolicy`) | `SecurityOptions` | Infrastructure |

Sections of packages planned for a later release (`Messaging:AzureServiceBus`, `Storage:AzureBlob`, `Privacy:Dsr`, `Ai`) are added with those packages.

Connection strings use `ConnectionStrings:<name>`; under Aspire they are injected by the AppHost. Three may exist per database:

| Name | Role | Used by |
|---|---|---|
| `<name>` | runtime | The service |
| `<name>-direct` (optional) | runtime | Outbox relay, job runner and singleton workers, when `<name>` goes through a transaction-mode pooler (`05-persistence.md` §10) |
| `<name>-maintenance` | maintenance | The retention job only (`05-persistence.md` §4, §7); required when the retention job is enabled (startup check) |

The migrator worker has its own connection string with the migrator role.

## 3. Startup diagnostics

- At Information level, once: the configuration **fingerprint** and a dump of effective options with every `[SecretData]` member masked and every member classified personal redacted. The dump is produced by the same redacting formatter as logs.
- `dotnet run -- --yaf-check` (a host extension `RunYafChecksAndExit`) builds the container, runs all startup checks and exits with the result, without listening. CI uses it for each sample service ("the config that ships is valid").

## 4. Feature flags

`IFeatureFlags` (`IsEnabledAsync(name, context)`) with a default implementation over configuration (`Features:<name>`), tenant-aware targeting through an optional adapter for `Microsoft.FeatureManagement`. Flags gate endpoints (`[FeatureGate("…")]` equivalent provided as `RequireFeature("…")` metadata that works in every HTTP style) and requests (`[RequiresFeature]` marker checked by the pipeline). A disabled feature answers `404`.

## 5. Acceptance criteria

- Unit tests per options class: defaults valid or explicitly invalid-until-configured; each validation rule.
- Host tests: several invalid sections produce one aggregated failure listing all of them; `--yaf-check` exit codes; secrets absent from the startup dump; reload of an invalid value is rejected.
- The generated configuration reference is in sync (CI).
