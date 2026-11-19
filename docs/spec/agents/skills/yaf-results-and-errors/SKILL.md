---
name: yaf-results-and-errors
description: How YAF code reports failure - Result and Result<T>, ErrorDescriptor catalogues (IErrorCatalog), error kinds, generated and explicit error codes, the approved error catalogue file, when an exception is the right tool, and how errors become RFC 9457 problem details. Load BEFORE declaring or renaming an error, adding a failure path to an aggregate, handler or validator, changing a method's return type to or from Result, throwing or catching an exception in domain or application code, or editing errors.approved.txt.
license: MIT
metadata:
  owner: yaf
  version: "1"
---

# YAF results and errors

Business outcomes are values. Exceptions mean a bug or an infrastructure failure.

## When to load

- Adding a failure path anywhere in domain or application code.
- Declaring, renaming, moving or deleting an `ErrorDescriptor`.
- Choosing an `ErrorKind`, or wondering which HTTP status an error gives.
- Writing `throw`, `try`/`catch` outside infrastructure.
- A test failed on `errors.approved.txt`.

## Where does this go?

| Situation | Use |
|---|---|
| Input or requested change breaks a rule | `Error.Validation(...)` → 400 |
| No valid identity | `Unauthenticated` → 401 (raised by the framework, rarely by you) |
| Caller lacks the permission and may know the resource exists | `Error.Forbidden(...)` → 403 |
| Missing, or in another tenant / not visible to the caller | `Error.NotFound(...)` → 404 |
| Duplicate, taken name, wrong state for the operation, stale stored version | `Error.Conflict(...)` → 409 |
| Client-supplied expected version (`If-Match`) does not match | `PreconditionFailed` → 412 |
| A dependency is down and a retry is safe | `Unavailable` → 503 |
| Anything else that is an expected outcome | `Error.Failure(...)` → 500 with a generic detail |
| Null argument, impossible state, corrupted stored state | Exception (`Guard`, `CorruptStateException`) |
| Database, broker, network failure | Exception from infrastructure; the pipeline boundary converts known ones |

## Rules

1. **REQUIRED** Domain factories and fallible behaviours, value object `Create`, validators, command/query/job/event handlers return `Result` or `Result<T>`.
2. **REQUIRED** Every error is a `static readonly ErrorDescriptor` field of a nested class named `Errors` implementing `IErrorCatalog`. Never create an `Error` inline (YAF3001).
3. **REQUIRED** The kind is chosen at declaration (`Error.NotFound(...)`), never at the return site.
4. **REQUIRED** Codes are unique per compilation (YAF3002) and across all catalogues of a service: the approval test over `errors.approved.txt` and a startup check catch a duplicate between two assemblies. Default code is `<service code>.<declaring type path>.<field>` in snake case; pin a code clients depend on with `[ErrorCode("catalog.session.title_required")]`.
5. **REQUIRED** Messages are English, safe to show an end user, with no secrets or internals. User-supplied values appear only through `{Placeholders}` filled by `.With((name, value))` (YAF3003 warns on a mismatch).
6. **REQUIRED** A changed or removed code is a breaking change: update `errors.approved.txt` on purpose and use a `!` / `BREAKING CHANGE:` commit.
7. **REQUIRED** Never put an exception inside a result, and never convert an unexpected exception to a result in a handler. Let it propagate.
8. **REQUIRED** Repositories return plain values (`T?`, lists). A missing row is `null`; the handler turns it into `NotFound`.
9. **REQUIRED** A resource in another tenant is reported as `NotFound`, not `Forbidden`.
10. **REQUIRED** Do not call a rollback API: returning a failed result rolls the unit of work back.
11. **REQUIRED** Validation errors carry a `Target` (`.At("speakers[1].email")`; validators derive it from the rule expression).
12. **RECOMMENDED** Use the small combinator set (`Then`, `Map`, `Ensure`, `Tap`, `Match`, `Result.Combine`); stop chaining when an `if (result.IsFailure) return …` reads better.
13. **RECOMMENDED** In tests compare `result.FirstError.Code` with `Errors.X.Code`; never compare messages.

## How to

1. Write the test asserting the code.
2. Declare the descriptor in the declaring type's `Errors`.
3. Return it; fill arguments with `With`.
4. Run the catalogue approval test; review and accept the diff.

```csharp
internal sealed class ReserveSeatHandler(ISessionRepository sessions)
    : IRequestHandler<ReserveSeatCommand, Result<ReserveSeatResponse>>
{
    public async ValueTask<Result<ReserveSeatResponse>> HandleAsync(ReserveSeatCommand cmd, CancellationToken ct)
    {
        var session = await sessions.FindAsync(cmd.SessionId, ct);
        if (session is null) return Session.Errors.NotFound;

        var seat = session.ReserveSeat(cmd.RegistrationId);
        if (seat.IsFailure) return seat.Errors.ToArray();

        await sessions.UpdateAsync(session, ct);
        return new ReserveSeatResponse(seat.Value);
    }
}
```

The HTTP layer maps the result; handlers never build `ProblemDetails`. The body always has `type`, `title`, `status`, `detail`, `code`, `traceId` and an `errors` array of `{ code, message, target, arguments }`. With mixed kinds the most severe decides the status.

## Anti-patterns

| Wrong | Why | Instead |
|---|---|---|
| `return Error.NotFound("Session not found");` inside a method body | No declared code, not in `/errors` (YAF3001) | Field in `Errors`, return the field |
| `Errors.Full` declared `Failure`, "converted" to 409 in the endpoint | Kind belongs to the declaration | `Error.Conflict(...)` |
| `$"No seats for {email}"` as the message | Leaks personal data into errors and logs | Template with a placeholder; classified arguments are redacted |
| `catch (Exception e) { return Errors.Unexpected; }` | Hides bugs, skips the single error boundary | Let it propagate |
| `throw new SessionFullException()` | Business outcome as exception | `return Errors.Full.With(("Capacity", Capacity));` |
| `default(Result<T>)` treated as success | It is a failure (`CoreErrors.Uninitialised`) | `Result.Success(value)` |
| `return list;` where `list` is an `IReadOnlyList<T>` and the method returns `Result<IReadOnlyList<T>>` | C# defines no implicit conversion from an interface type | `return Result.Success(list);` |
| Returning `Forbidden` for another tenant's id | Confirms the resource exists | `NotFound` |
| Editing `errors.approved.txt` to make CI green without reading the diff | Silently breaks clients | Review each changed code; mark breaking |
| Asserting `result.FirstError.Message` | Brittle, and messages are not the contract | Assert the code |

## Verify

- `dotnet build YAF.slnx` — YAF3001/YAF3002 are errors, YAF3003 a warning treated as error.
- `dotnet test --project <service>/tests/Application.Tests` and `Domain.Tests`.
- Error catalogue approval test green; `errors.approved.txt` diff reviewed and committed; no `errors.received.txt` left behind.
- For a new kind/status path: an API test asserting status and `code`.

## References

- `docs/articles/errors.md` — the full `Result` surface, code generation, problem details shape.
- `adr/0008-results-and-error-catalogue.md` — why an own result type and declared kinds.
- `samples/conference/` Catalog `Domain/Sessions/Session.Errors.cs` and the service's `errors.approved.txt`.
- Skills: `yaf-http-endpoints` (mapping at the edge), `yaf-public-api-change` (codes are public API).
