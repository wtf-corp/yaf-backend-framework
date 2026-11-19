# One use case, two endpoint styles

Use case: create a session (`CreateSessionCommand` → `Result<CreateSessionResponse>`), then read it. The application code is identical in both. Neither endpoint states a permission: `CreateSessionCommand` carries `[RequiresPermission("sessions", "manage")]`, `GetSessionQuery` carries `[RequiresPermission("sessions", "view")]`, and the helpers derive the edge metadata from them.

## Minimal APIs (`YAF.Http.MinimalApis`)

```csharp
internal sealed class SessionEndpoints : IEndpoint
{
    public static void Map(IEndpointRouteBuilder routes)
    {
        var group = routes.MapYafGroup("/sessions");            // default authorization, tags, version segment

        group.MapPost("/", Create).WithName("CreateSession");                       // request is the bound body: derived
        group.MapGet("/{sessionId}", Get).Dispatches<GetSessionQuery>().WithName("GetSession");   // request built in the handler: declared
    }

    /// <summary>Creates a draft session.</summary>
    private static async Task<IResult> Create(CreateSessionCommand command, IDispatcher dispatcher, CancellationToken ct)
        => (await dispatcher.SendAsync(command, ct))
            .ToCreatedHttpResult("GetSession", r => new { sessionId = r.Id });

    /// <summary>Returns one session.</summary>
    private static async Task<IResult> Get(SessionId sessionId, IDispatcher dispatcher, CancellationToken ct)
        => (await dispatcher.SendAsync(new GetSessionQuery(sessionId), ct)).ToHttpResult();
}

// Program.cs
app.MapEndpoints<Program>();          // source-generated discovery
```

## Controllers (`YAF.Http.Controllers`)

```csharp
[Route("api/v{version}/sessions")]
public sealed class SessionsController(IDispatcher dispatcher) : YafControllerBase(dispatcher)
{
    /// <summary>Creates a draft session.</summary>
    [HttpPost]
    public Task<IActionResult> Create(CreateSessionCommand command, CancellationToken ct)
        => SendCreated(command, nameof(Get), r => new { sessionId = r.Id }, ct);

    /// <summary>Returns one session.</summary>
    [HttpGet("{sessionId}", Name = nameof(Get))]
    [Dispatches(typeof(GetSessionQuery))]
    public Task<IActionResult> Get(SessionId sessionId, CancellationToken ct)
        => Send(new GetSessionQuery(sessionId), ct);
}

// Program.cs
builder.Services.AddControllers().AddYafControllers();
app.MapControllers();
```

Response-type metadata is applied by convention from the request's result type; add `[ProducesError]` only for statuses the convention cannot infer.

## What is the same in both

| Aspect | Behaviour |
|---|---|
| Success | `201` + `Location` for create, `200` with the value for get |
| Failure | `application/problem+json` from `IProblemDetailsMapper`, with `code`, `traceId`, `errors[]` |
| Validation | The application pipeline's validators always run; edge validators are optional additions with the same body shape |
| Authorization | One marker on the request type; the edge requirement is derived from it and the pipeline checks it again; both appear in the authorization inventory |
| Tenant, actor, correlation | Execution context set by `UseYafHttp()` before the endpoint runs |
| Concurrency | `ETag` on get (`Cache-Control: private, no-cache`); `If-Match` → `412` |
| OpenAPI | `Microsoft.AspNetCore.OpenApi` document, same schemas; committed `openapi/v1.json` |

## An endpoint that dispatches no request

Rare (a redirect, a static descriptor). It has no marker to derive from, so it states its access by hand:

```csharp
group.MapGet("/export-formats", ListFormats).RequirePermission("sessions", "view");
```

## Versioning

`MapYafGroup` and the controller route convention put the major version in the URL segment (`/api/v1/…`), with one committed OpenAPI document per version. For header or media-type versioning, or several versions of one endpoint side by side, reference `YAF.Http.Versioning` (the adapter over `Asp.Versioning`) and follow its article.

## Collections and paging (any style)

```csharp
public sealed record ListSessionsQuery(string? Cursor = null, int Limit = 50, string? Sort = null)
    : IQuery<Result<PagedList<SessionSummary>>>;
```

```json
{ "items": [ { "id": "0193…", "title": "Keynote" } ], "hasMore": true, "nextCursor": "b3BhcXVl", "totalCount": null }
```

`sort=startsAt,-title` is checked against a whitelist; an unknown field or query parameter is `400`.

## Concurrency and idempotency (any style)

```http
PUT /api/v1/sessions/0193… HTTP/1.1
If-Match: "0193b2c4-…"

POST /api/v1/registrations HTTP/1.1
Idempotency-Key: 6f1c2f0e-…
```

The handler compares the expected version (`PreconditionFailed` → `412`). The version is the aggregate root's and changes on every update, also when only a child changed. A replayed idempotent `POST` returns the stored status and body; the same key with a different payload is `409`.

## Choosing a style

| Prefer | When |
|---|---|
| Minimal APIs | New services; smallest surface |
| Controllers | Teams and tooling built around MVC conventions and filters |

Pick one per service. The parity suite in the framework's tests runs the same expectations against both. A FastEndpoints package (one class per endpoint) is planned for a later release and will have to pass the same suite.
