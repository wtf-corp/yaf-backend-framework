---
name: yaf-configuration
description: Configuration in YAF and services built on it - typed options classes, source-generated validators, AddYafOptions, startup checks that fail fast and report together, secrets handling, keyed pluggable providers, unsafe (Allow*/Disable*) options, reloadable settings and the generated configuration reference. Load BEFORE adding or renaming a configuration key or options class, reading IConfiguration or an environment variable, adding an appsettings value, introducing a setting that relaxes a check, registering a provider chosen by configuration, or editing configuration-reference.md.
license: MIT
metadata:
  owner: yaf
  version: "1"
---

# YAF configuration

Typed options are the only access path. Every options class is validated before the host listens. A misconfigured service does not start, and says why.

## When to load

- Adding, renaming or removing a setting or an options class.
- Needing a secret (key, password, token, connection string).
- Selecting an implementation by configuration (transport, storage, key provider).
- Adding a switch that turns a control off.
- A startup check or the configuration reference sync check failed.

## Config or constant?

| Question | Answer |
|---|---|
| Could an operator reasonably need to change it without a code change? | Option |
| Does it differ per environment or tenant? | Option (or feature flag for tenant targeting) |
| Is it a secret? | Option marked `[SecretData]`, no default, from a secret store |
| Does changing it change the wire contract, error codes or data format? | Constant in code |
| Is it a tuning knob nobody has asked for? | Constant until someone does |

## Rules

1. **REQUIRED** Application and infrastructure code injects `IOptions<T>` / `IOptionsMonitor<T>`. `IConfiguration` appears only in composition roots and `Add*` extensions (YAF10001). `Environment.GetEnvironmentVariable` is a banned API elsewhere.
2. **REQUIRED** Register every options class with `services.AddYafOptions<T>("Section")` (bind + generated validator + `ValidateOnStart`).
3. **REQUIRED** Every options class has `[OptionsValidator] partial class <Name>Validator : IValidateOptions<T>`; nested objects use `[ValidateObjectMembers]` / `[ValidateEnumeratedItems]`; cross-field rules are hand-written in the validator.
4. **REQUIRED** Checks needing I/O (topic exists, key usable, TLS required) are `IStartupCheck`s, not validators. All failures are reported together and the host exits non-zero; there is no degraded start.
5. **REQUIRED** Members holding secrets are `[SecretData]`, have no default, and have no value in any committed `appsettings*.json` (template test). Secrets come from user secrets (Development), environment variables, an AppHost secret parameter, or a vault provider.
6. **REQUIRED** Every `string` member of an options class is classified (YAF2001) so the startup dump can mask it.
7. **REQUIRED** A provider chosen by configuration is a keyed service: the adapter registers itself under its key; a missing registration fails startup naming the package to reference. A service whose main connection string goes through a transaction-mode pooler also sets `ConnectionStrings:<name>-direct` for the relay, the job runner and singleton workers.
8. **REQUIRED** A switch that weakens a control is named `Allow…` or `Disable…`, defaults to `false`, is added to `UnsafeOptionsRegistry`, and is refused outside Development unless listed in `Security:AcknowledgedUnsafe`. Prefer a design that needs no switch: local runs use signed development tokens (`dotnet user-jwts`) instead of an option that turns signature validation off.
9. **REQUIRED** No hard-coded default time zone, region, issuer, tenant or URL. If the feature needs one, the option is required.
10. **REQUIRED** Options are fixed for the process unless documented as reloadable; reloadable ones use `IOptionsMonitor<T>` and reject an invalid reload, keeping the previous value.
11. **REQUIRED** A new or changed key updates XML docs on the member and the generated `docs/articles/configuration-reference.md` in the same pull request; a renamed or removed key is a breaking change.
12. **REQUIRED** The environment is selected only by `DOTNET_ENVIRONMENT` / `ASPNETCORE_ENVIRONMENT`; never infer it from host names or branches.
13. **RECOMMENDED** One options class per feature, section name = feature name; durations as `TimeSpan`, sizes in bytes with the unit in the name.
14. **RECOMMENDED** Defaults should be the safe, production-suitable values; Development relaxations live in `appsettings.Development.json`.

