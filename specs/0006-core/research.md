# 0006 — Core (M1): research

What the plan relies on about third-party behaviour, with the sources and the versions it was checked against. "Probe" means the planner built and ran it on this machine on 2026-11-24, in a scratch folder outside the repository that was then discarded. Nothing here was committed.

## 1. Environment

- SDKs installed: `10.0.401` and `11.0.100` (`dotnet --list-sdks`). The repository pins `11.0.100` with `rollForward` `latestFeature` (`global.json`).
- The compiler in SDK `11.0.100` is Roslyn `5.12.0-1.26529.118`; `Microsoft.CodeAnalysis.CSharp.dll` has assembly version `5.12.0.0`. SDK `10.0.401` has assembly version `5.9.0.0`.
- Reference pack `Microsoft.NETCore.App.Ref` `11.0.0`.
- `IsAotCompatible` and `IsTrimmable` set to `true` emit `[assembly: AssemblyMetadata("IsTrimmable", "True")]` and `[assembly: AssemblyMetadata("IsAotCompatible", "True")]` (probe). T1's failing test reads them.

## 2. C# 15 union types and `[Union]` on `Result<T>`

Source: "Union types - C# reference", learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/union (`ms.date` 2026-10-19, updated 2026-10-23), sections "Custom union types", "Non-boxing access pattern", "Union member providers", "Union exhaustiveness".

What the page says:

- `System.Runtime.CompilerServices.UnionAttribute` and `IUnion` ship in the .NET 11 runtime. The probe found both public in CoreLib: `UnionAttribute` (`AttributeTargets` Class | Struct, not inherited), `IUnion` with `object? Value { get; }`.
- The *basic union pattern* needs `[Union]`, one public single-parameter constructor per case type, and a public `object?` `Value` property.
- A *union member provider* is a public nested interface `IUnionMembers` that the type implements. The compiler then reads the union members only from it: a static `Create` method per case type and `object? Value`, optionally `bool HasValue` and one `bool TryGetValue(out TCase)` per case type.
- With `TryGetValue`, a type pattern on the union calls it and does not box; with `HasValue`, a `null` pattern calls it.
- A custom union may represent its `default` value as a non-null case ("Soundness").
- An implicit *union conversion* exists from each case type. A user-defined implicit conversion for the same source type takes priority.

Why it matters: `03` §1 gives `Result<T>` a public `T Value` that throws on failure. The basic pattern needs a public `object? Value`, so the two clash. The member provider moves `object? Value` into the nested interface (explicit implementation), and the public `T Value` stays.

