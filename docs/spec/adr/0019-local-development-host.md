# ADR-0019 — Local development host

> **Status** Accepted · **Date** 2026-11 · **Deciders** maintainers

## Context

A developer or agent must be able to run a whole solution — services, database, broker, object store, telemetry — with one command, with the same wiring every time. The framework supports several brokers and AWS as a first-class cloud target, so the local host must be able to emulate AWS services and switch transports, while a fresh clone must start without anyone creating an account first.

## Drivers

- One command to a running, observable system; no hand-installed dependencies besides the SDK and Docker.
- Local wiring mirrors deployed wiring: connection strings, secrets, ordering of migrations before services.
- AWS messaging and storage are exercisable locally.
- The default path needs no third-party account and no token.
- Local runs validate tokens exactly as production does.
- End-to-end tests reuse the same host definition.

## Options considered

| Option | For | Against |
|---|---|---|
| Docker Compose + scripts | Universal | No typed model, no dashboard, wiring duplicated in scripts |
| Per-developer local installs | Fast start | Drift, onboarding cost |
| Cloud dev environments only | Realistic | Cost, latency, needs accounts |
| **Aspire AppHost** | Typed orchestration, service discovery, dashboard, test host | Fast-moving versions; development-time only |
| LocalStack as the default broker and store | Closest to an AWS deployment | Needs an account token, so the first run of a clone or of a generated solution fails for anyone without one |
| In-memory transport as the AppHost default | Fewest containers | The default run never meets a real broker: serialization, topology and redelivery problems appear late |
| An option that accepts unsigned tokens in Development | No issuer to run | An unsafe switch that must be guarded by an environment probe, and a validation path that differs from production |

## Decision

1. **Aspire is the development host** for the reference sample and for every solution the templates generate. It is development-time only; production deployment is out of scope for the framework.
2. **AppHost composition** (helpers in `YAF.Aspire.Hosting`):
   - PostgreSQL with one database per service.
   - Each service's **migrator** runs first; the service waits for its completion.
   - Secrets are AppHost **parameters marked secret**: the encryption key, the redaction HMAC key, and any provider tokens. They come from the AppHost's user secrets or are prompted by the dashboard; they are never committed.
   - A development issuer (`AddYafDevIssuer`) that mints signed tokens for the configured development users and is injected into the services as their JWT provider. No option switches signature validation off; a service run on its own uses `dotnet user-jwts`.
3. **RabbitMQ is the default broker**, with a local S3-compatible container as the object store: a real broker, no account, works offline. The AppHost passes `Messaging:Transport=RabbitMq` to every service. The templates generate the same default.
4. **LocalStack is opt-in** (AppHost parameter `UseLocalStack`, template option `--aws`) and is the AWS provider for SNS, SQS, S3 and KMS in local runs: the AppHost starts the container, provisions topics, queues, buckets and keys (topology mode `Provision`), and sets `Aws:LocalStack` and `Messaging:Transport=AwsSqs` for the services. LocalStack requires an account and an auth token; the token is a secret AppHost parameter, and the AppHost fails at start with a clear message when LocalStack is switched on without it. The in-memory transport is the default for a service run on its own and for tests. The Azure Service Bus emulator joins the AppHost in the later release that brings `YAF.Messaging.AzureServiceBus`.
5. **Telemetry** goes to the Aspire dashboard over OTLP through the generated `ServiceDefaults` project (logs, traces, metrics, health).
6. **Service defaults belong to the app.** The solution template generates the project; the framework contributes sources, meters and health checks.
7. **End-to-end tests** start the AppHost through `Aspire.Hosting.Testing` and talk to services over HTTP. They are black-box: replacing services inside a host is done with the test host factory in `YAF.Testing.Integration` instead.
8. **Version policy.** Aspire, its hosting integrations and the community LocalStack integration release on different cadences and do not always align (Aspire made a major version jump in late 2026, and the AppHost project shape changed across majors). Versions are pinned in `Directory.Packages.props`, upgraded together in one pull request, and verified by the end-to-end suite.

## Consequences

- New contributors run `dotnet run --project <AppHost>` and get a dashboard with every resource.
- The default run does not exercise the AWS adapters. Teams that deploy to AWS switch LocalStack on, which needs a token locally and in CI; CI runs the SNS/SQS conformance and end-to-end suites only when the token secret is present and reports them as skipped otherwise, so a release must check that they ran.
- Two local setups (RabbitMQ with a storage emulator; LocalStack) must both be kept working.
- One more development resource, the issuer, in every AppHost.
- Aspire upgrades are a maintenance task with real breakage risk; pinning contains it.
- A service still runs alone on the in-memory transport with only PostgreSQL, which keeps the inner loop fast.

## Enforcement

- CI job runs the sample's end-to-end tests through the AppHost with the default setup on every run; the LocalStack row of the matrix runs when the token secret is present and is reported as skipped otherwise.
- A test starts the sample AppHost with no secret parameter set other than the generated keys, to prove the no-account default.
- Template tests generate a solution, build it with warnings as errors and run its tests.
- Startup checks in services: LocalStack options honoured only in Development or Testing; required secrets present. The development issuer refuses to start outside Development; the security pack's `TokenAttacks` suite proves unsigned tokens are refused in every environment.
- Secret scanning (pre-commit and CI) keeps tokens and keys out of the repository.
- Dependabot groups Aspire-related packages so they move together.

## Guidance for agents

- Run and verify changes through the AppHost when they touch wiring, messaging, storage or telemetry; say which transport you used.
- Add a new infrastructure dependency to the AppHost and the template in the same change.
- Never hard-code ports, connection strings, tokens or keys; use resource references and secret parameters.
- Do not make LocalStack mandatory for a test that the in-memory transport can prove, and never make it a requirement of the default run of the sample or a template.
- Get development tokens from the development issuer or `dotnet user-jwts`; do not add a way to bypass token validation.
- Do not upgrade one Aspire package alone; move the group and run the end-to-end suite.
- Do not treat the AppHost as deployment configuration.

## Related

0003, 0006, 0012, 0016, 0017, 0018, 0020
