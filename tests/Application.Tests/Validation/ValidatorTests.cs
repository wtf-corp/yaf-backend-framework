using YAF.Core.Results;

namespace Application.Tests.Validation;

public sealed class ValidatorTests
{
    private static readonly ScheduleTalkCommand s_valid = new("Domain events", "ada@example.org", 40, new Room("A-1"));

    [Fact]
    public async Task MaxLength_TooLong_FailsAtTheCamelCaseTarget()
    {
        var validator = new TalkValidator(v => v.For(x => x.TalkTitle).MaxLength(5, TalkErrors.TitleTooLong));
        var error = await SingleErrorAsync(validator, s_valid with { TalkTitle = "Sagas!" });

        error.Should().BeEquivalentTo(new { TalkErrors.TitleTooLong.Code, Kind = ErrorKind.Validation, Target = "talkTitle" });
        error.Message.Should().Be("The title has more than 5 characters.");
        error.Arguments.Should().Equal(new Dictionary<string, object?> { ["MaxLength"] = 5 });
        (await ValidateAsync(validator, s_valid with { TalkTitle = "Sagas" })).IsSuccess.Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task NotEmpty_Empty_Fails(string? title)
    {
        var validator = new TalkValidator(v => v.For(x => x.TalkTitle).NotEmpty(TalkErrors.TitleRequired));
        var error = await SingleErrorAsync(validator, s_valid with { TalkTitle = title });

        error.Should().BeEquivalentTo(new { TalkErrors.TitleRequired.Code, Target = "talkTitle", Message = "A title is required." });
    }

    [Fact]
    public async Task NotNull_Null_Fails()
    {
        var validator = new TalkValidator(v => v.For(x => x.SpeakerEmail).NotNull(TalkErrors.EmailRequired));
        var error = await SingleErrorAsync(validator, s_valid with { SpeakerEmail = null });

        error.Should().BeEquivalentTo(new { TalkErrors.EmailRequired.Code, Target = "speakerEmail" });
        (await ValidateAsync(validator, s_valid with { SpeakerEmail = "" })).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task MinLength_TooShort_Fails()
    {
        var validator = new TalkValidator(v => v.For(x => x.TalkTitle).MinLength(3, TalkErrors.TitleTooShort));
        var error = await SingleErrorAsync(validator, s_valid with { TalkTitle = "AI" });

        error.Should().BeEquivalentTo(new { TalkErrors.TitleTooShort.Code, Target = "talkTitle", Message = "The title has fewer than 3 characters." });
        (await ValidateAsync(validator, s_valid with { TalkTitle = "DDD" })).IsSuccess.Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public async Task InRange_Outside_Fails(int capacity)
    {
        var validator = new TalkValidator(v => v.For(x => x.Capacity).InRange(1, 100, TalkErrors.CapacityOutOfRange));
        var error = await SingleErrorAsync(validator, s_valid with { Capacity = capacity });

        error.Should().BeEquivalentTo(new { TalkErrors.CapacityOutOfRange.Code, Target = "capacity", Message = "The capacity is between 1 and 100." });
        error.Arguments.Should().Equal(new Dictionary<string, object?> { ["Minimum"] = 1, ["Maximum"] = 100 });
        (await ValidateAsync(validator, s_valid with { Capacity = capacity == 0 ? 1 : 100 })).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Matches_NoMatch_Fails()
    {
        var validator = new TalkValidator(v => v.For(x => x.Room!.RoomCode).Matches("^[A-Z]-[0-9]+$", TalkErrors.RoomCodeInvalid));
        var error = await SingleErrorAsync(validator, s_valid with { Room = new Room("a1") });

        error.Should().BeEquivalentTo(new { TalkErrors.RoomCodeInvalid.Code, Target = "room.roomCode" });
        (await ValidateAsync(validator, s_valid)).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Must_WithRequest_SeesTheWholeRequest()
    {
        ScheduleTalkCommand? seen = null;
        var validator = new TalkValidator(v => v.For(x => x.SpeakerEmail)
            .Must(email => email!.Contains('@', StringComparison.Ordinal), TalkErrors.EmailInvalid)
            .Must((command, email) => (seen = command).Capacity > 50 || email!.EndsWith(".org", StringComparison.Ordinal), TalkErrors.EmailInvalid));
        var command = s_valid with { SpeakerEmail = "ada@example.com" };

        var error = await SingleErrorAsync(validator, command);

        seen.Should().BeSameAs(command);
        error.Should().BeEquivalentTo(new { TalkErrors.EmailInvalid.Code, Target = "speakerEmail" });
        (await ValidateAsync(validator, command with { Capacity = 60 })).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_TwoBrokenRules_ReturnsBoth()
    {
        var validator = new TalkValidator(v =>
        {
            v.For(x => x.SpeakerEmail).NotNull(TalkErrors.EmailRequired).Must(email => email!.Length > 3, TalkErrors.EmailInvalid);
            v.For(x => x.Capacity).InRange(1, 100, TalkErrors.CapacityOutOfRange);
        });

        var result = await ValidateAsync(validator, s_valid with { SpeakerEmail = null, Capacity = 0 });

        // The first broken check of a chain ends it, so the Must after NotNull does not run on null.
        result.Errors.Select(error => (error.Code, error.Target)).Should().Equal(
            (TalkErrors.EmailRequired.Code, "speakerEmail"),
            (TalkErrors.CapacityOutOfRange.Code, "capacity"));
    }

    [Theory]
    [InlineData("Domain events")]
    [InlineData(null)]
    public async Task Validate_AllHold_Succeeds(string? title)
    {
        var validator = new TalkValidator(v =>
        {
            v.For(x => x.TalkTitle).MinLength(3, TalkErrors.TitleTooShort).MaxLength(200, TalkErrors.TitleTooLong)
                .Matches("^D", TalkErrors.TitleRequired);
            v.For(x => x.SpeakerEmail).NotNull(TalkErrors.EmailRequired).NotEmpty(TalkErrors.EmailRequired);
            v.For(x => x.Capacity).InRange(1, 100, TalkErrors.CapacityOutOfRange);
        });

        (await ValidateAsync(validator, s_valid with { TalkTitle = title })).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Rule_NotAMemberOrNotAString_Throws()
    {
        FluentActions.Invoking(() => new TalkValidator(v => v.For(_ => s_valid))).Should().Throw<ArgumentException>().WithParameterName("member");
        FluentActions.Invoking(() => new TalkValidator(v => v.For(x => x.Capacity).Matches("1", TalkErrors.CapacityOutOfRange)))
            .Should().Throw<InvalidOperationException>().WithMessage("*capacity*");
    }

    private static async Task<Error> SingleErrorAsync(TalkValidator validator, ScheduleTalkCommand command) =>
        (await ValidateAsync(validator, command).ConfigureAwait(false)).Errors.Should().ContainSingle().Subject;

    private static ValueTask<Result> ValidateAsync(TalkValidator validator, ScheduleTalkCommand command) =>
        validator.ValidateAsync(command, TestContext.Current.CancellationToken);
}
