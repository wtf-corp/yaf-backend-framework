using System.Reflection;
using System.Runtime.Loader;
using YAF.Analyzers.TypedIds;
using YAF.Core.Ids;

namespace Analyzers.Tests.TypedIds;

public sealed class TypedIdGeneratorTests
{
    // The id as 02 §3 declares it, and a class that uses the generated members the way a consumer's code would.
    private const string Source = """
        using System;
        using System.ComponentModel;
        using System.Text.Json;
        using YAF.Core.Ids;

        namespace Probe;

        [TypedId]
        public readonly partial record struct SessionId;

        public static class Use
        {
            public static object New() => SessionId.New();

            public static object From(Guid value) => Create<SessionId>(value);

            public static object Parse(string text) => Parse<SessionId>(text);

            public static bool TryParse(string? text) => SessionId.TryParse(text, null, out _);

            public static object[] TryParseWithId(string? text) => [SessionId.TryParse(text, null, out var id), id];

            public static string ToJson(object id) => JsonSerializer.Serialize((SessionId)id);

            public static object FromJson(string json) => JsonSerializer.Deserialize<SessionId>(json);

            public static object? Convert(string text) => TypeDescriptor.GetConverter(typeof(SessionId)).ConvertFromInvariantString(text);

            private static T Create<T>(Guid value) where T : struct, ITypedId<T> => T.From(value);

            private static T Parse<T>(string text) where T : IParsable<T> => T.Parse(text, null);
        }
        """;

    private static readonly Guid s_value = new("0193a4b5-6c7d-7e8f-9a0b-1c2d3e4f5a6b");

    [Fact]
    public async Task TypedId_Generated_ParsesWhatToStringWrites()
    {
        using var result = await RunAsync();
        var id = Call(result, "New");

        var parsed = Call(result, "Parse", id.ToString()!);

        parsed.Should().Be(id);
    }

    [Fact]
    public async Task TypedId_Generated_CompilesWithoutDiagnostics()
    {
        using var result = await RunAsync();

        result.Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task New_IsVersion7()
    {
        using var result = await RunAsync();

        var id = (ITypedId)Call(result, "New");

        id.Value.Version.Should().Be(7);
    }

    [Fact]
    public async Task From_Guid_WrapsValue()
    {
        using var result = await RunAsync();

        var id = (ITypedId)Call(result, "From", s_value);

        id.Value.Should().Be(s_value);
    }

    [Fact]
    public async Task ToString_IsGuidInFormatD()
    {
        using var result = await RunAsync();

        var text = Call(result, "From", s_value).ToString();

        text.Should().Be(s_value.ToString("D"));
    }

    [Fact]
    public async Task TryParse_NotAGuid_ReturnsFalse()
    {
        using var result = await RunAsync();

        var parsed = Call(result, "TryParse", "not-a-guid");

        parsed.Should().Be(false);
    }

    [Fact]
    public async Task TryParse_Guid_ReturnsTrueAndId()
    {
        using var result = await RunAsync();

        var parsed = (object[])Call(result, "TryParseWithId", s_value.ToString("D"));

        parsed[0].Should().Be(true);
        parsed[1].Should().Be(Call(result, "From", s_value));
    }

    [Fact]
    public async Task Json_RoundTrip_KeepsValue()
    {
        using var result = await RunAsync();
        var id = Call(result, "From", s_value);
        var json = (string)Call(result, "ToJson", id);

        var read = Call(result, "FromJson", json);

        json.Should().Be($"\"{s_value:D}\"");
        read.Should().Be(id);
    }

    [Fact]
    public async Task TypeConverter_FromString_ReturnsId()
    {
        using var result = await RunAsync();

        var converted = Call(result, "Convert", s_value.ToString("D"));

        converted.Should().Be(Call(result, "From", s_value));
    }

    private static Task<HarnessResult> RunAsync() => GeneratorHarness.RunAsync(Source, [new TypedIdGenerator()]);

    private static object Call(HarnessResult result, string method, params object?[] arguments)
    {
        result.Assembly.Should().NotBeNull("the snippet must compile: {0}", string.Join(Environment.NewLine, result.Diagnostics));
        var use = result.Assembly!.GetType("Probe.Use", throwOnError: true)!;

        // TypeDescriptor resolves the converter by its assembly-qualified name, which finds the snippet only in its own load context.
        using var scope = AssemblyLoadContext.EnterContextualReflection(result.Assembly);
        return use.GetMethod(method, BindingFlags.Public | BindingFlags.Static)!.Invoke(null, arguments)!;
    }
}
