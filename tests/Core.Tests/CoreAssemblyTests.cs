using System.Reflection;
using YAF.Core.Results;

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

    [Fact]
    public void Assembly_CarriesYafServiceCore()
    {
        var assembly = Assembly.Load("YAF.Core");

        var service = assembly.GetCustomAttribute<YafServiceAttribute>();

        service.Should().NotBeNull();
        service!.ServiceCode.Should().Be("core");
        service.ErrorRegistry.FullName.Should().Be("YAF.Generated.YafErrorRegistry");
    }
}
