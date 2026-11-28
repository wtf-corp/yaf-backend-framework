using System.Collections;
using System.Text.RegularExpressions;
using YAF.Core.Results;

namespace YAF.Application.Validation;

/// <summary>
/// The checks of one rule of a <see cref="Validator{T}"/>, started by <c>Rule(x =&gt; x.Member)</c>. Each check takes the
/// <see cref="ErrorDescriptor"/> it fails with, at the rule's target, and returns this builder; the first check that fails ends the chain.
/// The <see cref="MustAsync"/> checks run after every synchronous check of the chain has passed, in the order they were added.
/// </summary>
/// <remarks>
/// The length and range checks pass their limits as the arguments <c>MaxLength</c>, <c>MinLength</c>, <c>Minimum</c> and <c>Maximum</c>,
/// which fill the placeholders of those names in the template. They and <see cref="Matches"/> let <see langword="null"/> pass: chain
/// <see cref="NotNull"/> or <see cref="NotEmpty"/> first when the member is required.
/// </remarks>
/// <typeparam name="T">The type of the validated input.</typeparam>
/// <typeparam name="TProperty">The type of the member the rule checks.</typeparam>
public sealed class RuleBuilder<T, TProperty>
{
    // Bounds the time a pattern may take on one value, so that catastrophic backtracking cannot hold a request.
    private static readonly TimeSpan s_matchTimeout = TimeSpan.FromSeconds(1);

    private readonly string _target;
    private readonly List<(Func<T, TProperty, bool> Holds, Error Failure)> _checks = [];
    private readonly List<(Func<TProperty, CancellationToken, ValueTask<bool>> Holds, Error Failure)> _asyncChecks = [];

    internal RuleBuilder(string target) => _target = target;

    /// <summary>Fails when the value is <see langword="null"/>.</summary>
    /// <param name="error">The error to fail with.</param>
    public RuleBuilder<T, TProperty> NotNull(ErrorDescriptor error) => Add(error, (_, value) => value is not null);

    /// <summary>Fails when the value is <see langword="null"/>, the default of its type, white space only, or a collection without items.</summary>
    /// <param name="error">The error to fail with.</param>
    public RuleBuilder<T, TProperty> NotEmpty(ErrorDescriptor error) => Add(error, (_, value) => !IsEmpty(value));

    /// <summary>Fails when a string has more than <paramref name="maxLength"/> characters, or a collection more items.</summary>
    /// <param name="maxLength">The largest length that holds.</param>
    /// <param name="error">The error to fail with, with argument <c>MaxLength</c>.</param>
    /// <exception cref="InvalidOperationException">The member is neither a string nor a collection.</exception>
    public RuleBuilder<T, TProperty> MaxLength(int maxLength, ErrorDescriptor error)
    {
        RequireMember(typeof(IEnumerable), nameof(MaxLength));
        return Add(error, (_, value) => value is null || Length(value) <= maxLength, ("MaxLength", maxLength));
    }

    /// <summary>Fails when a string has fewer than <paramref name="minLength"/> characters, or a collection fewer items.</summary>
    /// <param name="minLength">The smallest length that holds.</param>
    /// <param name="error">The error to fail with, with argument <c>MinLength</c>.</param>
    /// <exception cref="InvalidOperationException">The member is neither a string nor a collection.</exception>
    public RuleBuilder<T, TProperty> MinLength(int minLength, ErrorDescriptor error)
    {
        RequireMember(typeof(IEnumerable), nameof(MinLength));
        return Add(error, (_, value) => value is null || Length(value) >= minLength, ("MinLength", minLength));
    }

    /// <summary>Fails when the value is below <paramref name="minimum"/> or above <paramref name="maximum"/>; both bounds hold.</summary>
    /// <param name="minimum">The smallest value that holds.</param>
    /// <param name="maximum">The largest value that holds.</param>
    /// <param name="error">The error to fail with, with arguments <c>Minimum</c> and <c>Maximum</c>.</param>
    public RuleBuilder<T, TProperty> InRange(TProperty minimum, TProperty maximum, ErrorDescriptor error)
    {
        var order = Comparer<TProperty>.Default;
        return Add(error, (_, v) => v is null || (order.Compare(v, minimum) >= 0 && order.Compare(v, maximum) <= 0),
            ("Minimum", minimum), ("Maximum", maximum));
    }

