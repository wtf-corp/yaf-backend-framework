# 11 — Privacy and compliance by design (`YAF.Privacy` and cross-cutting)

## 1. Position

YAF implements the controls that **code can enforce** and produces **evidence** that they work. It does not make an application compliant, and nothing in the framework, its docs or its generated reports may say so (§9). ADR-0005 records the decision and the mapping method.

**Scope of the first release.** 1.0 ships the mechanisms: classification, redaction, retention and purge, legal hold, subject keys and crypto-shredding, generated exporters and erasers per context (called through `IDataSubjectOperations`, §5), processing restrictions, and the inventory and control-matrix tooling. What `YAF.Privacy` stores (legal holds, retention state, restrictions) is mapped by `YAF.Privacy.EfCore`, which depends on `YAF.Privacy` and `YAF.Persistence.EfCore`. Planned for a later release, and designed so they can be added without a breaking change: the privacy coordinator bounded context (the data subject request aggregate and the orchestration of exporters and erasers across services), the incident register (`IIncidentRegister`), `DecisionRecord`, and `YAF.AI`.

Sources used for the control catalogue: ISO/IEC 27001:2022 Annex A, GDPR, NIS2 (Directive 2022/2555, with Implementing Regulation 2024/2690 as a benchmark), the EU AI Act (Regulation 2024/1689 as amended), and, as supporting catalogues, OWASP ASVS 5.0, OWASP API Security Top 10, NIST SSDF, SLSA, the Cyber Resilience Act, DORA, SOC 2 and PCI DSS. Clause identifiers in §8 were compiled in November 2026 from secondary copies for some standards; the implementer MUST verify each identifier against the official text before publishing `docs/compliance/`.

## 2. Data classification

One taxonomy, `YAF`, built on `Microsoft.Extensions.Compliance.Abstractions` (`DataClassification`, `DataClassificationAttribute`):

| Attribute | For | At rest | In logs / traces | In audit `changes` | In integration events |
|---|---|---|---|---|---|
| `[PublicData]` | Ids, codes, statuses, published content | clear | as is | clear | allowed |
| `[InternalData]` | Business data that is not personal | clear | as is | clear | allowed |
| `[PersonalData(category, Purpose = …)]` | Identifies a person: names, contact, identifiers, addresses, online ids | subject-key encrypted; clear only with `[StoredInClear("reason")]` | keyed hash (HMAC) | subject-key encrypted | only with `[AllowPersonalData("reason")]` |
| `[SensitivePersonalData(category)]` | Special categories, financial account data, precise location, credentials of a person | subject-key encrypted (always) | erased | subject-key encrypted | refused |
| `[SecretData]` | Grants access: passwords, tokens, keys, connection strings, presigned URLs | key-ring encrypted | `***` | member name only | refused |

- `PersonalDataCategory`: `Identity`, `Contact`, `Location`, `Financial`, `Health`, `Biometric`, `Behavioural`, `Credentials`, `Other`. `Purpose` is a string from the service's purpose list (`IPurposeCatalog`). Because the catalogue is a run-time object, every purpose used in an attribute is checked against it by a startup check and by a test in the template (`PurposeAssert.AllDeclaredPurposesAreCatalogued`), not by the compiler.
- `PersonalDataAttribute` properties: `Category`, `Purpose`, `ProvidedBySubject` (default `true`; decides portability scope), `PropagateRectification` (default `false`).
- Where classification is **mandatory** (analyzer `YAF2001`, error): every `string`, `byte[]`, `JsonElement`, dictionary and unknown-shaped member of a memento interface, an integration event, a job payload, a process state, an options class and a type marked `[LogPayload]`. Numeric, boolean, date, enum and typed-id members default to `Internal` and may be reclassified.
- Classification on a **type** applies to every member of that type (`EmailAddress` is `[PersonalData(Contact)]`).
- On positional records the attribute targets the property (`[property: PersonalData(…)]`); the analyzer `YAF2005` flags the bare form, which would land on the parameter and be missed by generated log methods.
- Free-text members are the weak point, because a person can type personal data into any of them. A member that holds text typed by a user (a note, a description, a comment) is marked `[FreeText]`. `YAF2006` (warning) flags a `[FreeText]` member classified `[InternalData]` or `[PublicData]` unless it says `[FreeText(ContainsPersonalData = false, Justification = …)]`. Whether a member is free text cannot be decided by an analyzer; placing the marker is a point of the review checklist.
- `[StoredInClear("reason")]` on a `[PersonalData]` member keeps it unencrypted so the store can sort and search it (`05-persistence.md` §6). `YAF2008` (error) refuses it on `[SensitivePersonalData]` and `[SecretData]`. The member and its reason are listed in the data inventory (§6).

