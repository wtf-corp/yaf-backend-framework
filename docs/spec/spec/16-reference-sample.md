# 16 — Reference sample: `samples/conference`

A complete, runnable system built only on YAF packages, used three ways: as the documentation's source of code snippets, as the place every pattern is shown once and tested, and as the end-to-end test bed. Namespace root `YAF.Samples.Conference`. All data is synthetic.

## 1. Domain

A conference organiser (the **tenant**) publishes sessions; people register for sessions.

| Context | Service | Owns | HTTP style |
|---|---|---|---|
| **Catalog** | `catalog` | `Conference` (name, dates, venue), `Session` (title, abstract, speakers, schedule, capacity, seats, lifecycle) | Minimal APIs |
| **Registrations** | `registrations` | `Attendee` (personal data: name, email, dietary note), `Registration` (attendee, session, status), `RegistrationProcess` | Controllers |

The sample has no privacy coordinator service: the bounded context that orchestrates data subject requests across services comes with a later release of `YAF.Privacy`. Export and erasure are shown through the per-context hooks (`IDataSubjectOperations`), which `Registrations` exposes on two privileged endpoints and an end-to-end test calls.

Ubiquitous language per context is kept in `samples/conference/docs/<context>/ubiquitous-language.md`; the sample's own decisions in `samples/conference/docs/adr/`.

### 1.1 Flows that cross contexts

```
Organiser publishes a session            Catalog:  PublishSession → SessionPublished (domain) → SessionPublishedV1 (integration)
                                         Registrations: keeps a SessionSummary projection (idempotent consumer)

Person registers                         Registrations: SubmitRegistration → RegistrationSubmitted (domain)
                                           → RegistrationProcess starts → ReserveSeatRequestedV1
                                         Catalog: reserves a Seat or refuses → SeatReservedV1 | SeatReservationRejectedV1
                                         Registrations: process confirms or rejects the Registration; timeout after 10 min cancels and sends ReleaseSeatRequestedV1

Organiser cancels a session              Catalog: CancelSession → SessionCancelledV2 (and V1 side by side, to show versioning)
                                         Registrations: cancels registrations (fan-out as jobs), notifies via server-sent events

Attendee asks for erasure                Registrations: EraseAttendeeData (privileged) → IDataSubjectOperations.EraseAsync(subject)
                                           → anonymises registrations, deletes the attendee, clears the searchable hash,
                                             overwrites members stored in clear, destroys the subject key
                                         Catalog: holds nothing personal about attendees; its generated inventory shows that

Attendee asks for a copy                 Registrations: ExportAttendeeData (privileged) → IDataSubjectOperations.ExportAsync(subject)
```

## 2. Solution layout

```
samples/conference/
├── Conference.slnx
├── README.md                          # run instructions and the concept → file map (§3)
├── docs/                              # ubiquitous language, sample ADRs
├── src/
│   ├── AppHost/                       # Aspire: PostgreSQL, RabbitMQ and a storage emulator (default); LocalStack opt-in; development issuer
│   ├── ServiceDefaults/
│   ├── Contracts/
│   │   ├── Conference.Contracts.Catalog/
│   │   └── Conference.Contracts.Registrations/
│   └── Services/
│       ├── Catalog/        Domain/ Application/ Infrastructure/ Migrator/ Api/
│       └── Registrations/  Domain/ Application/ Infrastructure/ Migrator/ Api/
└── tests/
    ├── Catalog/        Domain.Tests/ Application.Tests/ Api.IntegrationTests/
    ├── Registrations/  Domain.Tests/ Application.Tests/ Api.IntegrationTests/
    └── EndToEnd.Tests/                # Aspire.Hosting.Testing
```

`Domain.Tests` and `Application.Tests` reference `YAF.Testing` only; `Api.IntegrationTests` and `EndToEnd.Tests` reference `YAF.Testing.Integration`. The `Infrastructure` projects reference `YAF.Privacy.EfCore` for legal holds and retention state.

Each `Api` project has `Requests/*.http` files that exercise every endpoint with the development tenant and either the development API key or a signed token from the AppHost's development issuer.

## 3. Concept coverage (the README's map; every row has a test)

