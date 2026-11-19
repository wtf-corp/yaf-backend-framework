# ADR-0004 — Security by design

> **Status** Accepted
> **Date** 2026-11
> **Deciders** Framework maintainers

## Context

A framework decides what is easy and what is the default for every service built on it. If security is a module a service opts into, the service that forgets is exposed, and nobody notices until an incident. In the prior design several protections were conventions: a tenant filter that a single call could switch off and that had to be switched back by hand, authorization declared only on controllers so a job or a consumer reaching the same use case was unchecked, context providers any code could overwrite.

Much of YAF's code, and of the services on it, is written by AI agents. An agent optimises for the stated task; a guard that blocks the task looks like an obstacle. Security therefore has to be a property the code keeps by construction, verified by machines.

## Drivers

- A service built with the templates is secure before its author has made a single security decision.
- Leaving out a declaration results in refusal, never in exposure.
- Misconfiguration stops the host at startup, with the reason.
- Every weakening of a control is explicit, scoped, reasoned and recorded.
- Each property is verified by something that fails automatically.

## Options considered

| Option | For | Against |
|---|---|---|
| Security as opt-in features with documentation | Least friction | Relies on every developer and agent remembering; silent gaps |
| Secure defaults, freely overridable | Good start | Overrides accumulate unnoticed; no evidence |
| **Security as enforced properties: deny by default, fail closed, fail at startup, explicit recorded elevation, mechanical verification** | Gaps surface as build, startup or test failures | More up-front declarations; some friction in development |

## Decision

Security is a set of properties that every part of YAF keeps. `spec/09-security.md` is the contract; this ADR fixes the tenets.

### Tenets

| # | Tenet | Meaning in YAF |
|---|---|---|
| S1 | Deny by default | An endpoint without metadata requires an authenticated principal and a tenant. A request type without an authorization marker is refused by the pipeline. No CORS origin, trusted proxy or anonymous API docs until configured |
| S2 | Fail closed | No tenant: queries return nothing and writes are refused. Unknown key id: decryption throws. Privilege lookup failure: deny after a short, bounded stale window (privileged permissions: deny at once) |
| S3 | Fail at startup | Missing keys, missing required extension points, insecure flags outside Development, invalid options, missing topology: the host does not start, and reports all failures together |
| S4 | Defence in depth | Authorization at the edge and in the pipeline; tenant filter and row-level security; validation and domain invariants; redaction in logs and encryption at rest |
| S5 | Least privilege | Separate database roles for migrator, runtime and maintenance; the runtime role cannot alter schema or audit data; asynchronous work runs as the service, not as the user who started it; permissions per feature; privileged permissions flagged and audited |
| S6 | No secret in code, config files, logs or errors | Secrets come from secret stores; classified data is redacted in every sink; problem details never carry internals |
| S7 | Explicit elevation | Cross-tenant access, system principals and unsafe development switches are named, scoped, reasoned and recorded as security events |
| S8 | Mechanical enforcement | Each tenet has analyzers, architecture tests, startup checks or conformance tests; human review is never the only control |
| S9 | Secure supply chain | Locked, audited dependencies; a minimal dependency set; SBOM and provenance for every release |
| S10 | Evidence | Security-relevant facts are recorded (audit trail, security events) in tamper-evident form with synchronised UTC time |

### Threat model

OWASP API Security Top 10 (2023) is the checklist every framework feature is reviewed against; OWASP ASVS 5.0 Level 2 is the target for the parts a framework controls. The mapping of each risk to a control is in `spec/09-security.md` §2. For AI features, the OWASP Top 10 for LLM applications (ADR-0022).

Three boundaries are part of the model and are stated in the documentation:

- Row-level security protects against application bugs (a missing filter, a wrong context), not against arbitrary SQL executed as the runtime role, which could set the session variables itself.
- The broker is inside the trust boundary for the tenant id and the initiator carried in an envelope. The initiator grants nothing, so a forged envelope cannot gain a user's rights; it can start work as the consuming service. Envelope signing is a possible later addition.
- Cross-site scripting is controlled by output encoding in the client and by response headers. YAF validates input and does not rewrite it.

### What "by design" means per layer