## 3. Redaction

One policy, applied in every sink (`12-observability.md` §3): logs, trace attributes, the startup configuration dump, problem details arguments, business and security event data, exception data added by the framework.

- `[SecretData]` → `MaskingRedactor` (`***`). `[SensitivePersonalData]` → erased. `[PersonalData]` → `HmacRedactor` (`<key id>:<base64 hash>`), so entries about the same person correlate without revealing the value; the key is a secret in the `redaction` key ring, required outside Development.
- What remains in a sink after redaction is **not directly identifying**, but it is not anonymous: a keyed hash, and a user id in a log scope, are pseudonymous personal data for as long as the key or the identity mapping exists. They are covered by the sink's retention period (§4), and the docs say so.
- Unknown or foreign classifications fall back to erasure.
- `IRedactingFormatter.Format(object)` serializes any object with the policy applied (used by `[LogPayload]`).
- Exception messages cannot be redacted: framework code never puts values into exception messages (ids and names only); analyzer `YAF2007` flags interpolating a classified member into an exception message or a log template argument.

## 4. Retention and deletion

```csharp
[Retention("P2Y", After = RetentionAnchor.Member, Member = nameof(ClosedAt), Basis = "contract", Action = RetentionAction.Delete)]
public interface IMemento : IAggregateMemento, IDataSubjectOwnedMemento { … }
```

- Required on every type holding personal data (`YAF2002`), on soft-deletable types, and on framework stores (defaults: outbox 7 d, inbox 14 d, idempotency records 24 h, jobs 3 d, business events 2 y, security events 1 y, audit trail 6 y — all configurable under `Privacy:Retention`, with per-profile floors, e.g. a profile may forbid going below a statutory minimum).
- `RetentionAction`: `Delete`, `Anonymise` (members with `[OnErasure(Anonymise)]` are overwritten with type-appropriate neutral values; the row stays for statistics), `Shred` (destroy the subject key).
- `yaf.retention.purge` (cron) works in batches per type, under a system principal with cross-tenant elevation, skips records under **legal hold**, writes a deletion evidence record (type, count, policy, period — no personal data) to the audit trail, and exposes `yaf.retention.backlog{type}`.
- **Append-only stores** (audit trail and its seals, security events, business events) are purged by **dropping whole monthly partitions**, never by deleting rows. That part of the job connects with the maintenance database role, the only one allowed to do it; for the audit tables a signed checkpoint is written first so the chain stays verifiable (`05-persistence.md` §7). The effective retention of these stores is therefore the configured period rounded up to a month.
- **Legal hold**: `ILegalHolds.PlaceAsync(scope, reason)` / `ReleaseAsync` (scope: a data subject, a tenant, or a type within a tenant), stored through `YAF.Privacy.EfCore`. A hold blocks purge, erasure and partition drop for what it covers, and each is reported with the hold's reason.
- These periods are also what bounds the pseudonymous identifiers that erasure does not reach: user ids in stamps, audit `actor`/`initiator`, envelopes and job rows, and keyed hashes in logs (§5).
- An architecture test lists every personal-data type with its policy; the generated inventory (§6) shows it.

## 5. Data subject requests

Each service that holds personal data can export and erase it. Two interfaces do the work per type, and one façade is what the app calls:

```csharp
public interface IPersonalDataExporter
{
    ValueTask<PersonalDataExport> ExportAsync(DataSubjectId subject, ExportScope scope, CancellationToken ct);
}
public interface IPersonalDataEraser
{
    ValueTask<ErasureReport> EraseAsync(DataSubjectId subject, ErasureScope scope, CancellationToken ct);
}
public interface IDataSubjectOperations            // registered by AddYafPrivacy(); runs every exporter / eraser of the service
{
    ValueTask<Result<PersonalDataExport>> ExportAsync(DataSubjectId subject, ExportScope scope, CancellationToken ct);
    ValueTask<Result<ErasureReport>> EraseAsync(DataSubjectId subject, ErasureScope scope, CancellationToken ct);
}
```

