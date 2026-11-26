using System.Reflection;

namespace Domain.Tests.Mementos;

// 18 §2 (exit of M2) and 15 §4 rule 9: a domain test project references YAF.Testing, and neither ASP.NET Core, EF Core nor
// YAF.Testing.Integration. The memento round trip is what the test project uses YAF.Testing for.
public sealed class ReferenceTests
{
    private static readonly string[] s_forbiddenPrefixes =
    [
        "Microsoft.AspNetCore", "Microsoft.EntityFrameworkCore", "Npgsql", "YAF.Testing.Integration",
    ];

    [Fact]
    public void DomainTests_ReferenceTestingAndNeitherWebNorOrm()
    {
        var references = typeof(ReferenceTests).Assembly.GetReferencedAssemblies()
            .Select(reference => reference.Name!)
            .ToList();

        references.Should().Contain("YAF.Testing");
        references.Where(name => s_forbiddenPrefixes.Any(prefix => name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
            .Should().BeEmpty();
    }
}