| Layer | Built-in property |
|---|---|
| Domain | No static state; stored state that cannot be restored is a security event, not a silent default; classification declared on memento interfaces (ADR-0007) |
| Application | Every request declares its access (`[RequiresPermission]`, `[UserScoped]`, `[AllowAnonymousRequest]`, `[SystemOnly]`); undeclared requests are refused; the execution context is immutable, mandatory and cannot be created by application code; authorization is decided on the actor, never on the initiator; the input guard and validation run for every entry point, and input is rejected, never rewritten (ADR-0009) |
| Persistence | Tenant filter plus PostgreSQL row-level security set per transaction, framework tables included; every command runs in a transaction and queries in a read-only one; a write with a foreign tenant id is refused; field encryption driven by classification; audit rows written with the change and hash-chained by a sealer; three database roles (ADR-0010, ADR-0011) |
| Messaging and jobs | Tenant and initiator travel in the envelope and on job rows; the work runs as the service's system principal and may dispatch only `[SystemOnly]` requests; secrets refused in payloads; classified payloads encrypted at rest; TLS required; no polymorphic deserialization by type name (ADR-0012, ADR-0013) |
| HTTP | Strict token validation in every environment, hashed API keys, failed-credential throttling, strict JSON, size and time limits, rate limits, security headers, RFC 9457 errors without internals, one fixed middleware order validated at startup (ADR-0014, ADR-0015) |
| Configuration | Typed, validated options; secrets never defaults and never in committed files; unsafe options registry (ADR-0017) |
| Telemetry | Source-generated logging only; one redaction policy for logs, traces, dumps and errors (ADR-0016) |
| Delivery | Locked restore, advisory checks (dependency review on pull requests, daily audit, release gate), secret scan, static analysis, SBOM, attestations, protected main, CODEOWNERS on sensitive paths (ADR-0020) |

### Unsafe options

Every switch that weakens a control:

1. is an option named `Allow…` or `Disable…`, `false` by default;
2. is listed in `UnsafeOptionsRegistry` and in the generated `docs/compliance/unsafe-options.md`;
3. is refused outside Development by a startup check, unless explicitly acknowledged in `Security:AcknowledgedUnsafe`, which raises a `config.insecure_flag` security event and a startup warning.

No unsafe option bypasses authentication. There is no switch for unsigned tokens: local runs and tests use signed tokens from a development issuer (`dotnet user-jwts`; a test signing key in `YAF.Testing.Integration`), so token validation runs the same code in every environment.

Test doubles in `YAF.Testing` and `YAF.Testing.Integration` throw when constructed in a Production environment, and a production project may not reference those packages.

### Elevation

Cross-tenant access is a disposable scope (`CrossTenant("reason")`) that needs a permission or a system principal, writes `elevation.started`/`.ended` events and ends when disposed. A system principal always carries a reason. There is no flag to flip back. The relay and the job runner hold a standing elevation recorded once when they start. Execution contexts are created only by framework entry points; the scope factory is internal.

### Verification layers