- **Default implementations are generated.** For each `IDataSubjectOwnedMemento` type the inventory generator emits an exporter (all classified members, with category and purpose) and an eraser that applies each member's `[OnErasure]` action. A service overrides them only for special cases. An architecture test fails when a data-subject-owned type has neither.
- **The app decides how a request arrives.** It calls `IDataSubjectOperations` from its own `[SystemOnly]` or privileged command (behind an endpoint, a back-office action or a consumer). Receiving a request, verifying the requester, tracking its deadline and collecting the results of several services are the app's in 1.0; the coordinator context that does this (aggregate `DataSubjectRequest` with a state machine and due dates, request/reply events, a process manager assembling the result) is planned for a later release and will call the same interfaces.
- **Access / portability (Art. 15, 20)**: one JSON document per service (and CSV per type on request) in the object store, encrypted, with a time-limited download recorded as `data.exported`. Portability scope = members marked `ProvidedBySubject = true`.
- **Erasure (Art. 17)**, per subject, in this order: (1) check legal holds and `Keep` reasons → partial erasure with reasons; (2) delete or anonymise mutable rows, which includes overwriting `[StoredInClear]` members and clearing the `<name>_hash` columns of `[Searchable]` members; (3) **destroy the subject's data key** (`crypto.subject_key_destroyed`) and evict it from the key cache, which renders unreadable every remaining copy encrypted with it: audit `changes`, process state, soft-deleted rows, backups; (4) publish `privacy.subject-erased.v1` (it carries the subject id and tenant, nothing else) so other contexts and downstream recipients act (Art. 19): every service registered with `AddYafPrivacy()` consumes it, runs its own erasers and drops its cached key; (5) write an erasure receipt without personal data.
- **Rectification (Art. 16)**: ordinary update commands; `privacy.subject-rectified.v1` is published when a memento member marked `PropagateRectification = true` changes.
- **Restriction and objection (Art. 18, 21)**: `IProcessingRestrictions.IsRestrictedAsync(subject, purpose)`; the pipeline's `PurposeMiddleware` (enabled by `AddYafPrivacy`) refuses requests marked `[ProcessingPurpose("marketing")]` for restricted subjects, and query-side filters can exclude them.
- **Automated decisions (Art. 22)**: a `DecisionRecord` is planned together with `YAF.AI` (`13-ai.md`); 1.0 has no mechanism for it.

Crypto-shredding position (to be repeated in the docs): destroying the key makes data permanently unintelligible to the controller; regulators treat encrypted data as personal data while the key exists, and no general statement equates key destruction with erasure. YAF therefore deletes physically where rows are mutable and uses shredding for append-only stores and backups.

