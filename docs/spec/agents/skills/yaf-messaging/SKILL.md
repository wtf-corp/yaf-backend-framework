---
name: yaf-messaging
description: How YAF services talk across bounded contexts - integration event contracts and versioning, publishing through the outbox, consumers and the inbox, idempotency and ordering, the CloudEvents envelope, choosing and configuring a transport (in-memory, RabbitMQ, AWS SNS/SQS), the claim check, and writing a new transport adapter against the conformance suite. Load BEFORE adding or changing an integration event record, an IIntegrationEventHandler, anything in a Contracts project, messaging configuration, or a YAF.Messaging.* adapter; and before publishing anything to a broker.
license: MIT
metadata:
  owner: yaf
  version: "1"
---

# YAF messaging

Inside a context: domain events, in the transaction. Across contexts: integration events, outbox → broker → inbox, at least once.

## When to load

- Defining, versioning or consuming an integration event.
- Publishing from a domain event handler.
- Changing `Messaging:*`, `Outbox:*` or `Inbox:*` configuration, or switching transport.
- Writing or changing a transport adapter.
- Debugging duplicates, ordering, parked or dead-lettered messages.

## Where does this go?

| The thing is… | It goes in |
|---|---|
| A fact other contexts may react to | `[IntegrationEvent("<app>.<context>.<name>", version: n)]` record in `<App>.Contracts.<Context>` |
| Mapping a domain event to an integration event | `IDomainEventHandler<T>` in the producer's application layer calling `IIntegrationEventPublisher.Publish` |
| Reaction in another service | `IIntegrationEventHandler<T>` in that service's `Application/<Aggregates>/IntegrationEvents/` |
| Deferred work in the same service | A job, not a message (`yaf-jobs-and-processes`) |
| Multi-step coordination with timeouts | A process manager (`yaf-jobs-and-processes`) |
| Broker-specific code | Only inside a `YAF.Messaging.<Broker>` adapter |

## Rules

1. **REQUIRED** Nothing is published to a broker from application code. Use `IIntegrationEventPublisher.Publish` inside a unit of work; `IMessageTransport` outside `YAF.Infrastructure` and adapters is YAF6001.
2. **REQUIRED** The wire name is the attribute string plus version (`conference.catalog.session-published.v1`), never the CLR type name. Do not change it once released.
3. **REQUIRED** A changed contract is a new type (`…V2`) beside the old one; publish both until consumers have moved. Adding an optional member needs no new version.
4. **REQUIRED** Contracts projects depend only on `YAF.Core` and contain primitives and ids, no domain types.
5. **REQUIRED** No `[SecretData]` in a contract (YAF2004). Personal data needs `[property: PersonalData(...)]` on the member **and** `[AllowPersonalData("reason")]` on the type (YAF2003). Prefer ids.
6. **REQUIRED** Every `string` and free-form member of a contract is classified (YAF2001).
7. **REQUIRED** Handlers return `Result`. A failed result or exception rolls back the handler's effects *and* its inbox row, and the message is retried, then dead-lettered.
8. **REQUIRED** Give every consumer a stable name: `[Consumer("catalog.reserve-seat")]`. The inbox key is `(consumer, message id)`.
9. **REQUIRED** Handlers tolerate different messages about the same fact and out-of-order arrival; the inbox only removes duplicates of the same message. Carry a version or sequence in the event when order matters.
10. **REQUIRED** Use `PublishOptions.PartitionKey` (usually the aggregate id) when per-entity order matters. A transport without `Ordering` fails startup unless `Messaging:AllowUnordered` is set.
11. **REQUIRED** A consumer has no effect outside its unit of work. External calls go in a job scheduled by the handler. A consumer may dispatch requests through `IDispatcher`, and only `[SystemOnly]` ones.
12. **REQUIRED** Changing a contract updates its schema approval file under `contracts.approved/`.
13. **REQUIRED** Topology mode is `Provision` only in Development and tests; elsewhere `Validate` (default) or `None`.
14. **REQUIRED** A new transport adapter derives its tests from the transport conformance suite in `YAF.Testing.Integration` and passes them against the real broker in a container; it declares honest `TransportCapabilities`.
15. **RECOMMENDED** Keep payloads small (ids and the facts consumers need). Large payloads use the claim check automatically above `Messaging:ClaimCheckThreshold`.
16. **RECOMMENDED** Return a result carrying `MessagingErrors.Poison` for a message that can never succeed, instead of burning retries.
17. **REQUIRED** A consumer runs as the service's system principal (`Actor`) in the tenant of the envelope (`yaftenant`). The user who caused the event arrives as `Initiator` (envelope attribute `yafinitiator`) for stamps, audit and correlation; it grants nothing. Never decide access on it, and never treat an envelope value as proof of a user's rights.
18. **REQUIRED** Payloads are checked like commands (control characters and invalid Unicode are refused, nothing is rewritten). A payload with classified members is encrypted in the outbox at rest by the framework; that is no reason to put more personal data in it.

## How to

