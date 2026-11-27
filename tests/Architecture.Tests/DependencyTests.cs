using System.Text.Json;
using ArchUnitNET.Loader;
using ArchUnitNET.xUnitV3;
using static ArchUnitNET.Fluent.ArchRuleDefinition;
using ReflectionAssembly = System.Reflection.Assembly;

namespace Architecture.Tests;

// Assembly references allowed by ADR-0003 ("Allow-list for core packages", "Enforcement"), 01 §6 item 2, 02 §12 item 1 and 15 §8 item 3.
public sealed class DependencyTests
{
    private static readonly ReflectionAssembly s_core = ReflectionAssembly.Load("YAF.Core");

    private static readonly ReflectionAssembly s_domain = ReflectionAssembly.Load("YAF.Domain");

    private static readonly ReflectionAssembly s_testing = ReflectionAssembly.Load("YAF.Testing");

    // The packages of the thin slice (specs/0013-thin-slice/plan.md#T60).
    private static readonly string[] s_thinSlice =
    [
        "YAF.Application", "YAF.Infrastructure", "YAF.Persistence.EfCore", "YAF.Persistence.EfCore.PostgreSql", "YAF.Http",
        "YAF.Http.MinimalApis", "YAF.Testing.Integration",
    ];

    private static readonly ArchUnitNET.Domain.Architecture s_architecture = new ArchLoader()
        .LoadAssemblies([s_core, ReflectionAssembly.Load("YAF.Analyzers"), s_domain, s_testing, .. s_thinSlice.Select(ReflectionAssembly.Load)])
        .Build();

    public static TheoryData<string> ThinSlicePackages => new(s_thinSlice);

    [Fact]
    public void Core_ReferencesOnlyAllowList()
    {
        var references = ReferencedAssemblyNames("YAF.Core");

        references.Should().NotBeEmpty().And.OnlyContain(name => IsAllowedForCore(name));
    }

    [Fact]
    public void Analyzers_ReferencesOnlyRoslyn()
    {
        var references = ReferencedAssemblyNames("YAF.Analyzers");

        references.Should().NotBeEmpty().And.OnlyContain(name => IsAllowedForAnalyzers(name));
    }

    [Fact]
    public void Core_HasNoPublicTypeInInternalNamespace()
    {
        var rule = Types().That().ResideInAssembly(s_core).And().ArePublic()
            .Should().NotResideInNamespaceMatching(@"(^|\.)Internal(\.|$)");

        rule.Check(s_architecture);
    }

    [Fact]
    public void Domain_ReferencesOnlyCore()
    {
        var references = ReferencedAssemblyNames("YAF.Domain");

        references.Should().Contain("YAF.Core").And.OnlyContain(name => IsAllowedForDomain(name));
    }

    [Fact]
    public void Testing_ReferencesOnlyAllowList()
    {
        var references = ReferencedAssemblyNames("YAF.Testing");

        references.Should().Contain("YAF.Domain").And.OnlyContain(name => IsAllowedForTesting(name));
    }

    [Fact]
    public void Domain_HasNoPublicTypeInInternalNamespace()
    {
        var rule = Types().That().ResideInAssembly(s_domain).And().ArePublic()
            .Should().NotResideInNamespaceMatching(@"(^|\.)Internal(\.|$)");

        rule.Check(s_architecture);
    }

    [Fact]
    public void Testing_HasNoPublicTypeInInternalNamespace()
    {
        var rule = Types().That().ResideInAssembly(s_testing).And().ArePublic()
            .Should().NotResideInNamespaceMatching(@"(^|\.)Internal(\.|$)");

        rule.Check(s_architecture);
    }

    [Fact]
    public void Application_ReferencesOnlyDomainCoreAndPlatform()
    {
        var references = ReferencedAssemblyNames("YAF.Application");

        references.Should().Contain("YAF.Domain").And.OnlyContain(name => IsAllowedForApplication(name));
    }

    [Fact]
    public void Infrastructure_ReferencesOnlyApplicationAndPlatform()
    {
        var references = ReferencedAssemblyNames("YAF.Infrastructure");

        references.Should().NotBeEmpty().And.OnlyContain(name => IsAllowedForInfrastructure(name));
    }

    // Read from the declared dependencies: the compiled assembly names YAF.Infrastructure only once it uses one of its types.
    [Fact]
    public void PersistenceEfCore_ReferencesInfrastructure()
    {
        var dependencies = DeclaredDependencies("YAF.Persistence.EfCore");

        dependencies.Should().Contain("YAF.Infrastructure");
    }

    [Fact]
    public void PersistenceEfCore_ReferencesOnlyAllowList()
    {
        var references = ReferencedAssemblyNames("YAF.Persistence.EfCore");

        references.Should().NotBeEmpty().And.OnlyContain(name => IsAllowedForPersistenceEfCore(name));
    }

    [Fact]
    public void PersistencePostgreSql_ReferencesOnlyAllowList()
    {
        var references = ReferencedAssemblyNames("YAF.Persistence.EfCore.PostgreSql");

        references.Should().NotBeEmpty().And.OnlyContain(name => IsAllowedForPersistencePostgreSql(name));
    }

    [Fact]
    public void Http_ReferencesNeitherSanitizerNorVersioning()
    {
        var references = ReferencedAssemblyNames("YAF.Http");

        references.Should().NotBeEmpty().And.OnlyContain(name => IsAllowedForHttp(name));
    }

    [Fact]
    public void HttpMinimalApis_ReferencesOnlyAllowList()
    {
        var references = ReferencedAssemblyNames("YAF.Http.MinimalApis");

        references.Should().NotBeEmpty().And.OnlyContain(name => IsAllowedForHttpMinimalApis(name));
    }

