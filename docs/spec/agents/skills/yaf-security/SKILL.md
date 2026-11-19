---
name: yaf-security
description: The YAF security-by-design tenets as working rules - authentication and authorization, tenant isolation, explicit elevation, cryptography and keys, input and output handling, security events, unsafe options and secrets - with a pre-merge security checklist and the list of changes that need human review. Load BEFORE touching authentication, permissions or capabilities, tenant resolution or cross-tenant access, encryption or keys, any Allow*/Disable* option, file upload or outbound-URL code, CORS, rate limits, CI workflows, or dependencies; and before approving any change in those areas.
license: MIT
metadata:
  owner: yaf
  version: "1"
---

# YAF security by design

Deny by default, fail closed, fail at startup, enforce mechanically. If a control gets in your way, the control is doing its job: change the design, not the control.

## When to load

- Adding an endpoint, request type, permission or capability.
- Anything that reads or widens tenant scope.
- Encrypting, hashing, generating secrets or tokens; adding or rotating keys.
- Accepting files, URLs, HTML or large input from callers.
- Writing a consumer, job or cron job that acts on behalf of a user.
- Adding an option that relaxes a check; adding a package; editing `.github/workflows`.
- A guard (analyzer, startup check, architecture or security test) fails and you are tempted to bypass it.

## Tenets

| # | Tenet | Day-to-day meaning |
|---|---|---|
| S1 | Deny by default | Nothing is reachable, allowed, trusted or exposed until declared |
| S2 | Fail closed | Missing tenant → no rows; unknown key → throw; lookup failure → deny |
| S3 | Fail at startup | Invalid or insecure configuration stops the host with a clear message |
| S4 | Defence in depth | Edge + pipeline authorization; filter + row-level security; validation + invariants |
| S5 | Least privilege | Per-feature permissions; runtime DB role cannot alter schema or audit data |
| S6 | No secret in code, files, logs, errors | Secret stores only; one redaction policy |
| S7 | Explicit elevation | Named, scoped, reasoned, recorded |
| S8 | Mechanical enforcement | Analyzer, test or startup check for every rule that matters |
| S9 | Secure supply chain | Locked, audited, minimal dependencies; SBOM and provenance |
| S10 | Evidence | Audit trail and security events, tamper-evident, UTC |

## Rules

Access

1. **REQUIRED** Every request type has one access marker; an endpoint's edge metadata is derived from the marker of the request it sends, never repeated by hand (YAF4003, authorization inventory approval). Both checks run.
2. **REQUIRED** Permissions come from the service's `ICapabilityCatalog`. Mark a permission `privileged: true` when it grants broad or irreversible power; its use is always recorded, and it is always checked against `IPrivilegeSource`, never against the privilege cache. Call `IPrivilegeCacheInvalidator` when your app revokes a user's rights.
3. **REQUIRED** Object-level checks ("may this caller touch *this* registration") go through `IResourceAuthorizer<TResource>` in the handler. Not visible → `NotFound`.
4. **REQUIRED** Never weaken token validation: no `alg=none`, no symmetric algorithms, issuer/audience/lifetime always checked. There is no switch that accepts unsigned tokens: local runs use signed tokens from a development issuer (`dotnet user-jwts`), tests the signing key of `YAF.Testing.Integration`.
5. **REQUIRED** API keys are stored as key id + salt + hash only, shown once, never logged; they authenticate as their owner.
6. **REQUIRED** A `UserId` is derived from (issuer, subject) of the token. Do not derive, parse or compare user ids yourself; an app that keeps its own users maps identities through `IUserIdResolver`.

Tenancy

