# 18 — Build plan: recreating YAF from this specification

This file orders the work for the software factory (ADR-0029) building the framework in an empty repository. For the initial build this specification **is** the approved spec: its `spec/` files are committed to the new repository as `specs/0000-framework/` and milestones reference them by section. A milestone is one epic issue and one plan: a planner agent writes `specs/NNNN-<slug>/plan.md` from the referenced sections, a plan reviewer agent checks it, a human approves it where it is flagged, and its tickets are then created as issues and worked one by one, each in its own small pull request. The full spec-then-plan process of ADR-0024 and ADR-0025 applies to every change after the initial build.

## 1. Ground rules

1. Read `00-overview.md`, then the ADRs 0001–0006 and 0023–0029, then the spec file of the milestone. Load the matching skill before writing code in an area.
2. **M0 is installed by hand.** The factory does not build its own gates: the human owner and one agent session copy the kit (`factory/` of this specification: `.claude/`, the `factory-*` workflows, `.github/factory/`, `CODEOWNERS`, the templates) into the repository, create the GitHub App `yaf-factory`, the branch rules, the labels and the Project, and prove the gates with the acceptance criteria of `17` §7 **before any other milestone starts**. From M1 on, all work goes through the factory.

   > **Amended 2026-11-24**, by the owner's decisions of 2026-11-20 (`specs/0005-repository-skeleton/plan.md`, decision log Q1 and Q6). Only the factory kit is installed by hand. During the initial build the factory may build M0's code half and its guard files, through factory tickets that are each in tier high. The trial of `17` §7 for public API that the merged plan declares (tier medium, merges automatically) runs on M1's first ticket that declares public API; the other trials run before M1's tickets start.
3. **A milestone is an epic, a plan and its tickets.** The plan holds the approach, the declared public API, schema and error-code delta (`factory:delta`), a decision log, every deviation from this specification, and a **Tickets** table. It is merged through its own pull request after the plan reviewer's check; a human maintainer approves it when it is flagged (it declares a public API change, a schema change, a new dependency, a deviation, or a ticket in tier high), which is the case for most plans of the initial build. Merging the plan creates the tickets as sub-issues of the milestone's epic. There is no `tasks.md` and no progress log: status, evidence and deviations live on the tickets and their pull requests. A deviation that changes scope, security or a public contract is also made in `specs/0000-framework/` through a pull request a code owner approves, so the specification stays the single description of the framework.
4. **One ticket, one worktree, one pull request**, within the size limits of the policy (200 lines soft, 400 hard). Tickets that do not depend on each other and whose `touches` do not overlap may run in parallel, up to `limits.parallelImplementers`. A pull request in tier low or medium merges automatically; one in tier high waits for a human maintainer (`17` §2.2).
5. **The human checks the exit criteria.** When all tickets of a milestone are `Done` the orchestrator stops and reports; the human owner checks the exit criteria of §2 and closes the epic before the next milestone's tickets are picked up. The orchestrator also stops at any ticket labelled `needs-human`.
6. The repository must be **green at the end of every milestone**: builds with zero warnings, all tests pass, generated references in sync. No milestone leaves a stub that throws `NotImplementedException` in a public path.
7. Build vertically where possible: a thin end-to-end slice first (M3), then widen.
8. Take current stable versions of third-party packages from the feed; record them in `Directory.Packages.props`; where this spec's assumption about a third-party API turns out wrong, follow the library's documentation and note the deviation in the plan's decision log. Do not invent APIs.
9. Anything this specification leaves open is listed in §4. Do not guess silently: decide, record the decision in the plan's decision log or, once the plan is merged, in a comment on the ticket (or an ADR when it is architectural), and continue; ask the human when it changes scope, security or public contracts.
10. Never weaken a guard to get green (ADR-0026). A guard change needs the `guard-change` label applied by a human maintainer; an agent never applies it, nor `size-accepted` or `major-release`.
11. Build only what §2 lists. The packages of §6 are not part of the initial build, even where a contract mentions them.

