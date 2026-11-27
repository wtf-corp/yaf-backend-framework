# 03 — Results and errors (`YAF.Core`)

YAF owns its result type. No third-party result or union library is referenced.

## 1. Types

```csharp
public enum ErrorKind
{
    Validation = 1,      // the input or the requested change breaks a rule            → 400 (422 when configured)
    Unauthenticated,     // no valid identity                                           → 401
    Forbidden,           // identity lacks the permission                               → 403
    NotFound,            // does not exist, or not visible to the caller                → 404
    Conflict,            // state conflict: duplicate, stale version                    → 409
    PreconditionFailed,  // If-Match / expected version mismatch supplied by the client → 412
    RateLimited,         //                                                             → 429
    Unavailable,         // a dependency is down; safe to retry                         → 503
    Failure              // anything else                                               → 500
}

public sealed record ErrorDescriptor(string Code, ErrorKind Kind, string MessageTemplate)
{
    public Error ToError();
    public Error With(params ReadOnlySpan<(string Name, object? Value)> args);   // fills {placeholders}
    public Error At(string target);                                              // property path, e.g. "seats[2].email"
}

public sealed record Error(string Code, ErrorKind Kind, string Message)
{
    public string? Target { get; init; }
    public IReadOnlyDictionary<string, object?> Arguments { get; init; }   // the template arguments, for client-side localisation
}

public readonly struct Result            // success or one-or-more errors
public readonly struct Result<T>
```

`Result` / `Result<T>` surface:

| Member | Notes |
|---|---|
| `IsSuccess`, `IsFailure` | `default(Result<T>)` is a failure carrying `CoreErrors.Uninitialised` (never a silent success) |
| `T Value` | Throws `InvalidOperationException` on a failure |
| `IReadOnlyList<Error> Errors`, `Error FirstError` | Empty list on success |
| `static Success()`, `Success(T)`, `Failure(Error)`, `Failure(IEnumerable<Error>)` | |
| Implicit conversions | From `T`, from `Error`, from `ErrorDescriptor`, from `Error[]` / `List<Error>`. None back to `T`. The language does not allow a user-defined conversion from an interface type. On `Result<T>`, the union conversions that `[Union]` brings convert from an interface `T` (`Result<ISessionView>`, `Result<IReadOnlyList<…>>`) and from `IReadOnlyList<Error>`; `Result<IReadOnlyList<Error>>` has no conversion from that list. `Result.Success(value)` works for every `T` |
| `Match(onSuccess, onFailure)`, `Switch(...)` | |
| `Map(Func<T, TOut>)`, `Then(Func<T, Result<TOut>>)`, `Ensure(Func<T, bool>, ErrorDescriptor)`, `Tap(Action<T>)`, `Else(Func<IReadOnlyList<Error>, T>)` | Each has `Task` and `ValueTask` overloads (`ThenAsync` …). Keep the set to these |
| `Result.Then(Action)`, `Result.Then(Func<Result>)`, `Result.Then<T>(Func<Result<T>>)` | Continue from a non-generic result |
| `Result.Combine(params ReadOnlySpan<Result>)` | Success when all succeed; otherwise all errors, in order |
| `ResultFactory.Failure<TResult>(IReadOnlyList<Error>)` where `TResult : IResult` | For generic middleware that must short-circuit to an unknown `Result`/`Result<T>`; `Result` and `Result<T>` create a failure of their own type through an internal interface that the factory asks of `default(TResult)`. No generation, no reflection; any other `IResult` throws `InvalidOperationException` naming the type |
| Failure propagation between result types | `return other.Errors.ToArray();` (the `Error[]` conversion) |
| `IResult` (non-generic interface: `IsSuccess`, `Errors`) | Lets pipeline middleware inspect any result without knowing `T` |

`Result<T>` carries `[Union]` with the non-boxing members (`HasValue`, `TryGetValue(out T)`, `TryGetValue(out IReadOnlyList<Error>)`), through the public nested interface `Result<T>.IUnionMembers`, so callers get exhaustive `switch` patterns. The nested interface keeps the union pattern's `object? Value` from clashing with `T Value`. The non-generic `Result` carries no `[Union]`: its only case would be the error list. The bare `union` keyword is not used for these types (it boxes and cannot give `default` a safe meaning).

> **Amended 2026-11-24**, by the owner's decision of 2026-11-24 (`specs/0006-core/plan.md`, decision log Q3): `[Union]` only on `Result<T>`, through `IUnionMembers`, with the union conversions it brings.

