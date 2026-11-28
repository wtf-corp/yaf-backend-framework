using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Text.Json;
using YAF.Core.Results;

namespace YAF.Application.Validation;

/// <summary>
/// The base of a validator: declare its rules in the constructor with <see cref="Rule{TProperty}(Expression{Func{T, TProperty}})"/>,
/// each with an <see cref="ErrorDescriptor"/> of an <see cref="IErrorCatalog"/>; there is no inline message.
/// </summary>
/// <remarks>
/// Every rule runs, so that a failure lists every broken rule. Within one <c>Rule(…)</c> chain the checks run in order and the first that
/// fails ends the chain. An error's <see cref="Error.Target"/> is the camelCase member path, matching the JSON contract. Expressions are
/// compiled once per validator type, so <c>YAF.Application</c> is not AOT-compatible.
/// </remarks>
/// <typeparam name="T">The type of the input, typically a request.</typeparam>
public abstract class Validator<T> : IValidator<T>
{
    // Compiled readers by member type and path: each expression is compiled once per validator type (04 §4), not once per instance.
    private static readonly ConcurrentDictionary<(Type Member, string Path), Delegate> s_readers = new();

    private readonly List<Func<T, Error?>> _rules = [];

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException"><paramref name="instance"/> is <see langword="null"/>.</exception>
    public ValueTask<Result> ValidateAsync(T instance, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(instance);
        var errors = _rules.Select(rule => rule(instance)).OfType<Error>().ToList();
        return ValueTask.FromResult(errors.Count == 0 ? Result.Success() : Result.Failure(errors));
    }

    /// <summary>Starts a rule for a member of the input. Call it in the constructor and chain the checks to the builder it returns.</summary>
    /// <typeparam name="TProperty">The type of the member.</typeparam>
    /// <param name="member">The member, as a chain of members of the parameter, for example <c>x =&gt; x.Venue.Name</c>; its camelCase path
    /// (<c>venue.name</c>) is the target of the rule's errors. Reading a member of a <see langword="null"/> member on the way
    /// throws <see cref="NullReferenceException"/>: keep the chain to members that are never <see langword="null"/>.</param>
    /// <returns>The builder of the rule's checks.</returns>
    /// <exception cref="ArgumentException"><paramref name="member"/> is not a chain of members of its parameter.</exception>
    protected RuleBuilder<T, TProperty> Rule<TProperty>(Expression<Func<T, TProperty>> member)
    {
        ArgumentNullException.ThrowIfNull(member);
        var names = new List<string>();
        var node = member.Body;
        for (; node is MemberExpression access; node = access.Expression)
        {
            names.Insert(0, access.Member.Name);
        }

        if (names.Count == 0 || node != member.Parameters[0])
        {
            throw new ArgumentException($"A rule needs a member of the validated type, such as x => x.Title; '{member}' is not one.", nameof(member));
        }

        var read = (Func<T, TProperty>)s_readers.GetOrAdd((typeof(TProperty), string.Join('.', names)), static (_, m) => m.Compile(), member);
        var rule = new RuleBuilder<T, TProperty>(string.Join('.', names.Select(JsonNamingPolicy.CamelCase.ConvertName)), read);
        _rules.Add(rule.Check);
        return rule;
    }
}
