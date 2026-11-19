# 12 — Observability: logging, telemetry, health

OpenTelemetry is the only telemetry model. The framework emits signals; the app's service defaults decide exporters.

## 1. Identity of a signal

- **Trace context** is W3C `traceparent` / `tracestate`. YAF defines no custom correlation or activity headers.
- **Correlation id**: a business-level id that spans a whole user operation including async work. It is taken from W3C **baggage** key `yaf.correlation_id` when present, otherwise generated at the entry point; carried in the execution context, on outbox rows, jobs and messages; added to logs and spans as attribute `yaf.correlation_id`. No other baggage is propagated by default (`Telemetry:PropagateBaggage = false`), and baggage never carries personal data.
- Resource attributes (from the app's service defaults via `UseApplicationMetadata`): `service.name`, `service.version`, `service.namespace` (the app), `deployment.environment.name`.

## 2. Logging

Rules for framework code and for services built with the templates:

1. **Source-generated log methods only** (`[LoggerMessage]`), grouped in a `static partial class Log` per feature. `logger.LogInformation(...)` and friends are banned APIs in `src` and in the templates' service projects.
2. **Stable event ids and names.** Each package owns an id range, listed in its article. Messages are constant templates; values are parameters.
3. **Names and ids, not values.** The framework logs request names, error **codes**, ids, counts and durations. It never logs a request body, a result, an error message with arguments, or an exception message it composed from data. The aim is that logs hold **no directly identifying data**. They are not free of personal data: a user id and a keyed hash of a personal value are pseudonymous identifiers of a person for as long as the identity mapping or the redaction key exists. Logs are therefore a store with a stated retention period in the data inventory (`11-privacy-and-compliance.md`), and nothing in this file claims that logs fall outside erasure considerations.
4. **Objects are logged through `[LogProperties]`** or `IRedactingFormatter`; both honour classification (`11` §3). Logging an unclassified type with `[LogProperties]` is analyzer error `YAF12001`.
5. **Levels**: `Debug` diagnostics; `Information` lifecycle and business-relevant milestones; `Warning` handled anomalies (retry, validation failure is *not* a warning — it is `Debug`); `Error` a failed operation needing attention, logged **once** where it is handled; `Critical` data integrity or security failure.
6. **Scopes**: the entry point opens one scope with `yaf.tenant_id`, `yaf.principal` (kind and id of the `Actor`; the id is pseudonymised by the redaction policy when the actor is a person), `yaf.initiator` (kind and pseudonymised id, present for asynchronous work started by a user, where the actor is the service's system principal), `yaf.correlation_id`, `yaf.origin`. Trace and span ids come from the logging provider.
7. **Enrichment**: `EnableEnrichment()`, application metadata enricher, and a YAF enricher for the scope values, so they are present even with buffering.
8. **Sampling and buffering** (options `Telemetry:Sampling`, `Telemetry:LogBuffering`): trace-based log sampling for `Information` and below is available and off by default; per-request buffering of `Debug` logs that are flushed when the request fails is available and off by default. `Warning` and above are never sampled or dropped.
9. **HTTP logging** is off by default. When enabled through `AddYafHttpLogging()`, bodies are never logged, headers are allow-listed, route parameters are redacted per `RequestPathParameterRedactionMode.Strict` unless classified public, and query strings are dropped.
10. Output is the host's concern (console JSON → collector, or OTLP). Framework code never writes to the console.

Testing what reaches the log is part of the job: `YAF.Testing` exposes `FakeLogCollector` helpers and `LogAssert.ContainsNo(value)`; the template has a test that sends a request with personal data and asserts none appears in any log record or span attribute.

## 3. One redaction policy for all sinks

`AddYafRedaction()` (in `YAF.Infrastructure`, called by `AddYafInfrastructure`; a host without it gets an erasing fallback, so nothing classified is ever logged in clear) registers redactors per classification (`11` §3), enables redaction in logging, registers `IRedactingFormatter`, and installs:

- an `Activity` enrichment guard: framework code sets span attributes only through `YafActivity.SetTag(name, value, classification)`, which applies the redactor;
- the options dump formatter;
- the problem-details argument filter.

## 4. Traces

One `ActivitySource` per package, named as the package (`YAF.Application`, `YAF.Infrastructure.Messaging`, …), versioned, with a schema URL. Apps subscribe with `AddSource("YAF.*")`.

| Span | Kind | Name | Key attributes |
|---|---|---|---|
| Request pipeline | Internal | `yaf.request {RequestName}` | `yaf.request.kind` (command/query), `yaf.request.outcome`, `yaf.error.codes` |
| Unit of work | Internal | `yaf.uow` | `yaf.uow.retries`, `yaf.uow.domain_events` |
| Domain event handler | Internal | `yaf.domain_event {EventName}` | |
| Outbox publish | Producer | `send {topic}` | messaging conventions: `messaging.system`, `messaging.destination.name`, `messaging.message.id`, `messaging.operation.type=send`; **linked** to the trace that wrote the row |
| Message processing | Consumer | `process {queue}` | `messaging.operation.type=process`, delivery count; linked to the producer context from the envelope |
| Audit chain sealing | Internal | `yaf.audit.seal` | `yaf.audit.sealed_rows` |
| Job execution | Internal | `yaf.job {JobName}` | `yaf.job.attempt`; linked to the scheduling trace |
| Cron trigger | Internal | `yaf.cron {JobName}` | `yaf.cron.scheduled_for` |
| AI call (later release, `YAF.AI`) | Client | `chat {model}` | GenAI conventions (`13-ai.md`) |

- Messaging attribute names follow the OpenTelemetry messaging semantic conventions, which are not yet stable; they are defined once in `MessagingSemantics` constants so a change is a one-file edit.
- Async work is correlated with **links**, not parent/child, because the producing trace has usually ended.
- Errors: `ActivityStatusCode.Error` only for exceptions and `Failure`/`Unavailable` results; business failures (`Validation`, `NotFound`, `Conflict`) leave the status unset and record `yaf.request.outcome`.
- HTTP server spans: the built-in ASP.NET Core activity data (available from .NET 11) is used; no separate ASP.NET Core instrumentation package.

## 5. Metrics

One `Meter` per package created through `IMeterFactory`. Names are lower-case dotted, units UCUM. No tenant id, user id or other high-cardinality value as a dimension.

| Instrument | Type | Dimensions |
|---|---|---|
| `yaf.request.duration` | histogram (s) | request name, kind, outcome |
| `yaf.uow.retries` | counter | |
| `yaf.outbox.pending`, `yaf.outbox.lag` | gauge | context |
| `yaf.outbox.published`, `yaf.outbox.failed`, `yaf.outbox.parked` | counter | type |
| `yaf.inbox.duplicates` | counter | consumer |
| `yaf.messaging.process.duration` | histogram (s) | consumer, outcome |
| `yaf.messaging.ignored`, `yaf.messaging.dead_lettered` | counter | type, reason |
| `yaf.jobs.pending`, `yaf.jobs.lag`, `yaf.jobs.duration`, `yaf.jobs.failed`, `yaf.jobs.parked` | | queue, job name |
| `yaf.cron.triggered`, `yaf.cron.skipped` | counter | job name |
| `yaf.security.events` | counter | type, severity |
| `yaf.authz.decisions` | counter | outcome |
| `yaf.ratelimit.rejected` | counter | policy |
| `yaf.crypto.operations`, `yaf.crypto.reencrypt.remaining` | counter / gauge | purpose |
| `yaf.retention.backlog`, `yaf.retention.purged` | gauge / counter | type |
| `yaf.audit.unsealed` | gauge (rows committed and not yet linked into the chain) | |
| `yaf.audit.seal.lag` | gauge (age of the oldest unsealed row, s) | |
| `yaf.audit.chain.verified`, `yaf.audit.chain.broken` | counter | |
| `yaf.idempotency.replayed`, `yaf.idempotency.conflicts` | counter | request name |
| `yaf.ai.calls`, `yaf.ai.tokens`, `yaf.ai.duration` (later release, `YAF.AI`) | | feature, model, outcome |

`docs/articles/telemetry-reference.md` lists every span, metric and log event id; it is generated from a registry (`TelemetryCatalog`) that the code uses for the names, so documentation cannot drift.

## 6. Health

Three endpoints (mapped by the app's service defaults; YAF contributes checks with tags):

| Endpoint | Tag | Healthy when |
|---|---|---|
| `/health/live` | `live` | The process runs and every registered heartbeat is fresh. **No dependency checks** |
| `/health/startup` | `startup` | Startup checks passed, migrations are not pending, topology validated |
| `/health/ready` | `ready` | Database reachable; transport reachable; not shutting down. Degraded (still `200`) when: outbox or jobs have parked items, lag above threshold, audit rows unsealed for longer than `Audit:SealLagThreshold`, re-encryption pending, optional dependency down |

Each check has a timeout of at most 2 s, real work (`SELECT 1`, a transport ping), and returns no details in the body outside Development. An open circuit to an optional dependency degrades; it does not fail readiness.

## 7. Resilience for outbound calls

- HTTP clients registered through `AddYafHttpClient<T>()` get the standard resilience handler (rate limiter, total timeout, retry with jitter on transient faults for idempotent methods only, circuit breaker, attempt timeout) with options under `Resilience:<client>`, trace propagation, extended client logging with redaction, the outbound URL policy, and a response size cap.
- Database retries are EF Core's execution strategy only; framework loops (relay, runners) use backoff of their own; **retries exist at one layer per call path**.
- Non-HTTP dependencies use named `ResiliencePipeline`s (`Microsoft.Extensions.Resilience`).

## 8. Service defaults

The framework does not own an app's service defaults, but the solution template generates a `ServiceDefaults` project that: configures OpenTelemetry logs, metrics and traces with OTLP export when `OTEL_EXPORTER_OTLP_ENDPOINT` is set; adds `YAF.*` sources and meters, runtime and HTTP client instrumentation; applies application metadata; maps the three health endpoints; enables service discovery and the HTTP client defaults above; sets a parent-based ratio sampler from configuration.

## 9. Acceptance criteria

- An end-to-end test with in-memory exporters asserts for one HTTP command that triggers a domain event, an integration event and a job: the span tree and links described in §4, correlation id on every span and log record, no classified value in any attribute or log.
- Metrics tests with `MetricCollector<T>` for the outbox, jobs and security counters, and for `yaf.audit.unsealed` rising while the sealer is stopped and returning to zero after it runs.
- A consumer's and a job's log scope carry the system principal as `yaf.principal` and the pseudonymised user as `yaf.initiator`; the clear user id appears in no log record.
- Health tests: liveness ignores a down database; readiness fails on it; a stalled heartbeat fails liveness; parked jobs degrade readiness.
- The telemetry reference is generated and in sync (CI).
