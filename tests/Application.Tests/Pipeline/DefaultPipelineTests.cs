using YAF.Application.Pipeline;
using YAF.Testing;

namespace Application.Tests.Pipeline;

public sealed class DefaultPipelineTests
{
    [Fact]
    public void UseDefaults_OrdersTheBuiltInSteps()
    {
        var builder = new PipelineBuilder();

        builder.UseDefaults();

        builder.Build().Steps.Should().Equal(
            new PipelineStep(typeof(TelemetryMiddleware<,>), PipelineScope.All),
            new PipelineStep(typeof(ExceptionBoundaryMiddleware<,>), PipelineScope.All),
            new PipelineStep(typeof(ValidationMiddleware<,>), PipelineScope.All),
            new PipelineStep(typeof(UnitOfWorkMiddleware<,>), PipelineScope.Commands),
            new PipelineStep(typeof(QueryTransactionMiddleware<,>), PipelineScope.Queries));
    }

    [Fact]
    public void Render_Defaults_MatchesTheApprovedText()
    {
        var text = new PipelineBuilder().UseDefaults().Build().Render();

        Approvals.Verify(text, "pipeline");
    }
}
