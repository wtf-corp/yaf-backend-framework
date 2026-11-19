# 09 — Security by design

Security is not a feature module; it is a set of properties every part of the framework keeps. This file states the properties, the mechanisms, and how each is verified. ADR-0004 records the decision; `docs/compliance/controls.md` (generated) maps each control to its tests.

## 1. Design tenets

| # | Tenet | Meaning in YAF |
|---|---|---|
| S1 | **Deny by default** | An endpoint without metadata requires an authenticated principal and a tenant. A request type without an authorization marker is refused by the pipeline. No CORS origin, no proxy, no anonymous docs until configured |
| S2 | **Fail closed** | No tenant → queries return nothing and writes are refused. Unknown key id → decryption throws. Privilege lookup failure → deny after a short, bounded stale window (privileged permissions: deny at once) |
| S3 | **Fail at startup** | Missing keys, missing required extension points, insecure flags outside Development, invalid options, missing topology: the host does not start, and says why |
| S4 | **Defence in depth** | Authorization at the edge *and* in the pipeline; tenant filter *and* row-level security; validation *and* domain invariants; redaction in logs *and* encryption at rest |
| S5 | **Least privilege** | Separate database roles (migrator, runtime, maintenance); runtime role cannot alter schema or audit data; asynchronous work runs as the service, not as the user who started it; permissions are per feature; privileged permissions are flagged and audited |
| S6 | **No secret in code, config files, logs or errors** | Secrets come from secret stores; classified data is redacted in every sink; problem details never carry internals |
| S7 | **Explicit elevation** | Cross-tenant access, system principals and unsafe development switches are named, scoped, reasoned and recorded as security events |
| S8 | **Mechanical enforcement** | Each tenet has analyzers, architecture tests, startup checks or conformance tests; a human review is never the only control |
| S9 | **Secure supply chain** | Locked, audited dependencies; minimal dependency set; SBOM and provenance for every release |
| S10 | **Evidence** | Security-relevant facts are recorded (audit trail, security events) in tamper-evident form with synchronised UTC time |

## 2. Threat model the framework addresses

OWASP API Security Top 10 (2023) is the checklist; ASVS 5.0 Level 2 is the target for the parts a framework controls.

| Risk | Framework control |
|---|---|
| API1 Broken object-level authorization | Tenant isolation (two layers), not-visible-is-404, `IResourceAuthorizer<T>` hook, cross-tenant request suite in `YAF.Testing.Integration` |
| API2 Broken authentication | Strict token validation in every environment (there is no unsigned-token switch), hashed API keys, failed-credential throttling |
| API3 Broken object property-level authorization | Separate request/response contracts, unknown JSON members refused, no domain type serialized, classification on response members |
| API4 Unrestricted resource consumption | Rate limits, body and time limits, clamped paging, bounded queues and buffers, job concurrency limits |
| API5 Broken function-level authorization | Undeclared requests refused; permission catalogue; authorization inventory approval test |
| API6 Unrestricted access to sensitive business flows | Idempotency keys, per-principal limits, named rate-limit policies for sensitive flows |
| API7 SSRF | `IOutboundUrlPolicy` for user-supplied URLs (scheme allow-list, private-range block, DNS-rebinding-safe resolve-then-connect) used by framework HTTP clients |
| API8 Security misconfiguration | Validated options, secure defaults, startup guards, security headers |
| API9 Improper inventory management | Committed OpenAPI documents, versioning with deprecation headers, `/.well-known/yaf` |
| API10 Unsafe consumption of APIs | Typed clients with the standard resilience handler, response size limits, schema-strict deserialization of inbound messages |

Boundaries the threat model states explicitly:

- **Row-level security** protects against application bugs (a missing filter, a wrong context). It does not protect against arbitrary SQL executed as the runtime role, which could set the session variables itself; injection is prevented by parameterised access only (§7).
- **The broker is inside the trust boundary** for the tenant id and the initiator carried in an envelope (`06-messaging.md`). Whoever can publish to it can start work in any tenant, as the consuming service. Because the initiator grants nothing (§4), a forged envelope cannot gain a user's rights. Broker access is therefore restricted to services and encrypted in transit; signing envelopes is a possible later addition.
- **Cross-site scripting.** YAF serves JSON. The control is output encoding in the client that renders the data, supported by the response headers (`nosniff`, a restrictive content security policy, JSON content type). Input is validated, not rewritten (§7).

## 3. Authentication

Two schemes, composed under one policy scheme:

**Bearer (OIDC JWT)** — `Auth:Jwt` (`JwtAuthOptions`):

