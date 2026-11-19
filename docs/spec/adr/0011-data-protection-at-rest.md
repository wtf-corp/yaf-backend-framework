# ADR-0011 — Data protection at rest

> **Status** Accepted
> **Date** 2026-11
> **Deciders** Framework maintainers

## Context

Services store secrets and personal data. A database dump, a backup, a replica or an over-privileged account must not reveal them, and a data subject's data must be erasable even from stores that are append-only by design (the audit trail, process state, backups).

In the prior design encryption ran inside the domain: an entity's snapshot encrypted the properties its memento listed and hydration decrypted them in place, through a process-wide provider. That coupled the domain to static state, required rules about detaching objects so clear text was not saved back, did not bind a ciphertext to its row, and offered no erasure for immutable records.

## Drivers

- Protection follows from how data is classified, not from code each developer remembers to write.
- No cryptography and no static state in the domain.
- Keys can be rotated without downtime during rolling deployments.
- A ciphertext is useless outside the row it belongs to.
- Erasure must reach append-only stores and backups.
- Algorithms can be replaced later without a data migration emergency.

## Options considered

| Option | For | Against |
|---|---|---|
| Storage-level encryption only (disk, managed database) | Transparent | Does not protect against database access, dumps or replicas read by the wrong role; no per-subject erasure |
| Encryption in the domain on snapshot/hydrate | Explicit | Static providers; in-place mutation hazards; domain knows crypto |
| ORM value converters | Simple; work for projections | No access to row context for binding; no per-subject keys; no asynchronous key lookup; static again |
| ORM interceptors (encrypt command parameters on save, decrypt on materialization) | Transparent to repositories | The ORM has no supported hook that ties a parameter to its row; materialization hooks are synchronous and do not run for projections, so read models would return ciphertext |
| **Classification-driven protection in YAF's mapping layer (generated data class, repository base, read-model helpers), envelope encryption with per-subject keys** | Declarative; row-bound; shreddable; supported APIs only; one batched asynchronous key lookup | Encrypted members cannot be searched or sorted; read models must use the helpers; key management to operate |
| Database-native column encryption | Server-side | Keys visible to the database; provider-specific; weak rotation story |
| Table and column names in the associated data | Nothing to record | A rename migration makes every value undecryptable |
| **A stable logical field id in the associated data** | Renames are free | An approved file of field ids to keep |
| Encrypt personal data always, no exception | Simplest rule | No sorted or prefix-searchable name lists; apps build unreviewed side columns |
| **Encrypt personal data by default, per-member opt-out with a recorded reason** | Safe default; the exception is explicit and listed | Members stored in clear are not shredded and not protected in dumps |

## Decision

Contract: `spec/05-persistence.md` §6, `spec/09-security.md` §6, `spec/11-privacy-and-compliance.md` §5.

**Classification decides treatment.** Attributes on memento interface members (ADR-0007) drive storage:

| Classification | At rest |
|---|---|
| `[SecretData]` | Encrypted with the service key ring |
| `[SensitivePersonalData]` | Encrypted with the data subject's key (key ring when there is no subject) |
| `[PersonalData]` | Encrypted with the subject's key (key ring when the type has no data subject). A `[Searchable]` member is encrypted too and gets a hash column. Clear only with `[StoredInClear("reason")]` |
| `[InternalData]`, `[PublicData]` | Clear |

**Applied by persistence, not the domain, and in YAF's own mapping layer.** For a protected member `Email` the generated data class (ADR-0007, ADR-0010) has a mapped `EmailProtected` column with the ciphertext, a mapped `EmailHash` when the member is searchable, and an unmapped clear `Email` that implements the memento member. The repository base calls `IFieldProtector.ProtectAsync` before a save and `UnprotectAsync` after a load, on the whole loaded graph in one batch: the reader is closed first, then the subject keys are resolved asynchronously in one lookup. The clear property is never a column, so clear text cannot be saved by accident and no detach-before-hydrate rule exists.

**Read models.** A read-model row type marks a protected column `[Protected]` and is materialised through `ToUnprotectedListAsync()` / `FirstOrDefaultUnprotectedAsync()`, which unprotect after the query. Materialising a protected column without the helper is analyzer error `YAF5001`.

