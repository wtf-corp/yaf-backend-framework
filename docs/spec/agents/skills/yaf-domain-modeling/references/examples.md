# A complete small aggregate: `Session` with `Seat`

Illustrative; adapt names. Files are under `Domain/Sessions/` of the Catalog service.

## Ids and status

```csharp
[TypedId] public readonly partial record struct SessionId;
[TypedId] public readonly partial record struct SeatId;

public enum SessionStatus { Draft, Published, Cancelled, Completed }
public enum SessionTrigger { Publish, Reserve, Cancel, Complete }
```

## `Session.cs`

```csharp
public partial class Session : AggregateRoot<SessionId, Session, Session.IMemento>,
                               IOriginator<Session, Session.IMemento>
{
    private Session(SessionId id) : base(id) { }

    public static Result<Session> Create(string title, int capacity, Period when)
    {
        var session = new Session(SessionId.New()) { Title = title, Capacity = capacity, When = when };
        return session.CheckInvariants().Then(() =>
        {
            session.Raise(new SessionDrafted(session.Id));
            return session;
        });
    }

    public static readonly StateMachine<SessionStatus, SessionTrigger> Lifecycle =
        StateMachine<SessionStatus, SessionTrigger>.Define(b => b
            .From(SessionStatus.Draft).On(SessionTrigger.Publish).To(SessionStatus.Published)
            .From(SessionStatus.Published).On(SessionTrigger.Reserve).To(SessionStatus.Published)
            .From(SessionStatus.Published).On(SessionTrigger.Cancel).To(SessionStatus.Cancelled)
            .From(SessionStatus.Published).On(SessionTrigger.Complete).To(SessionStatus.Completed)
            .Otherwise(Errors.TransitionNotAllowed));

    protected override Result CheckInvariants() => Check(
        new TitlePresent(Title),
        new CapacityPositive(Capacity));
}
```

## `Session.State.cs`

```csharp
public partial class Session
{
    private readonly List<Seat> _seats = [];

    public string Title { get; private set; } = "";
    public int Capacity { get; private set; }
    public Period When { get; private set; }
    public SessionStatus Status { get; private set; } = SessionStatus.Draft;
    public IReadOnlyCollection<Seat> Seats => _seats.AsReadOnly();
}
```

## `Session.Errors.cs`

```csharp
public partial class Session
{
    internal sealed class Errors : IErrorCatalog
    {
        public static readonly ErrorDescriptor NotFound = Error.NotFound("Session was not found.");
        public static readonly ErrorDescriptor Full = Error.Conflict("Session has no free seats (capacity {Capacity}).");
        public static readonly ErrorDescriptor TitleRequired = Error.Validation("Title is required.");
        public static readonly ErrorDescriptor CapacityOutOfRange = Error.Validation("Capacity must be positive.");
        public static readonly ErrorDescriptor TransitionNotAllowed =
            Error.Validation("Session cannot go from {From} with {Trigger}.");
    }
}
```

## `Session.Memento.cs`

```csharp
public partial class Session
{
    public interface IMemento : IAggregateMemento
    {
        [PublicData] string Title { get; set; }
        int Capacity { get; set; }
        Period When { get; set; }                 // value object: stored as two columns
        SessionStatus Status { get; set; }        // enum: stored as its name
        IList<Seat.IMemento> Seats { get; }
        Seat.IMemento NewSeat();
    }

    protected override void SnapshotCore(IMemento m)
    {
        m.Title = Title;
        m.Capacity = Capacity;
        m.When = When;
        m.Status = Status;
        MementoCollections.Sync(m.Seats, _seats, m.NewSeat);   // by id
    }

    protected override void RestoreCore(IMemento m)
    {
        Title = m.Title;
        Capacity = m.Capacity;
        When = m.When;
        Status = m.Status;
        _seats.Clear();
        _seats.AddRange(m.Seats.Select(Seat.Restore));
    }

    public static Session Restore(IMemento m)
    {
        var session = new Session(SessionId.From(m.Id));
        session.RestoreFrom(m);          // base: version, then RestoreCore
        return session;
    }
}
```

## Child entity `Seat.cs`

