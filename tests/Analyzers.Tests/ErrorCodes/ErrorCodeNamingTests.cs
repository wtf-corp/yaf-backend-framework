using YAF.Analyzers.ErrorCodes;

namespace Analyzers.Tests.ErrorCodes;

public sealed class ErrorCodeNamingTests
{
    [Theory]
    [InlineData("Full", "full")]
    [InlineData("NotFound", "not_found")]
    [InlineData("HTTPRequest", "http_request")]
    [InlineData("IOError", "io_error")]
    [InlineData("Http2Error", "http2_error")]
    [InlineData("SessionV2", "session_v2")]
    [InlineData("TitleTooLong_", "title_too_long_")]
    [InlineData("URL", "url")]
    public void ToSnakeCase_Identifier_SplitsWords(string identifier, string expected)
    {
        var snake = ErrorCodeNaming.ToSnakeCase(identifier);

        snake.Should().Be(expected);
    }
}