**Format and algorithm.** `yaf1:<key-id>:<base64url(nonce ‖ ciphertext ‖ tag)>`; AES-256-GCM, random 96-bit nonce, 128-bit tag; associated data = logical field id, row id, tenant id. A ciphertext copied to another row, field or tenant fails authentication. The `yaf1` tag names the suite behind `ICryptoSuite`; a future suite gets a new tag and values are moved by re-encryption. No algorithm is selectable from application settings.

**Logical field ids.** The field id defaults to `<memento type>.<member>` when the member is first declared and is recorded in `fields.approved.txt`, checked by an approval test. Table and column names are not part of the associated data, so rename migrations do not touch stored values. A rename of the memento type or the member keeps the recorded id by pinning it with `[FieldId("…")]`.

**Stored in clear by decision.** `[StoredInClear("reason")]` on a `[PersonalData]` member keeps it an ordinary column for the cases that need sorting or prefix search. It is refused on `[SensitivePersonalData]` and `[SecretData]` (`YAF2008`), is listed with its reason in the generated data inventory, and is erased by overwrite through the generated eraser.

**Key rings by purpose.** Separate rings for `encryption`, `lookup`, `redaction`, `api-key-pepper`, `messaging` (outbox and job payloads with classified members) and `audit` (checkpoint signatures). Keys are 256-bit random, identified by short ids, supplied by an `IKeyProvider` (configuration or secret store by default; an AWS KMS adapter in 1.0, Azure Key Vault in a later release). One current key per ring; any key in the ring decrypts; an unknown key id or a bad tag throws and raises a security event. A key's value never changes in place. Startup fails without a valid current key outside Development; an ephemeral key in Development is an explicit option.

**Rotation in three deployments.** (1) Add the new key everywhere. (2) Make it current: new writes use it, and `ReEncryptionJob` rewrites, in guarded batches, every value not under the current key, including clear text left in columns classified later; it reports the remaining count as a metric and a log line. (3) When the count is zero, remove the old key. Adding and switching in one step is refused guidance because overlapping instances would meet a key they do not have.

**Searchable members.** `[Searchable]` adds a companion `<name>_hash` column holding HMAC-SHA-256 of the normalised value under the lookup ring, enabling equality lookups; the member itself stays encrypted. Hash columns are cleared on erasure. Range, prefix and substring search over protected data are not supported.

**Per-subject keys and crypto-shredding.** Each data subject (per tenant) has a data key, wrapped by the key ring and stored in `yaf_subject_keys` (envelope encryption). Personal data of that subject, wherever it sits (rows, audit `changes`, process state), is encrypted with it. Erasure deletes or anonymises mutable rows (overwriting members stored in clear and clearing lookup hashes) and then **destroys the subject key**, which makes every remaining copy, including those in backups, permanently unreadable. Unwrapped keys are cached only briefly and the entry is evicted when the key is destroyed. Reading a value whose key is destroyed yields the member's default and marks the aggregate as erased. The audit hash chain covers ciphertext, so shredding does not break verification.

**Honest legal status.** Destroying the key makes the data unintelligible to the controller. Regulators treat encrypted data as personal data while a key exists, and no general statement equates key destruction with erasure; guidance accepts it as rendering data unintelligible, conditional on key hygiene and on the algorithm remaining unbroken. YAF therefore: deletes physically wherever rows are mutable; uses shredding for append-only stores and backups; keeps integration events and logs free of directly identifying data, while stating that the user ids and keyed hashes they carry are pseudonymous personal data bounded by retention, not reached by erasure; requires that every copy of the key, including key-store backups, is destroyed and that the destruction is recorded; keeps crypto-agility for the algorithm caveat. The documentation describes this as "erasure by rendering data permanently unintelligible", never as certified erasure (ADR-0005).

## Consequences

**Positive**

- Classifying a member is enough to protect it; forgetting to classify fails the build.
- The domain has no cryptographic code or static state.
- Stolen rows cannot be replayed into other rows or tenants.
- Tables and columns can be renamed without touching stored values.
- Read models return clear values through one helper; only supported ORM APIs are used.
- Rotation and algorithm change are routine operations with a measurable end.
- Erasure has an answer for immutable records and backups.

**Negative / costs**