```csharp
public sealed class Seat : Entity<SeatId, Seat, Seat.IMemento>, IOriginator<Seat, Seat.IMemento>
{
    private Seat(SeatId id) : base(id) { }
    public Guid RegistrationId { get; private set; }      // another context's id: by value only

    internal static Seat Create(SeatId id, Guid registrationId) => new(id) { RegistrationId = registrationId };

    public interface IMemento : IOwnedEntityMemento { Guid RegistrationId { get; set; } }

    protected override void SnapshotCore(IMemento m) => m.RegistrationId = RegistrationId;
    protected override void RestoreCore(IMemento m) => RegistrationId = m.RegistrationId;
    public static Seat Restore(IMemento m) { var s = new Seat(SeatId.From(m.Id)); s.RestoreFrom(m); return s; }
}
```

## Behaviour `Behaviors/Session.ReserveSeat.cs`

```csharp
public partial class Session
{
    public Result<SeatId> ReserveSeat(Guid registrationId)
    {
        var next = Lifecycle.Fire(Status, SessionTrigger.Reserve);
        if (next.IsFailure) return next.Errors.ToArray();

        var rule = Check(new SeatLimitNotExceeded(_seats.Count, Capacity));
        if (rule.IsFailure) return rule.Errors.ToArray();

        var seat = Seat.Create(SeatId.New(), registrationId);
        _seats.Add(seat);
        Raise(new SeatReserved(Id, seat.Id, registrationId));
        return seat.Id;
    }
}
```

## Rule, event, specification

```csharp
public sealed class SeatLimitNotExceeded(int taken, int capacity) : IBusinessRule
{
    public ErrorDescriptor Error => Session.Errors.Full;
    public bool IsSatisfied() => taken < capacity;
}

public sealed record SeatReserved(SessionId SessionId, SeatId SeatId, Guid RegistrationId) : DomainEvent;

public sealed class OpenSessions(DateTimeOffset now) : Specification<Session.IMemento>
{
    public override Expression<Func<Session.IMemento, bool>> ToExpression()
        => s => s.Status == SessionStatus.Published && s.When.Start > now;
}
```

## Tests (`Domain.Tests/Sessions/`)

```csharp
public class SessionReserveSeatTests
{
    [Fact]
    public void ReserveSeat_WhenFull_ReturnsFullError()
    {
        var session = SessionMother.Published(capacity: 1);
        session.ReserveSeat(Guid.CreateVersion7());

        var result = session.ReserveSeat(Guid.CreateVersion7());

        result.IsFailure.Should().BeTrue();
        result.FirstError.Code.Should().Be(Session.Errors.Full.Code);
        session.Seats.Should().HaveCount(1);
    }

    [Fact]
    public void ReserveSeat_WhenRoom_RaisesSeatReserved()
    {
        var session = SessionMother.Published(capacity: 2);

        var result = session.ReserveSeat(Guid.CreateVersion7());

        result.IsSuccess.Should().BeTrue();
        session.DomainEvents.OfType<SeatReserved>().Should().ContainSingle();
    }

    [Fact]
    public void Memento_RoundTrips_WithChildren()
        => MementoAssert.RoundTrips(SessionMother.PublishedWithSeats(2), () => new FakeSessionMemento());

    [Fact]
    public void Lifecycle_EveryStateReachable_AndNonFinalStatesHaveAnExit()
        => StateMachineAssert.IsWellFormed(Session.Lifecycle, final: [SessionStatus.Cancelled, SessionStatus.Completed]);
}
```

Notes:

- Assert on error **codes**, never on messages.
- `SessionMother` is a test builder in the test project; test data is synthetic.
- `FakeSessionMemento` is a plain class implementing `Session.IMemento` in the test project; it needs no EF Core. `MementoAssert` comes from `YAF.Testing`, which a domain test project can reference without pulling in ASP.NET Core or EF Core.
- Memento members are typed (`Period`, `SessionStatus`); the specification compares them directly. `Seat.RegistrationId` is a `Guid` only because it is another context's id.
- The storage class is not written by hand: the infrastructure project declares `[MementoData] internal sealed partial class SessionData : Session.IMemento;` and the generator emits the members (`yaf-persistence`).