> **Amended 2026-11-27**, by the owner's decisions of 2026-11-26 (`specs/0013-thin-slice/plan.md`, decision log; deviation 5, confirmed by the owner on 2026-11-26): `ResultFactory` is not source-generated. Generated code in an application assembly could reach `YAF.Core` only through a registration, which is static mutable state; the internal interface gives the same signature, AOT-compatible and without reflection.

Design rules:

- The **kind is part of the declaration**, not chosen at the return site. One error always maps to one HTTP status.
- A `Result` never carries an exception. An unexpected exception propagates and is handled once, at the edge.
- `Error.Message` is English, safe to show to an end user, and MUST NOT contain secrets or internals. Values the user sent may appear only through template arguments; arguments of a classified type are redacted in logs (`12-observability.md`).

## 2. Declaring errors

Errors are static fields of a nested class implementing the marker `IErrorCatalog`:

```csharp
public partial class Session
{
    internal sealed class Errors : IErrorCatalog
    {
        public static readonly ErrorDescriptor NotFound = Error.NotFound("Session was not found.");
        public static readonly ErrorDescriptor Full     = Error.Conflict("Session has no free seats (capacity {Capacity}).");
        public static readonly ErrorDescriptor TransitionNotAllowed =
            Error.Validation("Session cannot go from {From} with {Trigger}.");

        [ErrorCode("catalog.session.title_required")]            // explicit, rename-proof code
        public static readonly ErrorDescriptor TitleRequired = Error.Validation("Title is required.");
    }
}
```

- `Error.Validation / NotFound / Conflict / …(messageTemplate)` (one per `ErrorKind`: `Validation`, `Unauthenticated`, `Forbidden`, `NotFound`, `Conflict`, `PreconditionFailed`, `RateLimited`, `Unavailable`, `Failure`) are factory methods that take only the message template, with no `code` parameter. At compile time the **error code generator** in the `Analyzers` project, shipped inside `YAF.Core`, replaces each call with a descriptor that carries the generated code (an interceptor per call site).
- Default code: `<ServiceCode>.<DeclaringTypePath>.<Field>` in snake case with dots, where `ServiceCode` comes from the MSBuild property `YafServiceCode` (for example `catalog`) and the declaring type path omits the catalogue class itself and the namespace: `catalog.session.full`. `[ErrorCode]` overrides it. Codes are lower-case `[a-z0-9_.]`, at most 100 characters.
- Naming: `Errors` in aggregates, handlers and validators alike. One catalogue per declaring type.
- Analyzer `YAF3001` (error): an `Error` or `ErrorDescriptor` created anywhere other than the initialiser of a static field of type `Error` or `ErrorDescriptor` of an `IErrorCatalog` type. `YAF3002` (error): two descriptors with the same code in one compilation. An analyzer sees one compilation only; the same code declared in two assemblies of a service (the default code omits the namespace, so `Session.Errors.NotFound` in the domain and a `Session` type elsewhere would collide) is caught by the approval test and the startup check of §2.1. `YAF3003` (warning): a message template placeholder without a matching argument at a `With` call. It checks a call only with what is known at compile time, namely a descriptor that is a field of the same compilation, a constant template and constant argument names; other calls are not reported. `YAF3004` (error): a factory call that needs a default code while `YafServiceCode` is missing or invalid, or a generated or `[ErrorCode]` code that breaks the rules above. `YAF3002` and `YAF3004` cover catalogue fields initialised by a factory of `Error` and fields initialised by `new ErrorDescriptor(…)` whose code is a constant. The code of such a field is its `code` argument, so it needs no `YafServiceCode`.

  > **Amended 2026-11-25**, by the owner's decisions of 2026-11-25 (`specs/0008-m1-maintenance/plan.md`, decision log): `YAF3001` names the type of the catalogue field; `YAF3002` and `YAF3004` also cover fields initialised by `new ErrorDescriptor(…)`; `YAF3003` is limited to what is known at compile time.
- Without the generator (a project that forgot it) the factory methods throw at type initialisation with a message naming the missing package; there is no silent fallback code.

> **Amended 2026-11-24**, by the owner's decisions of 2026-11-24 (`specs/0006-core/plan.md`, decision log Q5 and the decision on review finding F3): the diagnostic `YAF3004`, and the factories' signature `(string messageTemplate)`, because the interceptor replaces the whole call.

### 2.1 The error catalogue is an approved contract