| Concept | Where |
|---|---|
| Aggregate with factory, behaviours returning `Result`, invariants | `Catalog/Domain/Sessions/` |
| Builder for a many-input aggregate | `Catalog/Domain/Conferences/ConferenceBuilder.cs` |
| Typed ids, child entity ids assigned by the aggregate | `SessionId`, `SeatId` |
| Memento with child collection synced by id, typed members (ids, enum, smart enumeration, value objects), value object with own memento | `Session.Memento.cs` |
| Generated data class from the memento interface, with an EF-only member added in the other partial part | `Catalog/Infrastructure/Sessions/SessionData.cs` (`[MementoData]`) |
| Aggregate version bumped when only a child changes; concurrent seat reservations | `Catalog: ReserveSeat`, `Api.IntegrationTests/SeatLimitConcurrencyTests.cs` |
| Shipped value objects | `Money` (ticket price), `Period` (schedule), `EmailAddress` (attendee, speaker), `Percentage` (refund policy) |
| Smart enumeration | `SessionFormat` |
| State machine with Mermaid export | `Session.Lifecycle`, `Registration.Lifecycle` |
| Business rules, policy, domain service | `Rules/SeatLimitNotExceeded`, `ICancellationPolicy`, `ScheduleConflictDetector` |
| Specification used for querying and checking, written against typed members (no status strings) | `Specifications/OpenSessions` |
| Error catalogue with generated and explicit codes, approved file | `Session.Errors`, `tests/Catalog/Application.Tests/errors.approved.txt` |
| Command + validator + handler; query + read model; paged list with cursor | `Catalog/Application/Sessions/` |
| Authorization markers, capability catalogue, privileged permission with step-up | `Application/Authorization/Capabilities.cs`, `PublishSession` |
| User-scoped and anonymous requests | `Registrations: GetMyRegistrations`, `Catalog: GetPublicProgramme` |
| Resource-level authorization | `Registrations: CancelRegistration` (own registration or organiser) |
| Optimistic concurrency with `ETag` / `If-Match` | `Catalog: UpdateSession` |
| Idempotent command with `Idempotency-Key` | `Registrations: SubmitRegistration` |
| Domain event handler in the transaction, querying changes the command has just made | `Catalog/Application/Sessions/EventHandlers/` |
| Permission declared once on the request and derived at the edge; hand-written attribute on an endpoint that dispatches nothing | `Catalog: PublishSession`; `Catalog: DownloadSlides` |
| Integration events, V1 and V2 side by side, contract approval | `Contracts/…`, `PublishSessionCancelled` |
| Idempotent consumer through the inbox; projection | `Registrations/Application/Sessions/IntegrationEvents/` |
| Consumer and job running as the system actor with the user as initiator; `[SystemOnly]` command dispatched from a consumer | `Catalog: ReserveSeatOnRegistration` → `ReserveSeat` |
| Process manager with timeout and compensation | `Registrations/Application/Registrations/RegistrationProcess.cs` |
| Job with delay and quiet hours; job fan-out | `SendSessionReminder`, `CancelRegistrationsForSession` |
| Cron job with tenant fan-out | `CloseFinishedSessions` |
| Cross-tenant elevation (operator report) | `Catalog: CountSessionsAcrossTenants` |
| Additional tenants in one request | `Catalog: ListSessionsAcrossMyTenants` |
| Tenant isolation incl. row-level security tests | each `Api.IntegrationTests/TenantIsolationTests.cs` |
| Classified members, subject-key encryption, searchable hash | `Attendee.IMemento` (email searchable) |
| Personal member stored in clear with a reason, so a list can be sorted by it | `Attendee.IMemento.FamilyName` (`[StoredInClear("sorted attendee list")]`) |
| Read model with protected columns, materialised through the query helper | `Registrations: ListAttendees` (`ToUnprotectedListAsync`) |
| Member holding markup | `Session.IMemento.Abstract` (`[Markup]`, sanitized through `YAF.Markup.HtmlSanitizer`) |
| Secret member | `Conference.IMemento.WebhookSecret` |
| Retention and erasure actions; purge | `Registration.IMemento`, `Attendee.IMemento` |
| Export and erasure through the generated hooks; legal hold | `Registrations: ExportAttendeeData`, `EraseAttendeeData`; `EndToEnd.Tests/ErasureTests.cs` |
| Audit trail incl. sealing, chain verification and a partition dropped behind a checkpoint; read audit | `Attendee` (`[AuditRead]` on `GetAttendee`); `Api.IntegrationTests/AuditTrailTests.cs` |
| Business events | `registration.confirmed`, `session.published` |
| Security events on denial, elevation, privileged use | integration tests assert them |
| Logging: generated methods, `[LogProperties]`, `[LogPayload]`, log-leak test | `Registrations/Application/Attendees/` |
| Telemetry: spans, links, metrics asserted | `EndToEnd.Tests/TelemetryTests.cs` |
| Object storage with presigned URLs and API-streamed files | `Catalog: session slides` |
| Server-sent notifications | `Registrations: registration status stream` |
| Feature flag gating an endpoint | `Catalog: waitlist` |
| Two HTTP styles over the same application patterns | the two `Api` projects |
| Transport switch | AppHost parameter `UseLocalStack`; CI matrix |

Anything a pattern needs that the sample would otherwise not exercise is added to the sample, never left "for the reader".

## 4. Rules for the sample

- It is copied from: every pattern is the recommended one, with no shortcut that needs a disclaimer. If a shortcut is unavoidable, it carries a `// SAMPLE-ONLY:` comment and an entry in the sample's ADRs.
- Development stand-ins for privileges and API keys follow the template's rule (Development only).
- Documentation articles include sample files by reference; moving or renaming a sample file breaks the docs build, which is intended.
- The sample references YAF through project references inside the repository and builds in the main solution; a CI job additionally builds it against the **packed** packages to prove the packages are complete.

## 5. Acceptance criteria

- `dotnet run --project samples/conference/src/AppHost` brings the system up; the `.http` files run green in order.
- The AppHost starts from a clean clone with Docker and no account or token.
- All sample tests pass; `EndToEnd.Tests` passes for RabbitMQ on every run and for LocalStack when its token is available (reported as skipped otherwise).
- The README's concept map has no row without an existing file and test (checked by a small test that parses the table).
