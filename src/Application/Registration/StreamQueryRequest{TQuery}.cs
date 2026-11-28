using YAF.Core.Results;

namespace YAF.Application.Registration;

/// <summary>
/// Carries a stream query through the query pipeline: the "all" and "queries" middleware are closed over this request, so they see it in
/// place of <typeparamref name="TQuery"/>. Its <see cref="Result"/> is the outcome of the whole enumeration.
/// </summary>
/// <typeparam name="TQuery">The stream query type.</typeparam>
/// <param name="Query">The stream query being enumerated.</param>
internal sealed record StreamQueryRequest<TQuery>(TQuery Query) : IQuery<Result>, IStreamQueryRequest;
