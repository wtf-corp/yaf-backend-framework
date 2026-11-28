namespace YAF.Application.Registration;

/// <summary>
/// Marks <see cref="StreamQueryRequest{TQuery}"/>, whose pipeline lasts as long as the caller enumerates the stream, so that a middleware
/// can tell it from a query that returns one result.
/// </summary>
internal interface IStreamQueryRequest;
