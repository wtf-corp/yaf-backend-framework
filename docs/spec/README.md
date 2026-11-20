# YAF — framework specification

A specification from which an agent (or a team) can build the YAF backend framework, its reference system, its templates, its documentation and its delivery pipeline from an empty repository.

YAF is a set of libraries for .NET 11 and later for building backend services as bounded contexts: DDD building blocks, a CQRS pipeline with its own dispatcher, outbox/inbox messaging over pluggable brokers, EF Core persistence on PostgreSQL with two-layer tenant isolation and a tamper-evident audit trail, HTTP adapters for controllers and minimal APIs, and security, privacy and telemetry plumbing designed in rather than added on. An adapter for FastEndpoints, the Azure adapters, the privacy coordinator and a governed AI gateway are planned for releases after 1.0 (`spec/18-build-plan.md` §6).

## Reading order

1. `spec/00-overview.md` — what YAF is, the principles, what is deliberately improved.
2. `adr/README.md`, then ADR-0002 … 0006 (architecture, dependencies, security by design, compliance by design, packaging) and ADR-0023 … 0029 (how agents and humans work here; ADR-0029 is the software factory: tickets, risk tiers, automatic merge).
3. `spec/18-build-plan.md` — milestones, dependencies, open points.
4. `factory/README.md` — the kit that is installed by hand at M0, before anything else is built.
5. The spec file of the area you are about to build, with its skill from `agents/skills/`.

## Contents

### `spec/` — normative specification

| File | Area |
|---|---|
| `00-overview.md` | Vision, principles, improvements, non-goals |
| `01-repository-and-packages.md` | Repository layout, package list and dependency graph, build settings, banned APIs, conventions |
| `02-domain.md` | `YAF.Core`, `YAF.Domain`: typed ids, aggregates, mementos, value objects, events, rules, policies, specifications, state machines, privacy markers |
| `03-results-and-errors.md` | Own `Result` type, error catalogues, generated codes, RFC 9457 mapping |
| `04-application.md` | CQRS contracts, pipeline middleware, validation, the built-in dispatcher, unit of work, execution context, authorization model, process managers |
| `05-persistence.md` | EF Core model, interceptors, repositories, tenant isolation with row-level security, field protection, audit trail, migrations |
| `06-messaging.md` | Integration events, outbox, inbox, CloudEvents envelope, transport abstraction (in-memory, RabbitMQ, AWS SNS/SQS; Azure Service Bus later), conformance suite |
| `07-jobs-and-scheduling.md` | Jobs, delays, cron, heartbeats |
| `08-http.md` | The endpoint styles (minimal APIs, controllers; FastEndpoints later) over one core, REST conventions, framework endpoints, OpenAPI, limits |
| `09-security.md` | Security by design: tenets, threat model, authentication, authorization, cryptography, security events, verification |
| `10-configuration.md` | Typed validated options, startup checks, secrets, feature flags |
| `11-privacy-and-compliance.md` | Classification, redaction, retention, data subject requests, generated evidence, control catalogue, what must never be claimed |
| `12-observability.md` | Logging, traces, metrics, health, resilience |
| `13-ai.md` | Optional governed AI gateway (design of a package planned after 1.0) |
| `14-dev-host-and-templates.md` | Aspire AppHost with PostgreSQL and RabbitMQ by default and LocalStack as the opt-in AWS provider, `dotnet new` templates |
| `15-testing.md` | Test levels, `YAF.Testing`, architecture rules, security test pack, gates |
| `16-reference-sample.md` | The `conference` reference system and its concept coverage |
| `17-ci-release-docs.md` | GitHub workflows, the factory gates and required checks, automatic versioning, release with provenance, documentation site |
| `18-build-plan.md` | Milestones, definition of done, what comes after 1.0, open points |

### `adr/` — decision records the repository starts with

29 ADRs plus the template; see `adr/README.md`. Each ends with **Enforcement** and **Guidance for agents**.

### `agents/` — agent guidance (tool-neutral)

| File | Purpose |
|---|---|
| `README.md` | Design of the instruction hierarchy; in-repo and recommended third-party skills |
| `AGENTS.root.md` | Desired content of the root `AGENTS.md` |
| `AGENTS.nested.md` | Desired content of the nested `AGENTS.md` files and pointer files |
| `skills/` | 23 skills: 15 framework concerns, 8 process skills |

