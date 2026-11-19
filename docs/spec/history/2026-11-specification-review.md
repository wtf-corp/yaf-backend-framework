# Specification review, November 2026

This file records one revision of the specification: what a review found, what was decided, and what changed. It is history, not a rule. The specification in `spec/`, `adr/` and `agents/` is what binds; where this file and the specification differ, the specification is right.

## 1. Why the review happened

The first complete draft of the specification was reviewed before any code existed, with three questions:

1. Can the costs the ADRs record under "Negative / costs" be avoided?
2. Does the specification hold up against good practice for .NET libraries, domain-driven design, persistence, messaging, security, privacy and delivery?
3. Does it contradict itself, or describe something that cannot be built as written?

Because the repository had not started, the corrections were made in place: the ADRs kept the status "Accepted" and were rewritten rather than superseded. That is allowed only here. Once the repository exists, an accepted ADR is changed by a superseding ADR (ADR-0001).

## 2. How it was done

1. The reviewer read all 19 spec files, the options and consequences of all 28 ADRs and the root agent instructions, and reported findings in three groups: defects, avoidable costs, and unlisted costs.
2. The owner decided 13 questions, one at a time, each with two to four options (§4).
3. The decisions were written as one brief with fixed names, and applied by six editors working in parallel, each owning a separate set of files.
4. A final pass searched the folder for leftovers of the old rules and for names that differed between files, and fixed them (§7).

Limits of the method: nothing was prototyped, so claims about EF Core, Npgsql, semantic-release and Aspire behaviour rest on documentation and experience, not on a test. The final pass was a text search, not a second full reading.

## 3. Findings: defects

These would have failed or contradicted themselves whatever else was decided.

| # | Finding | Correction |
|---|---|---|
| 1 | A new version was written only for modified entities. Two commands that each add a child left the root untouched, both committed, and an aggregate invariant (a seat limit) could break | `UpdateAsync` always writes a new version on the root row; child rows have no version |
| 2 | Field protection was placed in EF Core interceptors: encrypting "copies in the command parameters" has no supported hook; materialization interceptors are synchronous and are skipped for projections, so read models would have returned ciphertext | Protection moved to the YAF mapping layer (decision 3a) |
| 3 | "Queries never open a transaction" against a tenant setting that is transaction-local | Every query runs in one `READ ONLY` transaction; the context never executes a command outside a transaction |
| 4 | The idempotency step sat outside the unit of work but had to write in its transaction, and shared the inbox table | It sits inside the unit of work and has its own table, `yaf_idempotency` |
| 5 | A typed-id EF converter was to be generated into domain projects, which may not reference EF Core | Generic converters in `YAF.Persistence.EfCore`, applied by convention |
| 6 | The runtime role could not delete from audit and event tables, which nevertheless had retention periods; purging would also break the start of the hash chain | Monthly partitions, a maintenance role, signed checkpoints (decision 4b) |
| 7 | `IExecutionContextScopeFactory` was open to application code, against "application code cannot set the context" | Internal to the framework; `CronContext.ForEachTenantAsync` for fan-out; analyzer `YAF4004` |
| 8 | Jobs "cannot reach other use cases through the dispatcher" against "`[SystemOnly]` requests exist for jobs, cron and process managers" | Asynchronous entry points may dispatch, and only `[SystemOnly]` requests; `YAF4002` covers request and domain-event handlers only |
| 9 | Associated data of ciphertexts included table and column names, so a rename migration would have made data undecryptable | A stable logical field id, recorded in `fields.approved.txt` |
| 10 | `IsAotCompatible` on Domain and Application against compiled expression trees in specifications and validators, under warnings as errors | The claim stays on `YAF.Core` only (decision 8b) |
| 11 | `YAF.Privacy` held aggregates but had no package that could map them | `YAF.Privacy.EfCore` |
| 12 | Smaller contradictions: `[Searchable]` members encrypted in one file and not in another; first release `1.0.0` in one file and `0.1.0` in another; `specs/` attributed to ADR-0021 instead of ADR-0024; test libraries named in ADR-0018 and listed as open in the build plan; `Analyzers` a project in one file and a package in another; error-code uniqueness checked within one compilation only; `YAF2006` not decidable by an analyzer; `Purpose` "validated at build time" against a runtime interface; `T → Result<T>` conversion assumed for interface types | Each settled in the files concerned; the analyzer id scheme (`YAF` + spec file number + three digits) is now stated once in `spec/01` |

