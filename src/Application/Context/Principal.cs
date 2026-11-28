using YAF.Core.Ids;

namespace YAF.Application.Context;

/// <summary>Who acts in a scope: an anonymous caller, a user, another service, or the service itself for a reason.</summary>
public sealed class Principal
{
    private Principal(PrincipalKind kind, UserId? userId, string? name)
    {
        Kind = kind;
        UserId = userId;
        Name = name;
    }

    /// <summary>Gets the unauthenticated caller.</summary>
    public static Principal Anonymous { get; } = new(PrincipalKind.Anonymous, null, null);

    /// <summary>Gets the kind of principal.</summary>
    public PrincipalKind Kind { get; }

    /// <summary>Gets the user's id for a <see cref="PrincipalKind.User"/>; otherwise <see langword="null"/>.</summary>
    public UserId? UserId { get; }

    /// <summary>Gets the name of a service or the reason of a system principal; otherwise <see langword="null"/>.</summary>
    public string? Name { get; }

    /// <summary>Creates the principal of an authenticated user.</summary>
    /// <param name="id">The user's id.</param>
    public static Principal User(UserId id) => new(PrincipalKind.User, id, null);

    /// <summary>Creates the principal of another service.</summary>
    /// <param name="name">The service's name; required.</param>
    /// <exception cref="ArgumentException"><paramref name="name"/> is null, empty or white space.</exception>
    public static Principal Service(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return new(PrincipalKind.Service, null, name);
    }

    /// <summary>Creates the principal of the service itself, acting for a reason that is audited, such as <c>cron:purge-retention</c>.</summary>
    /// <param name="reason">Why the service acts; required.</param>
    /// <exception cref="ArgumentException"><paramref name="reason"/> is null, empty or white space.</exception>
    public static Principal System(string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        return new(PrincipalKind.System, null, reason);
    }
}
