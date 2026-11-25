# Results and errors

YAF code reports a business outcome as a value, not an exception. A use case that can fail returns a <xref:YAF.Core.Results.Result> or a <xref:YAF.Core.Results.Result%601>: a success, or a failure with one or more errors. Each error comes from a descriptor declared once, in an error catalogue, with a stable code that clients match on. An exception means a bug or an infrastructure failure, and it propagates to the edge of the application, where it is handled once.

The types live in `YAF.Core`, in the namespace `YAF.Core.Results`. YAF uses no third-party result or union library.

## Results

`Result` is a success or a list of errors. `Result<T>` is a success with a value of type `T`, or a list of errors. Both are read-only structs and implement <xref:YAF.Core.Results.IResult> (`IsSuccess`, `Errors`), so pipeline code can inspect any result without knowing `T`.

- `IsSuccess` and `IsFailure` tell the two apart.
- `Value` returns the value of a success and throws `InvalidOperationException` on a failure.
- `Errors` lists the errors in order; on a success it is empty. `FirstError` returns the first one and throws on a success.
- `Result.Success()`, `Result.Success(value)`, `Result.Failure(error)` and `Result.Failure(errors)` create results. A failure needs at least one error.

A method that returns `Result<T>` can return a value, an `Error`, an `ErrorDescriptor`, an `Error[]` or a `List<Error>`, and the result is converted implicitly. There is no conversion back to `T`: read `Value` after checking `IsSuccess`, or use the combinators below.

C# allows no user-defined conversion from an interface type. For a `T` that is an interface, such as `IReadOnlyList<string>`, return `Result.Success(value)`, which works for every `T`:

