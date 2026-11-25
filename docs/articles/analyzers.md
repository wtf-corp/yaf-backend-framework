# Analyzers

`YAF.Core` ships Roslyn analyzers next to its source generators. They run in every project that references `YAF.Core` and report, at compile time, code that the building blocks of YAF cannot accept: a raw `Guid` where a typed id belongs, and an error that is not declared once, in a catalogue, with a valid and unique code.

Each diagnostic has the id `YAF`, the number of the specification file of its area and a three-digit sequence: `YAF1001` belongs to the domain, `YAF3001` to results and errors. A diagnostic of severity Error fails the build. A Warning fails it too where warnings are treated as errors, as in YAF itself.

A diagnostic marks code to change. Fix the code as described below; do not silence the diagnostic.

| Id | Category | Severity | Title |
|---|---|---|---|
| [`YAF1001`](#yaf1001) | `YAF.Ids` | Warning | Raw Guid id parameter |
| [`YAF3001`](#yaf3001) | `YAF.Errors` | Error | Error created outside an error catalogue |
| [`YAF3002`](#yaf3002) | `YAF.Errors` | Error | Two errors have the same code |
| [`YAF3003`](#yaf3003) | `YAF.Errors` | Warning | Message placeholder without an argument |
| [`YAF3004`](#yaf3004) | `YAF.Errors` | Error | Error code cannot be computed or is invalid |

## YAF1001

**Raw Guid id parameter.** Warning. Message: "The parameter '{0}' of '{1}' is a raw Guid; use a typed id".

**What it reports.** A parameter of type `Guid` or `Guid?` whose name is `id` or ends with `Id`, on a method, constructor or indexer that is public or protected, in a type that is visible outside its assembly. A raw `Guid` lets the id of one aggregate be passed where another's is expected. Generated code and the static `From(Guid)` of a type that implements <xref:YAF.Core.Ids.ITypedId%601> for itself are not reported.

**Example.** A public method `Register(Guid sessionId)` is reported. `Register(SessionId sessionId)`, where `SessionId` is a typed id, is not.

**How to fix it.** Declare a typed id for the aggregate with <xref:YAF.Core.Ids.TypedIdAttribute> and use it as the parameter type. Each aggregate type has its own id type; do not reuse one across aggregates.

## YAF3001

**Error created outside an error catalogue.** Error. Message: "'{0}' is created outside the initialiser of a static field of an IErrorCatalog type".

**What it reports.** `new Error(…)`, `new ErrorDescriptor(…)` or a call of one of the nine factories of <xref:YAF.Core.Results.Error> (`Error.Validation`, `Error.NotFound`, …) anywhere other than the initialiser of a static field of a type that implements <xref:YAF.Core.Results.IErrorCatalog>. Errors created by an `ErrorDescriptor` through `ToError`, `With` or `At`, and generated code, are not reported.

**Example.** `return Error.Conflict("Session has no free seats.");` inside a method is reported. A static field of the catalogue initialised by the same factory is not:

[!code-csharp[](../../tests/Core.Tests/Results/ErrorFactoryTests.cs#catalogue)]

**How to fix it.** Declare the error once, as a `static readonly ErrorDescriptor` field of the nested `Errors` class of the type that returns it, initialised by the factory of its kind. Return the field, or an error created from it with `With` or `At`. See [Results and errors](results-and-errors.md#error-catalogues).

## YAF3002

**Two errors have the same code.** Error. Message: "The code '{0}' of error '{1}' is also the code of '{2}'".

**What it reports.** Two or more catalogue fields, declared with the factories of `Error`, that have the same code in one compilation: a default code, a code given by <xref:YAF.Core.Results.ErrorCodeAttribute>, or one of each. Each of the fields is reported. Clients match on codes, so each code is declared once.

**Example.** A field `NotFound` of `Session.Errors` with the attribute `[ErrorCode("catalog.session.missing")]` and a field `Missing` of the same catalogue, whose default code with `YafServiceCode` `catalog` is also `catalog.session.missing`, are both reported. Removing the attribute, or pinning a different code, clears the diagnostic.

**How to fix it.** Rename one of the fields, or give it a different code with `[ErrorCode]`. Mind that a rename changes a default code that clients may already depend on: pin the old code with `[ErrorCode]` on the field that keeps it.

An analyzer sees one compilation only. The same code declared in two assemblies of a service is caught by the service's approval test of its error catalogue, not by `YAF3002`.

## YAF3003

**Message placeholder without an argument.** Warning. Message: "The placeholder '{0}' of the message template of '{1}' has no argument", with the placeholder in braces, for example '{Capacity}'.

**What it reports.** A call `descriptor.With((name, value), …)` that passes no argument for a `{Name}` placeholder of the descriptor's message template. A placeholder without an argument stays in the message as it is. Names are compared case-sensitively. The analyzer checks a call only when the descriptor is a field declared in the same compilation, its template is a constant and every argument name is a constant; other calls are not reported.

**Example.** For a descriptor with the template "Session has no free seats (capacity {Capacity})." the call `Full.With(("Seats", 40))` is reported, and so is `Full.With(("capacity", 40))`. Passing `("Capacity", 40)` fills the placeholder:

[!code-csharp[](../../tests/Core.Tests/Results/ErrorDescriptorTests.cs#fill-arguments)]

**How to fix it.** Pass an argument with the exact name of each placeholder, or correct the placeholder in the template.

## YAF3004

**Error code cannot be computed or is invalid.** Error. Message: "The code of error '{0}' {1}", where the second part says what is wrong:

- "needs the MSBuild property YafServiceCode, which is missing";
- "needs the MSBuild property YafServiceCode to match [a-z0-9_]+, but it is '…'";
- "is '…', which is not lower-case [a-z0-9_.], starts or ends with a dot, or is longer than 100 characters".

**What it reports.** A factory call that initialises a catalogue field and needs a default code while the project's `YafServiceCode` is missing or is not one segment of `[a-z0-9_]`. Also such a field whose default code or `[ErrorCode]` code breaks the rules for codes: lower-case `[a-z0-9_.]`, not starting or ending with a dot, at most 100 characters.

**Example.** In a project without `YafServiceCode`, `Error.NotFound("Session was not found.")` on a catalogue field with no `[ErrorCode]` is reported; with `<YafServiceCode>catalog</YafServiceCode>` its code is `catalog.session.not_found`. The same field with `[ErrorCode("Catalog.Session.Missing")]` is reported in any project, because of its capitals.

**How to fix it.** Set `<YafServiceCode>` in the project file to one lower-case segment, for example `catalog` (see [Results and errors](results-and-errors.md#configuration)). Correct an `[ErrorCode]` code to the rules above. A default code that breaks them, for example one longer than 100 characters, is fixed with a shorter field or type name, or with an `[ErrorCode]` code.
