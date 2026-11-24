using System.Reflection;

namespace Core.Tests;

public sealed class CoreAssemblyTests
{
    [Fact]
    public void Metadata_AotAndTrimming_AreDeclared()
    {
        var assembly = Assembly.Load("YAF.Core");

        var metadata = assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .ToDictionary(attribute => attribute.Key, attribute => attribute.Value, StringComparer.Ordinal);

        metadata.Should().Contain("IsTrimmable", "True");
        metadata.Should().Contain("IsAotCompatible", "True");
    }
}
