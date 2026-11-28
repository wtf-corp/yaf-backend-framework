using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Text.Json;
using YAF.Core.Results;

namespace YAF.Application.Validation;

/// <summary>
/// The base of a validator: declare its rules in the constructor with <see cref="Rule{TProperty}(Expression{Func{T, TProperty}})"/>,
/// <see cref="RuleForEach{TItem}"/>, <see cref="When"/>, <see cref="Include"/> and <see cref="Nested{TChild}"/>, each check with an
/// <see cref="ErrorDescriptor"/> of an <see cref="IErrorCatalog"/>; there is no inline message.
/// </summary>
/// <remarks>
/// Every rule runs, so that a failure lists every broken rule. Within one <c>Rule(…)</c> chain the checks run in order and the first that
/// fails ends the chain. An error's <see cref="Error.Target"/> is the camelCase member path, matching the JSON contract, with the index of
/// an item (<c>speakers[1]</c>) and the path of a nested validator's member after its parent's (<c>venue.name</c>). Expressions are
/// compiled once per validator type, so <c>YAF.Application</c> is not AOT-compatible.
/// </remarks>
/// <typeparam name="T">The type of the input, typically a request.</typeparam>
public abstract class Validator<T> : IValidator<T>
{
    // Compiled readers by member type and path: each expression is compiled once per validator type (04 §4), not once per instance.
    private static readonly ConcurrentDictionary<(Type Member, string Path), Delegate> s_readers = new();

    private readonly List<Func<T, CancellationToken, ValueTask<IReadOnlyList<Error>>>> _rules = [];

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException"><paramref name="instance"/> is <see langword="null"/>.</exception>
    public async ValueTask<Result> ValidateAsync(T instance, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(instance);
        var errors = await RunAsync(_rules, instance, ct).ConfigureAwait(false);
        return errors.Count == 0 ? Result.Success() : Result.Failure(errors);
    }

    /// <summary>Starts a rule for a member of the input. Call it in the constructor and chain the checks to the builder it returns.</summary>
    /// <typeparam name="TProperty">The type of the member.</typeparam>
    /// <param name="member">The member, as a chain of members of the parameter, for example <c>x =&gt; x.Venue.Name</c>; its camelCase path
    /// (<c>venue.name</c>) is the target of the rule's errors. Reading a member of a <see langword="null"/> member on the way
    /// throws <see cref="NullReferenceException"/>: keep the chain to members that are never <see langword="null"/>, or guard it with
    /// <see cref="When"/>.</param>
    /// <returns>The builder of the rule's checks.</returns>
    /// <exception cref="ArgumentException"><paramref name="member"/> is not a chain of members of its parameter.</exception>
    protected RuleBuilder<T, TProperty> Rule<TProperty>(Expression<Func<T, TProperty>> member)
    {
        var (target, read) = Reader(member);
        var rule = new RuleBuilder<T, TProperty>(target);
        _rules.Add(async (instance, ct) => await rule.CheckAsync(instance, read(instance), target, ct).ConfigureAwait(false) is { } error
            ? [error]
            : []);
        return rule;
    }

    /// <summary>
    /// Starts a rule for each item of a collection member of the input; a <see langword="null"/> collection has no items. Every item is
    /// checked, and an error's target carries the item's index, for example <c>speakers[1]</c>.
    /// </summary>
    /// <typeparam name="TItem">The type of the items.</typeparam>
    /// <param name="member">The collection member, as for <see cref="Rule{TProperty}(Expression{Func{T, TProperty}})"/>.</param>
    /// <returns>The builder of the checks of one item.</returns>
    /// <exception cref="ArgumentException"><paramref name="member"/> is not a chain of members of its parameter.</exception>
    protected RuleBuilder<T, TItem> RuleForEach<TItem>(Expression<Func<T, IEnumerable<TItem>>> member)
    {
        var (target, read) = Reader(member);
        var rule = new RuleBuilder<T, TItem>(target);
        _rules.Add(async (instance, ct) =>
        {
            var errors = new List<Error>();
            var index = 0;
            foreach (var item in read(instance) ?? [])
            {
                if (await rule.CheckAsync(instance, item, $"{target}[{index}]", ct).ConfigureAwait(false) is { } error)
                {
                    errors.Add(error);
                }

                index++;
            }

            return errors;
        });
        return rule;
    }

    /// <summary>Applies the rules that <paramref name="rules"/> declares only to an input that meets <paramref name="condition"/>.</summary>
    /// <param name="condition">The condition under which the rules apply.</param>
    /// <param name="rules">Declares the rules, with the methods of this class, for example
    /// <c>() =&gt; Rule(x =&gt; x.EndsAt).Must(…)</c>.</param>
    protected void When(Func<T, bool> condition, Action rules)
    {
        ArgumentNullException.ThrowIfNull(condition);
        ArgumentNullException.ThrowIfNull(rules);
        var first = _rules.Count;
        rules();
        var conditional = _rules[first..];
        _rules.RemoveRange(first, conditional.Count);
        _rules.Add((instance, ct) => condition(instance) ? RunAsync(conditional, instance, ct) : ValueTask.FromResult<IReadOnlyList<Error>>([]));
    }

    /// <summary>Adds the rules of another validator of the same input; its errors keep their targets.</summary>
    /// <param name="validator">The validator whose errors to add.</param>
    protected void Include(IValidator<T> validator)
    {
        ArgumentNullException.ThrowIfNull(validator);
        _rules.Add(async (instance, ct) => (await validator.ValidateAsync(instance, ct).ConfigureAwait(false)).Errors);
    }

    /// <summary>
    /// Validates a member of the input with the validator of its type, when the member is not <see langword="null"/>. Each error's target
    /// is the member's path followed by the error's own target, for example <c>venue.name</c>.
    /// </summary>
    /// <typeparam name="TChild">The type of the member.</typeparam>
    /// <param name="member">The member, as for <see cref="Rule{TProperty}(Expression{Func{T, TProperty}})"/>.</param>
    /// <param name="validator">The validator of the member's type.</param>
    /// <exception cref="ArgumentException"><paramref name="member"/> is not a chain of members of its parameter.</exception>
    protected void Nested<TChild>(Expression<Func<T, TChild?>> member, IValidator<TChild> validator)
    {
        ArgumentNullException.ThrowIfNull(validator);
        var (target, read) = Reader(member);
        _rules.Add(async (instance, ct) =>
        {
            if (read(instance) is not { } child)
            {
                return [];
            }

            var result = await validator.ValidateAsync(child, ct).ConfigureAwait(false);
            return [.. result.Errors.Select(error => error with { Target = error.Target is null ? target : $"{target}.{error.Target}" })];
        });
    }

    private static async ValueTask<IReadOnlyList<Error>> RunAsync(
        IEnumerable<Func<T, CancellationToken, ValueTask<IReadOnlyList<Error>>>> rules, T instance, CancellationToken ct)
    {
        var errors = new List<Error>();
        foreach (var rule in rules)
        {
            errors.AddRange(await rule(instance, ct).ConfigureAwait(false));
        }

        return errors;
    }

    private static (string Target, Func<T, TMember> Read) Reader<TMember>(Expression<Func<T, TMember>> member)
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

        var read = (Func<T, TMember>)s_readers.GetOrAdd((typeof(TMember), string.Join('.', names)), static (_, m) => m.Compile(), member);
        return (string.Join('.', names.Select(JsonNamingPolicy.CamelCase.ConvertName)), read);
    }
}
