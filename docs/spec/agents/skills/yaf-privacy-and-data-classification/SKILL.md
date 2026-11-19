---
name: yaf-privacy-and-data-classification
description: Classifying data in a YAF service and what follows from it - the classification attributes, encryption at rest and its opt-out, redaction in logs and errors, retention policies, erasure actions, personal data exporters and erasers, data subject operations, the rules for personal data in events, jobs and logs, the generated data inventory, and the compliance claims that must never be made. Load BEFORE adding a string, byte[] or free-form member to a memento interface, request, response, integration event, job, process state or options class; before storing or logging anything about a person; before adding a soft-deletable or personal-data type; and before writing docs or comments that mention GDPR or compliance.
license: MIT
metadata:
  owner: yaf
  version: "1"
---

# YAF privacy and data classification

You declare what data is; the framework acts on it (encryption at rest, redaction, retention, export, erasure, inventory). An unclassified member fails the build.

## When to load

- Adding a member that could hold text, bytes or free-form data.
- Modelling anything about a person (attendee, speaker, user).
- Putting data into an integration event, job payload, process state, log or error.
- Adding retention, soft delete or a legal-hold-sensitive type.
- Writing about privacy or compliance in docs, README or PR descriptions.

## Which classification?

First match wins. When unsure between two, choose the stricter.

| The value… | Attribute | At rest | In logs |
|---|---|---|---|
| Grants access: password, token, key, connection string, presigned URL | `[SecretData]` | key-ring encrypted | `***` |
| Special category, financial account data, precise location, a person's credentials | `[SensitivePersonalData(category)]` | subject-key encrypted | erased |
| Identifies or describes a person: name, email, phone, address, identifiers, online ids | `[PersonalData(category, Purpose = "…")]` | subject-key encrypted (clear only with `[StoredInClear("reason")]`) | keyed hash |
| Business data, not about a person | `[InternalData]` | clear | as is |
| Ids, codes, statuses, published content | `[PublicData]` | clear | as is |

## Rules

1. **REQUIRED** Classify every `string`, `byte[]`, `JsonElement`, dictionary and unknown-shaped member of memento interfaces, integration events, job payloads, process state, options classes and `[LogPayload]` types (YAF2001).
2. **REQUIRED** On positional records target the property: `[property: PersonalData(...)]` (YAF2005).
3. **REQUIRED** `Purpose` names an entry of the service's `IPurposeCatalog`. A startup check and a test verify it (the build cannot see the catalogue), so run the service's tests after adding a purpose.
4. **REQUIRED** A memento with personal data implements `IDataSubjectOwnedMemento` (or `[NoDataSubject("reason")]`) and has `[Retention(...)]` (YAF2002). Soft-deletable types need retention too: it is their purge path.
5. **REQUIRED** Each personal member has an erasure action; the default is delete. `[OnErasure(ErasureAction.Keep, Reason = "…")]` must name the legal basis.
6. **REQUIRED** No personal data in integration events unless the member is classified and the type has `[AllowPersonalData("reason")]` (YAF2003). No `[SecretData]` in events or jobs (YAF2004). Prefer ids.
7. **REQUIRED** Never interpolate a classified value into a log template argument or an exception message (YAF2007). Log ids; log objects only via `[LogProperties]` or `IRedactingFormatter`. A keyed hash of a personal value, and a user id, are still personal data (pseudonymous): they fall under the retention period of the store they sit in (logs, stamps, audit rows, envelopes, job rows). Do not write that such stores "contain no personal data"; say "no directly identifying data".
8. **REQUIRED** Mark a member that holds text a person types freely (notes, comments, descriptions) `[FreeText]`. A `[FreeText]` member is `[PersonalData]`, or states `[FreeText(ContainsPersonalData = false, Justification = "…")]` (YAF2006 checks marked members). Whether an unmarked string is free text is a review question: reviewers ask it for every new string a user fills in.
9. **REQUIRED** Personal data lives in entities (classified memento members), not in value objects' free text, cache keys, object-store keys, URLs or metric dimensions.
10. **REQUIRED** Every data-subject-owned type has an exporter and an eraser; the generated defaults suffice unless you need special handling (architecture test).
11. **REQUIRED** Looking up by a protected value uses `[Searchable]`: the value stays encrypted and gets a keyed-hash column for equality, which erasure clears. No clear-text shadow columns. When the store must sort, range-filter or prefix-search a `[PersonalData]` member, mark it `[StoredInClear("reason")]`: it is then kept unencrypted, listed with its reason in the data inventory, and erased by overwrite instead of crypto-shredding. It needs human review and is an error on `[SensitivePersonalData]` and `[SecretData]` (YAF2008).
12. **REQUIRED** Mark processing that a person may restrict or object to with `[ProcessingPurpose("…")]` on the request.
13. **REQUIRED** Test data is synthetic. Never copy production data into tests, fixtures, docs or prompts.
14. **REQUIRED** Regenerate and commit `docs/compliance/data-inventory.md` when classification, retention or erasure metadata changes.
15. **REQUIRED** Never write that YAF or an app is "GDPR/NIS2/ISO 27001/AI Act compliant" or "certified", that crypto-shredding is recognised erasure, or that the audit trail is tamper-proof. Say: "supports", "tamper-evident", "renders data permanently unintelligible".
16. **RECOMMENDED** Collect less: question every new personal member before adding it.
17. **RECOMMENDED** Mark members the person supplied with `ProvidedBySubject = true` so portability exports are correct.

