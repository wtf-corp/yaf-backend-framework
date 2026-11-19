# 06 — Messaging: integration events, outbox, inbox, transports

## 1. Concepts

| Kind | Scope | Delivery | Defined in |
|---|---|---|---|
| Domain event | One bounded context | In-process, in the command's transaction | The service's domain |
| Integration event | Across contexts | Outbox → broker → inbox, at least once | A contracts project shared by producer and consumers |
| Job | One bounded context | Jobs store, at least once (`07-jobs-and-scheduling.md`) | The service's application layer |
| Business event | Analytics / timeline | Stored | The service |

Nothing is published to a broker directly from a handler. An analyzer (`YAF6001`) flags any use of `IMessageTransport` outside `YAF.Infrastructure` and adapters.

## 2. Integration event contracts

```csharp
[IntegrationEvent("conference.catalog.session-published", version: 1)]
public sealed record SessionPublishedV1(Guid SessionId, string Title, DateTimeOffset StartsAt, int Capacity)
    : IIntegrationEvent;
```

- The **type name on the wire** is the attribute's string plus version (`conference.catalog.session-published.v1`), never the CLR name, so renaming or moving the class changes nothing.
- A changed contract is a new type (`…V2`) next to the old one; the producer publishes both until consumers have moved. Additive optional members do not need a new version.
- Contracts live in `<App>.Contracts.<Context>` projects with no dependency other than `YAF.Core`. They contain primitives and ids, no domain types.
- Personal data in an integration event needs `[PersonalData]` on the member plus `[AllowPersonalData("reason")]` on the type; otherwise analyzer `YAF2003` fails the build. Prefer ids.
- A contracts project has an approval test of its JSON schema per event (`contracts.approved/…json`), so a breaking change is visible in review.

## 3. Publishing: the outbox

```csharp
public interface IIntegrationEventPublisher
{
    void Publish<T>(T integrationEvent, PublishOptions? options = null) where T : IIntegrationEvent;
}
public sealed record PublishOptions(string? PartitionKey = null, DateTimeOffset? NotBefore = null, string? DeduplicationId = null);
```

- `Publish` adds a row to `yaf_outbox` through the current unit of work; it throws outside one. It is typically called from a domain event handler in the application layer (`SessionPublished` → `SessionPublishedV1`). No base class or per-service writer type is needed.
- The row holds the **fully formed envelope** (§5), built at write time from the execution context: tenant, initiator (the user who started the operation, when there is one), correlation id, trace context of the causing request.

`yaf_outbox`: `id` (UUIDv7 = message id), `tenant_id`, `type`, `partition_key`, `envelope` (`jsonb`), `created_at`, `not_before`, `attempts`, `last_error`, `claimed_by`, `claimed_until`, `published_at`, `parked_at`.

- `yaf_outbox` and `yaf_inbox` carry `tenant_id` and are under row-level security like any tenant-owned table (`05-persistence.md` §4). The relay works under the service's system principal with an audited standing elevation, because it reads rows of every tenant.
- When the event type has classified members, the envelope's `data` is encrypted at rest in the row with key-ring purpose `messaging` and decrypted by the relay just before publishing. Published rows are removed after their retention (§3.1).

### 3.1 The relay

`OutboxRelay` (hosted service, one per context):

1. Claim a batch: `UPDATE yaf_outbox SET claimed_by, claimed_until WHERE id IN (SELECT id … WHERE published_at IS NULL AND not_before <= now AND (claimed_until IS NULL OR claimed_until < now) ORDER BY id FOR UPDATE SKIP LOCKED LIMIT @batch)`. Rows that share a `partition_key` with an earlier unpublished row are skipped, so order per key is kept across instances.
2. Publish each through `IMessageTransport.PublishAsync`, awaiting the broker's confirmation.
3. Mark `published_at` (retention job deletes rows older than `Outbox:Retention`, default 7 days) or record the failure: `attempts + 1`, `not_before = now + backoff(attempts)` (exponential with jitter, capped), keep `last_error` (message only, no payload).
4. After `Outbox:MaxAttempts` (default 20) the row is parked (`parked_at` set), a security-relevant operational event `yaf.outbox.parked` is raised and the health check reports degraded.

