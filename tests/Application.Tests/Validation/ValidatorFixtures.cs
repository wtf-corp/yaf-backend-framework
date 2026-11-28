using System.Linq.Expressions;
using YAF.Application;
using YAF.Application.Validation;
using YAF.Core.Results;

namespace Application.Tests.Validation;

/// <summary>A command with a member of each shape the basic rules check.</summary>
internal sealed record ScheduleTalkCommand(string? TalkTitle, string? SpeakerEmail, int Capacity, Room? Room) : ICommand<Result>;

internal sealed record Room(string? RoomCode);

/// <summary>The errors of the fixtures, with the placeholders the rules fill.</summary>
internal abstract class TalkErrors : IErrorCatalog
{
    public static readonly ErrorDescriptor TitleRequired = Error.Validation("A title is required.");
    public static readonly ErrorDescriptor TitleTooLong = Error.Validation("The title has more than {MaxLength} characters.");
    public static readonly ErrorDescriptor TitleTooShort = Error.Validation("The title has fewer than {MinLength} characters.");
    public static readonly ErrorDescriptor EmailRequired = Error.Validation("An email address is required.");
    public static readonly ErrorDescriptor EmailInvalid = Error.Validation("The email address is not valid.");
    public static readonly ErrorDescriptor CapacityOutOfRange = Error.Validation("The capacity is between {Minimum} and {Maximum}.");
    public static readonly ErrorDescriptor RoomCodeInvalid = Error.Validation("The room code is not valid.");
}

/// <summary>A validator whose rules a test sets.</summary>
internal sealed class TalkValidator : Validator<ScheduleTalkCommand>
{
    public TalkValidator(Action<TalkValidator> rules) => rules(this);

    public RuleBuilder<ScheduleTalkCommand, TProperty> For<TProperty>(Expression<Func<ScheduleTalkCommand, TProperty>> member) => Rule(member);
}