- `Authority` (HTTPS metadata required outside Development), `Audiences` (≥1).
- Always validated: signature, issuer, audience, lifetime (clock skew default 60 s), algorithm allow-list (`RS256`, `ES256`, `PS256` by default; `none` and symmetric algorithms refused), `typ`.
- `MapInboundClaims = false`. Claim names for subject, tenant(s), authentication methods are configurable.
- Multiple providers: `Auth:Jwt:Providers:<name>`; the scheme is selected by issuer. The name is a configuration label only.
- The principal's `UserId` is a name-based UUID over `(issuer, subject)`, so two providers can never collide and a GUID-looking subject from one provider cannot impersonate another's user. The configured provider name is not an input, so renaming it changes nothing.
- `IUserIdResolver` (optional, registered by the app) maps an external identity `(issuer, subject, claims)` to the app's own user id. It is the extension point for account linking and for moving users to another identity provider. Its result is cached like privileges (§4).
- **There is no switch that accepts unsigned tokens.** Local runs use properly signed tokens from a development issuer: `dotnet user-jwts` for a service run on its own (its signing key is trusted only when the environment is Development), and the test signing key in `YAF.Testing.Integration` for tests. The validation code path is the same in every environment.

**API key** — header `X-Api-Key` (`Auth:ApiKey:Enabled`, default off):

- Format `yaf_<key id>_<secret>`: key id 16 random bytes hex, secret 32 random bytes base64url. Stored: key id, per-key salt, hash (HMAC-SHA-256 with a server-side pepper from the key ring, then compared in constant time), expiry, owner principal. The key is shown once.
- Resolution through the app's `IApiKeyStore`; cached by key id for at most 60 s and never past expiry.
- An API key authenticates **as its owner principal** (user or service), with `amr = api-key`; privileges are the owner's.
- Never logged; refusals log the key id only.

**Service-to-service**: client-credentials tokens from the same or another OIDC provider map to `Principal.Service(name)`. Forwarding an end user's token to another service is not done by the framework; `IDownstreamTokenProvider` is the extension point (token exchange is the recommended implementation).

**Step-up**: a permission flagged `privileged` can require authentication strength: `Auth:StepUp:RequiredAmr` / `RequiredAcr`. The evaluator denies with `core.authorization.step_up_required` (→ `401` with `WWW-Authenticate: Bearer error="insufficient_user_authentication"`) when the token's `amr`/`acr` does not satisfy it. MFA itself is the identity provider's.

Required extension points, checked at startup (`UseYafHttp` refuses to start without them): `IPrivilegeSource`; `IApiKeyStore` when API keys are enabled.

## 4. Authorization

Specified in `04-application.md` §8 and `08-http.md` §2. Summary of guarantees:

1. Every request type declares one of: `[RequiresPermission]`, `[UserScoped]`, `[AllowAnonymousRequest]`, `[SystemOnly]`.
2. Tenant-scoped requests need a resolved tenant in which the principal holds at least one grant; the specific permission is then checked.
3. `[UserScoped]` requests have a principal and no tenant; they cannot require a tenant permission.
4. `[SystemOnly]` requests are refused for any non-system actor. Consumers, jobs, cron and process managers dispatch these, and only these.
5. **Actor and initiator.** Authorization is always decided on the execution context's `Actor`. Over HTTP the actor is the authenticated caller. In asynchronous work the actor is the service's system principal, and the user who started the operation travels as `Initiator` (kind and id), used for audit, stamps and correlation only, never for a permission check. A handler that needs that user's present rights asks `IPrivilegeSource` for the initiator explicitly.
6. Grants are cached per principal for a short time (`04` §8), with two exceptions: a permission flagged `privileged` in the capability catalogue is always checked against `IPrivilegeSource`, never the cache, so its revocation is immediate; and `IPrivilegeCacheInvalidator` lets the app evict a user's entry when it changes grants.
7. The edge check is derived from the request type's marker by the endpoint helpers (`08-http.md` §2); both checks run, one declaration.
8. Application code cannot create or replace an execution context: `IExecutionContextScopeFactory` is internal to the framework, cron fan-out uses `CronContext.ForEachTenantAsync(...)`, and analyzer `YAF4004` flags any other attempt.
9. Decisions are logged as security events when denied, and always for privileged permissions.
10. The **authorization inventory** (endpoint → access level → permissions) is an approved file per API; changes show in the diff.

## 5. Tenant isolation