## How to

1. Test first: default instance valid (or invalid until configured, on purpose); each rule; host test that a bad section fails startup with the key in the message.
2. Options class + validator + `AddYafOptions`.
3. Regenerate the configuration reference.

```csharp
/// <summary>Seat reservation behaviour of the Catalog service.</summary>
public sealed class ReservationOptions
{
    public const string Section = "Catalog:Reservations";

    /// <summary>How long a reservation is held before it expires.</summary>
    [Range(typeof(TimeSpan), "00:01:00", "1.00:00:00")]
    public TimeSpan HoldFor { get; set; } = TimeSpan.FromMinutes(10);

    /// <summary>API key of the venue system. Required when <see cref="NotifyVenue"/> is on.</summary>
    [SecretData]
    public string? VenueApiKey { get; set; }

    public bool NotifyVenue { get; set; }
}

[OptionsValidator]
internal sealed partial class ReservationOptionsValidator : IValidateOptions<ReservationOptions>
{
    // generated data-annotation checks; cross-field rule added by hand in a partial method:
    // NotifyVenue == true requires VenueApiKey
}

// ApplicationModule.cs
services.AddYafOptions<ReservationOptions>(ReservationOptions.Section);
```

```csharp
internal sealed class VenueReachableCheck(IOptions<ReservationOptions> options, VenueClient client) : IStartupCheck
{
    public async ValueTask<StartupCheckResult> RunAsync(CancellationToken ct)
        => !options.Value.NotifyVenue || await client.PingAsync(ct)
            ? StartupCheckResult.Ok()
            : StartupCheckResult.Fail("Catalog:Reservations:NotifyVenue is on but the venue system is unreachable.");
}
```

Check the configuration that ships without starting the server:

```bash
dotnet run --project samples/conference/src/Services/Catalog/Api -- --yaf-check
```

## Anti-patterns

| Wrong | Why | Instead |
|---|---|---|
| `configuration["Catalog:HoldMinutes"]` in a handler | Untyped, unvalidated, untestable (YAF10001) | `IOptions<ReservationOptions>` |
| `Environment.GetEnvironmentVariable("VENUE_KEY")` | Bypasses precedence, validation and masking | `[SecretData]` option |
| `"VenueApiKey": "dev-key"` in `appsettings.json` | Secret in a committed file | User secrets / AppHost parameter |
| `public string TimeZone { get; set; } = "Europe/…"` | App-specific default in shared code | Required option, no default |
| Validating in the constructor of the consumer | Fails on first use, not at startup | Options validator |
| Calling the broker in a validator | Validators must be pure and fast | `IStartupCheck` |
| `SkipTlsValidation` flag | Unregistered unsafe switch | `Allow…` option in `UnsafeOptionsRegistry`, or don't |
| `if (env.IsDevelopment()) services.AddFoo(); else services.AddBar();` scattered | Hidden environment branches | One keyed provider chosen by a setting |
| Renaming a key silently | Deployed environments stop working | Breaking-change commit; mention in the changelog |

## Verify

- `dotnet build YAF.slnx` (YAF10001, YAF2001).
- `dotnet test --project <project>/tests/…` — options tests; host test with several invalid sections reports all of them.
- `-- --yaf-check` exits 0 for each service with the committed settings plus test secrets.
- Generated `configuration-reference.md` and `unsafe-options.md` in sync (CI step); startup dump shows the new secret masked.

## References

- `docs/articles/configuration.md` — rules, precedence, startup checks, feature flags.
- `docs/articles/configuration-reference.md` (generated) — every section and key.
- `adr/0017-configuration.md` — read before proposing another access path.
- `samples/conference/src/Services/Catalog/Application/` — options classes; `src/AppHost` — secret parameters.
- Skills: `yaf-security` (unsafe options, secrets), `yaf-public-api-change` (keys are public API).
