using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using YAF.Core.Results;

namespace Core.Tests.Results;

public sealed class ErrorRegistryContractTests
{
    private static readonly ErrorDescriptor s_full = new("test.session.full", ErrorKind.Conflict, "Session is full.");

    [Fact]
    public void YafService_AppliesToAssembliesOnly()
    {
        var usage = typeof(YafServiceAttribute).GetCustomAttribute<AttributeUsageAttribute>();

        usage.Should().NotBeNull();
        usage!.ValidOn.Should().Be(AttributeTargets.Assembly);
        usage.AllowMultiple.Should().BeFalse();
        usage.Inherited.Should().BeFalse();
    }

    [Fact]
    public void YafService_KeepsCodeAndRegistry()
    {
        var attribute = new YafServiceAttribute("catalog", typeof(SampleRegistry));

        attribute.ServiceCode.Should().Be("catalog");
        attribute.ErrorRegistry.Should().Be<SampleRegistry>();
    }

    [Fact]
    public void YafService_RegistryKeepsItsPublicParameterlessConstructor()
    {
        var parameter = typeof(YafServiceAttribute).GetConstructors().Single().GetParameters()[1];
        var property = typeof(YafServiceAttribute).GetProperty(nameof(YafServiceAttribute.ErrorRegistry))!;

        parameter.GetCustomAttribute<DynamicallyAccessedMembersAttribute>()!.MemberTypes
            .Should().Be(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor);
        property.GetCustomAttribute<DynamicallyAccessedMembersAttribute>()!.MemberTypes
            .Should().Be(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor);
    }

    [Fact]
    public void ErrorRegistration_KeepsDescriptorTypeAndField()
    {
        var registration = new ErrorRegistration(s_full, "Catalog.Domain.Session+Errors", "Full");

        registration.Descriptor.Should().BeSameAs(s_full);
        registration.DeclaringType.Should().Be("Catalog.Domain.Session+Errors");
        registration.Field.Should().Be("Full");
    }

    [Fact]
    public void ErrorRegistry_ListsItsRegistrations()
    {
        IErrorRegistry registry = new SampleRegistry();

        registry.Errors.Should().ContainSingle().Which.Descriptor.Code.Should().Be("test.session.full");
    }

    private sealed class SampleRegistry : IErrorRegistry
    {
        public IReadOnlyList<ErrorRegistration> Errors { get; } = [new(s_full, "Catalog.Domain.Session+Errors", "Full")];
    }
}