Specified in `05-persistence.md` §4 (query filters, row-level security, roles, framework tables), `04-application.md` §7 (context, elevation) and `06`/`07` (tenant and initiator carried on messages and jobs). Additional rules:

- Cache keys, object-store keys, notification channels and rate-limit partitions include the tenant id; helpers refuse to build a key without one (`TenantKey.For(context, …)`).
- Logs and traces carry the tenant id as an attribute (not as a metric dimension).
- A tenant mismatch detected anywhere (`TenantInterceptor`, route-vs-context check, message for an unknown tenant) is a `tenant.violation` security event.

## 6. Cryptography

| Use | Algorithm | Notes |
|---|---|---|
| Field encryption | AES-256-GCM, random 96-bit nonce, 128-bit tag, associated data = logical field id, row id, tenant id | `05-persistence.md` §6 |
| Outbox and job payloads with classified members | Same suite, key ring of purpose `messaging` | `05-persistence.md` §4 |
| Key wrapping (subject keys) | AES-256-GCM with the key ring, or the KMS's wrap operation | |
| Lookup hashes, log pseudonyms | HMAC-SHA-256 with dedicated keys | Never the encryption key |
| API key hashing | HMAC-SHA-256 with pepper + per-key salt | High-entropy secrets do not need a slow hash |
| Audit chain | SHA-256; checkpoints signed with HMAC-SHA-256 under the `audit` key | `05-persistence.md` §7 |
| Random | `RandomNumberGenerator` | `System.Random` banned for security use |

- All algorithms sit behind `ICryptoSuite` with a version tag in every stored value (`yaf1`), so a future suite (`yaf2`, including post-quantum key encapsulation for wrapped keys where the platform supports it) can be introduced with re-encryption. No algorithm is configurable by name from app settings.
- Keys: 256-bit random, base64 in configuration; ids are `[a-z0-9-]{1,32}`; purposes are separated (`encryption`, `lookup`, `redaction`, `api-key-pepper`, `messaging`, `audit`), each with its own ring. A key's value never changes in place; rotation adds an id.
- ASP.NET Core Data Protection, when used by the app, MUST have a persisted, protected key ring; `AddYafHttp` fails startup outside Development if Data Protection is used with ephemeral keys.
- TLS: outbound connections to database, broker, object store and identity provider require TLS outside Development (startup checks per adapter).

## 7. Input and output handling

- Binding: strict JSON (`08-http.md` §3). Size, depth and collection-length limits (`[MaxItems]` validator rule; default cap 1 000 items per collection member).
- Input guard: `InputGuardMiddleware` **never alters input**. It rejects control characters (other than tab, CR, LF) and invalid Unicode (unpaired surrogates) in any string of a command with validation error `Input.InvalidCharacters`. Consumed message and job payloads get the same check. Text such as `a < b` or a name with an ampersand is stored as typed.
- Markup: a member that holds HTML is marked `[Markup]` and is passed through `IMarkupSanitizer` (port in `YAF.Application`) with an allow-list profile. The adapter package `YAF.Markup.HtmlSanitizer` wraps the sanitizer library; `YAF.Http` has no sanitizer dependency. A `[Markup]` member with no registered sanitizer fails startup. This happens once, at the application boundary, for every entry point; storage is not "re-sanitized".
- Output: JSON only from framework endpoints; `nosniff`; a content security policy that allows nothing by default and is relaxed for the API documentation UI in Development only; no reflection of input into error `detail` except through declared template arguments. `Cache-Control: no-store` is the default; a response that carries an `ETag` for conditional GET sends `private, no-cache` instead. Rendering stored values safely is the client's job (§2).
- Files: `IObjectStore` uploads take a declared content type and size limit, generate server-side keys (`tenants/{tenant}/{yyyy}/{mm}/{uuid}{ext}`), never trust client file names, and return time-limited URLs only for keys recorded for that tenant.
- SQL: LINQ and parameterised SQL only; interpolated raw SQL is a banned API.
- Deserialization: no polymorphic deserialization by type name anywhere (message types resolve through the registered wire-name table).

## 8. Security events

`ISecurityEventRecorder.Record(SecurityEvent)` → append-only table, structured log (category `YAF.Security`, level by severity), metric `yaf.security.events{type,severity}`, and every registered `ISecurityEventSink` (the hook for SIEM or incident tooling).