7. **REQUIRED** Tenant comes from `IExecutionContext` only. Never accept a tenant id from a body or query and act on it.
8. **REQUIRED** Cross-tenant access uses `await using (elevation.CrossTenant("reason"))`, as narrow as possible. Never `IgnoreQueryFilters`, never a "system" DB connection.
9. **REQUIRED** Cache keys, object keys, notification channels and rate-limit partitions are built with `TenantKey.For(context, …)`.
10. **REQUIRED** Asynchronous work (consumers, jobs, cron, process managers) runs as the service's system principal (`Actor`) and dispatches only `[SystemOnly]` requests. The user who started it is the `Initiator`: recorded for audit, never used for a permission decision, because it arrives through the broker or a stored row and may be stale. Application code never creates or replaces an execution context (YAF4004); `Principal.System("reason")` always has a reason.
11. **REQUIRED** Row-level security guards against application bugs (a missing filter, a wrong context). It does not guard against arbitrary SQL run as the runtime role, which could set the session variables itself: injection stays a critical defect.

Cryptography and secrets

12. **REQUIRED** Use the framework: classification-driven field protection in the mapping layer, `IFieldProtector`, `IKeyRing`, `RandomNumberGenerator`. No hand-rolled crypto; banned: MD5, SHA-1, DES, 3DES, ECB, `System.Random` for security.
13. **REQUIRED** Keys and secrets never appear in source, committed settings, tests' real values, logs, errors or exception messages. Options members holding secrets are `[SecretData]` with no default.
14. **REQUIRED** Never change a key's value in place; rotation adds a key id (add → make current → re-encrypt to zero → remove).
15. **REQUIRED** Outbound connections use TLS outside Development; do not add a switch to skip certificate validation.

Input and output

16. **REQUIRED** Input is checked, never rewritten: the pipeline's `InputGuardMiddleware` refuses control characters and invalid Unicode in command strings and in consumed payloads. Do not strip or escape HTML on the way in. Cross-site scripting is prevented where the value is rendered (output encoding in the client) and by the response headers. A member that holds HTML on purpose is `[Markup]` and passes through `IMarkupSanitizer` (`YAF.Markup.HtmlSanitizer`); use it only with a reason.
17. **REQUIRED** User-supplied URLs pass `IOutboundUrlPolicy` before any fetch. Uploads go through `IObjectStore` with declared type and size limit and server-generated keys.
18. **REQUIRED** SQL is LINQ or parameterised. No polymorphic deserialization by type name.
19. **REQUIRED** Errors returned to callers never include stack traces, SQL, internal names or unredacted input.

Operations

20. **REQUIRED** Record security-relevant facts with `ISecurityEventRecorder` using the defined types (`authz.denied`, `tenant.violation`, `elevation.started`, …). Do not invent a parallel log.
21. **REQUIRED** Any switch that weakens a control is named `Allow…`/`Disable…`, defaults to `false`, is registered in `UnsafeOptionsRegistry`, and is refused outside Development unless acknowledged.
22. **REQUIRED** `YAF.Testing` and `YAF.Testing.Integration` types never ship in production assemblies (architecture test).
23. **REQUIRED** A new dependency is justified in the PR, added to `Directory.Packages.props`, restored in locked mode; a core package needs an ADR for any new third-party reference.
24. **REQUIRED** Never disable, suppress or delete a failing guard to get green. Fix the cause or stop and ask.
25. **REQUIRED** Audit and event tables are append-only for the runtime role; only the retention job's maintenance role drops expired partitions. Do not add grants, and do not describe the audit trail as complete up to the last second: the newest rows are stored but chained by the sealer shortly after commit.

## How to: add a permission-protected capability

1. Add the permission to `Capabilities`; decide `privileged`.
2. Mark the request `[RequiresPermission(...)]`; the endpoint helper derives the edge requirement from it.
3. Add tests: caller without the permission → `403`; caller of another tenant → `404`/`403`; privileged use produces `authz.privileged_used`; a privileged permission revoked at the source is refused on the next request.
4. Accept the authorization inventory diff.