Producer:

1. Contract record + schema approval file.
2. Test: command commits → one outbox row; command fails → none.
3. Domain event handler that publishes.

Consumer:

1. Test with the in-memory transport: deliver twice → state changes once.
2. Handler with `[Consumer]`.
3. Register: `AddYafMessaging(m => m.UseOutbox<TDb>().UseInbox<TDb>().PublishEventsFrom(...).AddHandlersFrom<ApplicationModule>().UseTransportFromConfiguration())`.

```csharp
[IntegrationEvent("conference.registrations.registration-submitted", version: 1)]
public sealed record RegistrationSubmittedV1(Guid RegistrationId, Guid SessionId) : IIntegrationEvent;

[Consumer("catalog.reserve-seat")]
internal sealed class ReserveSeatOnRegistration(ISessionRepository sessions, IIntegrationEventPublisher publisher)
    : IIntegrationEventHandler<RegistrationSubmittedV1>
{
    public async ValueTask<Result> HandleAsync(RegistrationSubmittedV1 e, MessageContext ctx, CancellationToken ct)
    {
        var session = await sessions.FindAsync(SessionId.From(e.SessionId), ct);
        if (session is null) return MessagingErrors.Poison;

        var seat = session.ReserveSeat(e.RegistrationId);
        if (seat.IsFailure)
        {
            publisher.Publish(new SeatReservationRejectedV1(e.RegistrationId, seat.FirstError.Code));
            return Result.Success();                      // a business "no" is a handled message
        }

        await sessions.UpdateAsync(session, ct);
        return Result.Success();
    }
}
```

Switching transport is configuration plus a package reference:

```json
{ "Messaging": { "Transport": "RabbitMq", "Topology": "Validate" } }
```

## Transport cheat sheet

| | Ordering | Dedup | Native delay | Dead letter | Local run |
|---|---|---|---|---|---|
| In-memory | yes | yes | yes | in-memory list | built in; what tests use |
| RabbitMQ | consistent hash / single active consumer | inbox only | held by the relay | DLX + `x-delivery-limit` | container; the AppHost default |
| AWS SNS + SQS | FIFO (`MessageGroupId`) | FIFO | up to 15 min, longer held by the relay | redrive policy | LocalStack, opt-in (needs an account token) |

Azure Service Bus is planned for a later release; the transport contract and the conformance suite already allow for it.

Operational notes:

- A parked outbox row blocks later rows with the same partition key, degrades readiness and raises `yaf.outbox.parked`, until an operator releases or discards it. Set `OnParked = SkipAndContinue` for an event type only when its order does not matter.
- The relay uses `LISTEN/NOTIFY`, which needs a session-mode connection. Behind a transaction-mode pooler configure `ConnectionStrings:<name>-direct`; without it the relay polls.

## Anti-patterns

| Wrong | Why | Instead |
|---|---|---|
| Publishing from a command handler via the SDK | Survives a rollback; lost on a crash | Outbox through `IIntegrationEventPublisher` |
| Editing `SessionPublishedV1` to rename a member | Breaks running consumers | Add `SessionPublishedV2`, publish both |
| Using `typeof(T).FullName` as the type on the wire | Rename or move breaks consumers | The attribute's name |
| Attendee name and email in an event "for convenience" | Spreads personal data to every subscriber | Send `AttendeeId`; consumers ask or already know |
| Handler that sends an email directly | Runs again on redelivery | Schedule a job |
| "Already handled?" check with a `SELECT` then insert | Races under concurrent delivery | Rely on the inbox unique row in the same transaction |
| Assuming V1 arrives before V2 or A before B | Only per-key order, and only if supported | Partition key + version check |
| `Topology: Provision` in production | App identity needs broker admin rights | IaC provisions; app validates |
| Mocking `IMessageTransport` in tests | Skips envelope, retry, inbox | In-memory transport with `WaitForIdleAsync()` |

## Verify

- `dotnet build YAF.slnx` (YAF2001, YAF2003, YAF2004, YAF6001).
- `dotnet test --project <service>/tests/Application.Tests` and the API/integration tests: rollback publishes nothing; duplicate delivery is idempotent; in the handler the tenant, initiator and correlation id come from the envelope and the actor is the system principal.
- Contract schema approval files updated on purpose.
- For an adapter: its conformance suite green against the containerised broker (the SNS/SQS suite runs only when the LocalStack token is present and is reported as skipped otherwise).
- Health: no parked outbox rows after the test run (`yaf.outbox.parked`).

## References

- `docs/articles/messaging.md` — outbox relay, inbox, envelope fields, capabilities, configuration keys.
- `docs/articles/transports/<broker>.md` — per-broker setup and limits; read before configuring one.
- `adr/0012-messaging-outbox-inbox-and-transports.md`.
- `samples/conference/` — `Contracts.*` projects, Catalog `EventHandlers/`, Registrations `IntegrationEvents/`.
- Skills: `yaf-jobs-and-processes`, `yaf-privacy-and-data-classification`.