## 2. Milestones

| # | Milestone | Delivers | Spec sections | Exit criteria |
|---|---|---|---|---|
| M0 | Repository skeleton | Solution, build props, CPM, banned APIs, `.editorconfig`, `AGENTS.md` hierarchy, `.agents/skills`, ADRs, `specs/0000-framework/`, `ci.yml` (build + test + title check + secret scan + instruction validation); **the factory kit installed by hand**: `.claude/` agent definitions and settings, `factory-*` workflows, `.github/factory/` policy and scripts, `CODEOWNERS`, branch rules with the required checks and the merge queue, the `yaf-factory` app, labels, the `YAF factory` Project, issue and pull request templates | `01`, `17` §1–2, §6, `agents/`, `factory/`, ADR-0029 | CI green on an empty solution with one placeholder test; the gate acceptance criteria of `17` §7 pass on trial pull requests (low and medium merge automatically, high waits for a human, oversize and undeclared delta fail, the tier cannot be lowered, the gate scripts are read from the base branch) |
| M1 | Core | `YAF.Core`: results, errors, ids, classification attributes; the `Analyzers` project packed into it: typed-id generator, error-code generator, `YAF3001–3004`, `YAF2009` | `03`, `02` §2–3 | Acceptance of `03` §5 items 1, 2 and 4 |
| M2 | Domain | `YAF.Domain` complete, with typed memento members; `YAF.Testing` (pure helpers: memento, result, aggregate and state machine assertions, test clock, approval helper) | `02`, `15` | Acceptance of `02` §12 items 1 and 2; a domain test project references `YAF.Testing` and nothing from ASP.NET Core or EF Core |
| M3 | Thin slice | `YAF.Application` (contracts, built-in dispatcher, pipeline with telemetry/validation/unit of work with flush-and-dispatch rounds, read-only query transactions), `YAF.Persistence.EfCore(.PostgreSql)` (context, conventions and converters for typed members, `[MementoData]` generator, stamping, root versioning, repository base, unit of work), `YAF.Http` + `YAF.Http.MinimalApis` (result mapping, problem details), `YAF.Testing.Integration` (web factory, container fixture), sample `Catalog` with `Session` create/get; from `03`: `ResultFactory`, the `[assembly: YafService]` attribute and its static error registry | `04` §1–6, `05` §1–5, `08` §1–2, `15` §3, `03` §1, §2.1 | An HTTP test creates and reads a session on PostgreSQL; two concurrent commands that each change only a child of one aggregate do not both commit; acceptance items 3 and 5 of `03` §5 |
| M4 | Identity and tenancy | Execution context with actor and initiator, authentication schemes with signed development tokens, user id resolution, authorization markers and middleware, edge metadata derived from markers, capability catalogue and privilege cache rules, input guard, tenant filters, row-level security, runtime and migrator roles, elevation, security events, authorization inventory, cross-tenant suite | `04` §7–8, `05` §4, `09` §1–5, §8 | Security pack suites for authn/authz/tenant pass on the sample |
| M5 | Configuration and observability | Options helper, startup checks, unsafe-options registry, redaction, generated logging, spans, metrics, health, telemetry catalogue, generated references | `10`, `12` | Acceptance of `10` §5 and `12` §9 |
| M6 | Data protection and audit | Key rings, field protection in the mapping layer (repository base and read-model query helper, `YAF5001`), field ids and `fields.approved.txt`, `[StoredInClear]` and `YAF2008`, searchable hashes, subject keys, rotation and re-encryption job, audit rows with the chain sealer and anchor, monthly partitions, the maintenance role, checkpoints, business events | `05` §6–8, `09` §6 | Acceptance items of `05` §11 for protection and audit; a renamed column keeps its ciphertext readable; writes of one tenant do not wait for each other on the audit trail |
| M7 | Messaging | Outbox, relay, inbox, idempotency table, envelope with initiator, in-memory transport, conformance suite; then RabbitMQ and AWS SNS/SQS adapters; claim check; `YAF.Storage.AwsS3`, `YAF.Cryptography.AwsKms`; direct-connection and polling/lease fallbacks | `06` | Conformance green per transport (SNS/SQS when the LocalStack token is available, reported as skipped otherwise); sample `Registrations` consumes `SessionPublishedV1` |
| M8 | Jobs, cron, processes | Jobs store and runner, cron trigger on Cronos, process managers, heartbeats | `07`, `04` §9 | Sample registration flow incl. timeout works end to end |
| M9 | HTTP breadth | Controllers package, REST conventions (ETag, idempotency, paging), rate limits, headers, CORS, OpenAPI transformers and committed documents, framework endpoints, parity suite | `08` | Parity suite green for both styles |
| M10 | Privacy mechanisms | The remaining analyzers of `YAF2001–2008`, retention and purge (including partition drops with checkpoints), legal hold, restrictions, `YAF.Privacy.EfCore` | `11` | Acceptance of `11` §10 for retention and holds |
| M11 | Optional adapters | `YAF.Validation.FluentValidation`, `YAF.Markup.HtmlSanitizer`, `YAF.Http.Versioning` | `04` §4, `08` | Each adapter passes the contract tests of its port; `YAF.Http` references neither library |
| M12 | Export, erasure and evidence | Exporters/erasers generator, `IDataSubjectOperations`, crypto-shredding end to end, inventory and control matrix tooling | `11` | Acceptance of `11` §10 for export, erasure and generated evidence; an end-to-end test in the sample exports and erases one data subject across `Catalog` and `Registrations` |
| M13 | Dev host and templates | `YAF.Aspire.Hosting`, sample AppHost with PostgreSQL and RabbitMQ by default and opt-in LocalStack, `YAF.Templates`, `yaf` CLI, end-to-end tests | `14`, `16` | Acceptance of `14` §4 and `16` §5; a clean clone runs with one command and no account |
| M14 | Release and docs | semantic-release as the `release` job of `ci.yml`, `docs.yml`, package validation baselines, SBOM and attestations, DocFX site with all articles, compliance pages, README | `17` | Acceptance of `17` §7; a `1.0.0-alpha.N` prerelease from a dry run |
| M15 | Hardening | BreakIt suite, mutation testing baseline, benchmarks, full security pack on both styles, review of every public API name | `09` §12, `15` §7 | All acceptance criteria in all spec files of the 1.0 scope ticked in a final checklist |