### `factory/` — the software factory kit (Claude Code and GitHub)

The rules of the process are tool-neutral and live in the ADRs, `AGENTS.md` and the skills. `factory/` is their shipped implementation, laid out as it sits in the repository root and copied there at M0:

| Path | Purpose |
|---|---|
| `README.md` | What the kit is, the install steps, the human owner's setup checklist, what to verify against current documentation |
| `.claude/agents/`, `.claude/settings.json` | One agent definition per role (spec writer and reviewer, planner and plan reviewer, implementer, code reviewer, pull-request reviewer); permission deny rules |
| `.github/workflows/factory-*.yml` | Gates, pull-request and document review, auto-merge, ticket creation, status, digest |
| `.github/factory/` | `policy.json` (risk tiers, size limits, roles, limits), the gate scripts, labels, the GitHub setup script, reviewer prompts |
| `.github/CODEOWNERS`, templates | Code owners of the sensitive paths; pull request, ticket and epic templates |

### `history/` — how the specification got here

| File | Purpose |
|---|---|
| `2026-11-specification-review.md` | The review of the first complete draft: findings, the decisions taken with their options and reasons, what changed. History, not a rule |
| `2026-11-software-factory.md` | How the process became a software factory: what was asked, what current practice suggested, the decisions with their options, what was written and what is untested |

## Decisions taken while writing this specification

| Question | Decision |
|---|---|
| Namespace | `YAF.*` |
| Target platform | .NET 11 and later. The first release targets `net11.0` only (C# 15). Each new .NET is added as a further target in a minor release; a target is dropped in the next major after its support ends (ADR-0003) |
| Scope of 1.0 | One of each kind first: built-in dispatcher (no mediator adapters); in-memory, RabbitMQ and AWS SNS/SQS transports; minimal APIs and controllers; AWS storage and key adapters; the privacy mechanisms. Azure adapters, FastEndpoints, the privacy coordinator, the incident register and `YAF.AI` follow in later releases (`spec/18-build-plan.md` §6) |
| Domain additions | Specifications and policies; process managers and a framework inbox; privacy-aware domain (classification, data subjects, retention, crypto-shredding, export/erasure); state machines, smart enumerations and four ready value objects |
| Distribution | NuGet packages (one per building block and per vendor adapter) and `dotnet new` templates |
| Reference system | A new neutral domain: conference catalogue and registrations |
| AI Act scope | An optional `YAF.AI` package, after 1.0 |
| Own implementations | Result type, dispatcher, validation rule builder, approval-test helper are YAF's own; cron expressions are parsed by Cronos; other third-party engines are optional adapters |
| Versioning | Conventional Commits and semantic-release as the last job of the build pipeline (GitHub Actions), NuGet trusted publishing, attestations. Prereleases only until 1.0.0; afterwards breaking changes are batched on `next` |
| Process for the initial build | This specification is the approved spec; each milestone needs a plan only. Approval is a human's review on GitHub, not a line in a file |
| How the work is done | A software factory (ADR-0029): separate agents write and review specs, plans and code; a plan becomes GitHub issues, each worked in its own worktree and small pull request (200 lines soft, 400 hard by default; this repository uses 400 and 1000 during the initial build, until `1.0.0`); authoring is local, gating runs in GitHub Actions |
| Who merges | Risk tiers computed by script from one policy file: low and medium merge automatically on green checks and the reviewing agent's check, high (security, privacy, guard files, breaking changes, new dependencies, undeclared API or schema change) waits for a human maintainer. An agent never approves |

## Caveats for the implementer

- **Third-party facts age.** Package names, versions, licences and API shapes mentioned here were collected in November 2026, partly from secondary sources. Take current stable versions from the feed and verify APIs against the libraries' own documentation; `spec/18-build-plan.md` §4 lists the points known to need a decision.
- **Regulatory identifiers must be verified.** The clause references in `spec/11` were not all checked against official texts (some standards are paywalled; some regulations have been amended recently, including the AI Act's application dates). Publish only what has been verified.
- **The framework supplies controls and evidence, not compliance.** See `spec/11` §9.
- **LocalStack needs an account token.** The dev host and the templates therefore default to RabbitMQ, which needs no account; LocalStack is opt-in (`spec/14`).
- This specification deliberately contains no reference to any existing product it was derived from; the recreated repository must not either.
