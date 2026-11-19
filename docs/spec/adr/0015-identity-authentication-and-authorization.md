# ADR-0015 — Identity, authentication and authorization

> **Status** Accepted · **Date** 2026-11 · **Deciders** maintainers

## Context

The framework must know who acts, for which tenant, and whether they may, at every entry point: HTTP, message consumers, jobs, cron and process managers. Where users, tenants and grants are stored is the application's business. The prior design checked permissions on controllers only, used writable per-request context providers, and a waiver flag that had to be switched back by hand.

## Drivers

- Deny by default at the edge and in the application pipeline.
- Works with any OpenID Connect provider, and with several at once.
- Machine access without sharing user tokens.
- Privileged operations are distinguishable and auditable; stronger authentication can be demanded for them.
- Context cannot be changed by application code mid-request; elevation is explicit.
- Who may call what is reviewable as a diff.

## Options considered

| Option | For | Against |
|---|---|---|
| Edge-only authorization (attributes or filters) | Simple | Jobs, consumers and new adapters bypass it |
| Roles in tokens as the permission model | No lookup | Token bloat, stale grants, provider-specific |
| Writable context providers | Flexible | Any code can impersonate; hard to audit |
| Bearer only, no API keys | Smaller surface | Rules out simple machine clients; too rigid for a framework |
| **Pipeline-enforced markers + edge metadata derived from them; grants through an app extension point; immutable context** | Covers every entry point; access declared once | One required extension point per app; cache semantics to explain |
| Asynchronous work runs as the user who started it (principal from the envelope or job row) | Per-user checks continue in consumers and jobs | Whoever can publish to the broker can act as any user; work runs later for users whose rights were revoked; needs signed envelopes to be safe |
| **Asynchronous work runs as the service's system principal; the starting user is carried as initiator for audit only** | A forged envelope gains no user's rights; no stale authority | Use cases reached asynchronously need `[SystemOnly]` requests; per-user checks there are explicit |
| User id derived from provider name, issuer and subject | Distinct per configured provider | Renaming a provider in configuration changes every user id |
| **User id derived from issuer and subject, with an optional resolver** | Stable under configuration changes; apps can link accounts | The resolver is one more extension point |
| A Development switch that accepts unsigned tokens | No token tooling locally | An option that disables authentication exists and must be guarded; a different code path in Development |
| **Signed development tokens** | One validation path everywhere; no unsafe switch | Developers need `dotnet user-jwts` or the test key |

## Decision

**Principals.** `Principal` is `Anonymous`, `User(UserId)`, `Service(name)` or `System(reason)`.

**Bearer JWT.** Configured under `Auth:Jwt`, optionally with several named providers selected by issuer. Always validated: signature, issuer, audience, lifetime (small clock skew), an asymmetric algorithm allow-list (`none` and symmetric refused), `typ`. Inbound claim mapping is off; claim names are configurable. The `UserId` is a name-based UUID over `(issuer, subject)`, so providers cannot collide or impersonate each other's users; the configured provider name is a label and not an input. An app may register `IUserIdResolver` to map an external identity to its own user id (account linking, moving to another provider). There is no switch that accepts unsigned tokens: local runs use signed tokens from a development issuer (`dotnet user-jwts`), tests the signing key in `YAF.Testing.Integration`.

**API keys** (off by default). Format `yaf_<key id>_<secret>`; only key id, salt and a peppered HMAC are stored; compared in constant time; shown once. A key authenticates **as its owner principal** with `amr = api-key`. Resolution through the app's `IApiKeyStore`, cached briefly and never past expiry.

**Service principals.** Client-credentials tokens map to `Principal.Service`. The framework does not forward end-user tokens downstream; `IDownstreamTokenProvider` is the extension point.

**Step-up.** A permission declared `privileged` can require authentication strength through `amr`/`acr`; an insufficient token gets `401` with an `insufficient_user_authentication` challenge.

**Capabilities.** Each service declares features and permissions in an `ICapabilityCatalog` and publishes them at `GET /capabilities`. Grants come from the app's `IPrivilegeSource` via `IPermissionEvaluator`, cached per principal for a short configurable time with bounded stale-while-error, then deny. A permission flagged `privileged` is always checked against `IPrivilegeSource`, never the cache, and a failed lookup denies at once. `IPrivilegeCacheInvalidator` lets the app evict a user's entry when it changes grants.

**Enforcement points.** Every request type carries exactly one marker: `[RequiresPermission]`, `[UserScoped]`, `[AllowAnonymousRequest]` or `[SystemOnly]`. `AuthorizationMiddleware` refuses a request with none. Edge metadata and the OpenAPI security entry are **derived from the marker** by the endpoint helpers (ADR-0014); `[RequirePermission]` is written by hand only on an endpoint that dispatches no request. Both checks run. Resource-level rules use `IResourceAuthorizer<T>` in the handler.

