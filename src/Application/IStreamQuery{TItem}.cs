namespace YAF.Application;

/// <summary>
/// A read whose items are streamed through <see cref="IDispatcher.StreamAsync{TItem}"/> instead of returned at once. The query pipeline
/// holds its read-only transaction while the stream is enumerated.
/// </summary>
/// <typeparam name="TItem">The type of the streamed items.</typeparam>
/// <remarks>Exactly one <see cref="IStreamQueryHandler{TQuery, TItem}"/> handles each stream query type.</remarks>
public interface IStreamQuery<TItem>;
