using System.Text.Json;
using System.Text.Json.Serialization;
using Domain.Tests.Fixtures;
using YAF.Domain;

namespace Domain.Tests.Enumerations;

public sealed class EnumerationTests
{
    #region enumeration-json
    private static readonly JsonSerializerOptions s_options = new() { Converters = { new EnumerationJsonConverterFactory() } };
    #endregion

    [Fact]
    public void All_ListsFieldsInDeclarationOrder()
    {
        Channel.All.Should().Equal(Channel.Web, Channel.Phone, Channel.Store);
    }

    [Fact]
    public void FromCode_KnownCode_ReturnsTheInstance()
    {
        var result = Channel.FromCode("phone");

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(Channel.Phone);
        result.Value.RequiresAddress.Should().BeTrue();
    }

    [Fact]
    public void FromCode_UnknownCode_FailsWithUnknownEnumerationCode()
    {
        var result = Channel.FromCode("Phone");

        result.Errors.Select(e => e.Code).Should().Equal(DomainErrors.UnknownEnumerationCode.Code);
        DomainErrors.UnknownEnumerationCode.Code.Should().Be("core.enumeration.unknown_code");
        result.Errors[0].Arguments.Should().Contain("Code", "Phone").And.Contain("Enumeration", nameof(Channel));
    }

    [Fact]
    public void Json_RegisteredFactory_RoundTripsByCode()
    {
        var json = JsonSerializer.Serialize(new Purchase(Channel.Store), s_options);
        var read = JsonSerializer.Deserialize<Purchase>(json, s_options);

        json.Should().Be("""{"Channel":"store"}""");
        read!.Channel.Should().BeSameAs(Channel.Store);
    }

    [Fact]
    public void Json_AttributeOnType_WritesCode()
    {
        var json = JsonSerializer.Serialize(Priority.High);
        var read = JsonSerializer.Deserialize<Priority>("\"low\"");

        json.Should().Be("\"high\"");
        read.Should().BeSameAs(Priority.Low);
    }

    [Fact]
    public void Json_UnknownCode_ThrowsJsonException()
    {
        var read = () => JsonSerializer.Deserialize<Channel>("\"fax-secret\"", s_options);

        read.Should().Throw<JsonException>().Which.Message.Should().NotContain("fax-secret");
    }

    private sealed record Purchase(Channel Channel);

    [JsonConverter(typeof(EnumerationJsonConverterFactory))]
    private sealed record Priority : Enumeration<Priority>
    {
        public static readonly Priority Low = new("low", "Low");
        public static readonly Priority High = new("high", "High");

        private Priority(string code, string name)
            : base(code, name)
        {
        }
    }
}