## 4. Decisions

The option taken is in bold. Each was the reviewer's recommendation; the owner accepted all of them.

| # | Question | Options | Reason for the choice |
|---|---|---|---|
| 1 | Scope of the first release | **One of each kind**: built-in dispatcher, in-memory + RabbitMQ + SNS/SQS, minimal APIs + controllers, privacy mechanisms; mediator adapters dropped; Azure Service Bus, FastEndpoints, Azure storage and keys, the privacy coordinator, the incident register, `DecisionRecord` and `YAF.AI` later · Full scope as written | Once YAF owns the contracts and the pipeline, a mediator engine only locates a handler, and one of the three brought a commercial licence into CI. Contracts and conformance suites keep the door open for the rest |
| 2 | Memento boilerplate | **Generate the data class, allow typed members** · Generate, primitives only · Hand-written as before · Map aggregates with EF directly | Removes most of the per-entity ceremony ADR-0007 recorded and the string-typed specifications, without letting EF shape the domain |
| 3a | Where encryption happens | **The YAF mapping layer** · EF value converters · Keep interceptors and spike first | Uses only supported EF APIs; converters cannot see the row, the tenant or a per-subject key |
| 3b | Default for `[PersonalData]` | **Encrypt, with a per-member opt-out that needs a reason** (`[StoredInClear]`) · Encrypt always · Encrypt only sensitive data | A sorted list of names is a routine need; an explicit, inventoried exception is better than an unreviewed workaround |
| 4a | Building the audit chain | **Seal asynchronously** · Keep the per-tenant lock held to commit · Make it an option | The lock serialised every write of a tenant. Rows are still written in the business transaction; the cost is a short unsealed window, which the verifier reports |
| 4b | Retention of append-only tables | **Monthly partitions, a maintenance role, signed checkpoints** · Row deletes by a maintenance role · Never purge | Fast, no row-level delete grant, and the chain keeps a verifiable start |
| 5 | Input handling | **Reject, never rewrite**; sanitize only `[Markup]` members through an optional adapter · Strip HTML from every string · Reject any string containing markup | Stripping silently changed user data ("a < b") and is not the control for a JSON API |
| 6 | Identity of asynchronous work | **System actor, with the user as initiator for audit only** · Original user, re-authorized, signed envelopes · Original user, trusting the broker | A forged envelope can no longer gain a user's rights, and a job does not run with rights revoked in the meantime |
| 7 | Domain events and saving | **Save, dispatch, repeat, then commit** · Dispatch everything, save once | Handlers' queries see pending changes; the "store-assigned values only after the save" cost of ADR-0010 disappears |
| 8a | Later .NET versions | **Add each new .NET as a target in a minor; drop a target in the major after its support ends** · A new major per runtime · Also target `net10.0` now · Keep deferred | Users could not know how long a major is supported; one line of maintenance instead of two |
| 8b | Native AOT | **`YAF.Core` only** · Keep the promise and redesign · Drop everywhere | Every real service uses EF Core, which is not ready for it |
| 9 | Versioning | **Prereleases until 1.0.0, then majors batched on `next`** · `0.x` until stable · Every breaking merge a major | A breaking merge would otherwise publish a new major of every lockstep package |
| 10 | Default development transport | **RabbitMQ; LocalStack opt-in** · In-memory · LocalStack | LocalStack needs an account token, so a fresh clone did not run with one command |
| 11a | Gates for the initial build | **Plan approval only, against the specification committed as `specs/0000-framework/`** · Spec and plan per milestone · Gate only risky milestones | The spec for the initial build is already written and approved; copying it per milestone doubled the review and invited drift |
| 11b | Proof of human approval | **A code owner's review on the spec's or plan's own pull request; labels count only when a human maintainer applied them** · A signed approval commit · The `Status:` line | The gated agent could write the status line and apply the label itself |
| 12 | Dependencies and packages | **All four accepted**: Cronos instead of an own cron parser; `YAF.Testing` split into pure helpers and `YAF.Testing.Integration`; `Asp.Versioning` behind `YAF.Http.Versioning`; `YAF.Privacy.EfCore` | Daylight-saving handling is where own parsers fail; domain tests no longer pull in ASP.NET Core, EF Core and Docker tooling; `YAF.Http` follows its own adapter rule |
| 13 | Identity details | **All four accepted**: user id from issuer and subject only, with `IUserIdResolver`; signed development tokens instead of `AllowUnsignedTokens`; edge permission derived from the request marker; privileged permissions never served from the cache | Renaming a provider no longer changes user ids; one unsafe switch fewer; one declaration instead of two; revocation of dangerous rights is immediate |