Analyzers → architecture tests → startup checks → conformance suites → the security test pack (in `YAF.Testing.Integration`: token attacks, API key attacks, cross-tenant requests generated from the running app's endpoint list, authorization inventory, headers, CORS, rate limits, error-leak checks) → generated evidence (`docs/compliance/controls.md`). A "break it" suite flips each default and asserts that the matching guard fails the build, the startup or a test.

## Consequences

**Positive**

- A new service is deny-by-default, tenant-isolated, encrypted and audited without its author choosing any of it.
- A missing declaration is a refusal the developer sees immediately, not an exposure found later.
- Security review concentrates on the few explicit exits: anonymous, user-scoped, cross-tenant, elevation, unsafe options. Each is visible in an approved file diff.
- Evidence for audits is generated, not assembled by hand.

**Negative / costs**

- More declarations per use case (a marker on every request, classification on every member). The edge metadata is derived from the marker, so access is declared once.
- Development needs real keys, signed tokens and a real PostgreSQL; there is no "just turn it off" switch outside the registry.
- Two layers for the same control cost some performance: every statement runs in a transaction with the tenant settings (sent in the first command's batch, so no extra round trip), and authorization runs at the edge and in the pipeline.
- Asynchronous work cannot rely on the starting user's permissions; a use case reached from a consumer or a job needs a `[SystemOnly]` request.
- The broker is trusted for tenant id and initiator; a deployment that cannot restrict broker access needs envelope signing, which 1.0 does not have.
- Guards produce friction for agents; that is intended, and the guidance below says how to respond.

## Enforcement

| Mechanism | What it checks |
|---|---|
| Analyzers `YAF4001`, `YAF4003`, `YAF4004` | Request result type; permission exists in the service's catalogue; no execution context created outside framework entry points |
| Pipeline `AuthorizationMiddleware` | Undeclared request refused at run time (`authz.undeclared_request`); decision on the actor |
| Analyzers `YAF2001`–`YAF2008` | Classification present and correctly placed; no classified value in exception messages or log arguments; `[StoredInClear]` refused on sensitive and secret members |
| Analyzer `YAF5001` | A protected column read without the unprotecting helper |
| Analyzer `YAF6001`, `YAF8001`, `YAF10001` | Transport, endpoint and configuration access paths |
| Banned API list | Weak cryptography, `System.Random` for secrets, `IgnoreQueryFilters`, interpolated raw SQL, direct logger extension calls, clock APIs |
| Startup checks (`IStartupCheck`) | Keys, extension points, unsafe options, TLS to dependencies, middleware order, topology |
| Architecture tests | Vendor isolation, internal implementations, no test doubles in production, every tenant table has an RLS policy |
| Approved files | Authorization inventory, pipeline order, error catalogue, OpenAPI document |
| Security test pack and "break it" suite | Attacks and guard regressions |
| CI | Secret scan, CodeQL, dependency review, daily NuGet audit, audit gate on release; CODEOWNERS on `src/Http/Authentication`, `src/Infrastructure/Cryptography`, `.github/workflows` |
| `[Control("S<n>")]` tests | At least one per tenet; the generated matrix has no empty row |

## Guidance for agents

**Never weaken a guard to make something pass.**

- A failing security test, analyzer or startup check means the code is wrong or the requirement changed. Fix the code, or stop and ask. Do not suppress the diagnostic, delete the test, loosen the assertion or add an unsafe option.
- Never add `#pragma warning disable`, `[SuppressMessage]` or a `NoWarn` entry for a `YAF` security or classification diagnostic, or for a banned API.

**Never widen access without a spec that says so.**

- Do not add `[AllowAnonymousRequest]`, `[AllowAnonymous]`, `[UserScoped]`, `[CrossTenant]`, a cross-tenant elevation, a system principal, `IgnoreQueryFilters`, `yaf.bypass_rls`, or an entry in `Security:AcknowledgedUnsafe` unless the approved spec for the change names that exact exit and its reason. Quote the spec line in the pull request.
- Every new request type gets exactly one authorization marker; the endpoint helpers derive the edge metadata from it. Write `[RequirePermission]` by hand only on an endpoint that dispatches no request. Update the approved authorization inventory and call the change out.
- A consumer, job, cron or process-manager handler dispatches only `[SystemOnly]` requests. Never use the initiator for a permission decision, and never try to run asynchronous work "as the user".
- Never create or replace an execution context. Fan out over tenants with `CronContext.ForEachTenantAsync`.
- Tenant-owned data stays tenant-owned: a new table for tenant data implements the tenant memento interface and gets its row-level security policy in the same migration.

**Classify and protect data as you add it.**

- Every new `string`, `byte[]` or free-form member of a memento, event, job, options class or logged type gets a classification attribute, on the property (`[property: …]` for positional records).
- When unsure between two classifications, choose the stricter one and say so.
- Never put a value into an exception message, a log template argument or a problem `detail`. Use ids and names; use declared error arguments for values.
- Never add code that strips or rewrites user input. Reject what is invalid; mark a member that holds HTML `[Markup]`.
- Never add `[StoredInClear]` without a reason taken from the spec, and never read a protected column in a read model without the unprotecting helper.

**Secrets.**

- Never write a key, token, password, connection string or API key into code, tests, `appsettings*.json`, docs or commit messages, not even as a placeholder that looks real.
- Tests generate their keys at run time (`RandomNumberGenerator`) or use the helpers in `YAF.Testing.Integration`.
- If you find a real secret in the repository or its history, stop and report it. Deleting it is not enough; it must be rotated by a human.

**Cryptography and authentication.**

- Do not implement or change a cryptographic algorithm, a token validation rule, a key format or a hashing scheme on your own initiative. Use `ICryptoSuite`, `IKeyRing`, `IFieldProtector`. Any change there needs an ADR and human review.
- Never make an algorithm, a validation flag or a clock skew configurable to "fix" a failing integration, and never add a way to accept an unsigned token. Use a signed development token.

**Scope and review.**

- Changes under `src/Http/Authentication`, `src/Infrastructure/Cryptography`, the tenant interceptor, the field protector, the audit sealer, the authorization middleware and `.github/workflows` always need a human reviewer. Say so in the pull request and do not merge on an agent's approval alone.
- Do not add a dependency, a GitHub Action or a build step that downloads and runs code without checking ADR-0003 and pinning it.
- Do not disable or skip a test in the security test pack. If it is flaky, report it.

**When blocked.**

- If a guard prevents the task as specified, the specification may be wrong. State which guard, why it fires, and the options; then wait. An explicit refusal with a reason is a correct outcome.

## Related

- ADR-0005 — compliance by design
- ADR-0010, ADR-0011 — tenancy and data protection at rest
- ADR-0014, ADR-0015 — HTTP adapters, identity and authorization
- ADR-0016, ADR-0017 — observability, configuration
- ADR-0020 — supply chain; ADR-0022 — AI; ADR-0026, ADR-0027 — coding rules and review
- `spec/09-security.md`
