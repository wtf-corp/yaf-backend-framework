# ADR-0017 — Configuration

> **Status** Accepted · **Date** 2026-11 · **Deciders** maintainers

## Context

Misconfiguration is an availability and a security problem: a missing key, a wildcard CORS origin or a development switch left on should stop a deployment, not surface at the first request or never. Pluggable engines (transport, storage, key provider) are chosen by configuration and must fail clearly when the package is absent.

## Drivers

- A service either starts correctly configured or does not start, and says everything that is wrong at once.
- Secrets never live in code or committed files and never appear in output.
- Configuration is discoverable: one reference, always current.
- No reflection-based binding at startup; binding mistakes surface at build time.
- Weakening a control is always visible.

## Options considered

| Option | For | Against |
|---|---|---|
| Read `IConfiguration` where needed | No ceremony | Untyped, unvalidated, untestable, undocumented |
| Options with reflection-based binding and lazy validation | Typed | Fails on first use; slower startup; errors only at run time |
| Custom configuration framework | Full control | Reinvents the platform |
| **Platform options + source generators + aggregated startup checks** | Typed, fast, fail-fast, standard | One helper and one validator class per options type |

## Decision

1. **Typed options are the only access path.** `IConfiguration` appears only in composition roots and `Add*` extensions; environment variables are not read directly elsewhere.
2. **One registration helper**, `AddYafOptions<T>("Section")`: bind, validate with a source-generated `[OptionsValidator]` validator (nested members included) plus hand-written cross-field checks, `ValidateOnStart()`. Binding uses the configuration binding generator.
3. **Aggregated startup checks.** After the container is built and before listening, the host runs all options validation and every `IStartupCheck` (checks needing I/O: key usable, topology present, TLS required, extension points registered). All failures are logged with their keys and the process exits non-zero. No degraded start.
4. **Secrets.** Members holding secrets are `[SecretData]`, have no default, and come from user secrets (Development), environment variables or a vault provider added by the app.
5. **Precedence** is the host's default chain; the environment is selected only by the standard environment variable.
6. **Fixed unless documented reloadable.** Reloadable sets: rate limits, cron schedules, log levels, feature flags, sampling. A reload that fails validation is rejected and the previous value stays.
7. **Pluggable providers are keyed services.** `Messaging:Transport`, `Storage:Provider`, `Encryption:KeyProvider` name a key that an adapter package registers; an unknown key fails startup naming the package to reference.
8. **Generated reference.** `docs/articles/configuration-reference.md` is produced from the options classes (name, type, default, required, secret, reloadable, description).
9. **Unsafe options registry.** Every option that weakens a control is named `Allow…`/`Disable…`, defaults to `false`, is listed in `UnsafeOptionsRegistry`, and is refused outside Development unless acknowledged under `Security:AcknowledgedUnsafe`, which itself raises a security event and a warning. No option bypasses authentication: there is no setting for unsigned tokens, and local runs use signed development tokens (ADR-0015).
10. **Diagnostics.** A startup dump of effective options with secrets masked and personal values redacted, a configuration fingerprint, and `--yaf-check` to run all checks and exit without listening.
11. **Feature flags** go through `IFeatureFlags`; a disabled feature answers `404`.
12. **Connection strings by role.** `ConnectionStrings:<name>` is the runtime role's; `<name>-maintenance` is the maintenance role's and is used only by the retention job; `<name>-direct` is an optional session-mode connection for the relay, the job runner and singleton workers when `<name>` goes through a transaction-mode pooler (ADR-0010). The migrator worker has its own.

## Consequences

- Deployments fail early and loudly; operators get the full list in one run.
- Each options class costs a validator class and XML docs, which also feed the reference.
- Hot reload is deliberately narrow.
- Adding an adapter is a package reference plus a configuration value; forgetting the reference is a clear startup error.
- Development convenience switches exist but cannot leak into deployed environments unnoticed.

## Enforcement

- Analyzer `YAF10001`: `IConfiguration` injected outside composition roots; banned API: `Environment.GetEnvironmentVariable` outside them.
- Template tests: no `[SecretData]` option has a default; committed `appsettings*.json` hold no secret value; gitleaks in the pre-commit hook and CI.
- Host tests: several invalid sections yield one aggregated failure; `--yaf-check` exit codes; secrets absent from the dump; invalid reload rejected.
- CI: `--yaf-check` for every sample service; drift check of the generated reference and of `unsafe-options.md`.
- Security event `config.insecure_flag`.

## Guidance for agents

- New setting: add it to (or create) an options class with XML docs, a default or `[Required]`, a validator rule, and register with `AddYafOptions`. Regenerate the configuration reference.
- Never inject `IConfiguration` into a handler, service or middleware.
- Never write a secret, even a fake-looking one, into `appsettings*.json`, tests or docs. Mark secret members `[SecretData]`.
- A check that needs network or database access is an `IStartupCheck`, not an options validator.
- A switch that weakens security must follow the unsafe-option naming, default to `false` and be registered. Never add one that weakens or skips authentication.
- Do not make an option reloadable unless the feature is designed for it.
- Choose providers by keyed registration, not by `if` chains on configuration strings.

## Related

0003, 0004, 0005, 0011, 0014, 0016, 0019, 0021
