# 08 — HTTP adapters (`YAF.Http`, `.Controllers`, `.MinimalApis`)

A service chooses how it exposes its use cases: MVC controllers or minimal APIs. The choice is per service (mixing styles in one host works but is discouraged). Everything that matters — authentication, authorization, execution context, error mapping, rate limits, headers, OpenAPI — lives in `YAF.Http` and behaves identically in both. A third style, FastEndpoints (`YAF.Http.FastEndpoints`), is planned for a later release; the style-neutral core is what makes it an added package and not a change.

## 1. Host setup

```csharp
var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();                       // the app's own Aspire service defaults
builder.AddYafHttp(http => http                     // YAF.Http
    .AddAuthentication()                            // 09-security.md
    .AddApiDocs()
    .AddRateLimiting()
    .AddCors());
builder.AddCatalogApplication();
builder.AddCatalogInfrastructure();

var app = builder.Build();
app.UseYafHttp();                                   // one call; fixed, documented middleware order
app.MapYafEndpoints();                              // /errors, /capabilities, /notifications
app.MapControllers();                               // or app.MapEndpoints<Program>()
app.MapDefaultEndpoints();                          // health, from service defaults
app.Run();
```

`UseYafHttp()` installs, in this order: forwarded headers (trusted proxies from configuration only) → exception handler (`IExceptionHandler` producing problem details) → HSTS and HTTPS redirection (outside Development) → security headers → request limits and timeouts → CORS → authentication → **execution context** → rate limiter → authorization → idempotency-key capture. `YAF.Http` does not inspect or rewrite request bodies beyond JSON binding: the character check on input and the handling of `[Markup]` members happen in the application pipeline (`04-application.md` §3), and `YAF.Http` references no HTML sanitizer. Calling the pieces individually is possible; `IStartupFilter` validates that required pieces are present and ordered, and fails startup otherwise.

## 2. One mapping from results to responses

`YAF.Http` provides the style-neutral pieces:

- `IProblemDetailsMapper` (`03-results-and-errors.md` §4).
- `HttpResultMapper`: `Result<T>` → status + body by convention: `200` with the value; `201` + `Location` when the endpoint declares a location; `204` for `Result` and for `Result<Unit>`; `202` + `Location` for accepted long-running work; failures → problem details.
- `YafExceptionHandler : IExceptionHandler`: `OperationCanceledException` from the client → `499`-style close without logging an error; `BadHttpRequestException` and JSON binding errors → `400` problem with `code: core.request.malformed`; anything else → `500` generic problem, logged once with the trace id.

Style adapters are thin:

| Style | Package | What it adds |
|---|---|---|
| Controllers | `YAF.Http.Controllers` | `YafControllerBase` (`[ApiController]`, default authorization) with `protected Task<IActionResult> Send<T>(IRequest<Result<T>> request, CancellationToken ct)`, `SendCreated(request, routeName, routeValues)`, `SendAccepted`, `SendNoContent`; an MVC convention that applies response-type metadata from the request's result type |
| Minimal APIs | `YAF.Http.MinimalApis` | `interface IEndpoint { static abstract void Map(IEndpointRouteBuilder routes); }`, source-generated `MapEndpoints<TMarker>()` (no reflection scan); route helpers `MapYafGet/Post/Put/Patch/Delete<TRequest>(pattern, handler)` that know the request type; `result.ToHttpResult()` returning `Results<Ok<T>, ProblemHttpResult>`-style typed results for OpenAPI; route-group helper `MapYafGroup("/sessions")` that applies default authorization, tags and the version set |

Helper names: `services.AddYafControllers()`; `SendCreated(request, routeName, response => routeValues)`; `result.ToCreatedHttpResult(response => uri)` and `ToAcceptedHttpResult`, `ToNoContentHttpResult` for minimal APIs.

Rules for every style:

- An endpoint holds no logic: bind → build the request → `IDispatcher.SendAsync` → map. Analyzer `YAF8001` flags a repository, `DbContext` or domain type injected into an endpoint class.
- HTTP contracts (request/response records) live with the use case in the application project or in the API project; domain types are never serialized.
- Authorization is declared **once, on the request type** (`[RequiresPermission]`, `[UserScoped]`, `[AllowAnonymousRequest]`, `[SystemOnly]`; `04-application.md` §8). The endpoint helpers derive the edge metadata from that marker: `Send*` on a controller action and the `MapYaf*<TRequest>` route helpers know the request type, and add the matching `IAuthorizationRequirementData` (or `AllowAnonymous`) to the endpoint, plus the OpenAPI security entry and `x-yaf-permission`. For controllers the request type is found by a source generator that reads the `Send*` call in the action; an action it cannot resolve to exactly one request type is build error `YAF8002` and needs the request named with `[Dispatches(typeof(...))]`. A minimal API mapped with a plain `MapGet`/`MapPost` whose handler builds the request names it with `.Dispatches<TRequest>()` on the route builder. Both checks still run: the edge check gives an early `401/403` before binding, and `AuthorizationMiddleware` in the application pipeline remains the one that decides. A `[SystemOnly]` request cannot be mapped to an endpoint (`YAF8003`).
- The attribute is written by hand only on an endpoint that dispatches no request (a file download served from the object store, a webhook receiver):

