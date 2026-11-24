# ADR-0008 — Results and the error catalogue

> **Status** Superseded by ADR-0031
> **Date** 2026-11
> **Deciders** Framework maintainers

## Context

Business operations fail in expected ways: input is invalid, a thing does not exist, a rule is broken. Clients need to tell these apart and to match on stable codes for messages and behaviour. The prior design used a third-party result library with its own error type alongside a framework error type; the error's kind (and so the HTTP status) was chosen at each return site; codes were derived from full type names, so renaming a namespace changed codes clients depended on; validation responses carried two parallel maps.

C# 15 on .NET 11, YAF's minimum runtime, adds union types.

## Drivers

- One error always means one thing: one code, one kind, one status.
- Codes are stable contracts; a change is visible and deliberate.
- No third-party type in every method signature of every service (ADR-0003).
- Cheap on the success path.
- Pipeline middleware can inspect any outcome without knowing its type.
- One response shape for all failures.

## Options considered

| Option | For | Against |
|---|---|---|
| Third-party result library | Ready-made | Vendor type in every signature; kind chosen per use; two error types; licence and API drift exposure |
| Exceptions for business outcomes | Familiar; no result plumbing | Costly; invisible in signatures; hard to aggregate validation errors; easy to leak messages |
| Native `union` keyword (C# 15) | Language support, exhaustive switches | Always boxes; `default` holds null, so an uninitialised result cannot be made a safe failure |
| **Own `Result` / `Result<T>` structs carrying the `[Union]` pattern** | Stable contract, no allocation, exhaustive switches | Combinators and async overloads to write and test |

## Decision

Contract: `spec/03-results-and-errors.md`.

**Own types in `YAF.Core`.** `Result` and `Result<T>` are readonly structs holding a value or one or more errors. `default` is a failure carrying `CoreErrors.Uninitialised`, never a silent success. Implicit conversions exist from a value, an `Error`, an `ErrorDescriptor` and error collections, never back to the value. The language allows no user-defined conversion from an interface type, so where `T` is an interface the success is written `Result.Success(value)`. Combinators are limited to `Match`, `Switch`, `Map`, `Then`, `Ensure`, `Tap`, `Else`, `Combine`, each with `Task` and `ValueTask` forms. A non-generic `IResult` lets middleware read `IsSuccess` and `Errors`. The structs carry `[Union]` with the non-boxing members, so callers get exhaustive switches.

**The kind is part of the declaration.** `ErrorKind`: `Validation`, `Unauthenticated`, `Forbidden`, `NotFound`, `Conflict`, `PreconditionFailed`, `RateLimited`, `Unavailable`, `Failure`. An error is declared once as an `ErrorDescriptor` (code, kind, message template) in a static field of a nested class implementing `IErrorCatalog`, through factories such as `Error.NotFound("…")`. Return sites cannot change the kind.

**Generated, stable codes.** A source generator fills the code at compile time: `<service code>.<declaring type path>.<field>` in lower snake case with dots (`catalog.session.full`), independent of namespaces. `[ErrorCode("…")]` pins a code explicitly. Without the generator the factories throw at type initialisation; there is no fallback code.

**Messages and arguments.** Message templates have named placeholders filled with `With(...)`; the arguments travel with the error so clients can localise. `At(target)` sets the property path. Messages are safe to show and contain no secrets or internals.

**The catalogue is an approved contract.** Each service has a test comparing all declared errors (code, kind, template) with `errors.approved.txt`. A difference fails and must be accepted in review; removing or changing a code is a breaking change. Codes are unique per service: because the default code leaves the namespace out, two types with the same name in different assemblies could produce the same code, and an analyzer sees one compilation only, so the approval test and a startup check over the registry look for duplicates across all of a service's catalogues. `GET /errors` serves the catalogue from a generated registry, without reflection scans.

**Exceptions only for bugs and infrastructure.** A `Result` never carries an exception. Programmer errors throw (`Guard`). Infrastructure failures throw and are converted once, at the pipeline boundary, for the few known cases (concurrency conflict, dependency unavailable); everything else becomes a generic 500.

**One mapping to HTTP (RFC 9457).** A single mapper used by all endpoint styles (ADR-0014): all-validation errors give the validation status; otherwise the most severe kind decides. The body always has `type`, `title`, `status`, `detail`, `code`, `traceId` and an `errors` array of one shape. `Failure` returns a generic detail; the real message goes to the log.

**Rollback follows the result.** The unit of work rolls back on any failed result (ADR-0009); handlers never request a rollback.

## Consequences

**Positive**

- Signatures state that an operation can fail; callers must handle it.
- Clients match on short, stable codes and get arguments for localisation.
- A code or kind change is caught by the approval test before release.
- No allocation for success; no vendor type in app signatures.
- One problem-details shape for every failure.

**Negative / costs**

- The framework owns the type, its combinators and its generator.
- Declaring an error is more ceremony than writing a string inline.
- Default codes change if a declaring type is renamed; the approval test catches it and `[ErrorCode]` prevents it, but someone must decide.
- Leaving the namespace out of the code makes same-named types collide; found by the uniqueness check, resolved with `[ErrorCode]`.
- No implicit success conversion for interface-typed results; one explicit call instead.

## Enforcement

| Mechanism | What it checks |
|---|---|
| Analyzer `YAF3001` (error) | An `Error` or `ErrorDescriptor` created outside an `IErrorCatalog` static field |
| Analyzer `YAF3002` (error) | Duplicate codes in a compilation |
| Analyzer `YAF3003` (warning) | A template placeholder without an argument at `With` |
| Analyzer `YAF4001` | Request result types are `Result` or `Result<T>` |
| Approval test (template) | `errors.approved.txt` equals the declared catalogue; no code declared twice across the service's assemblies |
| Startup check | No duplicate code in the loaded registry |
| Generator tests | Codes for nested and generic types, `[ErrorCode]`, diagnostics |
| Mapper table test | Each kind and the mixed-kind precedence |
| Error-leak tests (security pack) | No stack trace, SQL or internal message in any error body |

## Guidance for agents

- Declare every error as a descriptor in the `Errors` catalogue of the type that produces it. Never create an error inline, never return a bare string, never use `Result.Failure` with an ad hoc error.
- Choose the kind by meaning: bad input or a broken rule is `Validation`; missing or not visible is `NotFound`; a state clash is `Conflict`. Use `Failure` only when nothing else fits; it becomes a 500.
- Do not throw for a business outcome and do not catch exceptions to turn them into results in handlers; the pipeline does that for the known cases.
- Put values into message arguments with `With`, never by string interpolation, so they can be redacted and localised.
- Assert on error **codes** in tests, not on messages.
- When the approval test fails, read the diff. A new code is fine; a changed or removed code is a breaking change that needs a human decision. Never just overwrite the approved file.
- Do not add combinators. If a chain becomes hard to read, use plain `if (result.IsFailure) return result.Errors;`.
- Never read `.Value` without checking success first.
- When the result type is an interface (`Result<IReadOnlyList<T>>`), return `Result.Success(value)`; the implicit conversion does not exist there and the compiler error says so in other words.
- When the uniqueness check reports a duplicate code, pin one of the two with `[ErrorCode]`; do not rename a type to get around it.

## Related

- ADR-0003 — own implementation first
- ADR-0007 — domain patterns that return results
- ADR-0009 — pipeline and rollback
- ADR-0014 — HTTP mapping
- `spec/03-results-and-errors.md`
