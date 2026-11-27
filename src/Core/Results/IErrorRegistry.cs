namespace YAF.Core.Results;

/// <summary>
/// The list of every error an assembly declares, one <see cref="ErrorRegistration"/> per <see cref="IErrorCatalog"/> field. The error
/// registry generator implements it for each assembly and names the implementation in <see cref="YafServiceAttribute"/>.
/// </summary>
/// <remarks>
/// Do not implement it by hand in an application. Readers find the registry through the assembly's <see cref="YafServiceAttribute"/> and
/// create it with its public parameterless constructor.
/// </remarks>
public interface IErrorRegistry
{
    /// <summary>Gets the registrations of the assembly's errors, in declaration order; never <see langword="null"/>.</summary>
    IReadOnlyList<ErrorRegistration> Errors { get; }
}