    [Fact]
    public void TestingIntegration_ReferencesOnlyAllowList()
    {
        var references = ReferencedAssemblyNames("YAF.Testing.Integration");

        references.Should().NotBeEmpty().And.OnlyContain(name => IsAllowedForTestingIntegration(name));
    }

    [Fact]
    public void Testing_AllowsApplication()
    {
        var allowed = IsAllowedForTesting("YAF.Application");

        allowed.Should().BeTrue();
    }

    [Theory]
    [MemberData(nameof(ThinSlicePackages))]
    public void ThinSlicePackage_HasNoPublicTypeInInternalNamespace(string assemblyName)
    {
        var rule = Types().That().ResideInAssembly(ReflectionAssembly.Load(assemblyName)).And().ArePublic()
            .Should().NotResideInNamespaceMatching(@"(^|\.)Internal(\.|$)").WithoutRequiringPositiveResults();

        rule.Check(s_architecture);
    }

    private static List<string> ReferencedAssemblyNames(string assemblyName) =>
        s_architecture.Assemblies.Single(assembly => assembly.Name == assemblyName).ReferencedAssemblyNames;

    // The project and package dependencies the build declared for a library, from this test application's deps.json.
    private static List<string> DeclaredDependencies(string library)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Architecture.Tests.deps.json")));
        var target = document.RootElement.GetProperty("targets").EnumerateObject().Single().Value;
        var entry = target.EnumerateObject().Single(property => property.Name.StartsWith(library + "/", StringComparison.Ordinal)).Value;

        return [.. entry.GetProperty("dependencies").EnumerateObject().Select(dependency => dependency.Name)];
    }

    // The .NET libraries and the one third-party package of ADR-0003's row for YAF.Core.
    private static bool IsAllowedForCore(string name) =>
        name.StartsWith("System.", StringComparison.Ordinal)
        || name == "Microsoft.Extensions.Compliance.Abstractions";

    // ADR-0003's row for YAF.Domain: nothing beyond Core.
    private static bool IsAllowedForDomain(string name) =>
        name.StartsWith("System.", StringComparison.Ordinal) || name == "YAF.Core";

    // ADR-0003's row for YAF.Application: the Microsoft.Extensions abstractions, which come with the platform (specs/0013-thin-slice/research.md §3).
    private static bool IsAllowedForApplication(string name) =>
        IsAllowedForDomain(name) || name == "YAF.Domain" || name.StartsWith("Microsoft.Extensions.", StringComparison.Ordinal);

    // ADR-0003's row for YAF.Infrastructure: YAF.Application's row plus Cronos.
    private static bool IsAllowedForInfrastructure(string name) =>
        IsAllowedForApplication(name) || name is "YAF.Application" or "Cronos";

    // ADR-0003's row for YAF.Persistence.EfCore: YAF.Application's row plus EF Core; Npgsql only in the PostgreSQL package (01 §3).
    private static bool IsAllowedForPersistenceEfCore(string name) =>
        IsAllowedForApplication(name)
        || name is "YAF.Application" or "YAF.Infrastructure" or "Microsoft.EntityFrameworkCore"
        || name.StartsWith("Microsoft.EntityFrameworkCore.", StringComparison.Ordinal);

    // ADR-0003's row for YAF.Persistence.EfCore.PostgreSql: YAF.Persistence.EfCore's plus Npgsql.
    private static bool IsAllowedForPersistencePostgreSql(string name) =>
        IsAllowedForPersistenceEfCore(name) || name is "YAF.Persistence.EfCore" or "Npgsql"
        || name.StartsWith("Npgsql.", StringComparison.Ordinal);

    // ADR-0003's row for YAF.Http: the ASP.NET Core shared framework, no HTML sanitizer and no Asp.Versioning.
    private static bool IsAllowedForHttp(string name) =>
        IsAllowedForApplication(name) || name == "YAF.Application" || name.StartsWith("Microsoft.AspNetCore.", StringComparison.Ordinal);

    private static bool IsAllowedForHttpMinimalApis(string name) => IsAllowedForHttp(name) || name == "YAF.Http";

    // ADR-0003's row for YAF.Testing.Integration (test framework, Testcontainers, ASP.NET Core test host) and its references in 01 §3.
    private static bool IsAllowedForTestingIntegration(string name) =>
        IsAllowedForTesting(name) || IsAllowedForHttp(name) || IsAllowedForPersistencePostgreSql(name)
        || name is "YAF.Testing" or "YAF.Http" or "YAF.Persistence.EfCore.PostgreSql" or "Testcontainers"
        or "Docker.DotNet" // the Docker client of Testcontainers, whose types its API exposes
        || name.StartsWith("Testcontainers.", StringComparison.Ordinal) || name.StartsWith("xunit.", StringComparison.Ordinal);

    // Pure helpers with no test framework, ASP.NET Core, EF Core or Testcontainers (15 §3, §8 item 3); the test clock wraps FakeTimeProvider.
    // YAF.Testing may reference YAF.Application (01 §3, §6 item 2; ADR-0003's row for YAF.Testing).
    private static bool IsAllowedForTesting(string name) =>
        name.StartsWith("System.", StringComparison.Ordinal)
        || name is "YAF.Core" or "YAF.Domain" or "YAF.Application" or "Microsoft.Extensions.TimeProvider.Testing";

    // netstandard (the target), Roslyn, and System.Collections.Immutable, which Roslyn's API exposes (specs/0006-core/plan.md, decision log).
    private static bool IsAllowedForAnalyzers(string name) =>
        name is "netstandard" or "System.Collections.Immutable" or "Microsoft.CodeAnalysis"
        || name.StartsWith("Microsoft.CodeAnalysis.", StringComparison.Ordinal);
}
