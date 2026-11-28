using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using YAF.Core.Ids;

namespace YAF.Persistence.EfCore.Conventions;

/// <summary>Stores a typed id as the <see cref="Guid"/> it wraps, a <c>uuid</c> column on PostgreSQL.</summary>
/// <typeparam name="TId">The typed id.</typeparam>
/// <remarks>
/// A <see cref="YafDbContext"/> applies it by convention to every typed id member of its data classes and to their nullable forms; a
/// service does not configure it.
/// </remarks>
/// <example>
/// <code language="csharp">
/// var stored = new TypedIdConverter&lt;SessionId&gt;().ConvertToProvider(sessionId);
/// </code>
/// </example>
public sealed class TypedIdConverter<TId> : ValueConverter<TId, Guid>
    where TId : struct, ITypedId<TId>
{
    /// <summary>Initializes a new instance.</summary>
    public TypedIdConverter()
        : base(id => id.Value, value => Restore(value))
    {
    }

    // An expression tree cannot name a static abstract member (CS8927), so the conversion calls this method (research §5).
    private static TId Restore(Guid value) => TId.From(value);
}
