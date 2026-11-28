using YAF.Core.Ids;

namespace YAF.Application.Context;

/// <summary>Names the user who started an operation, by kind and id, for audit. It grants nothing.</summary>
/// <param name="Kind">The kind of principal.</param>
/// <param name="Id">The user's id.</param>
public sealed record PrincipalRef(PrincipalKind Kind, UserId Id);