## How to: add personal data to an aggregate

1. Test first: the log/inventory tests and an erasure test for the type.
2. Classify, name the data subject, set retention and erasure.
3. Build; regenerate the inventory; check the stored value is ciphertext in an integration test.

```csharp
public partial class Attendee
{
    [Retention("P2Y", After = RetentionAnchor.Member, Member = nameof(LastRegistrationClosedAt),
               Basis = "contract", Action = RetentionAction.Delete)]
    public interface IMemento : IAggregateMemento, IDataSubjectOwnedMemento
    {
        [PersonalData(PersonalDataCategory.Identity, Purpose = "registration", ProvidedBySubject = true)]
        string FullName { get; set; }

        [PersonalData(PersonalDataCategory.Contact, Purpose = "registration", ProvidedBySubject = true)]
        [Searchable]
        string Email { get; set; }

        [SensitivePersonalData(PersonalDataCategory.Health)]
        [OnErasure(ErasureAction.Delete)]
        string? DietaryNeeds { get; set; }

        AttendeeStatus Status { get; set; }                  // enum: nothing to classify
        DateTimeOffset? LastRegistrationClosedAt { get; set; }
    }
}

// request: note [property: …]
public sealed record RegisterAttendeeCommand(
    [property: PersonalData(PersonalDataCategory.Identity, Purpose = "registration")] string FullName,
    [property: PersonalData(PersonalDataCategory.Contact, Purpose = "registration")] string Email,
    SessionId SessionId) : ICommand<Result<RegistrationId>>;

// event: ids only
[IntegrationEvent("conference.registrations.registration-submitted", version: 1)]
public sealed record RegistrationSubmittedV1(Guid RegistrationId, Guid SessionId) : IIntegrationEvent;
```

## What happens to a data subject request

| Request | Mechanism you rely on |
|---|---|
| Access / portability | Generated `IPersonalDataExporter` per type; `IDataSubjectOperations.ExportAsync` assembles this service's encrypted export |
| Erasure | `IDataSubjectOperations.EraseAsync`: legal hold and `Keep` reasons checked → rows deleted or anonymised, clear-stored members overwritten, hash columns cleared → subject key destroyed and its cache entry evicted → `privacy.subject-erased.v1` → receipt without personal data |
| Rectification | Your ordinary update command; set `PropagateRectification = true` where other contexts must follow |
| Restriction / objection | `IProcessingRestrictions` + `[ProcessingPurpose]` |

A service participates by calling `AddYafPrivacy()` (and maps what `YAF.Privacy` stores, legal holds and retention state, with `YAF.Privacy.EfCore`); override a generated exporter or eraser only for a real special case, with a test. The app calls `IDataSubjectOperations` in each service that holds the person's data. A coordinator that records a data subject request and runs it across services is planned for a later release; do not build a substitute inside a service.

## Anti-patterns

| Wrong | Why | Instead |
|---|---|---|
| `[InternalData] string Notes` typed by users | Free text will contain personal data | `[FreeText]` with `[PersonalData]`, or a justified `[FreeText(ContainsPersonalData = false, …)]` (YAF2006) |
| `[StoredInClear("easier")]` on an email | Gives up encryption and crypto-shredding for convenience | `[Searchable]` for equality; clear storage only for a stated query need |
| "Logs contain no personal data" in docs | Keyed hashes and user ids are pseudonymous personal data | "No directly identifying data; kept for the log retention period" |
| `[PersonalData] string Email` on a record parameter | Lands on the parameter; log generator misses it (YAF2005) | `[property: PersonalData(...)]` |
| `logger.…("Registered {Email}", email)` | Clear text in logs (YAF2007) | Log `AttendeeId`; or `[LogProperties]` |
| `throw new X($"Attendee {name} …")` | Exception messages are not redacted | Ids only |
| Attendee name/email in `RegistrationSubmittedV1` | Copies personal data to every subscriber | `AttendeeId` |
| `email_lower` clear column for lookups | Defeats encryption | `[Searchable]` |
| `[OnErasure(Keep)]` without a reason | Unjustified retention | Name the legal basis, or delete |
| Email in a cache key or blob path | Personal data outside erasure's reach | Ids |
| "This makes the service GDPR compliant" in docs | False claim | "Supports erasure and export (see controls matrix)" |
| Real customer rows in a test fixture | Unlawful processing; leaks | Synthetic builders from `YAF.Testing` |

## Verify

- `dotnet build YAF.slnx` (YAF2001–YAF2008).
- `dotnet test --project <service>/tests/Api.IntegrationTests`: stored value is ciphertext (or clear, for a `[StoredInClear]` member, and overwritten after erasure); read models return the clear value; personal value absent from every log record and span (`LogAssert.ContainsNo`); export contains every classified member of the subject and nothing of others; erasure leaves no readable copy and the audit chain still verifies.
- `docs/compliance/data-inventory.md` regenerated and reviewed.
- Retention architecture test lists your type with its policy.

## References

- `docs/articles/privacy.md` — classification, retention, data subject requests, crypto-shredding position.
- `docs/compliance/` — generated inventory and control matrix; read the opening "what must never be claimed" list before writing about compliance.
- `adr/0005-compliance-by-design.md` — compliance by design.
- `samples/conference/` Registrations `Domain/Attendees/`, the erasure and export integration tests.
- Skills: `yaf-logging-and-telemetry`, `yaf-persistence`, `yaf-security`.
