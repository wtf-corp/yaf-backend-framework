using YAF.Application;
using YAF.Application.Pipeline;
using YAF.Core.Results;
using YAF.Testing;

namespace Application.Tests.Pipeline;

public sealed class PipelineBuilderTests
{
    [Fact]
    public void InsertAfter_OpenGenericAnchor_PlacesTheMiddlewareAfterIt()
    {
        var builder = new PipelineBuilder().Use(typeof(OuterMiddleware<,>)).Use(typeof(InnerMiddleware<,>));

        builder.InsertAfter(typeof(OuterMiddleware<,>), typeof(RenameMiddleware));

        builder.Build().Steps.Select(step => step.Middleware).Should()
            .Equal(typeof(OuterMiddleware<,>), typeof(RenameMiddleware), typeof(InnerMiddleware<,>));
    }

    [Fact]
    public void Use_AppendsInOrder()
    {
        var builder = new PipelineBuilder();

        builder.Use(typeof(OuterMiddleware<,>)).Use<RenameMiddleware>().Use(typeof(InnerMiddleware<,>));

        builder.Build().Steps.Should().Equal(
            new PipelineStep(typeof(OuterMiddleware<,>), PipelineScope.All),
            new PipelineStep(typeof(RenameMiddleware), PipelineScope.All),
            new PipelineStep(typeof(InnerMiddleware<,>), PipelineScope.All));
    }

    [Fact]
    public void InsertBefore_PlacesBefore()
    {
        var builder = new PipelineBuilder().Use(typeof(OuterMiddleware<,>)).Use(typeof(InnerMiddleware<,>));

        builder.InsertBefore(typeof(OuterMiddleware<,>), typeof(RenameMiddleware));

        builder.Build().Steps.Select(step => step.Middleware).Should()
            .Equal(typeof(RenameMiddleware), typeof(OuterMiddleware<,>), typeof(InnerMiddleware<,>));
    }

    [Fact]
    public void ForCommands_ScopesTheStep()
    {
        var builder = new PipelineBuilder().Use(typeof(OuterMiddleware<,>));

        builder.ForCommands(commands => commands.Use<RenameMiddleware>()).ForQueries(queries => queries.Use(typeof(InnerMiddleware<,>)));

        builder.Build().Steps.Should().Equal(
            new PipelineStep(typeof(OuterMiddleware<,>), PipelineScope.All),
            new PipelineStep(typeof(RenameMiddleware), PipelineScope.Commands),
            new PipelineStep(typeof(InnerMiddleware<,>), PipelineScope.Queries));
    }

    [Fact]
    public void InsertAfter_UnknownAnchor_Throws()
    {
        var builder = new PipelineBuilder().Use(typeof(OuterMiddleware<,>));

        var insert = () => builder.InsertAfter(typeof(InnerMiddleware<,>), typeof(RenameMiddleware));

        insert.Should().Throw<InvalidOperationException>().WithMessage("*InnerMiddleware*");
    }

    [Fact]
    public void Use_NotAMiddleware_Throws()
    {
        var builder = new PipelineBuilder();

        var use = () => builder.Use<string>();

        use.Should().Throw<ArgumentException>().Which.ParamName.Should().Be("middleware");
    }

    [Fact]
    public void Render_OneLinePerStep()
    {
        var builder = new PipelineBuilder().Use(typeof(OuterMiddleware<,>)).ForCommands(commands => commands.Use<RenameMiddleware>());

        var text = builder.Build().Render();

        text.Should().Be("1. OuterMiddleware (all)\n2. RenameMiddleware (commands)\n");
    }

    [Fact]
    public void Middleware_IsContravariantInTheRequest()
    {
        object middleware = new OuterMiddleware<IRequest<Result>, Result>();

        middleware.Should().BeAssignableTo<IRequestMiddleware<RenameCommand, Result>>();
    }

    [Fact]
    public async Task InvokeAsync_RunsTheRestOfThePipelineThroughNext()
    {
        var inner = new InnerMiddleware<RenameCommand, Result>();
        var request = new RenameCommand();

        var result = await new RenameMiddleware().InvokeAsync(
            request,
            ct => inner.InvokeAsync(request, _ => ValueTask.FromResult(Result.Success()), ct),
            TestContext.Current.CancellationToken);

        result.ShouldSucceed();
    }

    private sealed record RenameCommand : ICommand<Result>;

    private sealed class OuterMiddleware<TRequest, TResult> : IRequestMiddleware<TRequest, TResult>
        where TRequest : IRequest<TResult>
    {
        public ValueTask<TResult> InvokeAsync(TRequest request, RequestDelegate<TResult> next, CancellationToken ct) => next(ct);
    }

    private sealed class InnerMiddleware<TRequest, TResult> : IRequestMiddleware<TRequest, TResult>
        where TRequest : IRequest<TResult>
    {
        public ValueTask<TResult> InvokeAsync(TRequest request, RequestDelegate<TResult> next, CancellationToken ct) => next(ct);
    }

    private sealed class RenameMiddleware : IRequestMiddleware<RenameCommand, Result>
    {
        public ValueTask<Result> InvokeAsync(RenameCommand request, RequestDelegate<Result> next, CancellationToken ct) => next(ct);
    }
}