**Parked rows and order.** By default a parked row keeps blocking later rows of the same `partition_key`, because publishing them would break the order the publisher asked for. Readiness stays degraded until an operator releases the row (it is retried from attempt 0) or discards it (it is marked as never published, with an audit entry), through `IOutboxAdmin`, the framework's admin endpoint or the `yaf` CLI. Where order does not matter, an event type opts out with `[IntegrationEvent(..., OnParked = OnParked.SkipAndContinue)]` or `Outbox:OnParked:<type>`; later rows of the key are then published past the parked one. Rows without a partition key never block anything.

Wake-up: polling every `Outbox:PollInterval` (default 2 s), plus an immediate signal after a local commit, plus optional PostgreSQL `LISTEN/NOTIFY` (`yaf_outbox`) so other instances wake too. `LISTEN/NOTIFY` needs a session-mode connection: when the app's main connection string goes through a transaction-mode pooler, the relay listens on the direct one (`ConnectionStrings:<name>-direct`); without it the relay relies on polling alone, and says so in a startup log event. Change-data-capture is an extension point (`IOutboxSource`), not shipped.

Metrics: `yaf.outbox.pending` (gauge), `yaf.outbox.lag` (age of the oldest unpublished row, seconds), `yaf.outbox.published`, `yaf.outbox.failed`, `yaf.outbox.parked`.

## 4. Consuming: handlers and the inbox

```csharp
internal sealed class ReserveSeatOnRegistration(ISessionRepository sessions)
    : IIntegrationEventHandler<RegistrationSubmittedV1>
{
    public async ValueTask<Result> HandleAsync(RegistrationSubmittedV1 e, MessageContext ctx, CancellationToken ct) { … }
}
```

For each received message the consumer runtime:

1. Deserializes the envelope. An unreadable envelope, an unknown `type` with **no** registered handler in this service, or a payload that fails schema binding is handled by policy: unknown types are acknowledged and counted (`yaf.messaging.ignored`); malformed messages go to the dead-letter destination with a reason. A service built before an event existed therefore never blocks its queue.
2. Checks every string of the payload as the input guard does for commands (`04-application.md` §3): control characters other than tab, CR and LF, or invalid Unicode, dead-letter the message with reason `Input.InvalidCharacters`. The payload is never rewritten.
3. Builds the execution context, origin `integration-event`: `Actor` is the consuming service's system principal; `Tenant` and correlation id come from the envelope; `Initiator` comes from `yafinitiator` when present. It starts a `process` activity **linked** to the producer's trace context.
4. For each handler of that type, in its own DI scope and unit of work:
   - inserts `(consumer, message_id)` into `yaf_inbox` (unique). A conflict means "already handled": skip and acknowledge;
   - runs the handler; its effects, the inbox row, any outbox rows and process state commit together;
   - a failed `Result` or an exception rolls back, inbox row included.
