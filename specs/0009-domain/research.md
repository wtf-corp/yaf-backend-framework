# 0009 — Domain (M2): research

What the plan relies on and how it was checked. Probes were built on 2026-11-25 in a scratch folder outside the repository, against `origin/main` at 1add91a, and are not committed.

## 1. Environment

- .NET SDK 11.0.100 (`dotnet --version`), `global.json` as on `main`.
- The probe had three projects: a library `YAF.Domain` and a library `YAF.Testing` with the settings of the root `Directory.Build.props` and `src/Directory.Build.props` (`AnalysisLevel` `latest`, `AnalysisMode` `All`, `EnforceCodeStyleInBuild`, the banned API and public API analyzers, the repository's `.editorconfig`), and a console app that used both. All three referenced `src/Core/Core.csproj` and `src/Analyzers/Analyzers.csproj` of the repository. Warnings were not errors, so that one build lists every finding.
- The library sources declared every public type of the plan's delta with stub bodies. Stub bodies make `CA1822` fire on methods that do not read instance data yet; those findings are not listed below.

## 2. Analyzer rules that the declarations of `02` set off

Probe D1, `dotnet build` of the `YAF.Domain` skeleton. Findings on declarations that `02` prescribes:

| Rule | Where | Why it fires | `02` |
|---|---|---|---|
| CA1716 | `IBusinessRule.Error` | An interface member named `Error`, a Visual Basic keyword | §8 |
| CA1030 | `AggregateRoot<…>.Raise` | A method whose name starts with `Raise` ("consider making it an event") | §4 |
| CA1033 | `AggregateRoot<…>`, `IAggregateRoot.DequeueDomainEvents` | An unsealed public type implements an interface method explicitly and offers no visible method of the same name | §4 |
| CA1000 | `Enumeration<TSelf>.All`, `Enumeration<TSelf>.FromCode` | Static members on a generic type | §6.2 |
| CA1724 | `Enumeration<TSelf>` | The type name equals the last part of the namespace `System.IO.Enumeration` | §6.2 |
| CA1000 | `StateMachine<TState, TTrigger>.Define` | A static member on a generic type | §10 |
| CA1030 | `StateMachine<TState, TTrigger>.Fire` | A method whose name starts with `Fire` | §10 |

Not reported, although expected from M1's history: CA1005 (three type parameters on `Entity<TId, TSelf, TMemento>`), CA1006 (`Expression<Func<T, bool>>`, `IEnumerable<Entity<…>>`, `Func<StateMachineBuilder<…>, StateMachine<…>>`), CA1000 on the `protected static` `Entity<…>.Check` and on the static abstract `IOriginator<TSelf, TMemento>.Restore`, CA1040 on `IAggregateMemento` and `IOwnedEntityMemento` (they have no members of their own but inherit some), CA1033 on the explicit `IAggregateRoot.CheckInvariants` (the protected `CheckInvariants` of `Entity<…>` is the visible method of the same name).

Findings on things the plan chose, each avoided in code:

- CA1030 on a builder hook named `RaiseCreated`. The hook is named `OnCreated`.
- CA1812 on an `internal` JSON converter factory that only an attribute names. The factory is public (§4), and its converter is created with `new` inside a generic helper.
- CA1034 (not probed here): plan 0006 met it on a public nested type (`Result<T>.IUnionMembers`), and a public nested `Errors` catalogue in a value object is one too. The catalogues of `YAF.Domain` are top-level classes (`DomainErrors`, `ValueObjectErrors`), as `CoreErrors` is.
- CA1062 on every public or protected method of consumer code that uses a parameter without a null check, for example `SnapshotCore(IMemento memento)` in a public fixture aggregate. Test fixtures are `internal`, or call `Guard.NotNull`.

The skeleton of `YAF.Testing` (the helpers of the plan's delta, with `TestClock` over `FakeTimeProvider`, §8) gave no finding besides RS0016.

## 3. `.editorconfig` sections for file names with several type parameters

The repository names a generic type's file `Result{T}.cs` and scopes an exception with `[src/Core/Results/Result\{T\}.cs]`. Probe D2, a file `Thing{TA,TB,TC}.cs` with a method that sets off CA1030:

| Section header | CA1030 on the file |
|---|---|
| `[Domain/Thing\{TA,TB,TC\}.cs]` | still reported |
| `[Domain/Thing\{TA\,TB\,TC\}.cs]` | not reported |

So the commas are escaped too. T2 writes `[src/Domain/AggregateRoot\{TId\,TSelf\,TMemento\}.cs]` and `[src/Domain/StateMachines/StateMachine\{TState\,TTrigger\}.cs]`.

## 4. `System.Text.Json` does not read `[JsonConverter]` from a base type

`02` §6.2: "`Enumeration<TSelf>` gives … a JSON converter by code". Probe J1:

- `[JsonConverter(typeof(EnumerationJsonConverterFactory))]` on the abstract record `Enumeration<TSelf>` only: `JsonSerializer.Deserialize<Format>("\"workshop\"")` for `sealed record Format : Enumeration<Format>` threw `JsonException: The JSON value could not be converted to Format`. The attribute on the base type is not used for the derived type.
- The factory added to `JsonSerializerOptions.Converters`: `Serialize(Format.Talk)` gave `"talk"` and `Deserialize<Format>("\"workshop\"")` gave the `Workshop` instance.
- The attribute on the concrete type (`[JsonConverter(typeof(EnumerationJsonConverterFactory))] sealed record Attributed : Enumeration<Attributed>`) with default options: `"one"`.

So the converter is a public factory that is either registered in the options or named on the concrete type. The typed ids of M1 do not have the problem: their generator writes the attribute on each id.

## 5. Type inference for `MementoCollections.Sync` and `MementoAssert.RoundTrips`

`02` §5 writes the call as `MementoCollections.Sync(memento.Seats, _seats, memento.NewSeat)`. A signature with the entity as a type parameter constrained to `Entity<TId, TEntity, TMemento>` cannot be called that way: C# does not infer a type argument from a constraint, so `TId` stays unknown. Probe I1, with the parameter typed as the base class:

    public static void Sync<TId, TEntity, TMemento>(IList<TMemento> mementos, IEnumerable<Entity<TId, TEntity, TMemento>> entities, Func<TMemento> create)

`MementoCollections.Sync(memento.Lines, _lines, memento.NewLine)` with `List<Line> _lines` compiled and ran: the three type arguments are inferred through the covariant `IEnumerable<T>` and the unique base class of `Line`. The same holds for `MementoAssert.RoundTrips(booking, () => new BookingMemento())` with the first parameter typed `Entity<TId, TEntity, TMemento>`.

## 6. Analyzers and error codes in a project that references `YAF.Core` as a project

Inside the repository `YAF.Domain` references `src/Core/Core.csproj`, not the package, so it gets neither the analyzers (`Core.csproj` references them with `PrivateAssets="all"`) nor `buildTransitive/YAF.Core.props`. Probe E1: a project with

    <ProjectReference Include="…/src/Analyzers/Analyzers.csproj" OutputItemType="Analyzer" ReferenceOutputAssembly="false" PrivateAssets="all" />
    <Import Project="…/src/Core/buildTransitive/YAF.Core.props" />
    <YafServiceCode>core</YafServiceCode>

built, and at run time `DomainErrors.UnknownEnumerationCode.Code` was `core.enumeration.unknown_code` (a factory call with `[ErrorCode]`), a nested catalogue of the app gave `probe.booking.title_required` (default code), and `[TypedId] readonly partial record struct BookingId;` got its generated members. `YAF2009` does not report the `Guid` properties of the memento base contracts: `RawGuidIdAnalyzer` looks at method and indexer parameters only (`src/Analyzers/Ids/RawGuidIdAnalyzer.cs`).

Also seen in the probe: `domainEvent with { OccurredAt = now }` on a variable of the abstract type `DomainEvent` keeps the concrete type and the `EventId`, so the aggregate tracker of M3 can stamp events; and constructing an entity with a default id throws `ArgumentException` from `Guard.NotDefault`.

## 7. `[CallerFilePath]` under a CI build

`Approvals.Verify` finds the approved file next to the test through `[CallerFilePath]` (`15` §3.1). The root `Directory.Build.props` sets `ContinuousIntegrationBuild` on GitHub Actions for every project. Checked in the repository:

    dotnet msbuild tests/Core.Tests/Core.Tests.csproj -restore -t:InitializeSourceRootMappedPaths -t:_SetPathMapFromSourceRoots -getProperty:PathMap -getProperty:DeterministicSourcePaths -p:ContinuousIntegrationBuild=true

gave `DeterministicSourcePaths` `true` and a `PathMap` that maps the repository root to `/_/`. The compiler applies the path map to `[CallerFilePath]`, so in CI the helper would look in `/_/tests/…`, which does not exist. A test project that calls `Approvals.Verify` sets `<DeterministicSourcePaths>false</DeterministicSourcePaths>`; the helper fails with a message that says so when the directory of the calling file does not exist.

## 8. `Microsoft.Extensions.TimeProvider.Testing`

- Versions on nuget.org on 2026-11-25 (`https://api.nuget.org/v3-flatcontainer/microsoft.extensions.timeprovider.testing/index.json`): the latest stable is 11.0.1. The plan pins 11.0.1.
- The probe compiled against 11.0.1: `Microsoft.Extensions.Time.Testing.FakeTimeProvider` with the constructor `FakeTimeProvider(DateTimeOffset startDateTime)` and the methods `GetUtcNow()`, `Advance(TimeSpan)` and `SetUtcNow(DateTimeOffset)`. `TestClock` uses these four and nothing else.
- `15` §3 and ADR-0018 name the package. ADR-0003's row for `YAF.Testing` ("nothing beyond `YAF.Application`'s row") covers it as a `Microsoft.Extensions.*` package. The licence is to be read from the package's nuspec by the ticket that adds it (T25), as M1 did for its packages.

## 9. ISO 4217

`02` §6.1: "`Currency` is an ISO 4217 code with minor units". Source: list one of the maintenance agency, `https://www.six-group.com/dam/download/financial-information/data-center/iso-currrency/lists/list-one.xml`, fetched 2026-11-25, `Pblshd="2026-09-17"`.

- 178 distinct alphabetic codes. 165 have a numeric minor unit: 17 with 0, 139 with 2, 7 with 3, 2 with 4. Examples: `EUR` 2, `USD` 2, `JPY` 0, `BHD` 3, `CLF` 4, `UYW` 4.
- 13 have "N.A.": `XAG`, `XAU`, `XBA`, `XBB`, `XBC`, `XBD`, `XDR`, `XPD`, `XPT`, `XSU`, `XTS`, `XUA`, `XXX`. They are not money amounts with a minor unit and are left out.

The ticket that adds the table (T11) fetches the list again and records its publication date in the source.

## 10. RFC 5321 length limits

`02` §6.1: "length limits from RFC 5321". `https://www.rfc-editor.org/rfc/rfc5321.txt`, read 2026-11-25:

- §4.5.3.1.1: "The maximum total length of a user name or other local-part is 64 octets."
- §4.5.3.1.2: "The maximum total length of a domain name or number is 255 octets."
- §4.5.3.1.3: "The maximum total length of a reverse-path or forward-path is 256 octets (including the punctuation and element separators)." A path is the address in angle brackets, so an address has at most 254 octets.

## 11. Mermaid state diagrams

`02` §10: "`ToMermaid()` renders the diagram". `https://mermaid.js.org/syntax/stateDiagram.html`, read 2026-11-25: the header is `stateDiagram-v2`; a transition is `s1 --> s2`; a transition with a label is `s1 --> s2: A transition`; `[*]` is the start and end pseudo-state. A state machine definition knows neither its initial nor its final states (`StateMachineAssert.IsWellFormed` is given them), so `ToMermaid()` writes the header and one labelled transition per line and no `[*]`.

## 12. Public API line shapes

Probe D1 with empty `PublicAPI.*.txt` files. Besides the shapes of plan 0006's research §5, records and abstract classes give lines that start with `abstract `, `virtual `, `override sealed `, `~abstract `, `~virtual `, `~static ` and a bare `~` (the copy constructor of an abstract record: `~YAF.Domain.Enumeration<TSelf>.Enumeration(YAF.Domain.Enumeration<TSelf>! original) -> void`). `delta.mjs` matches an entry that ends in `*` by line prefix, so each modifier needs an entry of its own in the `factory:delta` block.
