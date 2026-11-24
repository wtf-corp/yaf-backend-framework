using System.Globalization;
using System.Text;

namespace YAF.Core.Results;

/// <summary>
/// The declaration of an error: its code, its kind and its English message template. Declare one as a <c>static readonly</c> field of an
/// <see cref="IErrorCatalog"/>, initialised by a factory of <see cref="Error"/>, and turn it into an <see cref="Error"/> where it is returned.
/// </summary>
/// <remarks>
/// The template may contain <c>{Name}</c> placeholders. Values the user sent appear in the message only through them, filled by
/// <see cref="With(ReadOnlySpan{ValueTuple{string, object}})"/>.
/// </remarks>
/// <example>
/// <code language="csharp">
/// return Session.Errors.Full.With(("Capacity", capacity));
/// </code>
/// </example>
/// <param name="Code">The error code, lower-case <c>[a-z0-9_.]</c>, for example <c>catalog.session.full</c>. Clients match on it.</param>
/// <param name="Kind">The kind of the error, which decides the HTTP status.</param>
/// <param name="MessageTemplate">The English message, safe to show to an end user, with optional <c>{Name}</c> placeholders.</param>
public sealed record ErrorDescriptor(string Code, ErrorKind Kind, string MessageTemplate)
{
    /// <summary>Creates the error with the message template as its message, no target and no arguments.</summary>
    /// <returns>The error.</returns>
    public Error ToError() => new(Code, Kind, MessageTemplate);

    /// <summary>
    /// Creates the error with each <c>{Name}</c> placeholder of the template filled from the argument of that name (ordinal comparison).
    /// A placeholder without an argument stays as it is. Values are formatted with the invariant culture; a <see langword="null"/> value
    /// is written as an empty string.
    /// </summary>
    /// <param name="args">The template arguments as name and value pairs. They are kept, as given, in <see cref="Error.Arguments"/>; a
    /// later argument with the same name replaces an earlier one.</param>
    /// <returns>The error with the filled message and the arguments.</returns>
    public Error With(params ReadOnlySpan<(string Name, object? Value)> args)
    {
        var arguments = new Dictionary<string, object?>(args.Length, StringComparer.Ordinal);
        foreach (var (name, value) in args)
        {
            arguments[name] = value;
        }

        return new Error(Code, Kind, Fill(MessageTemplate, arguments)) { Arguments = arguments.AsReadOnly() };
    }

    /// <summary>Creates the error with the message template as its message and the given target.</summary>
    /// <param name="target">The property path the error is about, for example <c>speakers[1].email</c>.</param>
    /// <returns>The error with <see cref="Error.Target"/> set.</returns>
    public Error At(string target) => new(Code, Kind, MessageTemplate) { Target = target };

    private static string Fill(string template, Dictionary<string, object?> arguments)
    {
        var message = new StringBuilder(template.Length);
        var position = 0;
        while (position < template.Length)
        {
            var open = template.IndexOf('{', position, StringComparison.Ordinal);
            var close = open < 0 ? -1 : template.IndexOf('}', open + 1, StringComparison.Ordinal);
            if (close < 0)
            {
                break;
            }

            message.Append(template, position, open - position);
            if (arguments.TryGetValue(template[(open + 1)..close], out var value))
            {
                message.Append(Format(value));
            }
            else
            {
                message.Append(template, open, close - open + 1);
            }

            position = close + 1;
        }

        return message.Append(template, position, template.Length - position).ToString();
    }

    private static string? Format(object? value) =>
        value is IFormattable formattable ? formattable.ToString(null, CultureInfo.InvariantCulture) : value?.ToString();
}