> **Amended 2026-11-24**, by the owner's decisions of 2026-11-24 (`specs/0006-core/plan.md`, decision log Q1 and Q5). `ResultFactory`, the `[assembly: YafService]` attribute with its static error registry, and acceptance items 3 and 5 of `03` §5 move from M1 to M3: each needs a component of M2 or M3. M1 also delivers `YAF3004` (`03` §2).

> **Amended 2026-11-24**, by the owner's decision of 2026-11-24 (`specs/0007-no-package-lock-files/plan.md`, decision log; ADR-0032): M0 no longer delivers NuGet lock files; versions are pinned in `Directory.Packages.props`.

> **Amended 2026-11-25**, by the owner's decisions of 2026-11-25 (`specs/0008-m1-maintenance/plan.md`, decision log): the raw `Guid` id analyzer of row M1 is renamed from `YAF1001` to `YAF2009` (`02` §3, `01` §2.1).

> **Amended 2026-11-25**, by the owner's decisions of 2026-11-25 (`specs/0009-domain/plan.md`, decision log Q1 and Q2). Item 3 of `02` §12 and `MementoAssert.RoundTrips<T>()` over the generated data class go with the `[MementoData]` generator in M3. Item 4 is met by `YAF2009` in M1 and by M6 and M10 for the other analyzers. Each fake of `15` §3.1 comes with the milestone of its port, and `YAF.Testing` references `YAF.Application` from M3 on.

