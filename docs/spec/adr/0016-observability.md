# ADR-0016 — Observability

> **Status** Accepted · **Date** 2026-11 · **Deciders** maintainers

## Context

A request may cause domain events, outbox rows, messages in another service and jobs hours later. Operators need to follow that chain, and logs and traces leave the service, so they must not carry secrets or directly identifying data. The prior design used custom correlation and activity headers and allowed free-form logging next to a redaction path that only some call sites used.

## Drivers

- Standard signals any backend can ingest.
- One way to follow a user operation across synchronous and asynchronous hops.
- Classified data never reaches a sink in clear, whatever the sink.
- Names of spans, metrics and log events are stable and documented.
- Health endpoints that orchestrators can act on without restart storms.

## Options considered

| Option | For | Against |
|---|---|---|
| Custom headers and a logging library of choice | Familiar | Non-standard propagation; redaction depends on call-site discipline |
| OpenTelemetry with free-form `ILogger` calls | Standard | Extended redaction only applies to generated log methods |
| Parent/child spans across the bus | Single trace | Wrong for batches and long delays; misleading durations |
| **OpenTelemetry only, generated logging only, links for async work** | Standard and enforceable | Stricter authoring rules; messaging conventions still evolving |

## Decision

1. **OpenTelemetry is the only telemetry model.** The framework emits; the app's service defaults choose exporters. One `ActivitySource` and one `Meter` (via `IMeterFactory`) per package, named as the package.
2. **W3C trace context only.** No custom correlation or activity headers. `traceparent` is accepted and echoed as `traceresponse`.
3. **Correlation id** is a business-level id carried as W3C baggage item `yaf.correlation_id`, generated at the entry point when absent, stored in the execution context and on outbox rows, jobs and messages, and added to logs and spans. The log scope names the actor (`yaf.principal`) and, for asynchronous work started by a user, the initiator (`yaf.initiator`), both pseudonymised when they are persons. No other baggage is propagated by default; baggage never carries personal data.
4. **Logging is source generated.** `[LoggerMessage]` methods with stable event ids; `ILogger.Log*` extension calls are banned in `src` and in template service projects. The framework logs names, ids, codes, counts and durations, never payloads, results or composed exception messages. Objects are logged through `[LogProperties]` or `IRedactingFormatter`.
5. **One redaction policy for every sink**: logs, span attributes (`YafActivity.SetTag` with a classification), the configuration dump, problem-detail arguments, business and security event data. Secret → mask, sensitive personal → erase, personal → keyed hash, unknown → erase. The claim this supports is that telemetry holds **no directly identifying data**. A keyed hash and a user id remain pseudonymous personal data while the redaction key or the identity mapping exists, so telemetry is a store with a retention period in the data inventory, not one outside privacy considerations.
6. **Async work is correlated with span links**, not parenting: outbox publish, message processing and job execution link to the trace that caused them.
7. **Messaging semantic conventions** are followed but are not yet stable upstream, so attribute names live in one constants class.
8. **Outcome semantics.** Span status `Error` only for exceptions and `Failure`/`Unavailable` results; business failures set `yaf.request.outcome` and leave status unset.
9. **Catalogue.** Span, metric and log event names come from a `TelemetryCatalog` registry used by the code and rendered into `docs/articles/telemetry-reference.md`. Metrics carry no tenant or user dimensions.
10. **Health.** `/health/live` (process and loop heartbeats, no dependency checks), `/health/startup` (startup checks, migrations, topology), `/health/ready` (database, transport, not shutting down; *degraded* for parked items, lag, audit rows unsealed beyond `Audit:SealLagThreshold`, pending re-encryption, optional dependencies). The gauge `yaf.audit.unsealed` shows the rows committed and not yet linked into the audit chain.
11. **Resilience at one layer per call path.** HTTP clients get the standard resilience handler; database retries are the execution strategy; framework loops have their own backoff. No nested retries.
12. Sampling and buffering of low-severity logs are available and off by default; `Warning` and above are never sampled or dropped. HTTP logging is off by default and never logs bodies.

## Consequences

- Any OTLP backend works; the Aspire dashboard shows everything locally.
- Authors must write a log method per event; in exchange, redaction and event ids are uniform.
- A trace viewer shows async work as linked traces, which is accurate but needs a backend that renders links.
- A change in upstream messaging conventions is a one-file edit plus a documented telemetry change.
- Correlating by a person's identifier requires computing its keyed hash with the redaction key.
- Log retention must be set and documented by the operator, because pseudonymous identifiers in logs are personal data; the framework cannot erase from a log backend.

## Enforcement

- Banned API analyzer: `LoggerExtensions.Log*`, `Console.*` in library code.
- Analyzers `YAF12001` (unclassified type with `[LogProperties]`) and `YAF2007` (classified value interpolated into a log argument or exception message).
- End-to-end test with in-memory exporters asserting the span tree, links, correlation id everywhere and absence of classified values; `LogAssert.ContainsNo(value)` in the template.
- CI drift check of the generated telemetry reference.
- Health tests: liveness ignores a down database, readiness fails on it, a stalled heartbeat fails liveness, parked jobs and a stopped audit sealer degrade readiness.
- Startup check: redaction HMAC key present outside Development.

## Guidance for agents

- Add a `[LoggerMessage]` method to the feature's `Log` class; never call `logger.LogInformation` directly.
- Log ids, names and error codes. If you need an object in the log, classify its members and use `[LogProperties]`.
- Never put data values into exception messages.
- Set span attributes through `YafActivity.SetTag` with a classification; take names from `TelemetryCatalog`.
- Do not add tenant, user or any unbounded value as a metric dimension.
- Do not invent headers for correlation; the execution context already carries it.
- Do not describe logs or traces as free of personal data in docs or comments; say "no directly identifying data".
- Do not wrap a call in a retry if a lower layer already retries it.
- When you add a span, metric or log event, regenerate the telemetry reference.

## Related

0004, 0005, 0009, 0012, 0013, 0017, 0019, 0021