| Type | Raised when |
|---|---|
| `authn.failed`, `authn.throttled` | Invalid token or key; failed-credential limit hit |
| `authz.denied` | Permission, tenant or scope check refused |
| `authz.privileged_used` | A privileged permission was exercised |
| `authz.undeclared_request` | A request type without a marker reached the pipeline |
| `tenant.violation` | Tenant mismatch on write, route, message or key |
| `elevation.started` / `.ended` | Cross-tenant or system elevation, with reason |
| `crypto.decrypt_failed`, `crypto.key_missing`, `crypto.key_rotated`, `crypto.subject_key_destroyed` | |
| `audit.chain_broken` | Verification mismatch or a sealed row missing (critical) |
| `audit.seal_lag` | Unsealed audit rows older than `Audit:SealLagThreshold` |
| `audit.partition_dropped` | The retention job dropped an expired partition after its checkpoint |
| `data.exported`, `data.erased`, `data.read` | Data subject export, erasure, audited reads |
| `config.insecure_flag` | A development-only switch is active |
| `state.corrupt` | A memento could not be restored |
| `outbox.parked`, `jobs.parked` | Delivery gave up |
| `ai.*` | Reserved for the later `YAF.AI` package (`13-ai.md`) |

Fields: id, time (UTC from `TimeProvider`), type, severity, tenant, actor, initiator, origin, client address (truncated per `Privacy:IpTruncation`), correlation id, trace id, outcome, resource, reason, data (classified, redacted).

Incident support in 1.0 is the raw material: the security events above, the sinks, and a query API over security events and read audit (`11-privacy-and-compliance.md` §7). An incident register with reporting-deadline reminders (`IIncidentRegister`) is planned for a later release; reporting itself is a human process.

## 9. Environment separation and unsafe switches

- Every switch that weakens a control is an option named `Allow…` / `Disable…`, is `false` by default, is listed in `UnsafeOptionsRegistry`, and is refused outside Development by a startup check unless explicitly acknowledged with `Security:AcknowledgedUnsafe` (which itself raises `config.insecure_flag` and a startup warning). No such switch bypasses authentication.
- Test doubles in `YAF.Testing` and `YAF.Testing.Integration` throw if constructed when the environment is Production; an architecture test in the template fails if a non-test project references either package.
- The configuration **fingerprint** (hash of effective non-secret options + list of enabled features + package versions) is logged at startup and exposed on `/.well-known/yaf` for authenticated operators.

## 10. Supply chain and SDLC controls

Specified in `17-ci-release-docs.md`: locked restore, NuGet audit (dependency review on pull requests, a daily audit of `main` and `next`, a gate on high and critical advisories at release), package source mapping, dependency review, secret scanning (pre-commit hook and CI), static analysis (analyzers + CodeQL), SBOM, signed provenance attestations, protected main, CODEOWNERS on security-sensitive paths (`src/Http/Authentication`, `src/Infrastructure/Cryptography`, `.github/workflows`), `SECURITY.md` with a disclosure policy.

## 11. Verification

| Layer | What |
|---|---|
| Analyzers | Undeclared authorization, context creation outside entry points (`YAF4004`), unclassified members, protected columns read without the helper (`YAF5001`), inline errors, banned APIs, direct transport use, logging of classified data |
| Architecture tests | Layering, vendor isolation, internal implementations, no test doubles in production, every tenant table protected |
| Startup checks | Collected by `IStartupCheck`; all run, all failures reported together, non-zero exit |
| Conformance suites | Transport, HTTP style parity |
| Security test pack (in `YAF.Testing.Integration`) | Token attacks (`alg=none`, algorithm confusion, wrong issuer/audience, expired, future `nbf`), API key attacks, cross-tenant requests generated from the running app's endpoint list, authorization inventory, header and CORS checks, rate limits, error-leak checks (no stack trace or SQL in any error body) |
| Generated evidence | `docs/compliance/controls.md`: control id → mechanism → test names → last result, built in CI from `[Control("…")]` attributes on tests |

## 12. Acceptance criteria

- The security test pack passes against the reference sample in both HTTP styles (minimal APIs and controllers).
- A consumer handling an envelope whose initiator is a user cannot dispatch a `[RequiresPermission]` or `[UserScoped]` request; a forged initiator gains no permission.
- Revoking a privileged permission takes effect on the next request; a non-privileged one within the cache window or at once after `IPrivilegeCacheInvalidator`.
- An unsigned token is refused in Development as everywhere else.
- For each row of §1 there is at least one test tagged `[Control("S<n>")]`, and the generated matrix has no empty row.
- A "break it" suite flips each default (remove a marker, drop an RLS policy, enable an unsafe flag in Production, log a classified member) and asserts that the corresponding guard fails the build, the startup or a test.
