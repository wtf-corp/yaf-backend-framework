namespace YAF.Application.Pipeline;

/// <summary>
/// Registers the request middleware explicitly and in order, outermost first. The order becomes the service's
/// <see cref="IPipelineDescriptor"/>.
/// </summary>
/// <remarks>
/// A middleware type is either an open generic type with the two type parameters of
/// <see cref="IRequestMiddleware{TRequest, TResult}"/> (<c>typeof(TenantQuotaMiddleware&lt;,&gt;)</c>), which runs for every request in its
/// scope, or a closed type for one request. A type that is not a middleware, or an anchor that is not registered, throws while the pipeline
/// is built, so the service fails at startup.
/// </remarks>
/// <example>
/// <code>
/// app.UsePipeline(p => p
///     .Use(typeof(TenantQuotaMiddleware&lt;,&gt;))
///     .InsertBefore(typeof(TenantQuotaMiddleware&lt;,&gt;), typeof(AuditTrailMiddleware&lt;,&gt;))
///     .ForCommands(c => c.Use&lt;CreateSessionMiddleware&gt;()));
/// </code>
/// </example>
public sealed class PipelineBuilder
{
    private readonly List<PipelineStep> _steps;
    private readonly PipelineScope _scope;

    internal PipelineBuilder()
        : this([], PipelineScope.All)
    {
    }

    private PipelineBuilder(List<PipelineStep> steps, PipelineScope scope)
    {
        _steps = steps;
        _scope = scope;
    }

    /// <summary>Appends a middleware as the innermost step so far.</summary>
    /// <param name="middleware">The middleware type, open generic or closed.</param>
    /// <returns>This builder.</returns>
    /// <exception cref="ArgumentException"><paramref name="middleware"/> is not a middleware type.</exception>
    public PipelineBuilder Use(Type middleware)
    {
        _steps.Add(CreateStep(middleware));
        return this;
    }

    /// <summary>Appends a closed middleware type as the innermost step so far.</summary>
    /// <typeparam name="TMiddleware">The middleware type.</typeparam>
    /// <returns>This builder.</returns>
    /// <exception cref="ArgumentException"><typeparamref name="TMiddleware"/> is not a middleware type.</exception>
    public PipelineBuilder Use<TMiddleware>() => Use(typeof(TMiddleware));

    /// <summary>Inserts a middleware directly inside a registered one.</summary>
    /// <param name="existing">The registered middleware type, as it was registered (an open generic type for a generic middleware).</param>
    /// <param name="middleware">The middleware type to insert, open generic or closed.</param>
    /// <returns>This builder.</returns>
    /// <exception cref="ArgumentException"><paramref name="middleware"/> is not a middleware type.</exception>
    /// <exception cref="InvalidOperationException"><paramref name="existing"/> is not registered.</exception>
    public PipelineBuilder InsertAfter(Type existing, Type middleware)
    {
        var step = CreateStep(middleware);
        _steps.Insert(IndexOf(existing) + 1, step);
        return this;
    }

    /// <summary>Inserts a middleware directly outside a registered one.</summary>
    /// <param name="existing">The registered middleware type, as it was registered (an open generic type for a generic middleware).</param>
    /// <param name="middleware">The middleware type to insert, open generic or closed.</param>
    /// <returns>This builder.</returns>
    /// <exception cref="ArgumentException"><paramref name="middleware"/> is not a middleware type.</exception>
    /// <exception cref="InvalidOperationException"><paramref name="existing"/> is not registered.</exception>
    public PipelineBuilder InsertBefore(Type existing, Type middleware)
    {
        var step = CreateStep(middleware);
        _steps.Insert(IndexOf(existing), step);
        return this;
    }

    /// <summary>Registers middleware that runs only for commands, in the same order as the other steps.</summary>
    /// <param name="configure">Registers the steps; anchors may be any registered step.</param>
    /// <returns>This builder.</returns>
    /// <exception cref="InvalidOperationException">This builder is already scoped to queries.</exception>
    public PipelineBuilder ForCommands(Action<PipelineBuilder> configure) => For(PipelineScope.Commands, configure);

    /// <summary>Registers middleware that runs only for queries, in the same order as the other steps.</summary>
    /// <param name="configure">Registers the steps; anchors may be any registered step.</param>
    /// <returns>This builder.</returns>
    /// <exception cref="InvalidOperationException">This builder is already scoped to commands.</exception>
    public PipelineBuilder ForQueries(Action<PipelineBuilder> configure) => For(PipelineScope.Queries, configure);

    internal IPipelineDescriptor Build() => new PipelineDescriptor([.. _steps]);

    private PipelineBuilder For(PipelineScope scope, Action<PipelineBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        if (_scope != PipelineScope.All && _scope != scope)
        {
            throw new InvalidOperationException($"A pipeline scoped to {_scope} cannot be scoped to {scope}.");
        }

        configure(new PipelineBuilder(_steps, scope));
        return this;
    }

    private PipelineStep CreateStep(Type middleware)
    {
        ArgumentNullException.ThrowIfNull(middleware);
        if (!IsMiddleware(middleware))
        {
            throw new ArgumentException(
                $"{middleware} is not a request middleware: a non-abstract class implementing IRequestMiddleware<TRequest, TResult>.",
                nameof(middleware));
        }

        return new PipelineStep(middleware, _scope);
    }

    private int IndexOf(Type existing)
    {
        ArgumentNullException.ThrowIfNull(existing);
        var index = _steps.FindIndex(step => step.Middleware == existing);
        return index >= 0 ? index : throw new InvalidOperationException($"The pipeline has no step {existing} to insert next to.");
    }

    private static bool IsMiddleware(Type type)
    {
        if (!type.IsClass || type.IsAbstract)
        {
            return false;
        }

        return type.GetInterfaces().Any(contract =>
            contract.IsGenericType
            && contract.GetGenericTypeDefinition() == typeof(IRequestMiddleware<,>));
    }
}
