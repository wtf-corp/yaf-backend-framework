using ArchUnitNET.Loader;
using ArchUnitNET.xUnitV3;
using static ArchUnitNET.Fluent.ArchRuleDefinition;
using ReflectionAssembly = System.Reflection.Assembly;

namespace Architecture.Tests;

// Assembly references allowed by ADR-0003 ("Allow-list for core packages", "Enforcement") and 01 §6 item 2.
public sealed class DependencyTests
{
    private static readonly ReflectionAssembly s_core = ReflectionAssembly.Load("YAF.Core");

    private static readonly ArchUnitNET.Domain.Architecture s_architecture = new ArchLoader()
        .LoadAssemblies(s_core, ReflectionAssembly.Load("YAF.Analyzers"))
        .Build();

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

    private static List<string> ReferencedAssemblyNames(string assemblyName) =>
        s_architecture.Assemblies.Single(assembly => assembly.Name == assemblyName).ReferencedAssemblyNames;

    // The .NET libraries and the one third-party package of ADR-0003's row for YAF.Core.
    private static bool IsAllowedForCore(string name) =>
        name.StartsWith("System.", StringComparison.Ordinal)
        || name == "Microsoft.Extensions.Compliance.Abstractions";

    // netstandard (the target), Roslyn, and System.Collections.Immutable, which Roslyn's API exposes (specs/0006-core/plan.md, decision log).
    private static bool IsAllowedForAnalyzers(string name) =>
        name is "netstandard" or "System.Collections.Immutable" or "Microsoft.CodeAnalysis"
        || name.StartsWith("Microsoft.CodeAnalysis.", StringComparison.Ordinal);
}
