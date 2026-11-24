namespace YAF.Analyzers.ErrorCodes;

/// <summary>
/// A factory call that declares an error field: the kind (the factory's name), the code from <c>[ErrorCode]</c> or <see langword="null"/>,
/// the default code without the service code, and the interceptable location. Values only, so the incremental pipeline can cache it.
/// </summary>
internal sealed record ErrorFactoryCall(
    string Kind, string? ExplicitCode, string DefaultPath, int LocationVersion, string LocationData, string DisplayLocation);
