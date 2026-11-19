# ADR-0014 — HTTP adapters: controllers and minimal APIs over one core

> **Status** Accepted · **Date** 2026-11 · **Deciders** maintainers

## Context

Teams prefer different ways of exposing endpoints. The prior design offered only a base MVC controller that also owned the error mapping, so any other style would have re-implemented security-relevant behaviour. The use cases themselves are style-neutral: a request goes to the dispatcher and a `Result` comes back.

## Drivers

- A service picks controllers or minimal APIs without losing or duplicating any framework behaviour, and a further style (FastEndpoints first) can be added later as a package.
- One error format, one authorization model, one set of limits, one OpenAPI pipeline.
- Endpoints stay trivially thin so the style choice carries no business risk.
- API shape changes are visible in review.
- A permission is declared in one place.
- The core HTTP package carries no third-party library a service may not need.

## Options considered

| Option | For | Against |
|---|---|---|
| Controllers only | One path | Excludes the lighter styles the platform now favours |
| Minimal APIs only | Lean, AOT-friendly | Forces migration on teams with controllers; excludes REPR libraries |
| Each style re-implements mapping and auth | Idiomatic per style | A place per style for a security bug; drift |
| **One `YAF.Http` core + a thin adapter package per style** | Parity by construction, testable; a new style is a new package | Extra packages; a parity suite to maintain |
| Three styles (with FastEndpoints) in the first release | Widest choice on day one | A third adapter over a third-party library to keep in parity before the core has met real use |
| Permission declared on the request **and** on the endpoint, compared by a test | Explicit at the edge | Two declarations of one fact; the mismatch is found by a test instead of being impossible |
| `Asp.Versioning` and an HTML sanitizer referenced by `YAF.Http` | Fewer packages | Every service carries both; contradicts the rule that an adapter isolates one third-party dependency (ADR-0003, ADR-0006) |

## Decision

1. **Core.** `YAF.Http` owns authentication, the execution-context middleware, authorization requirement data, `IProblemDetailsMapper`, `HttpResultMapper`, `YafExceptionHandler`, rate limiting, security headers, request limits, CORS, a small version-set abstraction with the URL-segment convention, OpenAPI transformers and the framework endpoints. `UseYafHttp()` installs the middleware in one fixed order; a startup filter refuses a missing or misordered piece. Its only dependencies are the ASP.NET Core shared framework and `Microsoft.AspNetCore.OpenApi`: it references no API-versioning library and no HTML sanitizer, and it does not rewrite request bodies (the input check and `[Markup]` handling belong to the application pipeline, ADR-0009).
2. **Adapters.** `YAF.Http.Controllers` (`YafControllerBase` with `Send*` helpers) and `YAF.Http.MinimalApis` (`IEndpoint` with source-generated discovery, `MapYaf*<TRequest>` route helpers, `ToHttpResult()`, `MapYafGroup`). An endpoint binds, builds the request, calls `IDispatcher.SendAsync`, maps. Nothing else. `YAF.Http.FastEndpoints` is planned for a later release and is not part of 1.0; the core's style-neutral pieces and the parity suite, which takes the host as a parameter, are what it will plug into. `YAF.Http.Versioning` is an optional adapter over `Asp.Versioning` for services that need header or media-type versioning or version-neutral endpoints.
3. **Errors.** Every failure is `application/problem+json` per RFC 9457 with `code`, `traceId` and an `errors` array of one shape. The status comes from the declared error kind; `Failure` and unhandled exceptions return a generic detail.
4. **Authorization metadata.** The permission is declared once, as the marker on the request type. The endpoint helpers know which request an endpoint dispatches and derive the edge metadata (`IAuthorizationRequirementData`, or anonymous access) and the OpenAPI security entry from that marker. Both checks still run: the edge check gives an early `401/403`, and the application pipeline decides. `[RequirePermission(feature, permission)]` is written by hand only on an endpoint that dispatches no request; an endpoint with neither a request nor a declaration is denied. A controller action whose request type cannot be determined is build error `YAF8002`; a `[SystemOnly]` request mapped to an endpoint is `YAF8003`.
5. **REST conventions** implemented by the helpers: versioned routes; strict JSON on requests; `201` + `Location`; `PUT` never creates; `PATCH` as JSON Merge Patch over `Optional<T>`; `ETag` = aggregate version with `If-Match` → `412` and `If-None-Match` → `304`; `Idempotency-Key` on `POST` for `[Idempotent]` requests, recorded with its result in `yaf_idempotency` inside the command's transaction; collection envelope `{items, hasMore, nextCursor, totalCount}` with clamped limits and a total ordering; `202` + status resource for long-running work; a resource in another tenant is `404`.
6. **Framework endpoints.** `/health/live|ready|startup`, `GET /errors`, `GET /capabilities`, `GET /notifications` (server-sent events), `GET /.well-known/yaf`.
7. **API documentation.** `Microsoft.AspNetCore.OpenApi` only, OpenAPI 3.1 pinned, transformers for security schemes, tenant header, problem responses, permissions, classification and concurrency headers. Scalar is the UI, mapped only when `ApiDocs:Enabled`, with a content security policy of its own on its routes that is applied automatically only in Development; API responses keep `default-src 'none'`. Authenticated responses are `Cache-Control: no-store`, except those with an `ETag` for conditional `GET`, which are `private, no-cache`. Each API project emits `openapi/v1.json` at build; the file is committed and CI fails on drift.
8. **Sample.** The reference solution shows both styles over the same kind of application code.