5. Settles the message: `Ack` when all handlers succeeded or were duplicates; otherwise `Retry` (the transport's redelivery with backoff) until the transport's delivery limit, then dead-letter. A handler may return a result carrying `MessagingErrors.Poison` to dead-letter at once.

**Who a handler runs as.** A consumer never runs as the user named in the envelope. Authorization is decided on the `Actor`, the service's own system principal; the `Initiator` is a reference (kind and id) used for audit rows, stamps and correlation, and grants nothing. A handler is an entry point: it may work on repositories directly, or dispatch a request through `IDispatcher`, and then only a `[SystemOnly]` one (`[UserScoped]` requests are refused from a consumer). A handler that needs the initiating user's present rights asks `IPrivilegeSource` for that user explicitly. The broker is inside the trust boundary for the tenant id and the initiator (`09-security.md`); since the initiator carries no rights, a forged envelope cannot gain a user's permissions.

- `consumer` is a stable string from `[Consumer("catalog.reserve-seat")]` on the handler (default: handler type name; the attribute makes renames safe).
- Inbox rows are deleted after `Inbox:Retention` (default 14 days, MUST exceed the broker's maximum redelivery horizon; validated against transport metadata at startup where known).
- Handlers are still required to be safe under at-least-once delivery of *different* messages describing the same fact; the inbox only removes duplicates of the *same* message.
- Ordering: only per partition key and only when the transport supports it (capability `Ordering`). A handler that needs order checks a sequence or version carried in the event.

## 5. Envelope

CloudEvents 1.0, structured JSON mode, as the canonical form stored in the outbox:

```json
{
  "specversion": "1.0",
  "id": "0193b2c4-…",
  "source": "urn:yaf:conference:catalog",
  "type": "conference.catalog.session-published.v1",
  "time": "2026-11-18T10:15:30.123Z",
  "datacontenttype": "application/json",
  "subject": "0193…",
  "traceparent": "00-4bf9…-00f0…-01",
  "tracestate": "…",
  "yaftenant": "…",
  "yafinitiator": "user:0192…",
  "yafcorrelation": "…",
  "yafpartition": "…",
  "data": { "sessionId": "…", "title": "…" }
}
```

- Extension attribute names are lower-case alphanumeric as CloudEvents requires. `traceparent`/`tracestate` follow the distributed tracing extension. Baggage is **not** propagated by default.
- `yafinitiator` (kind and id of the user who started the operation) is optional: it is absent when the operation was started by a system principal. It is a pseudonymous identifier of a person, so it counts as personal data in whatever stores the envelope.
- Transports MAY use binary mode (attributes as native headers) where the broker has a CloudEvents binding (AMQP, Kafka); SNS/SQS uses structured mode in the body, because of its attribute limit.
- Serialization: `System.Text.Json`, camelCase, enums as strings, UTC ISO 8601 with milliseconds, source-generated contexts per contracts assembly, unknown members ignored on read (forward compatibility), strict on required members.
- Payload protection: members classified `[SecretData]` are refused at publish (`YAF2004`); personal data allowed by attribute is sent as is (transport encryption is required: TLS-only endpoints, checked at startup outside Development).
- **Claim check**: when the serialized envelope exceeds `Messaging:ClaimCheckThreshold` (default 200 KB) or the transport's `MaxMessageBytes`, `data` is stored through `IClaimCheckStore` (an `IObjectStore` bucket/prefix with lifecycle expiry) and replaced by `{"claimCheck": "<key>"}` with `datacontenttype: application/vnd.yaf.claim-check+json`.

## 6. Transport abstraction

```csharp
public interface IMessageTransport
{
    string Name { get; }                                   // "inmemory", "rabbitmq", "awssqs"
    TransportCapabilities Capabilities { get; }
    ValueTask PublishAsync(OutgoingMessage message, CancellationToken ct);       // completes when the broker confirmed
    ValueTask PublishBatchAsync(IReadOnlyList<OutgoingMessage> messages, CancellationToken ct);
    IAsyncDisposable Subscribe(SubscriptionSpec spec, MessageHandler onMessage);  // push; the transport owns polling
    ValueTask<TopologyReport> EnsureTopologyAsync(TopologyMode mode, CancellationToken ct);
    ValueTask<HealthCheckResult> CheckHealthAsync(CancellationToken ct);
}

public delegate ValueTask<Settlement> MessageHandler(IncomingMessage message, CancellationToken ct);
public enum SettlementKind { Ack, Retry, DeadLetter }

[Flags] public enum TransportCapabilities
{ None = 0, Ordering = 1, Deduplication = 2, NativeDelay = 4, LongDelay = 8, Batching = 16, DeadLetter = 32, Sessions = 64 }
```

- `OutgoingMessage`: id, type, body bytes, content type, headers, partition key, deduplication id, not-before. `IncomingMessage`: the same plus delivery count, enqueued time, native handle.
- **Topology model** (the same for every transport): one *topic* per producing context (`<app>.<context>.events`); one *queue/subscription* per consuming service, bound to the topics it listens to, filtered by `type` where the broker can filter. Names come from `MessagingOptions` (`App`, `Service`) and can be overridden per environment.
- `TopologyMode`: `Provision` (create what is missing; Development and tests), `Validate` (fail startup when something is missing; default elsewhere), `None`.
- Capabilities drive behaviour instead of hiding differences:
  - No `NativeDelay`/`LongDelay` for a requested delay → the relay holds the row (`not_before`) and publishes when due.
  - `Ordering` requested by a publisher (`PartitionKey` set) on a transport without it → startup error unless `Messaging:AllowUnordered` is set.
  - No `DeadLetter` → the runtime parks the message in `yaf_inbox_dead` instead.
- Selecting a transport is one registration plus configuration; application code is unchanged:

```csharp
builder.AddYafMessaging(m => m
    .UseOutbox<CatalogDbContext>()
    .UseInbox<CatalogDbContext>()
    .PublishEventsFrom(typeof(SessionPublishedV1).Assembly)
    .AddHandlersFrom<ApplicationModule>()
    .UseTransportFromConfiguration());       // Messaging:Transport = InMemory | RabbitMq | AwsSqs
```

Each transport package registers itself under its name with keyed DI; `UseTransportFromConfiguration` resolves the configured key and fails at startup naming the missing package when it is not referenced.

### 6.1 Transport mapping

The first release ships three transports: in-memory (in `YAF.Infrastructure`), RabbitMQ (`YAF.Messaging.RabbitMq`) and AWS SNS + SQS (`YAF.Messaging.AwsSqs`).

| | In-memory | RabbitMQ | AWS SNS + SQS |
|---|---|---|---|
| Topic | in-process channel | topic exchange | SNS topic (FIFO when ordering is on) |
| Consumer queue | channel per service | quorum queue bound by routing key = type | SQS queue subscribed with raw delivery and a `type` filter policy |
| Partition key | honoured | consistent-hash exchange or single active consumer | `MessageGroupId` |
| Dedup id | honoured | none (inbox only) | `MessageDeduplicationId` (FIFO) |
| Delay | honoured | held by the relay (no plugin required) | `DelaySeconds` up to 15 min; longer held by the relay |
| Retry | immediate, bounded | nack with requeue through a delay queue (TTL + DLX) | visibility timeout with backoff; extended while handling |
| Dead letter | in-memory list (inspectable in tests) | DLX, `x-delivery-limit` | redrive policy to a DLQ |
| Publish confirm | n/a | publisher confirms | HTTP 200 |
| Max size | unlimited | configured | from quota metadata |
| Local run | built in | RabbitMQ container, the dev host's default (`14-dev-host-and-templates.md`) | LocalStack, opt-in |

**Later release.** `YAF.Messaging.AzureServiceBus` (topics, subscriptions with SQL or correlation filters, `SessionId` as partition key, `MessageId` duplicate detection, `ScheduledEnqueueTime`, the built-in dead-letter queue) is planned and not part of 1.0. The contract, the capability flags (`Sessions` exists for it) and the conformance suite are written so that it, or an app's own transport, is added as a package without a breaking change.

The AWS adapter uses the AWS SDK clients directly (not a higher-level messaging framework), so envelope, retries and settlement stay YAF's. Credentials come from the SDK's default chain; LocalStack is enabled by configuration (`Aws:LocalStack:Enabled`, endpoint) only when the environment is Development or Testing.

The in-memory transport is a real implementation of the contract (delivery count, retry, dead-letter list, ordering), isolated per host instance, with test hooks `WaitForIdleAsync()` and `DeadLetters`.

### 6.2 Transport conformance suite

`TransportConformance` in `YAF.Testing.Integration` is an abstract xUnit suite every adapter's test project derives from (an app's own transport can derive from it too), run against the real broker in a container: publish/receive round trip, at-least-once redelivery after handler failure, dead-lettering after the limit, poison message, ordering per key (if capable), delay (if capable), topology validate/provision, large message via claim check, health check, graceful shutdown (in-flight messages are finished or returned).

## 7. Graceful shutdown and back-pressure

On `ApplicationStopping`: readiness fails, subscriptions stop taking new messages, in-flight handlers get `Messaging:ShutdownGrace` (default 20 s), then their messages are returned for redelivery. Concurrency per subscription is bounded (`MaxConcurrency`, default 8; 1 per partition key when ordered). The relay stops claiming and releases its claims.

## 8. Acceptance criteria

- Outbox: a rolled-back command publishes nothing; a committed one is published exactly once under normal operation and at least once when the relay crashes between publish and mark; two relays never publish the same row concurrently; per-key order holds with two relays; parking after max attempts; a parked row blocks its partition key until released or discarded, and does not block it with `SkipAndContinue`; the relay publishes rows of several tenants while a request of tenant A cannot read tenant B's outbox rows; a classified payload is not readable in the row.
- Inbox: the same message delivered twice changes state once; handler failure leaves no inbox row; two handlers for one event are independent.
- Envelope: golden-file test of the JSON; trace link from consumer span to producer span.
- Identity on consume: the handler's `Actor` is the service's system principal and its `Initiator` is the publishing user; an envelope forged with another user's id gains none of that user's permissions; dispatching a `[UserScoped]` request from a handler is refused; a payload with a control character is dead-lettered unchanged.
- Conformance suite green for in-memory and RabbitMQ on every run, and for SQS (LocalStack) when the LocalStack token is available to the run; otherwise that suite is reported as skipped, never as passed.
- Switching the sample between transports changes configuration only (a CI matrix job runs the sample's messaging tests per transport).