    /// <summary>Fails when a string does not match the regular expression, matched culture-invariantly within one second.</summary>
    /// <param name="pattern">The regular expression, for example <c>^[A-Z]{3}$</c>.</param>
    /// <param name="error">The error to fail with.</param>
    /// <exception cref="InvalidOperationException">The member is not a string.</exception>
    /// <exception cref="RegexMatchTimeoutException">At validation: matching took longer than the time limit.</exception>
    public RuleBuilder<T, TProperty> Matches(string pattern, ErrorDescriptor error)
    {
        ArgumentException.ThrowIfNullOrEmpty(pattern);
        RequireMember(typeof(string), nameof(Matches));
        return Add(
            error, (_, value) => value is not string text || Regex.IsMatch(text, pattern, RegexOptions.CultureInvariant, s_matchTimeout));
    }

    /// <summary>Fails when the predicate returns <see langword="false"/> for the value, which may be <see langword="null"/>.</summary>
    /// <param name="predicate">The condition the value must meet.</param>
    /// <param name="error">The error to fail with.</param>
    public RuleBuilder<T, TProperty> Must(Func<TProperty, bool> predicate, ErrorDescriptor error) =>
        Add(error, predicate is null ? throw new ArgumentNullException(nameof(predicate)) : (_, value) => predicate(value));

    /// <summary>Fails when the predicate returns <see langword="false"/> for the whole input and the value: a check across members.</summary>
    /// <param name="predicate">The condition, given the input and the value, which may be <see langword="null"/>.</param>
    /// <param name="error">The error to fail with.</param>
    public RuleBuilder<T, TProperty> Must(Func<T, TProperty, bool> predicate, ErrorDescriptor error) =>
        Add(error, predicate ?? throw new ArgumentNullException(nameof(predicate)));

    /// <summary>
    /// Fails when the asynchronous predicate returns <see langword="false"/> for the value, for example a check that reads the store.
    /// It runs only after every synchronous check of the chain has passed.
    /// </summary>
    /// <param name="predicate">The condition the value must meet, given the value, which may be <see langword="null"/>, and the token that
    /// cancels the validation.</param>
    /// <param name="error">The error to fail with.</param>
    public RuleBuilder<T, TProperty> MustAsync(Func<TProperty, CancellationToken, ValueTask<bool>> predicate, ErrorDescriptor error)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        ArgumentNullException.ThrowIfNull(error);
        _asyncChecks.Add((predicate, error.ToError()));
        return this;
    }

    /// <summary>Runs the checks in order; returns the error of the first that fails, at <paramref name="target"/>, or <see langword="null"/>.</summary>
    internal async ValueTask<Error?> CheckAsync(T instance, TProperty value, string target, CancellationToken ct)
    {
        foreach (var (holds, failure) in _checks)
        {
            if (!holds(instance, value))
            {
                return failure with { Target = target };
            }
        }

        foreach (var (holds, failure) in _asyncChecks)
        {
            if (!await holds(value, ct).ConfigureAwait(false))
            {
                return failure with { Target = target };
            }
        }

        return null;
    }

    private static bool IsEmpty(TProperty value) => value switch
    {
        null => true,
        string text => string.IsNullOrWhiteSpace(text),
        IEnumerable items => !items.Cast<object?>().Any(),
        _ => EqualityComparer<TProperty>.Default.Equals(value, default),
    };

    private static int Length(TProperty value) => value switch
    {
        string text => text.Length,
        _ => ((IEnumerable)value!).Cast<object?>().Count(),
    };

    private void RequireMember(Type type, string check)
    {
        if (!type.IsAssignableFrom(typeof(TProperty)))
        {
            throw new InvalidOperationException($"{check} cannot check '{_target}', a {typeof(TProperty).Name}: it needs a {type.Name}.");
        }
    }

    private RuleBuilder<T, TProperty> Add(ErrorDescriptor error, Func<T, TProperty, bool> holds, params (string Name, object? Value)[] arguments)
    {
        ArgumentNullException.ThrowIfNull(error);
        _checks.Add((holds, arguments.Length == 0 ? error.ToError() : error.With(arguments)));
        return this;
    }
}
