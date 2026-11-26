using System.Linq.Expressions;

namespace YAF.Domain.Specifications;

/// <summary>
/// A named condition on <typeparamref name="T"/>, written once as an expression and used both to check a candidate in memory and, by a
/// repository, to query the store.
/// </summary>
/// <remarks>
/// A specification compares typed members (a status with the enum value, an id with a typed id) and holds values, not services: pass the time
/// in. <see cref="And"/>, <see cref="Or"/> and <see cref="Not"/> compose into one expression with a single parameter, so that a query provider
/// sees an ordinary lambda. <see cref="IsSatisfiedBy"/> compiles the expression at run time, which is why <c>YAF.Domain</c> makes no AOT claim.
/// </remarks>
/// <example>
/// <code language="csharp">
/// public sealed class OpenSessions(DateTimeOffset now) : Specification&lt;Session.IMemento&gt;
/// {
///     public override Expression&lt;Func&lt;Session.IMemento, bool&gt;&gt; ToExpression()
///         =&gt; s =&gt; s.Status == SessionStatus.Published &amp;&amp; s.StartsAt &gt; now;
/// }
/// bool isOpen = new OpenSessions(clock.GetUtcNow()).And(new InTrack(trackId)).IsSatisfiedBy(memento);
/// </code>
/// </example>
/// <typeparam name="T">The type the condition applies to; normally the aggregate's memento interface.</typeparam>
public abstract class Specification<T>
{
    private Func<T, bool>? _compiled;

    /// <summary>Initializes a new instance of the <see cref="Specification{T}"/> class.</summary>
    protected Specification()
    {
    }

    /// <summary>Returns the condition as an expression.</summary>
    /// <returns>
    /// A lambda with one parameter. Use only what a query provider can translate when the specification is used to query: equality on typed
    /// ids, enums and smart enumerations, comparison on single-value value objects, member access on multi-value ones.
    /// </returns>
    public abstract Expression<Func<T, bool>> ToExpression();

    /// <summary>Checks whether a candidate meets the condition; the first call compiles <see cref="ToExpression"/>, later calls reuse it.</summary>
    /// <param name="candidate">The candidate to check.</param>
    /// <returns><see langword="true"/> when the candidate meets the condition.</returns>
    public bool IsSatisfiedBy(T candidate) => (_compiled ??= ToExpression().Compile())(candidate);

    /// <summary>Combines this specification with another that must hold as well.</summary>
    /// <param name="other">The specification that must hold too.</param>
    /// <returns>A specification that holds when both hold; its expression has one parameter.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is <see langword="null"/>.</exception>
    public Specification<T> And(Specification<T> other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return new AndSpecification<T>(this, other);
    }

    /// <summary>Combines this specification with another, either of which may hold.</summary>
    /// <param name="other">The alternative specification.</param>
    /// <returns>A specification that holds when either holds; its expression has one parameter.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is <see langword="null"/>.</exception>
    public Specification<T> Or(Specification<T> other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return new OrSpecification<T>(this, other);
    }

    /// <summary>Inverts this specification.</summary>
    /// <returns>A specification that holds when this one does not.</returns>
    public Specification<T> Not() => new NotSpecification<T>(this);
}
