using System.Reflection;
using YAF.Core.Ids;

namespace Core.Tests.Ids;

public sealed class TypedIdContractTests
{
    private static readonly Guid s_value = new("0193a5b2-7c4d-7e8f-9a0b-1c2d3e4f5a6b");

    [Fact]
    public void From_ThroughStaticAbstract_ReturnsSameValue()
    {
        var id = Create<SampleId>(s_value);

        id.Value.Should().Be(s_value);
    }

    [Fact]
    public void Value_ThroughNonGenericContract_ReturnsSameValue()
    {
        var value = ReadValue(SampleId.From(s_value));

        value.Should().Be(s_value);
    }

    [Fact]
    public void TypedIdAttribute_Usage_AllowsStructsOnly()
    {
        var usage = typeof(TypedIdAttribute).GetCustomAttribute<AttributeUsageAttribute>();

        usage.Should().NotBeNull();
        usage!.ValidOn.Should().Be(AttributeTargets.Struct);
        usage.AllowMultiple.Should().BeFalse();
        usage.Inherited.Should().BeFalse();
    }

    private static TId Create<TId>(Guid value)
        where TId : struct, ITypedId<TId> => TId.From(value);

    private static Guid ReadValue<TId>(TId id)
        where TId : ITypedId => id.Value;
}