Probe U1 (SDK 11.0.100, `net11.0`, C# 15 by default), a `readonly struct R<T>` with `[Union]`, `IUnionMembers` (`Create(T)`, `Create(IReadOnlyList<string>)`, `Value`, `HasValue`, both `TryGetValue`), a private constructor per case, public `T Value` that throws on failure, and `default` reported as the error case:

- `r switch { int v => …, IReadOnlyList<string> e => … }` built with 0 warnings and no discard arm.
- Removing the error arm gave `warning CS8509 … the pattern 'System.Collections.Generic.IReadOnlyList<string>' is not covered`. The switch is exhaustive over the two cases.
- `default(R<int>)` went to the error arm.
- 1 000 conversions plus `is int v` matches allocated 0 bytes (`GC.GetAllocatedBytesForCurrentThread`, Release).
- Side effect: `R<IEnumerable<int>> a = seq;` (interface-typed `T`) and `R<int> b = errorsList;` (an `IReadOnlyList<string>`) both compiled. These are union conversions, which exist for every case type. `03` §1 says there is no conversion from an interface `T`, and lists no conversion from `IReadOnlyList<Error>`. With `[Union]` both exist (plan Q3).
- `R<IReadOnlyList<string>>` (the success type equal to the error case type) still compiled and worked through a named factory. A conversion from that list type is then ambiguous.

The non-generic `Result` has no success value, so it has no second case type. The page offers no pattern for "success without a value" other than `Value == null`, which would make `HasValue` mean "has errors" (plan Q3).

## 3. Interceptors for the error code generator

Source: "Interceptors", github.com/dotnet/roslyn/blob/main/docs/features/interceptors.md (read 2026-11-24): stable since the .NET 9.0.2xx SDK; `[InterceptsLocation(int version, string data)]`, data opaque; generators should get it from `GetInterceptableLocation(this SemanticModel, InvocationExpressionSyntax, CancellationToken)`; a generator declares the attribute itself, `file`-local, to avoid clashes with other generators; the consumer lists the allowed namespaces in MSBuild property `InterceptorsNamespaces` (alias `InterceptorsPreviewNamespaces`). The page does not say whether child namespaces are covered, so the plan uses one exact namespace.

Probe I1: an incremental generator in a `netstandard2.0` project referencing `Microsoft.CodeAnalysis.CSharp` **5.0.0**, reading `build_property.YafServiceCode` from `AnalyzerConfigOptionsProvider.GlobalOptions`, finding calls of a static `NotFound(string)` factory in a static field initializer, emitting one interceptor per call that returns `<service code>.<field>` plus the message:

- With `<CompilerVisibleProperty Include="YafServiceCode" />` and `<InterceptorsNamespaces>$(InterceptorsNamespaces);Yaf.Generated</InterceptorsNamespaces>`, the consumer printed `catalog.notfound | Session was not found.`
- Without `InterceptorsNamespaces`: `error CS9137: The 'interceptors' feature is not enabled in this namespace. Add '<InterceptorsNamespaces>$(InterceptorsNamespaces);Yaf.Generated</InterceptorsNamespaces>' to your project.`
- A primary-constructor attribute `file sealed class InterceptsLocationAttribute(int version, string data) : Attribute;` gives `warning CS9113: Parameter 'version' is unread` in the consumer, which is an error under `TreatWarningsAsErrors`. An explicit constructor that discards both parameters gives no warning. The generated code must build warning-free in a consumer with warnings as errors.
- The generator class declared `internal sealed` was still loaded and ran.
- `GetInterceptableLocation` and `ForAttributeWithMetadataName` are available in 5.0.0, and 5.0.0 loads in the 5.12 compiler. The plan pins **5.0.0**: the oldest version that has every API the generators use, so hosts with an older compiler can still load the analyzers (an analyzer built against a newer Roslyn than its host's fails to load).

## 4. Packing the analyzers into `YAF.Core`

Probe P-pack: a `net11.0` library with `<ProjectReference … OutputItemType="Analyzer" ReferenceOutputAssembly="false" PrivateAssets="all" />` to the generator project (so the library uses the generator itself), `<None Include="…/netstandard2.0/Gen.dll" Pack="true" PackagePath="analyzers/dotnet/cs" />` and a props file packed to `buildTransitive;build`:

- The package held `analyzers/dotnet/cs/Gen.dll`, `build/Probe.Core.props`, `buildTransitive/Probe.Core.props`, `lib/net11.0/Lib.dll`, and no dependency on the generator project.
- A consumer with `TreatWarningsAsErrors`, restoring the package from a local folder, set only `<YafServiceCode>catalog</YafServiceCode>`: the props supplied `InterceptorsNamespaces` and `CompilerVisibleProperty`. It printed `catalog.notfound | …` for its own catalogue and `core.uninit | …` for the library's catalogue, which the library's own build had intercepted.
- The probe set no `PackageId` per project; T1 sets `AssemblyName`, `RootNamespace` and `PackageId` to `YAF.$(MSBuildProjectName)` in `src/Directory.Build.props`.

Convention for the folder: NuGet "Analyzer NuGet formats", learn.microsoft.com/nuget/guides/analyzers-conventions (`analyzers/dotnet/cs`). The probe is the evidence relied on.

## 5. Public API analyzer

`Microsoft.CodeAnalysis.PublicApiAnalyzers` 5.6.0 (already in `Directory.Packages.props`). Probe A1, a library with the analyzer and empty `PublicAPI.*.txt` files:

- Members emitted by a source generator are reported (`RS0016: Symbol 'Probe.GeneratedThing' is not part of the declared public API`). So the framework ids that YAF.Core generates for itself (T15) go into `src/Core/PublicAPI.Unshipped.txt`.
- Line shapes seen (the probe put every type in one namespace; the plan's namespaces differ), which decide the prefixes of the `factory:delta` block: `YAF.Core.Results.Error`, `YAF.Core.Results.Error.Code.get -> string!`, `static YAF.Core.Results.TenantId.From(System.Guid value) -> …`, `static readonly YAF.Core.Results.CoreErrors.Uninitialised -> …`, `const YAF.Core.Results.CoreErrors.Max = 100 -> int`, `override YAF.Core.Results.Error.ToString() -> string!`, `static … operator ==(…)`, `static … implicit operator …`, nested `YAF.Core.Results.R<T>.IUnionMembers.Create(T value) -> …`, enum members `… .Validation = 1 -> …`, and for a `record struct` the synthesized `~override YAF.Core.Results.TenantId.Equals(object obj) -> bool` (nullable-oblivious, hence `~`).
- Static abstract and static interface members (probe A3, on review question 1): the analyzer reports `ITypedId<TSelf>.From` and a static `IUnionMembers.Create` with no modifier, `YAF.Core.Ids.ITypedId<TSelf>.From(System.Guid value) -> TSelf`. With those exact lines in `PublicAPI.Unshipped.txt` the build had no RS0016 or RS0017. Rewriting the line as `static abstract YAF.Core.Ids.ITypedId<TSelf>.From(…)` gave `RS0017` for it and `RS0016` for the member. So no `static abstract` prefix is needed.
- `delta.mjs` matches an entry ending in `*` by line prefix (`entryMatches`), so every modifier that starts a line needs its own entry.

## 6. Writing analyzers under the repository's settings

Probe A2, an analyzer project with `AnalysisMode` `All` and `TreatWarningsAsErrors`, as `src/Directory.Build.props` and the root props give every `src/` project:

- `RS2008: Enable analyzer release tracking for the analyzer project containing rule 'YAF1001'`. `Microsoft.CodeAnalysis.CSharp` 5.0.0 brings `Microsoft.CodeAnalysis.Analyzers` 3.11.0 transitively (nuspec of 5.0.0), which owns the rule. The project needs `AnalyzerReleases.Shipped.md` and `AnalyzerReleases.Unshipped.md` as additional files, and each new diagnostic adds a row (shared between the diagnostic tickets).
- `CA1308` on `ToLowerInvariant` (the snake-case conversion). Codes are ASCII `[a-z0-9_.]`, so a hand-written ASCII lowering is used, and a non-ASCII identifier is an invalid code (YAF3004, Q5). No suppression.
- `CA1062` (validate `context`) and `CA1050` also fire. They are fixed in code. Assembly references of the built probe analyzer (a generator and a `DiagnosticAnalyzer`, `netstandard2.0`, Roslyn 5.0.0, Release), read with `Assembly.GetReferencedAssemblies()`: `netstandard` 2.0.0.0, `Microsoft.CodeAnalysis` 5.0.0.0, `Microsoft.CodeAnalysis.CSharp` 5.0.0.0, `System.Collections.Immutable` 9.0.0.0 (probe A5, on review F1). No `System.Reflection.Metadata` or `System.Memory`. T22's rule for `Analyzers` allows exactly these.
- `EnforceExtendedAnalyzerRules` turns on the RS1035 family (no file or environment access in analyzers).

`netstandard2.0` has no `IsExternalInit` and no nullable attributes. Records or `init` in the analyzer code need an internal declaration of `IsExternalInit` in the project. That is a source file, not a polyfill package (ADR-0003).

## 7. Testing generators in memory

Probe T1-harness (`net11.0` console referencing `Microsoft.CodeAnalysis.CSharp` 5.0.0 and the generator project):

- `CSharpGeneratorDriver.Create([generator.AsSourceGenerator()], parseOptions:, optionsProvider:)`, then `RunGeneratorsAndUpdateCompilation`, then `Emit` into a stream and load it in a collectible `AssemblyLoadContext`: the snippet's field read `catalog.notfound`.
- Metadata references from `AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")`, so no reference-assemblies package is needed.
- `InterceptorsNamespaces` is passed as a parse-option feature: `WithFeatures([new("InterceptorsNamespaces", "Yaf.Generated")])`.
- A custom `AnalyzerConfigOptionsProvider` supplies `build_property.YafServiceCode`.
- `LanguageVersion.Latest` in Roslyn 5.0.0 is C# 14. Test snippets use no C# 15 syntax. They reference the built `YAF.Core` assembly, whose C# 15 attributes are only read as metadata.
- No `Microsoft.CodeAnalysis.*.Testing` package is used. That keeps the test dependencies to what ADR-0018 and ADR-0003 name.

## 8. `Task` and `ValueTask` overloads of the combinators

`03` §1 asks for `Task` and `ValueTask` overloads of each combinator. Probe O1: two instance methods `MapAsync<TOut>(Func<T, Task<TOut>>)` and `MapAsync<TOut>(Func<T, ValueTask<TOut>>)`:

- `r.MapAsync(async x => { await Task.Yield(); return x + 1; })` → `error CS0121: The call is ambiguous`. That is the common call shape.
- With `[System.Runtime.CompilerServices.OverloadResolutionPriority(1)]` on the `Task` overload, the async lambda picked `Task`. A lambda returning `ValueTask.FromResult(…)` and a method group returning `ValueTask<int>` picked `ValueTask`, and one returning `Task.FromResult(…)` picked `Task`. Output `[Task][VT][Task][VT]`.

The attribute is in the BCL since .NET 9 (C# 13 feature), so no dependency.

## 9. `Microsoft.Extensions.Compliance.Abstractions`

Source: API reference "DataClassificationAttribute Class", learn.microsoft.com/dotnet/api/microsoft.extensions.compliance.classification.dataclassificationattribute (package v11.0.0, updated 2026-11-11):

- Namespace `Microsoft.Extensions.Compliance.Classification`. A non-sealed `public class DataClassificationAttribute : Attribute`, constructor `DataClassificationAttribute(DataClassification)`, properties `Classification` and `Notes`.
- `AttributeUsage`: Class, Field, GenericParameter, Interface, Parameter, Property, ReturnValue, Struct; `AllowMultiple = true`.
- The package `Microsoft.Extensions.Compliance.Testing` has its own `PublicDataAttribute` (and `PrivateDataAttribute`). YAF's `[PublicData]` has the same simple name; the two only meet if an app references the testing package (risk table).

Nuspec of 11.0.1 (MIT): on `net11.0` it depends on `Microsoft.Extensions.ObjectPool` 11.0.0 only. `YAF.Core`'s assembly does not reference ObjectPool unless it uses it. The package graph gets it transitively (decision log).

The constructor of `DataClassification` (taxonomy name and value) is taken from the same reference by T10's implementer for the version in `Directory.Packages.props`.

## 10. Packages and versions

Latest stable on nuget.org, queried 2026-11-24 (flat-container index). The implementer takes the current stable at implementation unless noted.

| Package | Version | Licence | Use | Chosen by |
|---|---|---|---|---|
| `Microsoft.Extensions.Compliance.Abstractions` | 11.0.1 | MIT | `YAF.Core` | `02` §2, ADR-0003 allow-list |
| `Microsoft.CodeAnalysis.CSharp` | **5.0.0, pinned** (5.12.0 is latest) | MIT | `Analyzers` (private assets), `tests/Analyzers.Tests` | ADR-0003 allow-list; version from §3 |
| `AwesomeAssertions` | 9.6.0 | Apache-2.0 | test projects | ADR-0018 |
| `TngTech.ArchUnitNET.xUnitV3` | 0.13.5 (brings `TngTech.ArchUnitNET` 0.13.5: Mono.Cecil, Newtonsoft.Json, CycleDetection, JetBrains.Annotations) | Apache-2.0 | `tests/Architecture.Tests` | ADR-0018 |
| `BenchmarkDotNet` | 0.15.8 (brings `Microsoft.CodeAnalysis.CSharp` 4.14.0, raised to the pinned 5.0.0 by transitive pinning, plus TraceEvent, ClrMD, Iced, Perfolizer) | MIT | `tests/Benchmarks` | the owner (plan Q2); recorded in ADR-0030 by T23 |
| `xunit.v3`, `Microsoft.CodeAnalysis.PublicApiAnalyzers`, `Microsoft.CodeAnalysis.BannedApiAnalyzers` | 4.0.3, 5.6.0, 5.6.0 | | already in `Directory.Packages.props` | plan 0005 |

BenchmarkDotNet console arguments, from "Console arguments", benchmarkdotnet.org/articles/guides/console-args.html: `--filter` (glob on the full name), `--job` (`Dry`, `Short`, …), `--memory` (MemoryDiagnoser), run through `BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args)`.

An `internal sealed` `DiagnosticAnalyzer` loads (probe A4, on review question 2): a consumer referencing the analyzer project got its `warning YAF9999: Method 'Bad' is bad`. So the analyzer classes stay `internal`, and `src/Analyzers` needs no `PublicAPI.*.txt` and no delta entry.

Not verified, left to the ticket that uses it, which records what it found on its pull request: the ArchUnitNET rule API for assembly dependencies (T22, from the project's documentation for 0.13.5).