```csharp
public static readonly Feature Registrations = Feature.Define("registrations",
    Permission.Define("view"),
    Permission.Define("manage"),
    Permission.Define("export", privileged: true));

[RequiresPermission("registrations", "export")]
[AuditRead]
public sealed record ExportRegistrationsQuery(SessionId SessionId) : IQuery<Result<ExportReference>>;
```

## Pre-merge security checklist

- [ ] No new endpoint or request without an access decision; inventory diff reviewed.
- [ ] No tenant id taken from input; no query-filter bypass; new tenant tables have row-level security.
- [ ] New members holding personal or secret data are classified; nothing sensitive in logs, events, errors, exception messages.
- [ ] No secret, key or token in the diff (gitleaks clean), including tests and docs.
- [ ] No new `Allow…`/`Disable…` option outside the registry; none enabled in committed non-Development settings.
- [ ] External calls only from jobs, with timeouts and the resilience handler; user-supplied URLs go through the policy.
- [ ] New packages reviewed (licence, maintenance, advisories); lock files updated.
- [ ] Guards untouched: no analyzer suppressed, no test skipped, no workflow permission widened.

## Requires human review (do not self-approve)

Changes under `src/Http/Authentication`, `src/Infrastructure/Cryptography`, `.github/workflows`; any `[AllowAnonymous]`, `[CrossTenant]`, `[SharedAcrossTenants]`, `[NotAudited]`, `[AllowPersonalData]`, `[Markup]`, `[StoredInClear]`; a new `privileged` permission; a new `[SystemOnly]` request; a new unsafe option; a new elevation site; a suppressed diagnostic; a new dependency in a core package; changes to database roles or row-level security policies. Approval is a code owner's review on the pull request; an agent never approves and never applies the `guard-change` label.

## Anti-patterns

| Wrong | Why | Instead |
|---|---|---|
| `#pragma warning disable YAF…` to ship | Removes the control | Fix or escalate |
| `[AllowAnonymous]` on a data endpoint for convenience | Deny-by-default broken | Proper marker; test auth in tests |
| `tenantId` from the request body used in a query | Caller picks the tenant | Execution context |
| Elevation wrapped around a whole handler | Widens far more than needed | Smallest scope, specific reason |
| `SHA256(apiKey)` compare with `==` | Unsalted, timing leak | `IApiKeyHasher`-style framework verification |
| Authorizing a job or consumer on the user id it carries | The initiator comes from a stored row or the broker and proves nothing | System actor + `[SystemOnly]` request |
| Stripping tags from every input string | Changes data ("a < b"); gives false comfort | Reject bad characters; encode on output; `[Markup]` where HTML is wanted |
| Catching a decrypt failure and returning empty | Hides tampering | Let it throw; it raises `crypto.decrypt_failed` |
| `"Password=dev"` in `appsettings.json` | Secret in a committed file | User secrets / AppHost parameter |
| Returning `ex.Message` to the client | Leaks internals | Generic detail + `traceId` |
| Widening CORS to `*` to fix a browser error | Any site can call the API | List the origin |

## Verify

- `dotnet build YAF.slnx` (analyzers, banned APIs).
- `dotnet test --solution YAF.slnx` including `tests/Architecture.Tests` and the security test pack (in `YAF.Testing.Integration`) against the affected API.
- `dotnet run --project <Api> -- --yaf-check` with the shipped configuration.
- `gitleaks` clean; `dotnet restore --locked-mode` succeeds.
- New controls carry `[Control("S<n>")]`-tagged tests so `docs/compliance/controls.md` has no empty row.

## References

- `docs/articles/security.md` — schemes, options, event taxonomy, startup checks.
- `adr/0004-security-by-design.md`, `adr/0015-identity-authentication-and-authorization.md` — security by design; read before proposing any exception to a tenet.
- `SECURITY.md` — disclosure policy; where to report a suspected vulnerability instead of opening an issue.
- `samples/conference/` API integration tests: authorization inventory, cross-tenant requests.
- Skills: `yaf-privacy-and-data-classification`, `yaf-configuration`, `yaf-persistence`.
