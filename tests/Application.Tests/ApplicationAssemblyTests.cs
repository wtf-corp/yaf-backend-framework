using System.Reflection;

namespace Application.Tests;

public sealed class ApplicationAssemblyTests
{
    [Fact]
    public void Metadata_AotAndTrimming_AreNotDeclared()
    {
        var assembly = Assembly.Load("YAF.Application");

        var keys = assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .Select(attribute => attribute.Key);

        keys.Should().NotContain(["IsTrimmable", "IsAotCompatible"]);
    }
}