## Consequences

- Behavioural parity is a tested property, not an intention.
- A team that uses FastEndpoints has no shipped adapter in the first release. When it arrives, its own validation and error features will be routed through YAF's mapper and its command bus will not be used.
- The edge check cannot drift from the request marker, and endpoints carry one declaration less. The cost is a generator that must find the request an action dispatches, and an explicit `[Dispatches]` where it cannot.
- Services that only version by URL segment carry no versioning library; those that need more add `YAF.Http.Versioning`, one more package to maintain.
- Mixing styles in one host works but is discouraged; conventions are applied per route group or base class.

## Enforcement

- Parity suite (`HttpParity` in `YAF.Testing.Integration`): one parametrised set of HTTP expectations run against two hosts built over one test application (success mapping, every error kind, validation targets, malformed JSON, unknown member, auth and tenant failures, `If-Match`, idempotent replay, rate limits, headers, CORS).
- OpenAPI snapshot tests per style; committed-document drift check in CI.
- Analyzer `YAF8001`: repository, `DbContext` or domain type injected into an endpoint class. `YAF8002`: a controller action whose dispatched request cannot be determined. `YAF8003`: a `[SystemOnly]` request mapped to an endpoint.
- `AuthorizationInventory` approval test; an endpoint with neither a dispatched request nor an explicit declaration fails, as does a hand-written attribute that disagrees with the request's marker.
- Architecture test: `YAF.Http` references neither `Asp.Versioning` nor an HTML sanitizer.
- Startup failures: `UseYafHttp` pieces missing or misordered, CORS `*` outside Development, docs enabled with prefilled secrets.

## Guidance for agents

- Follow the style the service already uses; do not introduce a second style in a host.
- Keep endpoints to bind → request → `SendAsync` → map. Put logic in the handler or the domain.
- Never build a `ProblemDetails` or pick a status code by hand; return the `Result` and let the mapper decide.
- Declare the permission on the request type only. Do not add `[RequirePermission]` to an endpoint that dispatches a request; add it only to one that dispatches none.
- Do not reference `Asp.Versioning` from a service unless it uses `YAF.Http.Versioning`; URL-segment versions need neither.
- Do not strip or rewrite input in an endpoint or a middleware; the application pipeline refuses bad input and sanitizes `[Markup]` members.
- Use named methods for minimal API handlers so XML comments reach the OpenAPI document.
- After changing a route or contract, rebuild and commit the regenerated `openapi/v1.json` and the authorization inventory.
- Return `404`, not `403`, for something the caller's tenant cannot see.

## Related

0004, 0008, 0009, 0015, 0016, 0017, 0018, 0021
