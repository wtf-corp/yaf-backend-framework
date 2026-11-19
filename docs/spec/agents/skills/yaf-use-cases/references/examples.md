# Use case examples (conference sample)

## 1. Command with validator, handler and tests

```csharp
// Application/Sessions/CreateSession/CreateSessionCommand.cs
[RequiresPermission("sessions", "manage")]
[Idempotent]
public sealed record CreateSessionCommand(
    [property: PublicData] string Title,
    int Capacity,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt) : ICommand<Result<CreateSessionResponse>>;

public sealed record CreateSessionResponse(SessionId Id);
```

```csharp
// CreateSessionValidator.cs
internal sealed class CreateSessionValidator : Validator<CreateSessionCommand>
{
    public CreateSessionValidator()
    {
        Rule(x => x.Title).NotEmpty(Errors.TitleRequired).MaxLength(200, Errors.TitleTooLong);
        Rule(x => x.Capacity).InRange(1, 10_000, Errors.CapacityOutOfRange);
        Rule(x => x.EndsAt).Must((cmd, end) => end > cmd.StartsAt, Errors.EndBeforeStart);
    }

    internal sealed class Errors : IErrorCatalog
    {
        public static readonly ErrorDescriptor TitleRequired = Error.Validation("Title is required.");
        public static readonly ErrorDescriptor TitleTooLong = Error.Validation("Title cannot exceed {Max} characters.");
        public static readonly ErrorDescriptor CapacityOutOfRange = Error.Validation("Capacity must be between {Min} and {Max}.");
        public static readonly ErrorDescriptor EndBeforeStart = Error.Validation("The session must end after it starts.");
    }
}
```

```csharp
// CreateSessionHandler.cs
internal sealed class CreateSessionHandler(ISessionRepository sessions)
    : IRequestHandler<CreateSessionCommand, Result<CreateSessionResponse>>
{
    public async ValueTask<Result<CreateSessionResponse>> HandleAsync(CreateSessionCommand cmd, CancellationToken ct)
    {
        var session = Session.Create(cmd.Title, cmd.Capacity, new Period(cmd.StartsAt, cmd.EndsAt));
        if (session.IsFailure) return session.Errors.ToArray();

        await sessions.AddAsync(session.Value, ct);
        return new CreateSessionResponse(session.Value.Id);
    }
}
```

```csharp
// Application.Tests/Sessions/CreateSessionHandlerTests.cs
public class CreateSessionHandlerTests
{
    private readonly InMemorySessionRepository _sessions = new();     // hand-written fake of the port

    [Fact]
    public async Task Handle_ValidCommand_AddsSession()
    {
        var handler = new CreateSessionHandler(_sessions);

        var result = await handler.HandleAsync(
            new CreateSessionCommand("Keynote", 100, T0, T0.AddHours(1)), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _sessions.Added.Should().ContainSingle().Which.Id.Should().Be(result.Value.Id);
    }

    [Fact]
    public async Task Validate_EndBeforeStart_ReturnsTargetedError()
    {
        var result = await new CreateSessionValidator().ValidateAsync(
            new CreateSessionCommand("Keynote", 100, T0, T0.AddHours(-1)), CancellationToken.None);

        var error = result.Errors.Should().ContainSingle().Subject;
        error.Code.Should().Be(CreateSessionValidator.Errors.EndBeforeStart.Code);
        error.Target.Should().Be("endsAt");
    }

    private static readonly DateTimeOffset T0 = new(2027, 3, 1, 9, 0, 0, TimeSpan.Zero);
}
```

## 2. Query with a read model

```csharp
// Application/Sessions/ListOpenSessions/
[RequiresPermission("sessions", "view")]
public sealed record ListOpenSessionsQuery(int Offset = 0, int Limit = 50) : IQuery<Result<PagedList<SessionSummary>>>;

public sealed record SessionSummary(SessionId Id, string Title, DateTimeOffset StartsAt, int FreeSeats);

public interface ISessionReadModel            // port; implemented in infrastructure with AsNoTracking projections
{
    ValueTask<PagedList<SessionSummary>> ListOpenAsync(DateTimeOffset now, Page page, CancellationToken ct);
}

internal sealed class ListOpenSessionsHandler(ISessionReadModel readModel, TimeProvider time)
    : IRequestHandler<ListOpenSessionsQuery, Result<PagedList<SessionSummary>>>
{
    public async ValueTask<Result<PagedList<SessionSummary>>> HandleAsync(ListOpenSessionsQuery q, CancellationToken ct)
        => await readModel.ListOpenAsync(time.GetUtcNow(), new Page(q.Offset, q.Limit), ct);
}
```

