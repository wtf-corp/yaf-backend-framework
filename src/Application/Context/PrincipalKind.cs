namespace YAF.Application.Context;

/// <summary>The kinds of principal that can act in a scope.</summary>
public enum PrincipalKind
{
    /// <summary>An unauthenticated caller.</summary>
    Anonymous,

    /// <summary>An authenticated user.</summary>
    User,

    /// <summary>Another service, authenticated as itself.</summary>
    Service,

    /// <summary>The service itself, acting for a stated reason, such as a job or a cron trigger.</summary>
    System,
}