> **Amended 2026-11-25**, by the owner's decisions of 2026-11-25 (`specs/0009-domain/plan.md`, decision log Q4 and Q5). `YafArchitectureRules`, `TestIds` and the synthetic data generators of `15` §3.1 come with M3, `[Control]` with M4, `LogAssert` with M5. The markers of `02` §11 ship in `YAF.Domain` with their readers: `[NoDataSubject]` in M6, `[Retention]` and its enumerations in M10, `[OnErasure]` in M12.

Dependencies: M1 → M2 → M3 → {M4, M5} → M6 → M7 → M8; M9 after M4; M10 after M6–M8; M11 after M9; M12 after M10; M13 after M9 and M12; M14 can start after M3 (CI parts and alpha prereleases) and finish last.

Parallelism: within a milestone the orchestrator runs tickets in parallel worktrees when they do not depend on each other and their `touches` do not overlap. Independent packages (the two broker adapters, the two HTTP styles, the three optional adapters of M11) are the natural candidates, each against the conformance or parity suite, whose ticket comes first and blocks them. Tickets that touch a shared file (`Directory.Packages.props`, `PublicAPI.Unshipped.txt`, `errors.approved.txt`, a migration folder) run one after another.

`1.0.0` is not a milestone of the agents' work: after M15 a maintainer cuts it with the reviewed pull request described in `17` §3. Until then `main` publishes prereleases only.

## 3. Definition of done for the whole recreation

- Every "Acceptance criteria" list in `spec/02`–`12` and `14`–`17` is satisfied and traceable to tests, apart from items that concern a package of §6.
- `docs/compliance/controls.md` has a passing test for every tenet S1–S10 and every control C-* that belongs to the 1.0 scope; controls of later packages are listed as "not shipped".
- The sample runs from a clean clone with one command and no account or secret beyond what the AppHost generates.
- `dotnet new yaf-solution` + `yaf-service` produce a running, tested service in each HTTP style (minimal APIs, controllers).
- No reference in code, docs or history to any product the specification was derived from.
- No package, option or sample code for anything listed in §6.

## 4. Open points the implementer must settle (and record)

| Topic | What to decide | Default if nothing argues otherwise |
|---|---|---|
| HTML sanitizer for `[Markup]` | Which library `YAF.Markup.HtmlSanitizer` wraps | A maintained, permissively licensed sanitizer library behind `IMarkupSanitizer` |
| API versioning package | Compatibility of `Asp.Versioning` with the pinned OpenAPI package on `net11.0` | Current stable in `YAF.Http.Versioning`; verify document-per-version support |
| Aspire AppHost project shape | SDK vs package-reference style | Whatever the current stable Aspire prescribes |
| LocalStack wiring | Own container wrapper vs community integration | Own wrapper in `YAF.Aspire.Hosting` (§`14`), opt-in |
| `[Union]` support on `Result` | Exact shape of the non-boxing pattern in the shipped C# 15 | Follow the language reference; if the pattern cannot be made non-boxing, ship without `[Union]` and record it |
| semantic-release before 1.0.0 | Whether a never-used `stable` release branch with `main` as a prerelease branch gives `1.0.0-alpha.N` as intended | The arrangement of `17` §3; otherwise the nearest configuration that publishes only prereleases from `main` |
| Commit-safe order for the audit sealer | How the sealer knows that no transaction older than a row can still commit | Seal only rows whose transaction id is below the oldest running transaction's snapshot minimum; verify with a concurrency test |
| Partitions of append-only tables | Native declarative partitioning managed by the retention job vs an extension | Native partitioning; the maintenance role creates the next months ahead and drops expired ones |
| Read-model decryption helper | Exact shape of the query helper so that it composes with paging and projection | `ToUnprotectedListAsync()` / `FirstOrDefaultUnprotectedAsync()` over row types with `[Protected]` members |
| OpenTelemetry messaging attribute names | They are not stable | Use the current convention behind constants |
| Regulatory clause identifiers | Verify against official texts | Publish only verified identifiers; mark the rest "unverified" |
| Package id prefix reservation and licence | Owner's decision | MIT; `YAF.` prefix |
| Merge queue | Whether the repository's GitHub plan offers the merge queue, and how it combines with code-owner review and auto-merge | Use it when available; otherwise require branches to be up to date before merging and let auto-merge serialise |
| Sub-issues and issue dependencies | Whether sub-issues and "blocked by" links are available through the API the scripts use | Use them; otherwise a task list in the epic and a `Blocked by: #n` line in the ticket body, read by `factory-status.yml` |
| Branch rules | Rulesets vs classic branch protection: required checks, code-owner review with no general approval count, the release app's bypass, pinning the gate workflows to the base branch | Rulesets; verify each rule with the trial pull requests of M0 |
| Agent identity for local work | How a local session obtains a `yaf-factory` installation token without a long-lived secret on disk | A short-lived token minted from the app's key held in the operating system's credential store; a machine user with a fine-grained token as the fallback |
| CI reviewer and secrets | Whether commands the reviewing agent runs can read the model API key, and how to limit that | Environment `factory-review` with a spend-limited key and no other secret; verify against the action's documentation |
| Claude Code specifics | Agent definition fields, worktree isolation, headless flags and the review action's inputs change between versions | Take them from the current documentation when installing the kit; record the versions verified against |
| Docs hosting of `type` URIs for problem details | Whether a public error-doc site exists | `urn:yaf:error:<code>` until an app configures a base URI |

