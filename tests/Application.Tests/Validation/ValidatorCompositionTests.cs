using System.Linq.Expressions;
using YAF.Application;
using YAF.Application.Validation;
using YAF.Core.Results;

namespace Application.Tests.Validation;

public sealed class ValidatorCompositionTests
{
    private static readonly SubmitProgrammeCommand s_valid = new("Architecture", [new Speaker("ada@example.org")], new Room("A-1"));

    [Fact]
    public async Task RuleForEach_SecondItemBroken_TargetsItsIndex()
    {
        var validator = new ProgrammeValidator(v => v.ForEach(x => x.Speakers!).Must(s => s.Email is not null, TalkErrors.EmailRequired));
        var command = s_valid with { Speakers = [new Speaker("ada@example.org"), new Speaker(null)] };

        var error = (await ValidateAsync(validator, command)).Errors.Should().ContainSingle().Subject;

        error.Should().BeEquivalentTo(new { TalkErrors.EmailRequired.Code, Target = "speakers[1]" });
        (await ValidateAsync(validator, s_valid)).IsSuccess.Should().BeTrue();
        (await ValidateAsync(validator, s_valid with { Speakers = null })).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task When_ConditionFalse_SkipsRules()
    {
        var validator = new ProgrammeValidator(v =>
            v.If(x => x.Room is not null, () => v.For(x => x.Room!.RoomCode).NotEmpty(TalkErrors.RoomCodeInvalid)));

        // Without the condition the rule would read a member of null.
        (await ValidateAsync(validator, s_valid with { Room = null })).IsSuccess.Should().BeTrue();
        (await ValidateAsync(validator, s_valid with { Room = new Room("") })).Errors.Should().ContainSingle()
            .Which.Target.Should().Be("room.roomCode");
    }

    [Fact]
    public async Task MustAsync_RunsAfterSyncRulesPass()
    {
        var calls = 0;
        var validator = new ProgrammeValidator(v => v.For(x => x.Track)
            .MustAsync((track, _) => ValueTask.FromResult(++calls > 0 && track != "Taken"), ProgrammeErrors.TrackTaken)
            .NotEmpty(TalkErrors.TitleRequired));

        (await ValidateAsync(validator, s_valid with { Track = "" })).Errors.Should().ContainSingle()
            .Which.Code.Should().Be(TalkErrors.TitleRequired.Code);
        calls.Should().Be(0);

        (await ValidateAsync(validator, s_valid with { Track = "Taken" })).Errors.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new { ProgrammeErrors.TrackTaken.Code, Target = "track" });
        calls.Should().Be(1);

        (await ValidateAsync(validator, s_valid)).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Include_AddsTheOthersErrors()
    {
        var other = new ProgrammeValidator(v => v.For(x => x.Track).NotEmpty(TalkErrors.TitleRequired));
        var validator = new ProgrammeValidator(v =>
        {
            v.For(x => x.Room).NotNull(TalkErrors.RoomCodeInvalid);
            v.With(other);
        });

        var result = await ValidateAsync(validator, s_valid with { Track = null, Room = null });

        result.Errors.Select(error => (error.Code, error.Target)).Should().Equal(
            (TalkErrors.RoomCodeInvalid.Code, "room"),
            (TalkErrors.TitleRequired.Code, "track"));
        (await ValidateAsync(validator, s_valid)).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Nested_PrefixesTheChildTarget()
    {
        var validator = new ProgrammeValidator(v => v.Child(x => x.Room, new RoomValidator()));

        (await ValidateAsync(validator, s_valid with { Room = new Room("a1") })).Errors.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new { TalkErrors.RoomCodeInvalid.Code, Target = "room.roomCode" });
        (await ValidateAsync(validator, s_valid)).IsSuccess.Should().BeTrue();
        (await ValidateAsync(validator, s_valid with { Room = null })).IsSuccess.Should().BeTrue();
    }

    private static ValueTask<Result> ValidateAsync(ProgrammeValidator validator, SubmitProgrammeCommand command) =>
        validator.ValidateAsync(command, TestContext.Current.CancellationToken);
}

/// <summary>A command with a collection and a child, for the composite rules.</summary>
internal sealed record SubmitProgrammeCommand(string? Track, IReadOnlyList<Speaker>? Speakers, Room? Room) : ICommand<Result>;

internal sealed record Speaker(string? Email);

internal abstract class ProgrammeErrors : IErrorCatalog
{
    public static readonly ErrorDescriptor TrackTaken = Error.Validation("The track is taken.");
}

/// <summary>A validator of the child type, for <c>Nested</c>.</summary>
internal sealed class RoomValidator : Validator<Room>
{
    public RoomValidator() => Rule(x => x.RoomCode).Matches("^[A-Z]-[0-9]+$", TalkErrors.RoomCodeInvalid);
}

/// <summary>A validator whose composite rules a test sets.</summary>
internal sealed class ProgrammeValidator : Validator<SubmitProgrammeCommand>
{
    public ProgrammeValidator(Action<ProgrammeValidator> rules) => rules(this);

    public RuleBuilder<SubmitProgrammeCommand, TProperty> For<TProperty>(Expression<Func<SubmitProgrammeCommand, TProperty>> member) =>
        Rule(member);

    public RuleBuilder<SubmitProgrammeCommand, TItem> ForEach<TItem>(Expression<Func<SubmitProgrammeCommand, IEnumerable<TItem>>> member) =>
        RuleForEach(member);

    public void If(Func<SubmitProgrammeCommand, bool> condition, Action rules) => When(condition, rules);

    public void With(IValidator<SubmitProgrammeCommand> validator) => Include(validator);

    public void Child<TChild>(Expression<Func<SubmitProgrammeCommand, TChild?>> member, IValidator<TChild> validator) =>
        Nested(member, validator);
}
