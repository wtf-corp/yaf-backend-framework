using YAF.Application.UnitOfWork;
using YAF.Core.Results;

namespace Application.Tests.Errors;

public sealed class ApplicationErrorsTests
{
    [Fact]
    public void DomainEventDepthExceeded_IsAFailureWithTheLimitPlaceholder()
    {
        var descriptor = ApplicationErrors.DomainEventDepthExceeded;

        descriptor.Code.Should().Be("core.unit_of_work.event_depth_exceeded");
        descriptor.Kind.Should().Be(ErrorKind.Failure);
        descriptor.MessageTemplate.Should().Be("Domain events kept raising further events beyond the limit of {Limit} rounds.");
        descriptor.With(("Limit", 10)).Message.Should().Be("Domain events kept raising further events beyond the limit of 10 rounds.");
    }
}
