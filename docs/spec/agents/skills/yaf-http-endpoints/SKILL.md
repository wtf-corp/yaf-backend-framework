---
name: yaf-http-endpoints
description: Exposing a YAF use case over HTTP with minimal APIs or MVC controllers - thin endpoints, result-to-response mapping, REST conventions (routes, versioning, ETag/If-Match, Idempotency-Key, paging), authorization metadata derived from the request type, tenant resolution, OpenAPI documents, rate limits and security headers. Load BEFORE adding or changing a controller, an IEndpoint, Program.cs middleware order, request/response contracts, the committed openapi/v1.json, or CORS and rate-limit settings.
license: MIT
metadata:
  owner: yaf
  version: "1"
---

# YAF HTTP endpoints

An endpoint binds, builds a request, calls `IDispatcher.SendAsync`, and maps the result. Both styles (minimal APIs, controllers) behave identically because the behaviour lives in `YAF.Http`. A FastEndpoints package is planned for a later release.

## When to load

- Adding or changing an endpoint in any style.
- Choosing a status code, route, paging shape or header.
- Touching `Program.cs` of an API host.
- The OpenAPI snapshot or the authorization inventory test failed.

## Where does this go?

| Concern | Place |
|---|---|
| Business logic, validation, transactions | Application pipeline, not the endpoint |
| Status and body from a `Result` | `HttpResultMapper` via the style helper (`Send`, `ToHttpResult()`) |
| Error body | `IProblemDetailsMapper`; never hand-built |
| Authentication, context, headers, limits | `builder.AddYafHttp(...)` + `app.UseYafHttp()` |
| Per-endpoint permission | The request type's marker (`[RequiresPermission]`, `[AllowAnonymousRequest]`, …). The style helper derives the edge authorization metadata and the OpenAPI security entry from it. `[RequirePermission(feature, permission)]` / `.RequirePermission(...)` is written by hand only on an endpoint that dispatches no request |
| API versions beyond the URL segment (header, media type, several versions side by side) | `YAF.Http.Versioning` (adapter over `Asp.Versioning`) |
| A stricter rate limit | A named policy on the endpoint |
| Request/response records | With the use case in the application project (or the API project when HTTP-only) |

## Rules

