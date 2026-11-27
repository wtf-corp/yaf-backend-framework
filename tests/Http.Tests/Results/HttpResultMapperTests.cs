using System.Text.Json;
using Http.Tests.ProblemDetails;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using YAF.Core.Results;
using YAF.Http;

namespace Http.Tests.Results;

public sealed class HttpResultMapperTests : IDisposable
{
    private readonly IHost _host;

    public HttpResultMapperTests()
    {
        var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());
        builder.AddYafHttp();
        _host = builder.Build();
    }

    [Fact]
    public async Task ToResponse_SuccessWithValue_Is200WithBody()
    {
        var context = Request();

        await HttpResultMapper.ToResponse(Result.Success(new SessionResponse(7, "Keynote")), context).ExecuteAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
        Body(context).GetProperty("title").GetString().Should().Be("Keynote");
    }

    [Fact]
    public async Task ToResponse_NonGenericSuccess_Is204()
    {
        var context = Request();

        await HttpResultMapper.ToResponse(Result.Success(), context).ExecuteAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status204NoContent);
        context.Response.Body.Length.Should().Be(0);
    }

    [Fact]
    public async Task ToCreatedResponse_Is201WithLocation()
    {
        var context = Request();

        await HttpResultMapper.ToCreatedResponse(Result.Success(new SessionResponse(7, "Keynote")), Location, context)
            .ExecuteAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status201Created);
        context.Response.Headers.Location.ToString().Should().Be("/api/v1/sessions/7");
        Body(context).GetProperty("id").GetInt32().Should().Be(7);
    }

    [Fact]
    public async Task ToAcceptedResponse_Is202WithLocation()
    {
        var context = Request();

        await HttpResultMapper.ToAcceptedResponse(Result.Success(new SessionResponse(7, "Keynote")), Location, context)
            .ExecuteAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status202Accepted);
        context.Response.Headers.Location.ToString().Should().Be("/api/v1/sessions/7");
        Body(context).GetProperty("id").GetInt32().Should().Be(7);
    }

    [Fact]
    public async Task ToResponse_Failure_IsProblemDetails()
    {
        var context = Request();

        await HttpResultMapper.ToResponse(Result.Failure(MapperErrors.Full.With(("Capacity", 40))), context).ExecuteAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status409Conflict);
        context.Response.ContentType.Should().StartWith("application/problem+json");
        Body(context).GetProperty("code").GetString().Should().Be(MapperErrors.Full.Code);
    }

    [Fact]
    public async Task ToResponse_FailureWithValueType_IsProblemDetails()
    {
        var context = Request();
        Result<SessionResponse> failure = MapperErrors.Missing;

        await HttpResultMapper.ToResponse(failure, context).ExecuteAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status404NotFound);
        Body(context).GetProperty("code").GetString().Should().Be(MapperErrors.Missing.Code);
    }

    [Fact]
    public async Task ToCreatedResponse_Failure_IsProblemDetailsWithoutLocation()
    {
        var context = Request();
        Result<SessionResponse> failure = MapperErrors.TitleRequired.At("title");

        await HttpResultMapper.ToCreatedResponse(failure, _ => throw new InvalidOperationException("Not called."), context)
            .ExecuteAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        context.Response.Headers.Location.Should().BeEmpty();
        Body(context).GetProperty("code").GetString().Should().Be(MapperErrors.TitleRequired.Code);
    }

    [Fact]
    public async Task ToAcceptedResponse_Failure_IsProblemDetailsWithoutLocation()
    {
        var context = Request();
        Result<SessionResponse> failure = MapperErrors.StoreDown;

        await HttpResultMapper.ToAcceptedResponse(failure, _ => throw new InvalidOperationException("Not called."), context)
            .ExecuteAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status503ServiceUnavailable);
        context.Response.Headers.Location.Should().BeEmpty();
        Body(context).GetProperty("code").GetString().Should().Be(MapperErrors.StoreDown.Code);
    }

    [Fact]
    public void ToResponse_UninitialisedResult_IsProblemDetailsWith500()
    {
        var context = Request();

        var response = HttpResultMapper.ToResponse(default(Result), context);

        response.Should().BeAssignableTo<IStatusCodeHttpResult>().Which.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
    }

    public void Dispose() => _host.Dispose();

    private static Uri Location(SessionResponse session) => new($"/api/v1/sessions/{session.Id}", UriKind.Relative);

    private static JsonElement Body(HttpContext context)
    {
        context.Response.Body.Position = 0;
        using var document = JsonDocument.Parse(context.Response.Body);
        return document.RootElement.Clone();
    }

    private DefaultHttpContext Request()
    {
        var context = new DefaultHttpContext { RequestServices = _host.Services };
        context.Request.Path = "/api/v1/sessions";
        context.Response.Body = new MemoryStream();
        return context;
    }

    internal sealed record SessionResponse(int Id, string Title);
}
