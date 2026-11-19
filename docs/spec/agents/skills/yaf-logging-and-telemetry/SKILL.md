---
name: yaf-logging-and-telemetry
description: Logging, tracing, metrics and health checks the YAF way - source-generated log methods, what may and may not be logged, [LogProperties] and the single redaction policy, span and metric names from the telemetry catalogue, links for async work, health check tags, and testing what actually reaches the log. Load BEFORE writing any log statement, [LoggerMessage] method, Activity, Meter, counter or health check; before logging an object, a request or an exception; before adding a hosted loop; and before changing telemetry or sampling configuration.
license: MIT
metadata:
  owner: yaf
  version: "1"
---

# YAF logging and telemetry

Logs and traces leave the service. Log names, ids, codes, counts and durations — never values. Everything else goes through the redaction policy.

## When to load

- Writing or changing a log statement, span, metric or health check.
- Wanting to see a request, payload or object in a log.
- Adding a background loop or an outbound HTTP client.
- A log-redaction or telemetry-reference test failed.

## What do I use?

| I want to record… | Use |
|---|---|
| That something happened, with ids | `[LoggerMessage]` method in a `static partial class Log` |
| An object's properties as fields | `[LogProperties]` parameter (type must be classified) |
| An object inside a message | `IRedactingFormatter.Format(obj)` as an argument |
| A whole request in the pipeline log | `[LogPayload]` on the request type |
| A step worth timing across services | A span from the package's `ActivitySource`, name from `TelemetryCatalog` |
| A rate, count or duration over time | An instrument from the package's `Meter` (via `IMeterFactory`), name from `TelemetryCatalog` |
| Who did what to which record | Audit trail (automatic) — not a log |
| A security-relevant fact | `ISecurityEventRecorder` — not a log |
| A business milestone for analytics | `IBusinessEventRecorder` — not a log |
| Is the service able to work | A health check with the right tag |

## Rules

Logging

1. **REQUIRED** Source-generated log methods only. `logger.LogInformation(...)` and friends are banned APIs in `src` and in service projects created from the templates.
2. **REQUIRED** Constant message templates; values are parameters; stable event ids from the package's or service's range.
3. **REQUIRED** Never log a request body, a result, an error message with arguments, a token, a key, or a value of a classified member. Never build an exception message from data (YAF2007).
4. **REQUIRED** A type passed with `[LogProperties]` has all its members classified (YAF12001). Use `[property: …]` on records. A personal member is logged as a keyed hash, which is still pseudonymous personal data: log it only when someone needs to correlate on it, and never describe logs as free of personal data ("no directly identifying data" is the accurate wording).
5. **REQUIRED** Log an error **once**, where it is handled. No log-and-rethrow.
6. **REQUIRED** Levels: `Debug` diagnostics and validation failures; `Information` lifecycle and milestones; `Warning` handled anomalies (retry, fallback); `Error` a failed operation needing attention; `Critical` integrity or security failure.
7. **REQUIRED** Do not add tenant, principal or correlation id as parameters: the entry point's scope already carries `yaf.tenant_id`, `yaf.principal` (the actor; in asynchronous work the system principal, with the initiating user recorded beside it), `yaf.correlation_id`, `yaf.origin`.
8. **REQUIRED** Library and service code never writes to the console.

Traces and metrics

9. **REQUIRED** One `ActivitySource` and one `Meter` per package/service, named after it. Span and instrument names come from `TelemetryCatalog`; add the entry there first so the generated reference stays in sync.
10. **REQUIRED** Set span attributes through `YafActivity.SetTag(name, value, classification)`; never `activity.SetTag` with caller data.
11. **REQUIRED** Async work (outbox, consumers, jobs) is correlated with **links**, not parent/child.
12. **REQUIRED** Mark a span as error only for exceptions and `Failure`/`Unavailable` results. Business failures set `yaf.request.outcome` only.
13. **REQUIRED** No tenant id, user id, entity id or other high-cardinality value as a metric dimension. Units are UCUM; names lower-case dotted.
14. **REQUIRED** No custom correlation headers; no baggage beyond `yaf.correlation_id`; never personal data in baggage.

Health and resilience

