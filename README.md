# YAF backend framework

YAF is a set of .NET libraries for building backend services as bounded contexts. It provides:

- DDD building blocks
- a CQRS pipeline with its own dispatcher
- transactional outbox and inbox messaging over pluggable brokers
- EF Core persistence on PostgreSQL, with tenant isolation and a tamper-evident audit trail
- HTTP adapters for controllers and minimal APIs
- security, privacy and telemetry plumbing that is part of the design from the start

> **Status:** pre-alpha. The repository has the specification and no code yet. The framework will be built from the specification, milestone by milestone.

## Specification

The full specification is in [`docs/spec`](docs/spec/README.md):

| Path | Content |
|---|---|
| [`docs/spec/spec/`](docs/spec/spec/) | Normative specification, one file per area (start with [`00-overview.md`](docs/spec/spec/00-overview.md)) |
| [`docs/spec/adr/`](docs/spec/adr/README.md) | Architecture decision records the repository starts with |
| [`docs/spec/agents/`](docs/spec/agents/README.md) | Desired `AGENTS.md` content and the in-repo agent skills |
| [`docs/spec/factory/`](docs/spec/factory/README.md) | The software factory kit (Claude Code agents, GitHub workflows, gate scripts) |
| [`docs/spec/history/`](docs/spec/history/) | How the specification got here |

The suggested reading order is in [`docs/spec/README.md`](docs/spec/README.md#reading-order). Milestones and open points are in [`18-build-plan.md`](docs/spec/spec/18-build-plan.md).

## How work happens

The framework is built by a software factory (ADR-0029): separate agents write and review specs, plans and code, and every ticket becomes one small pull request. A risk tier computed from [the policy](docs/spec/factory/.github/factory/policy.json) decides whether a pull request merges automatically on green checks or waits for a human maintainer.

- Agents start from [`AGENTS.md`](AGENTS.md). It loads the root instructions from the bundle and maps the bundle's paths to this repository.
- The kit is described in [`docs/spec/factory/README.md`](docs/spec/factory/README.md). Its gate scripts, policy and prompts are used in place. `.github/workflows/factory-*.yml` and `.github/CODEOWNERS` are installed from it.
- `.claude/agents/`, `.claude/skills/`, `.agents/skills/`, `.claude/settings.json` and the GitHub templates are generated references to `docs/spec/`. After changing their source, run `node .github/scripts/sync-references.mjs`.

## Planned platform

- .NET 11 and later (the first release targets `net11.0`)
- PostgreSQL as the first-class store
- Aspire as the local development host, with RabbitMQ by default and LocalStack as an opt-in for AWS
- Distribution as NuGet packages (`YAF.*`) and `dotnet new` templates

## License

[WTF-CORP WTFPL](LICENSE). Do what you want; mentioning the source is appreciated.