Settled elsewhere and therefore not open: the test libraries (ADR-0018), the cron library and the policy for later .NET versions (ADR-0003), the dispatcher (ADR-0009).

## 5. Suggested first plans

`0001-repository-skeleton` (M0, done by hand with the human owner; its plan is the kit's install checklist and the trial pull requests that prove the gates), `0002-results-and-errors`, `0003-typed-ids`, `0004-domain-building-blocks`, `0005-thin-slice`, then one plan per row of §2 (split M6, M7 and M9 into two or three plans each). Each folder holds `plan.md` and, where needed, `research.md`. A plan cites the requirements it implements by file and section of `specs/0000-framework/`, maps each acceptance criterion to a ticket and a test level, and ends with the Tickets table: key, title, sections, depends on, touches, expected tier, size estimate, failing test first, verification command. The implementation is reviewed against the specification, the merged plan and the ticket. The requirements are not copied into a second spec.

> **Amended 2026-11-24**, by the owner's decision of 2026-11-20 (`specs/0005-repository-skeleton/plan.md`, decision log Q2). The folder names above are suggestions; a plan's folder takes the next free number under `specs/`. The factory's own plans took 0001–0004, so M0's plan is `0005-repository-skeleton`.

Slice for small pull requests: a ticket is one concern that fits the soft size limit, names the failing test it starts with, and leaves the repository green when merged. A generator, its analyzer and its documentation are three tickets, not one. Dependent tickets are stacked and merge in order.

## 6. After 1.0

Planned for later releases, each through the full process (its own spec, plan and tickets) and each as an addition that breaks nothing:

| Addition | Spec sections that already describe it | Needs first |
|---|---|---|
| `YAF.Messaging.AzureServiceBus`, with the Service Bus emulator in `YAF.Aspire.Hosting` | `06`, `14` | The transport conformance suite (M7) |
| `YAF.Storage.AzureBlob`, `YAF.Cryptography.AzureKeyVault` | `06`, `05` §6 | The object-store and key-provider contract tests |
| `YAF.Http.FastEndpoints` | `08` | The parity suite (M9), widened to a third style |
| Privacy coordinator bounded context: data subject request aggregate and orchestration across services, with a `Privacy` service in the sample | `11`, `16` | `IDataSubjectOperations` (M12), `YAF.Privacy.EfCore` (M10) |
| Incident register (`IIncidentRegister`), `DecisionRecord` | `11`, `13` | `YAF.Privacy.EfCore` |
| `YAF.AI` | `13` | Redaction and classification (M5, M10) |

Third-party mediator adapters are not on this list: they were decided against (ADR-0003, ADR-0009).