Clients match on codes, so a changed code is a breaking change. Each service has a test (generated by the template) that compares all declared errors — code, kind, message template — with `errors.approved.txt` next to it. Any difference fails and writes `errors.received.txt`; updating the approved file makes the change visible in review. Removing or changing a code requires a breaking-change commit.

`IErrorCatalogProvider` (in `YAF.Application`) lists every descriptor of the loaded assemblies that declare `[assembly: YafService(…)]`, YAF's own (`core`) included, so the catalogue and `errors.approved.txt` list the `core.*` codes a client can receive (the generator emits the attribute and a static registry, so no reflection scan and no assembly-name prefixes are needed). `GET /errors` serves it (`08-http.md`).

> **Amended 2026-11-27**, by the owner's decisions of 2026-11-26 (`specs/0013-thin-slice/plan.md`, decision log; deviation 14, confirmed by the owner on 2026-11-26): the catalogue lists every assembly with a `YafService` attribute, YAF's own included. Clients receive `core.*` codes, and codes must be unique across them too.

**Codes are unique per service, across its assemblies.** The approval test fails when two descriptors of the service share a code, whichever assemblies declare them, and names both declaring types. The same check runs at startup over the registry (`IErrorCatalogProvider`); a duplicate stops the host. The fix is an `[ErrorCode]` on one of them.

## 3. Where results are used

| Place | Returns |
|---|---|
| Aggregate factory, builder `Build()`, behaviour that can fail | `Result<T>` / `Result` |
| Value object `Create` | `Result<T>` |
| Command handler | `Result<TResponse>` or `Result` |
| Query handler | `Result<TResponse>` |
| Validator | `Result` with one error per failed rule, each with `Target` |
| Repository | Plain values (`T?`, lists); a missing row is `null`, the handler decides it is `NotFound`. Store failures are exceptions |
| Domain event handler, job handler, integration event handler | `Result`; a failure rolls the unit of work back |

The transaction middleware rolls back on any failed result (`04-application.md` §6), so handlers never call a "request rollback" API.

## 4. Mapping to HTTP (RFC 9457)

`IProblemDetailsMapper` in `YAF.Http` turns `IReadOnlyList<Error>` into `ProblemDetails` through `IProblemDetailsService`; every endpoint style calls it.

- Status: all errors `Validation` → the validation status (`Http:ValidationStatusCode`, `400` by default, `422` allowed); otherwise the **most severe** kind decides (order: Unauthenticated, Forbidden, RateLimited, Unavailable, Failure, PreconditionFailed, Conflict, NotFound, Validation).
- Body:

```json
{
  "type": "https://errors.example.com/catalog.session.full",
  "title": "Conflict",
  "status": 409,
  "detail": "Session has no free seats (capacity 40).",
  "instance": "/sessions/0193…",
  "code": "catalog.session.full",
  "traceId": "4bf92f3577b34da6a3ce929d0e0e4736",
  "errors": [
    { "code": "catalog.session.full", "message": "…", "target": null, "arguments": { "capacity": 40 } }
  ]
}
```

- `type` is `ProblemDetailsOptions.TypeBaseUri` + code (default `urn:yaf:error:` + code). `errors` is always an array of the same shape, for validation and non-validation failures alike; there are not two parallel maps.
- `Failure` and unhandled exceptions return a generic `detail` ("An unexpected error occurred.") and the `traceId`; the real message goes to the log only.
- `Forbidden` on a tenant-owned resource the caller cannot see is reported as `NotFound` by handlers; the mapper does not rewrite kinds.

## 5. Acceptance criteria

- Unit tests: every combinator, sync and async; `default` results; implicit conversions, and `Result.Success(value)` where `T` is an interface; `Combine`; argument filling and `Target`.
- Generator tests compile snippets in memory and assert generated codes for nested types, generics, `[ErrorCode]`, and the four diagnostics (`YAF3001`–`YAF3004`).
- A test with two assemblies declaring the same code shows the approval check and the startup check both fail and name the two declaring types.
- A benchmark project (`tests/Benchmarks`, not run in CI) shows a successful `Result<T>` round trip allocates nothing.
- The mapper has a table-driven test covering each kind and the mixed-kind precedence.

> **Amended 2026-11-24**, by the owner's decisions of 2026-11-24 (`specs/0006-core/plan.md`, decision log Q1 and Q5): the third and fifth items are accepted in M3, not M1, together with `ResultFactory` (§1) and the `[assembly: YafService]` attribute with its registry (§2.1) (`18-build-plan.md` §2). The second item covers `YAF3004` (Q5).
