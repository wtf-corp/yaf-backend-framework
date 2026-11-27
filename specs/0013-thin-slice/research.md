# 0013 — Thin slice (M3): research

What the plan relies on and how it was checked. Probes were built on 2026-11-26 in a scratch folder outside the repository, against `origin/main` at bc5f4e0, and are not committed. Package metadata was read from the nuget.org flat container (`https://api.nuget.org/v3-flatcontainer/<id>/index.json` and `…/<id>.nuspec`) on 2026-11-26.

## 1. Environment

- .NET SDK 11.0.100 (`dotnet --version`), `global.json` as on `main`. The shared framework `Microsoft.NETCore.App` 11.0.0 is installed.
- Probe A: a library `YAF.Application` with the analysis settings of the root `Directory.Build.props` and `src/Directory.Build.props` (`AnalysisLevel` `latest`, `AnalysisMode` `All`, `EnforceCodeStyleInBuild`, the banned API analyzer, the repository's `.editorconfig` without its file-scoped sections), referencing `src/Domain/Domain.csproj` and `src/Analyzers/Analyzers.csproj` of the repository and importing `src/Core/buildTransitive/YAF.Core.props`. Warnings were not errors, so that one build lists every finding. CS1591 and CA1822 were off (stub bodies, no XML docs). The public API analyzer was left out.
- Probe B: a library `YAF.Persistence.EfCore` with the same settings over probe A, with `Microsoft.EntityFrameworkCore.Relational` and `Npgsql.EntityFrameworkCore.PostgreSQL` 11.0.0.
- Probe C: a console app over `src/Domain` and Npgsql's EF Core provider 11.0.0 that builds an EF model and prints `Database.GenerateCreateScript()`. It needs no database server.
- No probe was built for `YAF.Http` and `YAF.Http.MinimalApis` (risk in the plan).

## 2. Packages: versions, licences, dependencies

Latest stable versions on nuget.org on 2026-11-26:

| Package | Version | Licence (nuspec) | Dependencies for `net11.0` (or the nearest target) | Used by |
|---|---|---|---|---|
| `Microsoft.EntityFrameworkCore.Relational` | 11.0.0 | MIT | `Microsoft.EntityFrameworkCore` [11.0.0, 12), `Microsoft.Extensions.Caching.Memory` 11.0.0, `Microsoft.Extensions.Logging` 11.0.0 | `YAF.Persistence.EfCore` |
| `Microsoft.EntityFrameworkCore` | 11.0.0 | MIT | `…Abstractions`, `…Analyzers` [11.0.0, 12), `Microsoft.Extensions.Caching.Memory`, `Microsoft.Extensions.Logging` 11.0.0 | transitively |
| `Npgsql.EntityFrameworkCore.PostgreSQL` | 11.0.0 | PostgreSQL | `Microsoft.EntityFrameworkCore` and `.Relational` [11.0.0, 12), `Npgsql` 11.0.0 | `YAF.Persistence.EfCore.PostgreSql` |
| `Npgsql` | 11.0.0 | PostgreSQL | (`net10.0`) `Microsoft.Extensions.DependencyInjection.Abstractions`, `Microsoft.Extensions.Logging.Abstractions` 11.0.0 | transitively |
| `Microsoft.EntityFrameworkCore.Design` | 11.0.0 | MIT | — | the sample's infrastructure project, private assets, for `dotnet ef` |
| `dotnet-ef` (tool) | 11.0.0 | MIT | — | `.config/dotnet-tools.json` |
| `Testcontainers.PostgreSql` | 4.17.0 | MIT | `Testcontainers` 4.17.0 | `YAF.Testing.Integration` |
| `Testcontainers` | 4.17.0 | MIT | (`net10.0`) `Docker.DotNet.Enhanced` 4.3.3, `Docker.DotNet.Enhanced.X509` 4.3.3, `Microsoft.Extensions.Logging.Abstractions` 8.0.3, `SSH.NET` 2026.0.0, `SharpZipLib` 1.4.2 | transitively |
| `Microsoft.AspNetCore.Mvc.Testing` | 11.0.0 | MIT | `Microsoft.AspNetCore.TestHost`, `Microsoft.Extensions.DependencyModel`, `Microsoft.Extensions.Hosting` 11.0.0 | `YAF.Testing.Integration` |
| `xunit.v3.extensibility.core` | 4.0.3 | Apache-2.0 | `xunit.v3.common` [4.0.3] | `YAF.Testing.Integration` (fixtures, collections); the same version line as `xunit.v3` 4.0.3 on `main` |
| `TngTech.ArchUnitNET` | 0.13.5 | Apache-2.0 | (`netstandard2.0`) `CycleDetection` 2.0.0, `JetBrains.Annotations` 2026.2.0, `Mono.Cecil` 0.11.6, `Newtonsoft.Json` 13.0.4, `System.ValueTuple` 4.6.2 | `YAF.Testing` (Q3, decided by the owner on 2026-11-26; ADR-0033) |

All are permissively licensed (ADR-0003, licence policy). `TngTech.ArchUnitNET.xUnitV3` 0.13.5 is already pinned for `tests/Architecture.Tests`; the core package above is what a library that must not reference a test framework would use.

ADR-0003 allows EF Core and Npgsql in `YAF.Persistence.EfCore(.PostgreSql)`, "Test framework, Testcontainers, ASP.NET Core test host" in `YAF.Testing.Integration`, and "nothing beyond `YAF.Application`'s row" in `YAF.Testing`, which is why ArchUnitNET needs ADR-0033 (Q3, T69).

## 3. The `Microsoft.Extensions` abstractions are part of `Microsoft.NETCore.App` 11

Probe A first referenced `Microsoft.Extensions.DependencyInjection.Abstractions`, `.Logging.Abstractions`, `.Hosting.Abstractions`, `.Options` and `.Diagnostics.Abstractions` 11.0.0. Each reference gave NuGet warning NU1510 ("will not be pruned. This package is automatically available and does not need to be referenced explicitly"), which `TreatWarningsAsErrors` would make an error. With the five references removed the probe still built and used `IServiceCollection`, `ILogger` and `IOptions<T>`; its `project.assets.json` lists no `Microsoft.Extensions.*` package except `Compliance.Abstractions` (from `YAF.Core`) and `ObjectPool`. `C:/Program Files/dotnet/shared/Microsoft.NETCore.App/11.0.0/` contains `Microsoft.Extensions.Caching.Abstractions`, `.Configuration.Abstractions`, `.DependencyInjection.Abstractions`, `.Diagnostics.Abstractions`, `.FileProviders.Abstractions`, `.Hosting.Abstractions`, `.Logging.Abstractions`, `.Options` and `.Primitives`.

Consequence: `YAF.Application` takes **no package reference**; DI, logging, options, hosting and `IMeterFactory` come from the platform. Its architecture rule allows those assembly names.

## 4. Analyzer rules that the declarations of `04` set off

Probe A, a skeleton of every public type of `YAF.Application` in the plan's delta. Findings on declarations that `04` prescribes:

| Rule | Where | Why it fires | `04` |
|---|---|---|---|
| CA1040 | `IRequest<TResult>`, `ICommand<TResult>`, `IQuery<TResult>`, `IStreamQuery<TItem>` | Interfaces without members | §2 declares them as markers |
| CA1711 | `RequestDelegate<TResult>` | Type name ends in `Delegate` | §3 |
| CA1711 | `IDomainEventHandler<TEvent>` | Type name ends in `EventHandler` | §2 |
| CA1716 | parameter `next` of `IRequestMiddleware<TRequest, TResult>.InvokeAsync` | `Next` is a Visual Basic keyword, on an interface member | §3 |
| CA1724 | `Principal` | The name equals the last part of the namespace `System.Security.Principal` | §7 |

Findings on things the plan chose, each avoided in code:

- CA1008 on enums starting at 1 (`PipelineScope`, `PrincipalKind`): both start at 0 with a meaningful first member (`All`, `Anonymous`).
- CA1716 on a parameter named `set` of `AggregateRepository<…>.WithChildren(IQueryable<TData> set)` (`05` §3 writes `set`): the parameter is named `source`.
- CA1062 on public middleware that use `next` without a null check: checked in code.
- YAF2009 on a public constructor parameter `Guid Id` (a first draft of `PrincipalRef`): `PrincipalRef` takes a `UserId`.
- Nested record cases `Principal.User`, `Principal.System` were not tried: a nested type named `System` hides the namespace; `Principal` is one sealed class with a `Kind` and static factories, which is how `04` §7 writes `Principal.System("…")`.

Not reported: CA1005 on `AggregateRepository<TAggregate, TId, TData, TMemento>` (four type parameters), CA1000 on the static factories of the non-generic `Principal`, any rule on `TypedIdConverter<TId>` and `EnumerationConverter<T>`.

`.editorconfig` section names with braces and commas need escaping as in plan 0009 (research §3 there): `[src/Application/Pipeline/IRequestMiddleware\{TRequest\,TResult\}.cs]`.

## 5. EF Core value converters cannot name a static abstract member

Probe B. `ValueConverter<TId, Guid>(id => id.Value, value => TId.From(value))` does not compile: CS8927, "An expression tree may not contain an access of static virtual or abstract interface member". A converter's lambdas are expression trees. Calling a private static helper of the converter, `value => Create(value)` with `private static TId Create(Guid value) => TId.From(value);`, compiles, and probe C used it to map a typed id and a nullable typed id to `uuid` columns. `TypedIdConverter<TId>` keeps the shape of `05` §1 (generic over `ITypedId<TSelf>`) with that helper.

## 6. EF Core complex types for the shipped value objects

Probe C, `ComplexProperty` on a data class with a `Money Price` and a `Period? Schedule` member, `Currency` and `EmailAddress` mapped with value converters in `ConfigureConventions`:

- With `e.ComplexProperty(x => x.Price)` alone, model building fails: "No suitable constructor was found for the type 'SessionData.Price#Money (Money)' … Cannot bind 'Amount', 'Currency'". `Money`'s properties are get-only, so EF does not map them by convention, and an unmapped property cannot be bound to a constructor parameter.
- With each component named, `ComplexProperty(x => x.Price, p => { p.Property(m => m.Amount); p.Property(m => m.Currency); })`, the model builds and the script has `"Price_Amount" numeric NOT NULL`, `"Price_Currency" character varying(3) NOT NULL` (the converter applies inside the complex type).
- `Period?` with its **private** constructor `Period(DateTimeOffset start, DateTimeOffset end)` maps the same way to `"Schedule_Start"`, `"Schedule_End"`, both nullable. Optional complex types exist since EF Core 10 and "require at least one required property" (EF Core 10 "What's new", Complex types); `Period.Start` is one.
- Materialising rows through these constructors was not run (no database in the probe). T51 proves it on PostgreSQL.

So the convention of T38 maps each member of type `Money`, `Period` or `DateRange` (and their nullable forms) as a complex property **with every component named**, in `ApplyYafModel`, and converts `Currency`, `EmailAddress` and `Percentage` in `ConfigureConventions`. Sources: [EF Core 10 What's new](https://learn.microsoft.com/en-us/ef/core/what-is-new/ef-core-10.0/whatsnew) (optional and struct complex types; named query filters), [EF Core 11 What's new](https://learn.microsoft.com/en-us/ef/core/what-is-new/ef-core-11.0/whatsnew) (complex-type stabilisation, property configuration through member chains).

## 7. Child collections of a data class

EF maps navigations to entity types; an interface such as `Seat.IMemento` is not an entity type. Probe C declared the child collection twice: a mapped `public List<SeatData> SeatRows { get; } = [];` and an explicit implementation `IList<ISeatMemento> ISessionMemento.Seats` that returns a view over `SeatRows` (adds and removes pass through, an item of another type is an `InvalidCastException`). The model built a `SeatData` table with a foreign key to the root, and adding through the interface view added to `SeatRows`. The name `SeatRows` is the one the `yaf-persistence` skill uses (`set.Include(x => x.SeatRows)`). The generator of T34 emits this shape.

## 8. PostgreSQL read-only transactions

[PostgreSQL 19 documentation, SET TRANSACTION](https://www.postgresql.org/docs/current/sql-set-transaction.html): in a read-only transaction `INSERT`, `UPDATE`, `DELETE`, `MERGE`, `COPY FROM` to a non-temporary table and all DDL are refused; the access mode can be set with `SET TRANSACTION READ ONLY` inside an open transaction or with `BEGIN TRANSACTION READ ONLY`. `SET TRANSACTION` outside a transaction only warns. The query transaction of T48 issues `SET TRANSACTION READ ONLY` as the first statement of the transaction it begins; T52 proves that an insert in it fails. PostgreSQL 19 is the current release on 2026-11-26; T41 pins the container image to it after checking the tag.

## 9. Test host and containers

- [Integration tests in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests) (view `aspnetcore-11.0`): `WebApplicationFactory<TEntryPoint>` creates a `TestServer`; override `ConfigureWebHost` and use `ConfigureServices` / `ConfigureTestServices`; the test's service configuration runs after the app's `Program.cs`. `Microsoft.AspNetCore.Mvc.Testing` copies the app's `.deps` file and sets the content root, which it finds through `WebApplicationFactoryContentRootAttribute` or, failing that, by **searching for a `*.sln` file**. The repository and the sample use `.slnx`. Not verified whether the search finds `.slnx`; T72 sets the content root explicitly (`UseContentRoot` on the builder, or the attribute) and its first test fails if it is wrong.
- [Testcontainers for .NET, PostgreSQL module](https://dotnet.testcontainers.org/modules/postgres/): `new PostgreSqlBuilder("postgres:15.1").Build()`, `await container.StartAsync()`, `container.GetConnectionString()`. The image is a constructor argument.
- [`PostgreSqlContainer.cs` at tag 4.17.0](https://github.com/testcontainers/testcontainers-dotnet/blob/4.17.0/src/Testcontainers.PostgreSql/PostgreSqlContainer.cs): `public Task<ExecResult> ExecScriptAsync(string scriptContent, CancellationToken ct = default)` (and an overload with an `Encoding`) copies the script into the container under `/tmp` and runs `psql --username <configured user> --dbname <configured database> --file <path>` through `ExecAsync`. `psql --file` without `--single-transaction` runs each statement in its own transaction, which `CREATE DATABASE` requires. T41's `CreateDatabaseAsync` uses it, so the fixture issues `CREATE DATABASE` with no Npgsql reference of its own (ADR-0003, row `YAF.Testing.Integration`).
- `WebApplicationFactory<TEntryPoint>` starts a minimal-hosting app through the entry point of `TEntryPoint`'s assembly. An xUnit v3 test project is itself an executable whose entry point xUnit generates, so a test app declared inside the test project would start xUnit's entry point, not the app's. T72's test app is therefore a project of its own under `tests/Fixtures/` (reviewer question, round 5; T72's first test fails if it does not start).
- EF Core's `SaveChangesInterceptor.SavingChangesAsync` can return `InterceptionResult<int>.SuppressWithResult(0)`, which skips the database command ([Interceptors, SaveChanges interception](https://learn.microsoft.com/en-us/ef/core/logging-events-diagnostics/interceptors#savechanges-interception)). The unit tests of the stamping, tenant and versioning interceptors register such an interceptor last and read the change tracker; nothing else in `tests/Persistence.EfCore.Tests` touches a store.
- A library that offers xUnit v3 fixtures (`IAsyncLifetime`, collection fixtures) references `xunit.v3.extensibility.core`, not the `xunit.v3` package that test projects use (§2).

## 10. What was not checked, and where it is checked

| Assumption | Checked in |
|---|---|
| EF materialises `Money`, `Period` and `EmailAddress` through their constructors and converters | T51 (PostgreSQL round trip) |
| A concurrency token on the root's `Version` turns a stale update into `DbUpdateConcurrencyException`, also when only a child row changed and the repository marks the root modified | T45 (unit, through the change tracker), T52 (PostgreSQL) |
| `Database.CreateExecutionStrategy()` with Npgsql's retrying strategy re-runs the delegate and the unit of work can clear the change tracker between attempts | T47, T52 |
| Analyzer findings in `YAF.Http` and `YAF.Http.MinimalApis` | T53–T58; a new exception is a plan update and the owner's `guard-change` |
| Typed results (`Results<Ok<T>, ProblemHttpResult>`, `Created<T>`, `Accepted<T>`, `NoContent`) in `Microsoft.AspNetCore.Http.HttpResults` for .NET 11 | T58, against the [typed results documentation](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis/responses) |

## 11. A file-local error registry named by an assembly attribute (T94, #337)

Checked on 2026-11-27 with SDK 11.0.100, compiler 5.12.0-1.26529.118 (C# `latest`, the default for `net11.0`), `TreatWarningsAsErrors` on. The analyzers compile against `Microsoft.CodeAnalysis.CSharp` 5.0.0 (`Directory.Packages.props`), which knows file-local types (C# 11).

- [The `file` keyword](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/file) (C# reference, updated 2026-09-22): a file-local type is visible only in the file that declares it; "Source generators most often apply the `file` modifier to types they generate"; other types may use the same name without a collision. It cannot be the return or parameter type of a member, or the type of a field, of a type that is not file-local. The page says nothing about attribute arguments, so the probe below decided it.
- Probe: two projects, `A` (with `InternalsVisibleTo B`) and `B` (referencing `A`), each with the file the generator writes: `[assembly: Svc("<code>", typeof(global::YAF.Generated.YafErrorRegistry))]` above `namespace YAF.Generated { file sealed class YafErrorRegistry : IReg { … } }`.
  - With `internal sealed class` in both: `B` fails with **CS0436** ("The type 'YafErrorRegistry' … conflicts with the imported type 'YafErrorRegistry' in 'A'"), the error of #337.
  - With `file sealed class` in both: both build with no warning. `typeof` of the file-local type in an assembly-level attribute of the same file is accepted. At run time each assembly's attribute returns its own type, `Activator.CreateInstance` creates it, and `Assembly.GetType("YAF.Generated.YafErrorRegistry")` returns `null`. The metadata names were `YAF.Generated.<YAF_ErrorRegistry_g>F<64 hex digits>__YafErrorRegistry`, with a different hash per assembly; `IsPublic` was false.
- Not checked: trimming and native AOT with the file-local registry. `YafServiceAttribute.ErrorRegistry` keeps its `DynamicallyAccessedMembers(PublicParameterlessConstructor)` annotation, which does not depend on the type's name or accessibility.