- Encrypted members cannot be filtered, sorted or indexed, except by equality through a hash column. `[StoredInClear]` restores that for an ordinary personal member, which then has no protection in dumps and backups and is erased only by overwrite.
- Protection is not transparent to the query side: a read model must mark protected columns and use the helpers, and a raw query sees ciphertext.
- A field id file to keep; losing a recorded id (a rename without a pin) makes values undecryptable, which the approval test exists to prevent.
- Per-subject keys add a lookup (cached) and a table whose loss means data loss; it must be backed up and its backups included in key destruction.
- Key management is an operational duty: provisioning per environment, rotation discipline, custody.
- Deterministic lookup hashes reveal equality of values to someone who can read the column.
- Shredding's legal standing is defensible, not settled; it must be documented in the application's own assessments.

## Enforcement

| Mechanism | What it checks |
|---|---|
| Analyzer `YAF2001` | Every memento member that can hold free data is classified |
| Analyzer `YAF2008` | `[StoredInClear]` refused on sensitive personal and secret members |
| Analyzer `YAF5001` | A protected column materialised without the unprotecting helper |
| Generated data class and repository base | The clear property is unmapped; protection applied to every protected member on every save and load; no opt-out per query |
| `fields.approved.txt` approval test | A field id that changed or disappeared without a `[FieldId]` pin |
| Generated data inventory | Every member stored in clear, with its reason |
| Startup checks | Current key present and 256-bit per ring; key provider registered; ephemeral key only in Development; purposes not sharing a key |
| Banned API list | Weak algorithms, ECB, `System.Random`; direct use of `AesGcm` outside the crypto suite |
| Integration tests | Stored value is ciphertext with the key id; associated-data mismatch fails; a table and column rename leaves values readable; three-step rotation keeps every value readable and ends at zero; searchable lookup finds the row; a read model returns clear values through the helper; destroyed subject key yields erased values, also where it was cached; audit chain still verifies after shredding |
| Metric and health | `yaf.crypto.reencrypt.remaining`; readiness degraded while re-encryption is pending |
| Security events | `crypto.decrypt_failed`, `crypto.key_missing`, `crypto.key_rotated`, `crypto.subject_key_destroyed` |
| CODEOWNERS | `src/Infrastructure/Cryptography` and the field protector require human review |

## Guidance for agents

- To protect a member, classify it on the memento interface. Never call an encryption API from domain or application code, and never store a value you encrypted yourself.
- Never change the ciphertext format, the algorithm, nonce or tag sizes, the associated data, or key derivation. Any such change needs a new suite tag, an ADR and human review.
- Never edit or delete a line of `fields.approved.txt` to make the approval test pass. When you rename a memento type or a protected member, pin the old id with `[FieldId("…")]`.
- Never map the clear property of a protected member, and never write the `…Protected` or `…Hash` property yourself; the generator and the field protector own them.
- In a read model, mark protected columns `[Protected]` and materialise with `ToUnprotectedListAsync()` / `FirstOrDefaultUnprotectedAsync()`.
- Never reuse a key across purposes, change a key's value under an existing id, log or return a key, or put a key in code, tests or committed settings. Tests generate keys at run time.
- Never add a "decrypt failed, return the raw value" fallback for a value that carries the `yaf` marker. A failure there is a security event.
- Do not query, sort or index an encrypted member. If equality lookup is required, mark it `[Searchable]` and query the hash column; if sorting, range or prefix search is asked for, stop and raise it: the answer is `[StoredInClear("reason")]`, and the reason must come from the spec.
- Never mark personal data `[InternalData]` or `[PublicData]` to make a query or test easier.
- A type holding a subject's personal data implements the data-subject marker so its values use the subject key; without it, erasure by shredding will not reach them.
- Never write code that removes or bypasses subject-key destruction, restores a destroyed key, or copies subject keys elsewhere.
- Describe shredding in docs with the wording above. Never call it certified or recognised erasure.

## Related

- ADR-0004 — security tenets S4, S6; ADR-0005 — compliance position
- ADR-0007 — classification on mementos, generated data classes; ADR-0010 — repositories, read models and audit trail
- ADR-0016 — redaction in logs (the same classification); ADR-0017 — key configuration
- ADR-0022 — AI call content under subject keys (later release)
- `spec/05-persistence.md`, `spec/09-security.md`, `spec/11-privacy-and-compliance.md`