15. **REQUIRED** Liveness (`live`) checks no dependency; readiness (`ready`) checks required dependencies with ≤ 2 s timeouts; optional dependencies degrade, they do not fail.
16. **REQUIRED** Every hosted loop registers a heartbeat with `IHeartbeatRegistry`.
17. **REQUIRED** Outbound HTTP clients are registered with `AddYafHttpClient<T>()`; no hand-rolled retry loops; retries at one layer per call path.

Testing

18. **REQUIRED** A change that logs anything about a person, a credential or a payload has a test reading the collected records (`FakeLogCollector`, `LogAssert.ContainsNo(value)` from `YAF.Testing`).
19. **RECOMMENDED** Prove the test can fail: remove the attribute once and watch it go red.

## How to

1. Write the log test (what must be present, what must be absent).
2. Add the log method; classify the type if you log properties.
3. Add catalogue entries for new spans/metrics; regenerate `docs/articles/telemetry-reference.md`.

```csharp
internal sealed partial class RegisterAttendeeHandler(/* … */, ILogger<RegisterAttendeeHandler> logger)
    : IRequestHandler<RegisterAttendeeCommand, Result<RegistrationId>>
{
    public async ValueTask<Result<RegistrationId>> HandleAsync(RegisterAttendeeCommand cmd, CancellationToken ct)
    {
        // …
        Log.Registered(logger, registration.Id, cmd.SessionId, contact);
        return registration.Id;
    }

    private static partial class Log
    {
        [LoggerMessage(EventId = 2101, Level = LogLevel.Information,
            Message = "Registration {RegistrationId} submitted for session {SessionId}")]
        public static partial void Registered(ILogger logger, RegistrationId registrationId, SessionId sessionId,
            [LogProperties] AttendeeContact contact);     // Email → keyed hash, FullName → keyed hash
    }
}

public sealed record AttendeeContact(
    [property: PersonalData(PersonalDataCategory.Identity, Purpose = "registration")] string FullName,
    [property: PersonalData(PersonalDataCategory.Contact, Purpose = "registration")] string Email);
```

```csharp
[Fact]
public async Task Register_DoesNotLogPersonalData()
{
    await _api.RegisterAsync(fullName: "Ada Example", email: "ada@example.test");

    LogAssert.ContainsNo(_api.Logs, "ada@example.test");
    LogAssert.ContainsNo(_api.Logs, "Ada Example");
    _api.Logs.GetSnapshot().Should().Contain(r => r.Id.Id == 2101);
}
```

## Anti-patterns

| Wrong | Why | Instead |
|---|---|---|
| `logger.LogInformation("Saved {Attendee}", attendee)` | Record `ToString()` prints every member; banned API | `[LoggerMessage]` + ids or `[LogProperties]` |
| `$"…{email}"` in a template | No structure, no redaction | Parameters; classified types |
| `JsonSerializer.Serialize(request)` into a log | Bypasses redaction | `IRedactingFormatter` / `[LogPayload]` |
| `catch (Exception e) { Log.Failed(e); throw; }` at every layer | Duplicate errors | Log once at the boundary |
| Validation failure logged as `Warning`/`Error` | Noise; attackers can flood it | `Debug` (the pipeline already records the outcome) |
| `activity.SetTag("email", email)` | Personal data in traces | `YafActivity.SetTag(..., classification)` or an id |
| `tenant_id` as a metric tag | Unbounded cardinality | Trace/log attribute only |
| Parenting a consumer span to the producer's | The producer trace is over; breaks durations | Link |
| Readiness-style DB check in liveness | Restart storms when the DB blips | Keep liveness dependency-free |
| Ad hoc span name strings | Docs drift | `TelemetryCatalog` constant |

## Verify

- `dotnet build YAF.slnx` (banned APIs, YAF2007, YAF12001).
- `dotnet test --project <service>/tests/Application.Tests` and the API integration tests (log redaction test, telemetry assertions with in-memory exporters).
- Telemetry reference and event-id list regenerated and committed.
- Health: `/health/live` stays healthy with the database down; `/health/ready` does not.

## References

- `docs/articles/logging.md` — taxonomy, three ways to log an object, redaction keys, testing.
- `docs/articles/telemetry.md` and `docs/articles/telemetry-reference.md` (generated) — span, metric and event-id catalogue.
- `adr/0016-observability.md` — W3C-only context, links for async work.
- `samples/conference/` Registrations `Application/Registrations/RegisterAttendee/`, log redaction tests.
- Skills: `yaf-privacy-and-data-classification`, `yaf-security`.