[!code-csharp[](../../tests/Core.Tests/Results/ResultTests.cs#interface-value)]

The union conversions that `Result<T>` gets from `[Union]` (see [Exhaustive switch](#exhaustive-switch)) also convert from an interface `T` and from `IReadOnlyList<Error>`. For a `Result<IReadOnlyList<Error>>` that conversion is ambiguous, so it has none from that list; use `Result.Success(value)` there.

To pass a failure on to a result of another type, convert its errors to an array:

[!code-csharp[](../../tests/Core.Tests/Results/ResultTests.cs#propagate-errors)]

### Default results

`default(Result)` and `default(Result<T>)` are failures with the error <xref:YAF.Core.Results.CoreErrors.Uninitialised> (code `core.uninitialised`, kind `Failure`). An uninitialised field or array element is never a silent success:

[!code-csharp[](../../tests/Core.Tests/Results/ResultTests.cs#default-result)]

## Errors and descriptors

An <xref:YAF.Core.Results.ErrorDescriptor> declares an error: its code, its <xref:YAF.Core.Results.ErrorKind> and its English message template. An <xref:YAF.Core.Results.Error> is one occurrence of it, carried by a failed result, with the filled message, an optional `Target` and the template `Arguments`.

The kind is part of the declaration and decides the HTTP status at the edge, so one error always gives one status:

| Kind | For | Status |
|---|---|---|
| `Validation` | The input or the requested change breaks a rule | 400 (422 when configured) |
| `Unauthenticated` | No valid identity | 401 |
| `Forbidden` | The identity lacks the permission | 403 |
| `NotFound` | Does not exist, or is not visible to the caller | 404 |
| `Conflict` | A state conflict: duplicate, stale version | 409 |
| `PreconditionFailed` | The expected version the client sent does not match | 412 |
| `RateLimited` | Too many requests | 429 |
| `Unavailable` | A dependency is down; a retry is safe | 503 |
| `Failure` | Anything else | 500 |

A descriptor creates its errors three ways:

- `ToError()` uses the template as the message, with no target and no arguments. Returning the descriptor itself does the same.
- `With((name, value), …)` fills the `{Name}` placeholders and keeps the arguments, for client-side localisation. Placeholder names are case-sensitive; values are formatted with the invariant culture; a placeholder without an argument stays as it is.
- `At(target)` sets the property path the error is about, for example `speakers[1].email`. Validation errors carry one.

[!code-csharp[](../../tests/Core.Tests/Results/ErrorDescriptorTests.cs#fill-arguments)]

[!code-csharp[](../../tests/Core.Tests/Results/ErrorDescriptorTests.cs#target)]

The message is safe to show to an end user. Values the user sent appear only through template arguments, never concatenated into the template.

## Error catalogues

Every error is a `static readonly ErrorDescriptor` field of a class that implements the marker interface <xref:YAF.Core.Results.IErrorCatalog>. By convention the class is nested in the type that returns the errors and is named `Errors`, in aggregates, handlers and validators alike, with one catalogue per declaring type. Each field is initialised by the factory of its kind on <xref:YAF.Core.Results.Error>: `Error.Validation`, `Error.Unauthenticated`, `Error.Forbidden`, `Error.NotFound`, `Error.Conflict`, `Error.PreconditionFailed`, `Error.RateLimited`, `Error.Unavailable` or `Error.Failure`. A factory takes only the message template:

[!code-csharp[](../../tests/Core.Tests/Results/ErrorFactoryTests.cs#catalogue)]

A method then returns the field, or an error created from it with `With` or `At`.

## Generated error codes

The factories take no code. At compile time the error code generator, shipped inside the `YAF.Core` package, replaces each factory call in a catalogue with a descriptor that carries the generated code.

The default code is `<service code>.<declaring type path>.<field>`, in snake case and joined by dots:

- the service code is the MSBuild property `YafServiceCode` of the project that declares the error, for example `catalog`;
- the declaring type path is the chain of types that contain the catalogue, outermost first, without the namespace, without type parameters and without the catalogue class itself;
- each name is converted to snake case: `NoFreeSeats` gives `no_free_seats`, `HTTPRequest` gives `http_request`.

So the field `Full` of `Session.Errors`, in a project with `YafServiceCode` `catalog`, has the code `catalog.session.full`.

Renaming the field or its declaring type changes the default code. For a code that clients depend on, pin it with <xref:YAF.Core.Results.ErrorCodeAttribute> on the field, for example `[ErrorCode("catalog.session.title_required")]`. The attribute replaces the whole code, service code included.

A code is lower-case `[a-z0-9_.]`, does not start or end with a dot, and has at most 100 characters. A changed or removed code is a breaking change.

## Combinators

A small set of combinators chains results. Each skips its function on a failure and passes the errors on unchanged, a `default` result included:

| Member | Does |
|---|---|
| `Match(onSuccess, onFailure)` | Returns the value of one of the two functions |
| `Switch(onSuccess, onFailure)` | Runs one of the two actions |
| `Map(selector)` | Turns the value into another value |
| `Then(continuation)` | Continues with another operation that returns a result |
| `Ensure(predicate, descriptor)` | Fails with the descriptor's error when the predicate is false |
| `Tap(action)` | Runs an action with the value and keeps the result |
| `Else(fallback)` | Turns a failure into a success with a value computed from the errors |

The non-generic `Result` has `Then(action)`, `Then(() => Result)` and `Then(() => Result<T>)`, to continue from a result without a value. `Result.Combine(results)` succeeds when all of them succeed and otherwise returns all their errors, in order.

[!code-csharp[](../../tests/Core.Tests/Results/ResultCombinatorTests.cs#then)]

[!code-csharp[](../../tests/Core.Tests/Results/ResultCombinatorTests.cs#ensure)]

[!code-csharp[](../../tests/Core.Tests/Results/ResultCombinatorTests.cs#combine)]

Each combinator has an asynchronous form with a `Task` overload and a `ValueTask` overload: `MatchAsync`, `SwitchAsync`, `MapAsync`, `ThenAsync`, `EnsureAsync`, `TapAsync` and `ElseAsync`. The overload follows the delegate: a delegate returning a `ValueTask` gives a `ValueTask`.

[!code-csharp[](../../tests/Core.Tests/Results/ResultAsyncTests.cs#then-async)]

Stop chaining when an `if (result.IsFailure) return …` reads better.

## Exhaustive switch

`Result<T>` carries the C# `[Union]` attribute through its public nested interface <xref:YAF.Core.Results.Result%601.IUnionMembers>, so a `switch` over a result is a union of the value and the error list. The compiler checks that both cases are handled, and the success path allocates nothing. A `default` result takes the error case:

[!code-csharp[](../../tests/Core.Tests/Results/ResultUnionTests.cs#exhaustive-switch)]

The non-generic `Result` carries no `[Union]`: its only case would be the error list.

## Diagnostics

The analyzers shipped in `YAF.Core` keep catalogues correct at compile time. The full list, with how to fix each diagnostic, is in the analyzers article.

| Id | Severity | Reports |
|---|---|---|
| `YAF3001` | Error | An `Error` or `ErrorDescriptor` created outside the initialiser of a static field of an `IErrorCatalog` |
| `YAF3002` | Error | Two descriptors with the same code in one compilation |
| `YAF3003` | Warning | A message placeholder without a matching argument at a `With` call |
| `YAF3004` | Error | A factory call that needs a default code while `YafServiceCode` is missing or invalid, or a code that breaks the rules above |

## Rules

1. Domain factories and fallible behaviours, value object `Create`, validators and handlers return `Result` or `Result<T>`.
2. Every error is a `static readonly ErrorDescriptor` field of a nested class named `Errors` implementing `IErrorCatalog`. Never create an `Error` inline (`YAF3001`).
3. The kind is chosen at the declaration, never at the return site.
4. Codes are unique (`YAF3002`). Pin a code that clients depend on with `[ErrorCode]`. A changed or removed code is a breaking change.
5. Messages are English, safe to show to an end user, with no secrets or internals. User values appear only through placeholders filled by `With` (`YAF3003`).
6. A result never carries an exception. Do not catch an unexpected exception to turn it into a result; let it propagate.
7. A resource of another tenant, or one the caller may not see, is reported as `NotFound`.
8. Validation errors carry a `Target`.
9. `default(Result<T>)` is a failure. Create a success with a value or `Result.Success(value)`.

## Configuration

Set the MSBuild property `YafServiceCode` in every project that declares errors with the factories, for example `<YafServiceCode>catalog</YafServiceCode>`. It is the first segment of each default code and must be one segment of `[a-z0-9_]`. The project references `YAF.Core` directly, so that the generator runs in it.

Without the generator the factories throw `InvalidOperationException`, with a message that names the package and the property; called from a static field initialiser, the catalogue fails at type initialisation. There is no fallback code:

[!code-csharp[](../../tests/Core.Tests/Results/ErrorFactoryTests.cs#missing-generator)]

Nothing else in this building block is configured. The HTTP status of `Validation` errors is an option of the HTTP adapters.

## Testing it

- Assert on error codes, never on messages: compare `result.FirstError.Code` with `Errors.X.Code` of the catalogue.
- Test the failure paths too, not only the success, and a `default` result where a result can be left uninitialised.
- A test name says the method, the scenario and the expected outcome, for example `Ensure_OnSuccessAndPredicateFalse_FailsWithDescriptor`.

The snippets in this article are tests of `YAF.Core` that show these points. Those tests run without the generator, so they declare their descriptors with `new ErrorDescriptor(code, kind, template)`. An application's tests use the catalogue of the code under test.