Decided afterwards, on the owner's request:

| Question | Decision |
|---|---|
| A new NuGet advisory failed the restore of every pull request | Checked on a schedule: dependency review fails only the pull request that introduces a vulnerable version; a daily `audit.yml` keeps one issue open; the release refuses high and critical advisories unless a time-boxed suppression is approved (`spec/17` §2.1, ADR-0020 point 11) |
| File names that no longer matched their content | `adr/0009-application-pipeline-and-mediator-abstraction.md` became `adr/0009-application-pipeline-and-dispatcher.md` |

## 5. Recorded costs that went away, and new ones

| ADR | Cost removed | Cost added |
|---|---|---|
| 0003 | Maintaining three mediator adapters; an own cron parser; an open .NET policy | One third-party library (Cronos) on the allow-list; a wider CI matrix when a second target is added |
| 0007 | A hand-written data class per entity; specifications against storage shapes | One more generator to own |
| 0009 | Adapters, their conformance suite, unused engine features | Teams with an existing mediator write their own adapter |
| 0010 | The extra `set_config` round trip; store-assigned values arriving late | One save per round of domain events; three database roles instead of two; partition management |
| 0011 | No sorting or searching on any personal data | Members stored in clear are erased by overwrite, not by destroying a key |
| 0012 | A third broker to keep green | No shipped Azure Service Bus adapter in 1.0 |
| 0014 | Edge attribute and request marker both declared; a third HTTP style | A generator must find the request an endpoint dispatches |
| 0015 | User ids changing with a provider's configured name; revocation waiting for the cache | One more call to the privilege source on privileged requests |
| 0019 | A token needed for the default run | AWS behaviour is exercised locally only by opt-in |
| 0020 | A major per breaking merge; advisories blocking unrelated pull requests | A second long-lived branch (`next`); low and moderate advisories can ship while their issue is open |
| 0023–0025 | Two approvals per milestone of the initial build | Approval now depends on repository settings (CODEOWNERS, branch protection, a label-actor check) being configured correctly |
| — (not recorded before) | The per-tenant lock on every commit | A short window in which audit rows are stored but not yet chained |

## 6. Choices made while applying the decisions

The decisions left gaps that the editors filled. They were reported to the owner and stand unless changed.

