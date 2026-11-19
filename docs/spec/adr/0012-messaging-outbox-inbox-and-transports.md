# ADR-0012 — Messaging: outbox, inbox and pluggable transports

> **Status** Accepted · **Date** 2026-11 · **Deciders** maintainers

## Context

Bounded contexts talk to each other through integration events. Delivery must survive crashes on either side, must not publish what a rolled-back command produced, and must not tie application code to one broker. The prior design had one cloud's topic/queue pair behind a thin wrapper, an envelope holding only type and payload, optimistic "checked out" flags for claiming, and left idempotency to each handler.

## Drivers

- A committed change and its event are atomic; a rolled-back change publishes nothing.
- At-least-once delivery with duplicates removed by the framework, not by every handler author.
- Broker choice is configuration plus one package reference: RabbitMQ, AWS SNS/SQS, or none; further brokers (Azure Service Bus first) can be added later as packages.
- No commercial licence and no second pipeline competing with YAF's unit of work.
- Contracts that survive class renames and evolve without breaking consumers.
- Trace context, tenant and the initiating user travel with every message, without the message being able to grant that user's rights to anyone.

## Options considered

| Option | For | Against |
|---|---|---|
| A full messaging framework (bus, sagas, outbox included) | Mature, many transports | The leading ones are commercially licensed or bring their own handler model, middleware and outbox, which would replace YAF's pipeline and unit of work; lock-in at the application layer |
| Direct publish from handlers | Simple | Dual write: event sent although the transaction rolls back, or lost after commit |
| Change data capture from the WAL | Low latency, commit order | Replication slots, WAL retention, privileges that managed databases restrict; operationally heavy as a default |
| **Own outbox + inbox over a small transport port** | Atomic, portable, testable, no licence | We own relay, retry and conformance code |
| Consumers run as the user named in the envelope | Per-user permission checks in asynchronous work | Anyone who can publish to the broker acts as any user unless envelopes are signed, which needs key distribution between services; work fails when a user is deactivated mid-flow; rights are used that may have been revoked since |
| All three brokers in the first release | Widest reach on day one | Three adapters and three container setups to keep green before the contract has met real use; the Service Bus emulator has its own limits |

## Decision

