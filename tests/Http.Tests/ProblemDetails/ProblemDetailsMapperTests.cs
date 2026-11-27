using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using YAF.Core.Results;
using YAF.Http;

namespace Http.Tests.ProblemDetails;

public sealed class ProblemDetailsMapperTests
{
    private const string GenericDetail = "An unexpected error occurred.";

    [Theory]
    [InlineData(ErrorKind.Validation, 400)]
    [InlineData(ErrorKind.Unauthenticated, 401)]
    [InlineData(ErrorKind.Forbidden, 403)]
    [InlineData(ErrorKind.NotFound, 404)]
    [InlineData(ErrorKind.Conflict, 409)]
    [InlineData(ErrorKind.PreconditionFailed, 412)]
    [InlineData(ErrorKind.RateLimited, 429)]
    [InlineData(ErrorKind.Unavailable, 503)]
    [InlineData(ErrorKind.Failure, 500)]
    public void Map_EachKind_GivesItsStatus(ErrorKind kind, int status)
    {
        var mapper = CreateMapper();

        var problem = mapper.Map([MapperErrors.OfKind(kind).ToError()], Request());

        problem.Status.Should().Be(status);
    }

    // Each row pairs two neighbours of the order of 03 §4, the less severe first, so the whole order is pinned.
    [Theory]
    [InlineData(ErrorKind.Forbidden, ErrorKind.Unauthenticated, 401)]
    [InlineData(ErrorKind.RateLimited, ErrorKind.Forbidden, 403)]
    [InlineData(ErrorKind.Unavailable, ErrorKind.RateLimited, 429)]
    [InlineData(ErrorKind.Failure, ErrorKind.Unavailable, 503)]
    [InlineData(ErrorKind.PreconditionFailed, ErrorKind.Failure, 500)]
    [InlineData(ErrorKind.Conflict, ErrorKind.PreconditionFailed, 412)]
    [InlineData(ErrorKind.NotFound, ErrorKind.Conflict, 409)]
    [InlineData(ErrorKind.Validation, ErrorKind.NotFound, 404)]
    public void Map_MixedKinds_MostSevereWins(ErrorKind lesser, ErrorKind severer, int status)
    {
        var mapper = CreateMapper();
        Error[] errors = [MapperErrors.OfKind(lesser).ToError(), MapperErrors.OfKind(severer).ToError()];

        var problem = mapper.Map(errors, Request());

        problem.Status.Should().Be(status);
        problem.Extensions["code"].Should().Be(MapperErrors.OfKind(severer).Code);
    }

    [Theory]
    [InlineData(null, 400)]
    [InlineData("400", 400)]
    [InlineData("422", 422)]
    public void Map_AllValidation_UsesTheConfiguredStatus(string? configured, int status)
    {
        var mapper = CreateMapper(("Http:ValidationStatusCode", configured));
        Error[] errors = [MapperErrors.TitleRequired.At("title"), MapperErrors.TitleRequired.At("subtitle")];

        var problem = mapper.Map(errors, Request());

        problem.Status.Should().Be(status);
    }

    [Fact]
    public void Map_Failure_HidesTheMessage()
    {
        var mapper = CreateMapper();
        var failure = MapperErrors.Internal.With(("User", "app_owner"));

        var problem = mapper.Map([failure], Request());

        problem.Detail.Should().Be(GenericDetail);
        problem.Extensions["errors"].Should().BeEquivalentTo(new[]
        {
            new { Code = MapperErrors.Internal.Code, Message = GenericDetail, Target = (string?)null, Arguments = new Dictionary<string, object?>() },
        });
        JsonSerializer.Serialize(problem).Should().NotContain("db-01").And.NotContain("app_owner");
    }

    [Fact]
    public void Map_Body_HasCodeTraceIdAndErrorsArray()
    {
        var mapper = CreateMapper();
        using var activity = new Activity("request");
        activity.Start();
        Error[] errors = [MapperErrors.Full.With(("Capacity", 40)), MapperErrors.TitleRequired.At("title")];

        var problem = mapper.Map(errors, Request("/api/v1/sessions/7"));

        problem.Title.Should().Be("Conflict");
        problem.Detail.Should().Be("Session has no free seats (capacity 40).");
        problem.Instance.Should().Be("/api/v1/sessions/7");
        problem.Extensions["code"].Should().Be(MapperErrors.Full.Code);
        problem.Extensions["traceId"].Should().Be(activity.TraceId.ToHexString());
        JsonSerializer.Serialize(problem).Should().Contain(
            $$$"""
            "errors":[{"code":"{{{MapperErrors.Full.Code}}}","message":"Session has no free seats (capacity 40).","target":null,"arguments":{"Capacity":40}},{"code":"{{{MapperErrors.TitleRequired.Code}}}","message":"A session needs a title.","target":"title","arguments":{}}]
            """);
    }

    [Theory]
    [InlineData(null, "urn:yaf:error:")]
    [InlineData("https://errors.example.com/", "https://errors.example.com/")]
    public void Map_Type_IsBaseUriPlusCode(string? configured, string expectedBase)
    {
        var mapper = CreateMapper(("Http:ProblemDetails:TypeBaseUri", configured));

        var problem = mapper.Map([MapperErrors.Full.ToError()], Request());

        problem.Type.Should().Be(expectedBase + MapperErrors.Full.Code);
    }

    [Theory]
    [InlineData("Http:ValidationStatusCode", "200")]
    [InlineData("Http:ValidationStatusCode", "418")]
    [InlineData("Http:ProblemDetails:TypeBaseUri", "errors/")]
    public void AddYafHttp_InvalidSetting_FailsValidationNamingTheKey(string key, string value)
    {
        var act = () => CreateMapper((key, value));

        act.Should().Throw<OptionsValidationException>().WithMessage($"*{key}*");
    }

    [Fact]
    public void AddYafHttp_CalledTwice_RegistersTheMapperOnce()
    {
        var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());

        builder.AddYafHttp().AddYafHttp();

        builder.Services.Should().ContainSingle(service => service.ServiceType == typeof(IProblemDetailsMapper));
    }

    private static IProblemDetailsMapper CreateMapper(params (string Key, string? Value)[] settings)
    {
        var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());
        builder.Configuration.AddInMemoryCollection(
            settings.Where(setting => setting.Value is not null).Select(setting => KeyValuePair.Create(setting.Key, setting.Value)));
        builder.AddYafHttp();
        using var host = builder.Build();

        return host.Services.GetRequiredService<IProblemDetailsMapper>();
    }

    private static DefaultHttpContext Request(string path = "/api/v1/sessions")
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        return context;
    }
}