```csharp
[RequirePermission("sessions", "export")]      // or .RequirePermission("sessions", "export") on a route builder
```

  `RequirePermissionAttribute` implements `IAuthorizationRequirementData`, so it is enforced natively in both styles. An endpoint that neither dispatches a request nor carries an explicit declaration is refused by the default policy (authenticated and denied), and the `AuthorizationInventory` test (`15-testing.md`) lists it as a failure.

The reference sample exposes `Catalog` with minimal APIs and `Registrations` with controllers, over the same kind of application code.

## 3. REST conventions (defaults the helpers implement)

| Topic | Convention |
|---|---|
| Routes | `/api/v{major}/<plural-kebab-resource>`. `YAF.Http` has its own small version-set abstraction (`ApiVersionSet`: the versions a service exposes, which are deprecated, their sunset dates) and the URL-segment convention, with `Deprecation` and `Sunset` headers on deprecated versions and one OpenAPI document per version. Header or media-type versioning, version-neutral endpoints and the rest of `Asp.Versioning` come with the optional adapter `YAF.Http.Versioning`, which plugs that library in behind the same abstraction |
| JSON | camelCase, enums as strings, UTC ISO 8601, `JsonUnmappedMemberHandling.Disallow` on **requests**, duplicate properties refused, max depth 32, source-generated contexts |
| Create | `201` + `Location`; ids are server-generated UUIDv7 |
| Update | `PUT` full replace of the mutable representation, never creates; `PATCH` is JSON Merge Patch bound to `Optional<T>` members |
| Delete | `204`, idempotent |
| Concurrency | `ETag` = aggregate version (the root's version, which changes on every update of the aggregate, also when only a child changed) on single-resource `GET`; `If-None-Match` → `304`; `If-Match` on `PUT`/`PATCH`/`DELETE` → `412` on mismatch; required when `HttpOptions.RequireIfMatch` is on (default for aggregates marked `[RequiresIfMatch]`) |
| Idempotency | `Idempotency-Key` header on `POST`; captured into the execution context; honoured for requests marked `[Idempotent]`. The application pipeline claims the key and stores the result in `yaf_idempotency` (key, request name, payload hash, stored result, classification, retention) inside the command's transaction, so a key is recorded exactly when its effects are (`04-application.md` §3). Replay returns the stored status and body; a key reused with a different payload hash → `409`; a second request with the same key while the first is still running → `409` with `Retry-After`. Rows are purged after `Idempotency:Retention` (default 24 h) |
| Collections | `{ "items": [], "hasMore": true, "nextCursor": "…", "totalCount": null }`; `limit` clamped; unknown query parameters → `400`; sort via `sort=field,-other` against a whitelist; default total ordering |
| Long-running | `202` + `Location` of a status resource backed by a job |
| Errors | Always `application/problem+json` with `traceId` and `code` |
| Not visible vs forbidden | A resource in another tenant is `404`. `403` only when the caller may know the resource exists but lacks the permission |
| Tracing | `traceparent` accepted and echoed as `traceresponse`; no custom correlation or activity headers |
| Tenant | `X-Tenant-Id` header **or** a route segment `/tenants/{tenantId}/…` **or** a token claim, chosen by `TenantResolutionOptions` (default: header, validated against grants). A tenant id that appears in route or query MUST equal the resolved tenant unless the endpoint is marked `[CrossTenant]`. The marker only switches off that equality check and shows in the authorization inventory; it grants nothing. The handler still sees only the resolved tenant's data unless it opens a cross-tenant elevation (`04-application.md` §7), which has its own permission and audit |

## 4. Framework endpoints

| Endpoint | Access | Returns |
|---|---|---|
| `GET /health/live`, `/health/ready`, `/health/startup` | anonymous, no rate limit, no details in the body outside Development | From service defaults + YAF checks (`12-observability.md` §6) |
| `GET /errors` | authenticated, no tenant | The error catalogue: code, kind, message template |
| `GET /capabilities` | authenticated, no tenant (configurable to anonymous) | Features and permissions |
| `GET /notifications` | tenant-scoped | Server-sent events (`TypedResults.ServerSentEvents`) from `INotificationBroadcaster`; in-memory fan-out per instance by default, bounded buffer per subscriber |
| `GET /.well-known/yaf` | anonymous | Service name, version, API versions, links to OpenAPI when docs are enabled |

## 5. API documentation

- `Microsoft.AspNetCore.OpenApi` only (no Swashbuckle). OpenAPI **3.1** is pinned explicitly so the document does not change shape with a runtime or package update; 3.2 is an option.
- Transformers shipped: security schemes (bearer, API key when enabled); the tenant header on tenant-scoped operations; `problem+json` responses for the statuses an operation can produce (derived from the error kinds reachable from the request's validators and declared `[ProducesError]` attributes); permission requirements as `x-yaf-permission`; classification as `x-yaf-classification` on schema properties; `ETag`/`If-Match` headers; deprecation.
- XML doc comments flow into the document; endpoints are named methods, not lambdas, so comments are picked up.
- UI: Scalar, mapped only when `ApiDocs:Enabled` (default: Development only). Docs never prefill secrets. The UI's own routes get a content security policy that lets it load its script and styles from the same origin; that relaxed policy is applied only in Development and only to those routes. Enabling the docs UI outside Development requires `ApiDocs:ContentSecurityPolicy` to be set explicitly, and startup fails without it.
- **Build-time document**: each API project emits `openapi/v1.json` during build; the file is committed and a CI step fails when it differs (API changes are visible in review; breaking-change detection can be added with an OpenAPI diff tool).
- The transformers are written against the `Microsoft.OpenApi` version that ships with .NET 11; no compatibility layer for older versions.

## 6. Limits and protection defaults

| Control | Default | Configuration |
|---|---|---|
| Rate limit, authenticated | 300 requests/min per principal (partitioned, token bucket) | `RateLimiting:PerPrincipal` |
| Rate limit, anonymous | 60/min per client address | `RateLimiting:PerAddress` |
| Rate limit, per tenant | off; available as a named policy | `RateLimiting:PerTenant` |
| Failed credentials | 5 unknown API keys / invalid tokens per minute per address, then refused without lookup | `RateLimiting:FailedCredentials` |
| Rejection | `429` problem + `Retry-After` | |
| Request body | 1 MB (per-endpoint override `[RequestSizeLimit]`) | `Http:MaxRequestBodyBytes` |
| Request timeout | 30 s (`[RequestTimeout]` override) | `Http:RequestTimeout` |
| Headers | `X-Content-Type-Options: nosniff`, `Referrer-Policy: no-referrer`, `Content-Security-Policy: default-src 'none'; frame-ancestors 'none'` (API responses; the docs UI routes differ, §5), `Cache-Control: no-store` on authenticated responses, except those that carry an `ETag` for conditional `GET`, which get `private, no-cache` so the client may keep the body and must revalidate it; `Permissions-Policy` minimal, `Server` header removed | `Http:SecurityHeaders` |
| CORS | No origin allowed until listed; `*` refused outside Development; never with credentials | `Cors:AllowedOrigins` |
| HSTS | 1 year, include subdomains, outside Development | |
| Forwarded headers | Only from `Http:TrustedProxies` / `TrustedNetworks`; nothing trusted by default | |

Rate limit counters are in memory per instance by default; `IRateLimitStore` is the extension point for a shared store.

## 7. Acceptance criteria

- A parametrised integration suite (`HttpParity` in `YAF.Testing.Integration`) runs the **same** HTTP expectations against two hosts (controllers, minimal APIs) built over one test application: success mapping, each error kind, validation targets, malformed JSON, unknown member, a control character in a string (refused, nothing rewritten), text containing `<` and `&` stored and returned unchanged, missing auth, missing tenant, foreign tenant, missing permission, `If-Match`, `If-None-Match`, idempotent replay, replay with a different payload, rate limiting, security headers (including the cache header with and without `ETag`), CORS preflight. The suite takes the host as a parameter, so a later style is added to it without changing the expectations.
- Derived authorization: an endpoint dispatching a request marked `[RequiresPermission]` answers `403` at the edge without the attribute being written; changing the marker changes the edge metadata and the OpenAPI document; an endpoint with neither a request nor a declaration is denied and fails the inventory test; `YAF8002` and `YAF8003` have analyzer tests.
- Idempotency: when the command's transaction rolls back, no key is stored and a retry runs the handler again.
- OpenAPI snapshot tests for each style produce equivalent operations and identical schemas.
- Without `YAF.Http.Versioning`, a service with two URL-segment versions serves both, one document per version, with deprecation headers; `YAF.Http` references neither `Asp.Versioning` nor a sanitizer (architecture test).
- Startup fails with a clear message when `UseYafHttp` pieces are missing or misordered, when CORS `*` is configured outside Development, and when docs are enabled with prefilled secrets.