What erasure does **not** reach (also to be repeated in the docs): integration events, job payloads and logs carry no directly identifying data, but they, the stamps (`created_by`…), the audit `actor`/`initiator` columns and security events do carry user ids, and logs carry keyed hashes. These are pseudonymous personal data. They stay until the retention period of their store ends (§4) and can no longer be linked to the person only where the identity mapping itself is erased (the user record in the app's identity store, and for hashes the redaction key at its rotation). Whether that is sufficient is the controller's assessment; YAF does not claim these stores need no consideration.

## 6. Generated inventory and evidence

Built by the generators of the `Analyzers` project (shipped inside `YAF.Core`) + a CI tool (`yaf-compliance`, a dotnet tool project in `tools/`), written under `docs/compliance/` and published with the docs:

| Artefact | Content | Supports |
|---|---|---|
| `data-inventory.md` / `.json` | Per service: type → member → classification, category, purpose, storage treatment (subject key, key ring, or stored in clear with its reason), logical field id, retention, erasure action, whether it leaves the context (which integration events) | Input to records of processing and DPIAs |
| `controls.md` | Control id → requirement → mechanism → tests (`[Control("…")]`) → result of the last CI run | Accountability, audit preparation |
| `authorization-inventory.md` | Endpoint → access → permissions, per API | Access control review |
| `error-catalogue.md`, `configuration-reference.md` | | Change management |
| `sbom/` + provenance | Per release | Supply chain |
| `unsafe-options.md` | Every option that weakens a control and where it may be used | Configuration management |

A change that alters the inventory shows up as a diff in the pull request.

## 7. Time, integrity, incidents

- Time: `TimeProvider` everywhere; UTC `timestamptz`; clock APIs banned (`01` §4). Hosts are expected to be NTP-synchronised (infrastructure).
- Integrity of records: audit trail written in the business transaction and hash-chained by an asynchronous sealer, with signed checkpoints and an external anchor (`05-persistence.md` §7); append-only security and business events; no database role with row-level update or delete on them.
- Incident support: a query API over security events and read-audit ("which subjects and tenants did principal X touch between t1 and t2"), and proof-of-encryption evidence (key access events) for breach assessment. An incident register with deadline reminders (`IIncidentRegister`) is planned for a later release.

## 8. Control catalogue (what the framework implements)

Each row becomes one or more `[Control]`-tagged tests. Ids are YAF's own; the source column cites the clauses the control supports.

| Id | Control | Mechanism | Sources (verify before publishing) |
|---|---|---|---|
| C-AC-1 | Deny-by-default access | Fallback policy; undeclared request refused; inventory approval | ISO A.5.15, A.8.3; GDPR 25(2), 32; NIS2 21(2)(i); ASVS V8 |
| C-AC-2 | Tenant isolation, two layers | Query filters + row-level security + role separation | ISO A.8.3; GDPR 32(1)(b) |
| C-AC-3 | Privileged access is distinct and recorded | `privileged` permissions checked without cache, elevation scopes, system actor for asynchronous work, security events | ISO A.8.2; NIS2 21(2)(i) |
| C-AC-4 | Strong authentication support | Strict JWT validation, hashed API keys, step-up on `amr`/`acr`, throttling | ISO A.8.5; NIS2 21(2)(j); ASVS V6, V9 |
| C-CR-1 | Encryption of classified data at rest | AES-GCM field protection, key ring, rotation, re-encryption job | ISO A.8.24; GDPR 32(1)(a); NIS2 21(2)(h) |
| C-CR-2 | Transport encryption required | Startup TLS checks for all outbound dependencies; HSTS | ISO A.8.24; GDPR 32 |
| C-CR-3 | Crypto agility | Versioned suite tag, purpose-separated keys | ISO A.8.24 |
| C-DP-1 | Classification of all persisted and transmitted data | Attributes + analyzers | ISO A.5.12, A.5.13; GDPR 25(1), 30 |
| C-DP-2 | Masking in logs, traces, errors | Single redaction policy | ISO A.8.11, A.8.12; GDPR 5(1)(f) |
| C-DP-3 | Data minimisation across contexts | Personal data refused in events/jobs unless justified | GDPR 5(1)(c), 25 |
| C-DP-4 | Storage limitation | Mandatory retention policies, purge job, evidence | ISO A.8.10; GDPR 5(1)(e) |
| C-DP-5 | Erasure incl. append-only stores | Erasers + subject-key shredding + receipts | GDPR 17, 19; ISO A.8.10 |
| C-DP-6 | Access and portability | Exporters through `IDataSubjectOperations` | GDPR 15, 20 |
| C-DP-7 | Restriction / objection | Restrictions register + purpose middleware | GDPR 18, 21 |
| C-DP-8 | Data inventory | Generated | GDPR 30 (input), ISO A.5.34 |
| C-LG-1 | Security and audit logging with mandatory fields | Security events, audit trail, structured logs | ISO A.8.15; NIS2 21(2)(b); ASVS V16 |
| C-LG-2 | Records are tamper-evident | Hash chain sealed asynchronously, signed checkpoints, anchor, append-only roles | ISO A.5.33; GDPR 5(2) |
| C-LG-3 | Monitoring hooks | Metrics per security event, sinks, health | ISO A.8.16; NIS2 21(2)(b) |
| C-LG-4 | Synchronised, unambiguous time | `TimeProvider`, UTC, banned clock APIs | ISO A.8.17 |
| C-CF-1 | Validated configuration, secure defaults, no secrets in files | Options validation, startup checks, unsafe-options registry, secret scan | ISO A.8.9; NIS2 21(2)(e); ASVS V13 |
| C-CF-2 | Environment separation | Development-only switches refused elsewhere; test doubles cannot run in Production | ISO A.8.31 |
| C-SD-1 | Secure coding rules enforced | Analyzers, banned APIs, architecture tests | ISO A.8.25–A.8.28; SSDF PW |
| C-SD-2 | Security testing in the pipeline | Security test pack, conformance suites | ISO A.8.29; NIS2 21(2)(f) |
| C-SD-3 | Change control and traceability | Conventional commits, generated changelog, package validation, approved-file diffs | ISO A.8.32; SOC 2 CC8.1 |
| C-SD-4 | Synthetic test data only | Builders in `YAF.Testing`; no production data path | ISO A.8.33 |
| C-SC-1 | Dependency integrity and vulnerability gate | Pinned versions in `Directory.Packages.props`, audit as error, source mapping, dependency review | ISO A.8.8; NIS2 21(2)(d); SSDF PS/RV |
| C-SC-2 | SBOM and provenance per release | CycloneDX/SPDX SBOM, attestations | NIS2 21(2)(d); CRA Annex I part II; SLSA Build L2 |
| C-RS-1 | Resilient, recoverable messaging | Outbox, inbox, retries, dead-lettering, graceful shutdown, health | GDPR 32(1)(b)-(c); NIS2 21(2)(c) |
| C-IN-1 | Incident timeline support | Security event and read-audit queries, key access evidence | NIS2 23; GDPR 33 |

> **Amended 2026-11-24**, by the owner's decision of 2026-11-24 (`specs/0007-no-package-lock-files/plan.md`, decision log; ADR-0032): C-SC-1 no longer uses NuGet lock files. Its mechanism names what remains; the content-hash check of restored packages and the detection of changes in the transitive graph are given up.

Controls that arrive with later packages and are not in the 1.0 catalogue: C-DP-9 request deadlines (privacy coordinator; GDPR 12(3)), incident register with reporting deadlines (extends C-IN-1), C-AI-1…5 (`13-ai.md`; AI Act 12, 13, 14, 19, 26, 50).

**Enforced at build time**: C-AC-1 (markers), C-DP-1, C-DP-3, C-DP-4 (policy presence), C-LG-4, C-SD-1, C-SC-1, plus the generated artefacts. **Enforced at run time**: the rest.

## 9. What must never be claimed

- That YAF or an app built on it is "GDPR / NIS2 / ISO 27001 / SOC 2 / AI Act compliant" or "certified". Organisations and scoped management systems are assessed, not libraries.
- That the framework decides lawful basis or purposes, performs a DPIA, *is* the record of processing, or decides whether an incident is notifiable. It supplies inputs and timers.
- That crypto-shredding is formally recognised erasure, or that the audit trail is tamper-proof (it is tamper-evident, given a protected anchor, and the newest rows are chained seconds after commit, not at commit).
- That logs, events, stamps or audit columns are free of personal data. They are free of directly identifying data; user ids and keyed hashes in them are pseudonymous (§5).
- That classification guarantees no leakage: it is as complete as the annotations.
- Anything about controls that are organisational or infrastructural: backups and restore tests, network segmentation, MFA enrolment, key custody, training, supplier contracts, penetration tests, incident notification.

The docs site's compliance page opens with this list.

## 10. Acceptance criteria

- Analyzer tests for `YAF2001`–`YAF2008`.
- A purpose that is not in the catalogue fails the startup check and the template test.
- Integration tests in the reference sample, calling `IDataSubjectOperations`: export of a registrant contains every classified member and nothing of another subject; erasure deletes/anonymises rows, overwrites members stored in clear, clears lookup hashes, destroys the key, leaves audit rows unreadable for that subject while the chain still verifies, publishes the erased event, and a second service erases its copy; legal hold blocks erasure, purge and partition drop with a reason; retention purge deletes only expired, unheld records and writes evidence; an expired partition of an append-only store is dropped by the maintenance role after its checkpoint; restricted subject's request is refused for the tagged purpose.
- `docs/compliance/*` is generated in CI, is in sync with the code, and `controls.md` has at least one passing test per control id.
