using System.ComponentModel;
using System.Globalization;
using System.Text.Json;
using YAF.Core.Ids;

namespace Core.Tests.Ids;

public sealed class FrameworkIdTests
{
    private static readonly Guid s_value = new("0193a5b2-7c4d-7e8f-9a0b-1c2d3e4f5a6b");

    [Fact]
    public void TenantId_New_IsVersion7()
    {
        var id = TenantId.New();

        id.Value.Version.Should().Be(7);
    }

    [Fact]
    public void UserId_Parse_RoundTrips()
    {
        var id = UserId.From(s_value);

        var parsed = UserId.Parse(id.ToString(), CultureInfo.InvariantCulture);

        parsed.Should().Be(id);
        id.ToString().Should().Be(s_value.ToString("D"));
    }

    [Fact]
    public void DataSubjectId_TryParse_NotAGuid_ReturnsFalse()
    {
        var parsed = DataSubjectId.TryParse("not a guid", CultureInfo.InvariantCulture, out _);

        parsed.Should().BeFalse();
    }

    [Fact]
    public void CorrelationId_Json_RoundTripKeepsValue()
    {
        var id = CorrelationId.From(s_value);

        var json = JsonSerializer.Serialize(id);
        var read = JsonSerializer.Deserialize<CorrelationId>(json);

        json.Should().Be($"\"{s_value:D}\"");
        read.Should().Be(id);
    }

    [Fact]
    public void TenantId_TypeConverter_FromString_ReturnsId()
    {
        var converter = TypeDescriptor.GetConverter(typeof(TenantId));

        var id = converter.ConvertFrom(null, CultureInfo.InvariantCulture, s_value.ToString("D"));

        id.Should().Be(TenantId.From(s_value));
    }

    [Fact]
    public void FrameworkIds_FromThroughStaticAbstract_ReturnSameValue()
    {
        Create<TenantId>(s_value).Value.Should().Be(s_value);
        Create<UserId>(s_value).Value.Should().Be(s_value);
        Create<DataSubjectId>(s_value).Value.Should().Be(s_value);
        Create<CorrelationId>(s_value).Value.Should().Be(s_value);
    }

    private static TId Create<TId>(Guid value)
        where TId : struct, ITypedId<TId> => TId.From(value);
}
