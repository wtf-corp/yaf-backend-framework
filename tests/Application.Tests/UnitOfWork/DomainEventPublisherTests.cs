using System.Diagnostics;
using Application.Tests.Pipeline;
using YAF.Application;
using YAF.Application.Registration;
using YAF.Application.UnitOfWork;
using YAF.Testing;

namespace Application.Tests.UnitOfWork;

public sealed class DomainEventPublisherTests
{
    private readonly HandlerLog _log = new();
    private readonly TestClock _clock = new(new DateTimeOffset(2026, 11, 28, 9, 30, 0, TimeSpan.Zero));

    [Fact]
    public async Task Publish_RunsHandlersInRegistrationOrder()
    {
        var publisher = Publisher(new RecordingHandler(_log, "first"), new RecordingHandler(_log, "second"));

        var result = await publisher.PublishAsync([new SessionScheduled(1), new SessionScheduled(2)], TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        _log.Entries.Should().Equal("first:1", "second:1", "first:2", "second:2");
    }

    [Fact]
    public async Task Publish_FirstFailure_StopsAndReturnsIt()
    {
        var publisher = Publisher(new RecordingHandler(_log, "first", fails: true), new RecordingHandler(_log, "second"));

        var result = await publisher.PublishAsync([new SessionScheduled(1), new SessionScheduled(2)], TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Errors.Select(error => error.Code).Should().Equal(PublishErrors.HandlerRefused.Code);
        _log.Entries.Should().Equal("first:1");
    }

    [Fact]
    public async Task Publish_StampsOccurredAt()
    {
        var publisher = Publisher(new RecordingHandler(_log, "first"));
        var raised = new SessionScheduled(1);

        await publisher.PublishAsync([raised], TestContext.Current.CancellationToken);

        _log.Received.Should().ContainSingle().Which.OccurredAt.Should().Be(_clock.Now);
        _log.Received[0].EventId.Should().Be(raised.EventId);
    }

    [Fact]
    public async Task Publish_NoHandler_Succeeds()
    {
        var publisher = Publisher();
        var withoutRegistry = new DomainEventPublisher(new ApplicationRegistrations(), Runner(new ApplicationRegistrations()), _clock.TimeProvider);

        var result = await publisher.PublishAsync([new SessionScheduled(1), new SessionMoved(2)], TestContext.Current.CancellationToken);
        var none = await withoutRegistry.PublishAsync([new SessionMoved(3)], TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        none.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Publish_StartsADomainEventActivityPerEvent()
    {
        using var capture = new TelemetryCapture();
        var publisher = Publisher(new RecordingHandler(_log, "first", fails: true));

        await publisher.PublishAsync([new SessionScheduled(1)], TestContext.Current.CancellationToken);

        Activity[] activities;
        lock (capture.Activities)
        {
            activities = [.. capture.Activities.Where(activity => activity.DisplayName == "yaf.domain_event SessionScheduled")];
        }

        activities.Should().ContainSingle().Which.OperationName.Should().Be("yaf.domain_event");
        activities[0].Status.Should().Be(ActivityStatusCode.Unset);
    }

    private DomainEventPublisher Publisher(params IDomainEventHandler<SessionScheduled>[] handlers)
    {
        var registrations = new ApplicationRegistrations();
        registrations.Add(new PublishingRegistry());
        return new DomainEventPublisher(registrations, Runner(registrations, handlers), _clock.TimeProvider);
    }

    private static Dispatcher Runner(ApplicationRegistrations registrations, params IDomainEventHandler<SessionScheduled>[] handlers) =>
        new(new HandlerProvider(handlers), registrations, registrations.Pipeline.Build());
}