`Page` clamps the limit; the read model applies a total ordering. The tenant filter is automatic. The query runs in one `READ ONLY` transaction, so a write from it fails. The handler writes `Result.Success(...)` only when the value's static type is an interface; `PagedList<T>` is a class and converts implicitly.

## 3. Domain event handler that publishes an integration event

```csharp
// Application/Sessions/EventHandlers/PublishSessionPublished.cs
internal sealed class PublishSessionPublished(IIntegrationEventPublisher publisher, ISessionReadModel sessions)
    : IDomainEventHandler<SessionPublished>
{
    public async ValueTask<Result> HandleAsync(SessionPublished e, CancellationToken ct)
    {
        var s = await sessions.GetForContractAsync(e.SessionId, ct);
        publisher.Publish(
            new SessionPublishedV1(e.SessionId.Value, s.Title, s.StartsAt, e.Capacity),
            new PublishOptions(PartitionKey: e.SessionId.ToString()));
        return Result.Success();
    }
}
```

It runs inside the command's transaction, after the unit of work flushed the change that raised the event: the read model sees the published session, and the outbox row commits with the change or not at all.

## 4. Custom middleware

```csharp
internal sealed class TenantQuotaMiddleware<TRequest, TResult>(ITenantQuota quota, IExecutionContextAccessor context)
    : IRequestMiddleware<TRequest, TResult>
    where TRequest : ICommand<TResult>
    where TResult : IResult
{
    public async ValueTask<TResult> InvokeAsync(TRequest request, RequestDelegate<TResult> next, CancellationToken ct)
    {
        if (!await quota.HasRoomAsync(context.Current.TenantId, ct))
            return ResultFactory.Failure<TResult>(QuotaErrors.Exceeded);     // short-circuit with a declared error

        return await next(ct);
    }
}

// composition
services.AddYafApplication(app => app
    .AddHandlersFrom<ApplicationModule>()
    .UsePipeline(p => p.UseDefaults()
        .ForCommands(c => c.InsertAfter<ValidationMiddleware, TenantQuotaMiddleware>())));
```

After adding it, run the pipeline snapshot test and accept the new `pipeline.approved.txt`.

## 5. Rollback test (integration)

```csharp
[Fact]
public async Task ReserveSeat_WhenEventHandlerFails_NothingIsCommitted()
{
    await using var app = await _factory.WithFailingHandlerFor<SeatReserved>();

    var response = await app.Client.PostAsJsonAsync($"/api/v1/sessions/{_sessionId}/seats", new { registrationId = Guid.CreateVersion7() });

    response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
    (await app.Db.CountSeatsAsync(_sessionId)).Should().Be(0);
    (await app.Outbox.PendingAsync()).Should().BeEmpty();
}
```

The seat row was flushed before the failing handler ran; the rollback removes it with everything else.

## 6. A consumer that dispatches a system request

```csharp
[SystemOnly]
public sealed record CloseRegistrationsCommand(SessionId SessionId) : ICommand<Result>;

[Consumer("registrations.close-on-session-cancelled")]
internal sealed class CloseOnSessionCancelled(IDispatcher dispatcher) : IIntegrationEventHandler<SessionCancelledV1>
{
    public async ValueTask<Result> HandleAsync(SessionCancelledV1 e, MessageContext ctx, CancellationToken ct)
        => await dispatcher.SendAsync(new CloseRegistrationsCommand(SessionId.From(e.SessionId)), ct);
}
```

The consumer is an entry point, so it may dispatch. It runs as the service's system principal (`Actor`) in the tenant of the envelope; the user who cancelled the session is the `Initiator`, recorded in stamps and audit rows and used for nothing else.