1. **Contracts.** An integration event is a record implementing `IIntegrationEvent` with `[IntegrationEvent("<app>.<context>.<name>", version: n)]`. The wire type is that string plus `.v<n>`, never the CLR name. A changed contract is a new version type published side by side; additive optional members need no new version. Contracts live in `<App>.Contracts.<Context>` projects depending only on `YAF.Core`.
2. **Outbox.** `IIntegrationEventPublisher.Publish` adds a row to `yaf_outbox` through the current unit of work and throws outside one. The row stores the **fully formed envelope**, built at write time from the execution context of the causing request. `yaf_outbox` and `yaf_inbox` carry `tenant_id` and are under row-level security; the relay works under the service's system principal with an audited standing elevation. A payload with classified members is encrypted at rest in the row with key-ring purpose `messaging`.
3. **Envelope.** CloudEvents 1.0, structured JSON as the canonical form: `id`, `source`, `type`, `time`, `subject`, `traceparent`/`tracestate`, and extension attributes `yaftenant`, `yafinitiator`, `yafcorrelation`, `yafpartition`. Transports may use binary mode where the broker has a CloudEvents binding; SNS/SQS uses structured mode.
4. **Relay.** One `OutboxRelay` per context claims batches with `FOR UPDATE SKIP LOCKED`, skipping rows whose partition key has an earlier unpublished row (order per key across instances), publishes with broker confirmation, marks `published_at`, backs off with jitter on failure and parks a row after `Outbox:MaxAttempts`. A parked row keeps blocking later rows of its partition key, degrades readiness and raises `yaf.outbox.parked`, until an operator releases or discards it; an event type whose order does not matter opts out with `OnParked = SkipAndContinue`. Wake-up: polling, a local post-commit signal, optional `LISTEN/NOTIFY` on a session-mode connection (`ConnectionStrings:<name>-direct` when the main one goes through a transaction-mode pooler; polling alone otherwise). CDC stays an extension point (`IOutboxSource`).
5. **Inbox.** For each handler the runtime inserts `(consumer, message_id)` into `yaf_inbox` in the handler's unit of work. A unique violation means "already handled". Handler effects, inbox row, outbox rows and process state commit together; a failure leaves no inbox row. `consumer` is a stable name from `[Consumer]`.
6. **Identity on consume.** A consumer runs with `Actor` = the consuming service's system principal and `Initiator` = the reference from `yafinitiator`, in the tenant from `yaftenant`. Authorization is decided on the actor; the initiator is for audit, stamps and correlation and grants nothing. A handler may dispatch requests through `IDispatcher`, and only `[SystemOnly]` ones. The broker is inside the trust boundary for tenant id and initiator; because the initiator carries no rights, a forged envelope cannot gain a user's permissions. Envelope signing is a possible later addition.
7. **Payload check.** Strings in a consumed payload get the same check as command input: control characters (other than tab, CR, LF) and invalid Unicode dead-letter the message with `Input.InvalidCharacters`. Nothing is rewritten.
8. **Transport port.** `IMessageTransport` (publish, batch publish, subscribe with `Ack | Retry | DeadLetter` settlement, topology, health) plus `TransportCapabilities` flags (`Ordering`, `Deduplication`, `NativeDelay`, `LongDelay`, `Batching`, `DeadLetter`, `Sessions`). Capabilities drive behaviour: a missing delay is emulated by the relay's `not_before`; requested ordering on a transport without it is a startup error unless `Messaging:AllowUnordered`.
9. **Topology.** One topic per producing context, one queue or subscription per consuming service, filtered by type where the broker can. Modes: `Provision` (Development, tests), `Validate` (default elsewhere), `None`.
10. **Claim check.** Envelopes above `Messaging:ClaimCheckThreshold` or the transport's maximum store `data` through `IClaimCheckStore` and carry a reference.
11. **Shipped transports.** In the first release: in-memory (a real implementation with delivery counts, retry, dead letters and test hooks), RabbitMQ (quorum queues, publisher confirms) and AWS SNS + SQS (AWS SDK clients directly, so envelope and settlement stay YAF's). Selected by `Messaging:Transport` through keyed services. Azure Service Bus is planned for a later release as `YAF.Messaging.AzureServiceBus`; the port, the capability flags and the conformance suite are designed so that it is an added package and not a breaking change.
12. **Unknown and malformed messages.** A type with no handler in this service is acknowledged and counted; an unreadable or schema-invalid message is dead-lettered with a reason.

## Consequences

- Application code is identical on every broker; switching is configuration.
- An app on Azure Service Bus has no shipped adapter in the first release: it writes its own against the port and the conformance suite, or waits.
- A consumer cannot rely on the publishing user's permissions; a use case that needs them asks the privilege source explicitly, and work continues for a user who has been deactivated since unless the handler checks.
- A parked message stops its partition key until someone acts; that is the price of keeping order, and it needs an operator procedure and an alert.
- The broker is trusted for tenant id and initiator. A party able to publish to it can make a service do system-level work in any tenant; network and broker access control are part of the security boundary, and the threat model says so.
- The low-latency wake-up needs a session-mode database connection; behind a transaction-mode pooler without a direct connection string the relay polls.
- Publishing is delayed by the relay interval unless the post-commit signal fires; lag is a metric and a readiness input.
- Exactly-once is not promised. Handlers must still tolerate different messages describing the same fact, and ordering exists only per partition key on capable transports.
- Each new transport must pass the conformance suite, which needs the real broker in a container.
- The inbox retention must exceed the broker's redelivery horizon.

## Enforcement

- Analyzer `YAF6001`: `IMessageTransport` used outside `YAF.Infrastructure` and adapters.
- Analyzers `YAF2003` / `YAF2004`: personal data in an event without `[AllowPersonalData]`; secret data in an event.
- `TransportConformance` in `YAF.Testing.Integration`, run per adapter against the real broker; a CI matrix runs the sample's messaging tests per transport (the SNS/SQS rows when the LocalStack token is present, reported as skipped otherwise).
- Security pack suite `AsyncIdentity`: a forged initiator gains no permission; a `[UserScoped]` request dispatched from a consumer is refused.
- Database test: `yaf_outbox` and `yaf_inbox` have row-level security policies; a classified payload is not readable in the row.
- Contracts approval test (`contracts.approved/*.json`) per contracts project; golden-file test of the envelope.
- Startup checks: configured transport package present, topology valid, TLS endpoints outside Development, inbox retention against transport metadata.
- Architecture test: no vendor SDK outside its adapter package.

## Guidance for agents

- Publish only through `IIntegrationEventPublisher`, from a domain event handler in the application layer. Never inject a transport or broker client.
- Never edit a published contract type; add `…V<n+1>` and publish both.
- Give every event a wire name and every handler a `[Consumer]` name; do not rely on class names.
- Put ids in events, not personal data. If personal data is unavoidable, classify it and add `[AllowPersonalData("reason")]`.
- A handler runs as the service, not as the user in the envelope. Dispatch only `[SystemOnly]` requests from it; read `Initiator` for audit, never for a permission decision.
- Never build or replace an execution context in a handler; the runtime has built it.
- Set `OnParked = SkipAndContinue` only for an event whose consumers do not depend on order; say why in the spec.
- Do not write your own "already processed" table; the inbox does it. Do make handlers safe for re-ordered or semantically repeated facts.
- When adding a transport, derive its tests from the conformance suite first and declare capabilities honestly.

## Related

0002, 0003, 0008, 0009, 0010, 0013, 0016, 0018, 0019