- **`major-release` label.** After 1.0.0 breaking titles are rejected on `main`, so the planned merge of `next` needs an exception; a human maintainer applies this label.
- **Erasure across services in 1.0** works by event: each service consumes `privacy.subject-erased.v1` and runs its own erasers. The orchestrating coordinator is a later release.
- **`Privacy:EncryptPersonalData` was removed.** A global off-switch contradicted the per-member opt-out.
- **`YAF2006`** fires on a declared `[FreeText]` marker; where the marker belongs is a review question.
- **Analyzers `YAF8002` and `YAF8003`**: a controller action whose dispatched request cannot be determined (named explicitly with `[Dispatches(typeof(...))]`, or `.Dispatches<TRequest>()` on a route builder), and a `[SystemOnly]` request mapped to an endpoint.
- **Specifications on encrypted members** allow only equality, and only on `[Searchable]` members.
- **Streaming queries** run through the query pipeline with `IStreamQueryHandler<TQuery, TItem>`.
- **A consumed payload that fails the input check** is parked or dead-lettered, not retried. A request not marked `[SystemOnly]` sent from an asynchronous entry point fails with `core.authorization.system_actor`.
- **Idempotency records** are kept 24 hours by default; the same key with another payload is a conflict; a second request while the first runs gets `409` with `Retry-After`.
- **Read-audit rows** are written in a transaction of their own after the query, because the query's transaction is read-only.
- **Retention of the append-only tables** is rounded up to a month, since whole partitions are dropped.
- **Parked outbox rows** block their partition key by default; `OnParked = SkipAndContinue` per event type where order does not matter.
- **Behind a transaction-mode pooler** the relay, runners and singleton workers use a `-direct` connection string, or fall back to polling and a lease row (`yaf_leases`).
- **The default development setup** includes a local S3-compatible container for object storage.
- **The sample** shows export and erasure through two privileged `Registrations` endpoints that call `IDataSubjectOperations`.
- **Build plan.** The milestone numbers M0–M15 were kept: M11 is now the optional adapters, M12 export, erasure and evidence. Later-release items are listed in `spec/18` §6.
- **Log scope.** `yaf.principal` names the actor; `yaf.initiator` was added for asynchronous work.
- **Test helpers** throw their own `YafAssertionException`, so the pure `YAF.Testing` needs no test framework.

## 7. Names that changed

| Before | After |
|---|---|
| `[AllowMarkup]`, `SanitizationMiddleware` | `[Markup]`, `InputGuardMiddleware` |
| Envelope attribute `yafprincipal` | `yafinitiator` |
| `AllowUnsignedTokens`, the environment probe guarding it | Removed |
| `YAF.Mediator.MediatR`, `.Wolverine`, `.SourceGenerated` | Removed |
| `YAF.Testing` (one package) | `YAF.Testing` and `YAF.Testing.Integration` |
| `YAF.Testing.Security` | Namespace `YAF.Testing.Integration.Security` |
| `ReadOnlyGuard` (development only) | `QueryTransactionMiddleware` (`READ ONLY` transaction, every environment) |
| `RowLevelSecurityConnectionInterceptor` | `TransactionScopeInterceptor` |
| `AddYafPrivacyParticipant()` | `AddYafPrivacy()` |
| `HttpStyleParity` | `HttpParity` |
| Pipeline: … Sanitization → Validation → Idempotency → UnitOfWork | … InputGuard → Validation → UnitOfWork → Idempotency |
| ADR-0009 "…and mediator abstraction" | "…and built-in dispatcher" (file renamed) |

New names: `[MementoData]`, `[StoredInClear]`, `[FieldId]`, `[Protected]`, `[FreeText]`, `[Dispatches]`, `IFieldProtector`, `IMarkupSanitizer`, `IUserIdResolver`, `IPrivilegeCacheInvalidator`, `IDataSubjectOperations`, `AuditChainSealer`, `CronSchedule`, `ApiVersionSet`, packages `YAF.Http.Versioning`, `YAF.Markup.HtmlSanitizer`, `YAF.Privacy.EfCore`, tables `yaf_idempotency`, `yaf_audit_seals`, `yaf_audit_checkpoints`, `yaf_leases`, analyzers `YAF2008`, `YAF4004`, `YAF5001`, `YAF8002`, `YAF8003`, workflow `audit.yml`, labels `major-release`, `dependency-advisory`.

## 8. Files touched

- `README.md`; all of `spec/00` … `spec/18`.
- ADRs 0001–0027 except 0000 (template) and 0028 (one table cell only).
- `agents/README.md`, `agents/AGENTS.root.md`, `agents/AGENTS.nested.md`; every skill except `yaf-writing-adrs`.

## 9. What a reader should still verify

- The field-protection design (unmapped clear property, mapped ciphertext column, batch key resolution after load) against the EF Core version in use, early in M6.
- The semantic-release configuration for "prereleases only until 1.0.0" (it needs one release branch at all times) when the release job is built.
- That a `READ ONLY` transaction per query and the batched `set_config` behave as described with the Npgsql version in use, including behind a pooler.
- Commit-safe ordering in the audit sealer (only rows below the oldest running transaction's snapshot) under concurrent load.
- The points already listed in `spec/18` §4.
