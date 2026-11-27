using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;
using YAF.Application.Registration;
using YAF.Core.Results;

namespace YAF.Application.Pipeline;

/// <summary>What <see cref="TelemetryMiddleware{TRequest, TResult}"/> shares across its closed types: source, meter, names, log events.</summary>
internal static partial class TelemetryMiddleware
{
    private const string Name = "YAF.Application";

    private static readonly string? s_version = typeof(TelemetryMiddleware).Assembly.GetName().Version?.ToString();

    public static ActivitySource Source { get; } = new(Name, s_version);

    /// <summary>Meter <c>YAF.Application</c> of the factory, which owns it and disposes it with the host.</summary>
    public static Meter CreateMeter(IMeterFactory meterFactory) => meterFactory.Create(Name, s_version);

    /// <summary>The request's type name; for a stream query the query's rather than that of the request that carries it.</summary>
    public static string RequestName(Type request) =>
        request.IsGenericType && request.GetGenericTypeDefinition() == typeof(StreamQueryRequest<>)
            ? request.GetGenericArguments()[0].Name
            : request.Name;

    /// <summary>The outcome of a failure: the kind of its first error, in lower case with underscores.</summary>
    public static string Outcome(ErrorKind kind) => kind switch
    {
        ErrorKind.Validation => "validation",
        ErrorKind.Unauthenticated => "unauthenticated",
        ErrorKind.Forbidden => "forbidden",
        ErrorKind.NotFound => "not_found",
        ErrorKind.Conflict => "conflict",
        ErrorKind.PreconditionFailed => "precondition_failed",
        ErrorKind.RateLimited => "rate_limited",
        ErrorKind.Unavailable => "unavailable",
        _ => "failure",
    };

    public static partial class Log
    {
        [LoggerMessage(EventId = 1001, EventName = "Handling", Level = LogLevel.Debug, Message = "Handling {RequestName}")]
        public static partial void Handling(ILogger logger, string requestName);

        [LoggerMessage(EventId = 1002, EventName = "Handled", Level = LogLevel.Debug, Message = "Handled {RequestName}")]
        public static partial void Handled(ILogger logger, string requestName);

        [LoggerMessage(EventId = 1003, EventName = "Failed", Message = "Failed {RequestName} with {ErrorCodes}")]
        public static partial void Failed(ILogger logger, LogLevel level, string requestName, string errorCodes);
    }
}
