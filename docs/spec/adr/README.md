# Architecture decision records

The decisions the YAF repository starts with. An accepted ADR is immutable except for its status; a changed decision is a new ADR that supersedes the old one (ADR-0001). Agents draft ADRs as `Proposed`; a human accepts them, by a code owner's approving review on the pull request that carries the ADR. A change under `adr/` is always in the highest risk tier (ADR-0029).

Every ADR has the sections Context, Drivers, Options considered, Decision, Consequences, **Enforcement** (the analyzer, test, CI gate or startup check that keeps the decision true) and **Guidance for agents** (the working rules that follow from it). The template is `0000-adr-template.md`.

## Foundations

| ADR | Decision |
|---|---|
| [0001](0001-record-architecture-decisions.md) | Record architecture decisions |
| [0002](0002-architecture-principles.md) | Architecture principles: bounded contexts, clean layers, transactional inside and eventual across |
| [0003](0003-technology-stack-and-dependency-policy.md) | Technology stack; own the contracts, plug the engines; dependency allow-list |
| [0004](0004-security-by-design.md) | **Security by design** |
| [0005](0005-compliance-by-design.md) | **Compliance by design** |
| [0006](0006-repository-structure-and-packaging.md) | Repository structure and NuGet packaging |

## Building blocks

| ADR | Decision |
|---|---|
| [0007](0007-domain-model-patterns.md) | Domain model patterns |
| [0008](0008-results-and-error-catalogue.md) | Own result type and approved error catalogue (superseded by 0031) |
| [0031](0031-results-and-error-catalogue-revised.md) | Own result type and approved error catalogue, revised for M1: `[Union]` on `Result<T>` only, factories without a code parameter, `YAF3004` (Proposed) |
| [0009](0009-application-pipeline-and-dispatcher.md) | Application pipeline and own dispatcher behind YAF's request contracts |
| [0010](0010-persistence-tenancy-and-unit-of-work.md) | Persistence, tenancy and unit of work |
| [0011](0011-data-protection-at-rest.md) | Data protection at rest |
| [0012](0012-messaging-outbox-inbox-and-transports.md) | Messaging: outbox, inbox, CloudEvents, pluggable transports |
| [0013](0013-jobs-cron-and-process-managers.md) | Jobs, cron and process managers |
| [0014](0014-http-adapters.md) | HTTP adapters: minimal APIs and controllers (FastEndpoints later) |
| [0015](0015-identity-authentication-and-authorization.md) | Identity, authentication and authorization |
| [0016](0016-observability.md) | Observability |
| [0017](0017-configuration.md) | Configuration |
| [0022](0022-governed-ai-use.md) | Governed AI use (package planned after 1.0) |

## Engineering

| ADR | Decision |
|---|---|
| [0018](0018-testing-strategy.md) | Testing strategy |
| [0030](0030-benchmark-library.md) | BenchmarkDotNet as the benchmark library, test-only (Proposed) |
| [0019](0019-local-development-host.md) | Local development host: Aspire with PostgreSQL and RabbitMQ; LocalStack opt-in |
| [0020](0020-versioning-and-release-automation.md) | Automatic versioning and release (GitHub example) |
| [0032](0032-nuget-restore-without-lock-files.md) | Restore NuGet packages without lock files; replaces the lock-file mechanism named in 0003 and 0020 (Proposed) |
| [0021](0021-documentation-and-generated-references.md) | Documentation site and generated references |

## Agentic process

| ADR | Decision |
|---|---|
| [0023](0023-agentic-development-process.md) | Agentic development process: spec → plan → tickets → code → review, separate writing and reviewing agents, human approval placed by risk (superseded in part by 0034) |
| [0024](0024-specifications.md) | Specifications, reviewed by an agent and approved by a human |
| [0025](0025-implementation-plans.md) | Implementation plans: approach, declared delta and ticket list (superseded in part by 0034) |
| [0026](0026-coding-rules-for-agents-and-humans.md) | Coding rules for agents and humans (superseded in part by 0034) |
| [0027](0027-review.md) | Review: local code review and the pull-request review in CI (superseded in part by 0034) |
| [0028](0028-agent-instructions-and-skills.md) | Agent instructions (`AGENTS.md`), skills and agent definitions |
| [0029](0029-software-factory.md) | Software factory: tickets, risk tiers and automatic merge (superseded in part by 0034) |
| [0034](0034-guard-changes-declared-by-a-plan.md) | Guard changes declared by a merged plan: exact `.editorconfig` sections and package pins are tier medium, except on `neverLowered` paths; supersedes 0029 §4 in part (Proposed) |

Normative detail for each building block is in `../spec/`; the ADRs hold the decision and its reasons.