**Actor and initiator.** `IExecutionContext` has an `Actor`, the principal every authorization decision is made on, and an optional `Initiator`, a reference (kind and id) to the user who started the operation, used for audit, stamps and correlation and never for a permission check. Over HTTP the actor is the authenticated caller and there is no initiator. In consumers, jobs, cron and process managers the actor is the service's system principal and the initiator comes from the envelope attribute `yafinitiator` or the job row; the tenant comes from the same place and scopes the work. These entry points may dispatch requests, and only `[SystemOnly]` ones. A handler that needs the user's present rights asks `IPrivilegeSource` for the initiator explicitly. The broker is inside the trust boundary for tenant id and initiator; because the initiator grants nothing, a forged envelope cannot gain a user's rights.

**Tenant resolution.** Header, route segment or claim per `TenantResolutionOptions`, validated against grants. A tenant id in route or query must equal the resolved tenant unless the endpoint is `[CrossTenant]`.

**Execution context.** `IExecutionContext` is established once per scope by the entry point and is read-only. Running without one throws. Only framework entry points create one: `IExecutionContextScopeFactory` is internal, and application code that must work per tenant uses `CronContext.ForEachTenantAsync(...)`. Elevation is a disposable scope (`CrossTenant("reason")`) that needs a permission or a system principal, is recorded as a security event and ends on dispose.

**Required extension points.** `IPrivilegeSource` always; `IApiKeyStore` when API keys are enabled. The host refuses to start without them.

## Consequences

- Every use case is authorised by the same pipeline step, whichever entry point reaches it.
- Asynchronous work never acts with a user's authority: a use case reached from a consumer or a job is a `[SystemOnly]` request, and a per-user check there is an explicit call.
- Revoked ordinary grants and keys take effect within the cache window, or at once when the app calls the invalidator; privileged permissions take effect at once and cost a lookup on every such request.
- Apps must implement the privilege source; the templates ship Development-only stand-ins.
- MFA remains the identity provider's job; YAF can only demand evidence of it.
- A user id depends on the issuer; an identity provider that changes its issuer URL changes derived ids unless the app maps them with `IUserIdResolver`.
- Local development needs a signed token (`dotnet user-jwts`); there is nothing to switch off.

## Enforcement

- Analyzers: `YAF4001` (result types), `YAF4003` (permission exists in the catalogue), `YAF4004` (an execution context created or replaced outside framework entry points); pipeline refusal `core.authorization.undeclared` with security event `authz.undeclared_request`.
- Authorization inventory approval file per API; a hand-written `[RequirePermission]` that disagrees with the dispatched request's marker fails the test.
- Security test pack (in `YAF.Testing.Integration`): `alg=none` and unsigned tokens in every environment, algorithm confusion, wrong issuer or audience, expired and not-yet-valid tokens, API key attacks, failed-credential throttling, cross-tenant requests generated from the endpoint list, a forged initiator gaining nothing, immediate revocation of a privileged permission.
- Startup checks: missing extension points, non-HTTPS metadata outside Development, a development signing key trusted outside Development.
- Security events: `authn.failed`, `authn.throttled`, `authz.denied`, `authz.privileged_used`, `tenant.violation`, `elevation.*`.

## Guidance for agents

- Put exactly one authorization marker on every new request type; choose `[RequiresPermission]` unless there is a stated reason.
- Add new permissions to the capability catalogue first; flag anything destructive or cross-tenant as `privileged`.
- Never read claims or headers in application code; read `IExecutionContext`.
- Do not write `[RequirePermission]` on an endpoint that dispatches a request; the helper derives it from the request's marker.
- Never construct or mutate a context to "act as" someone. Only framework entry points create contexts; per-tenant fan-out uses `CronContext.ForEachTenantAsync`; cross-tenant work uses an elevation scope with a reason.
- In a consumer, job, cron or process-manager handler dispatch only `[SystemOnly]` requests. The initiator is for audit; never base a decision on it. If the user's present rights matter, ask `IPrivilegeSource` and say why.
- Do not log tokens or keys; log the key id.
- Do not weaken token validation options to make a test pass, and never add a way to accept unsigned tokens; use the test authentication from `YAF.Testing.Integration` or `dotnet user-jwts`.
- Do not use the provider name for anything but selecting configuration.
- Update the approved authorization inventory in the same change and explain the diff.

## Related

0004, 0005, 0009, 0010, 0014, 0016, 0017, 0018