1. **REQUIRED** Endpoints hold no logic and inject no repository, `DbContext` or domain type (YAF8001).
2. **REQUIRED** Use the style helper for mapping; do not write `if (result.IsFailure) return BadRequest(...)`.
3. **REQUIRED** Declare access once, on the request type. The helper derives the edge metadata from the request the endpoint sends: from the bound parameter when the request is the body, otherwise from `.Dispatches<TRequest>()` (minimal APIs) or `[Dispatches(typeof(TRequest))]` (controllers). Do not repeat the permission at the edge; both checks (edge and pipeline) still run. Access is denied by default: an endpoint with no metadata needs an authenticated principal and a tenant, and the `AuthorizationInventory` approval test lists every endpoint with its derived decision.
4. **REQUIRED** `[AllowAnonymous]`, `[UserScoped]` and `[CrossTenant]` are deliberate, reviewed exceptions; each shows up in the approved inventory.
5. **REQUIRED** Call `app.UseYafHttp()` once and do not reorder its pieces; a startup filter refuses a missing or misordered pipeline.
6. **REQUIRED** Routes are `/api/v{major}/<plural-kebab-resource>`; ids are server-generated; no verbs in paths except documented actions (`/sessions/{id}/publish`).
7. **REQUIRED** Create → `201` + `Location`. Delete → `204`, idempotent. `PUT` replaces and never creates. `PATCH` is JSON Merge Patch bound to `Optional<T>` members. Long-running → `202` + `Location`.
8. **REQUIRED** Collections return `{ items, hasMore, nextCursor, totalCount }`, never a bare array; `limit` is clamped; unknown query parameters are `400`.
9. **REQUIRED** Single-resource `GET` returns `ETag` (the aggregate root's version) with `Cache-Control: private, no-cache`, so conditional `GET` works; other responses get `no-store`. Writes honour `If-Match` → `412`.
10. **REQUIRED** A resource in another tenant is `404`. A tenant id in route or query must equal the resolved tenant unless the endpoint is `[CrossTenant]`.
11. **REQUIRED** Domain types are never serialized. Requests refuse unknown JSON members.
12. **REQUIRED** Endpoints are named methods with XML docs (lambdas lose the comments); a change to the API updates the committed `openapi/v1.json`.
13. **REQUIRED** No custom correlation or trace headers: `traceparent` in, `traceresponse` out.
14. **REQUIRED** CORS origins are listed explicitly; `*` is refused outside Development. API docs are served only when `ApiDocs:Enabled`; the content security policy is relaxed for the documentation UI in Development only.
15. **RECOMMENDED** One style per service. Mark repeatable `POST`s `[Idempotent]` and document `Idempotency-Key`.
16. **RECOMMENDED** Declare extra failure statuses with `[ProducesError]` so the OpenAPI document is complete.

## How to

1. API integration test first: success status and body, one failure `code`, missing permission → `403`, foreign tenant → `403`/`404`.
2. Add the endpoint in the service's style.
3. Build; review and commit the `openapi/v1.json` diff; accept the authorization inventory diff.

Minimal API:

```csharp
internal sealed class PublishSessionEndpoint : IEndpoint
{
    public static void Map(IEndpointRouteBuilder routes) => routes
        .MapYafGroup("/sessions")
        .MapPost("/{sessionId}/publish", Handle)
        .Dispatches<PublishSessionCommand>()          // edge permission and OpenAPI security come from its marker
        .WithName("PublishSession");

    /// <summary>Publishes a draft session so attendees can register.</summary>
    private static async Task<IResult> Handle(SessionId sessionId, IDispatcher dispatcher, CancellationToken ct)
        => (await dispatcher.SendAsync(new PublishSessionCommand(sessionId), ct)).ToHttpResult();
}
```

The same use case as a controller: `references/styles.md`.

Host order:

```csharp
builder.AddServiceDefaults();
builder.AddYafHttp(http => http.AddAuthentication().AddApiDocs().AddRateLimiting().AddCors());
builder.AddCatalogApplication();
builder.AddCatalogInfrastructure();

var app = builder.Build();
app.UseYafHttp();
app.MapYafEndpoints();
app.MapEndpoints<Program>();
app.MapDefaultEndpoints();
app.Run();
```

## Status quick reference

| Outcome | Status |
|---|---|
| `Result<T>` success | 200 (201 with location, 202 accepted) |
| `Result` success | 204 |
| `Validation` / malformed JSON / unknown member | 400 |
| `Unauthenticated` / `Forbidden` | 401 / 403 |
| `NotFound` / `Conflict` / `PreconditionFailed` | 404 / 409 / 412 |
| `RateLimited` / `Unavailable` | 429 + `Retry-After` / 503 |
| `Failure`, unhandled exception | 500, generic detail + `traceId` |

## Anti-patterns

| Wrong | Why | Instead |
|---|---|---|
| Injecting `ISessionRepository` into a controller | Bypasses authorization, validation, transaction (YAF8001) | Send a request through `IDispatcher` |
| `return BadRequest(result.Errors)` | Second error format; wrong status for other kinds | `Send(...)` / `ToHttpResult()` |
| `[AllowAnonymous]` to "make the test pass" | Opens the endpoint | Use the test auth of `YAF.Testing.Integration` (signed test tokens) |
| `[Authorize(Roles = "admin")]` | Not the YAF permission model; not in the inventory | `[RequiresPermission(...)]` on the request type |
| `.RequirePermission("sessions", "publish")` next to a request that already carries the marker | Two declarations that can drift | `.Dispatches<PublishSessionCommand>()` |
| Reading `X-Tenant-Id` in the endpoint | Unvalidated tenant | `IExecutionContextAccessor` (in the handler) |
| Returning `Session` or `SessionData` | Leaks state; breaks on refactor | Response record |
| Bare JSON array for a list | Cannot add paging later | Collection envelope |
| `app.UseAuthentication()` etc. by hand in another order | Context or limits run too early/late | `app.UseYafHttp()` |
| Lambda endpoint handlers | XML docs missing from OpenAPI | Named method |
| Returning `403` for another tenant's id | Confirms existence | `404` from the handler |

## Verify

- `dotnet build YAF.slnx` (YAF8001; build regenerates `openapi/v1.json`).
- `dotnet test --project <service>/tests/Api.IntegrationTests` (Docker): your tests, `AuthorizationInventory` approval, cross-tenant request suite, OpenAPI snapshot.
- `git diff -- **/openapi/v1.json **/authorization-inventory.approved.txt` reviewed and committed.

## References

- `docs/articles/http.md` — setup, mapping, conventions, limits, OpenAPI transformers.
- `references/styles.md` — the two styles side by side; read when adding the first endpoint of a service.
- `adr/0014-http-adapters.md` — why several styles over one mapping.
- `samples/conference/` — Catalog (minimal APIs), Registrations (controllers).
- Skills: `yaf-use-cases`, `yaf-results-and-errors`, `yaf-security`.
