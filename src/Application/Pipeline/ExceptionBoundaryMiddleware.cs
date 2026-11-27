using Microsoft.Extensions.Logging;

namespace YAF.Application.Pipeline;

/// <summary>The log event that <see cref="ExceptionBoundaryMiddleware{TRequest, TResult}"/> shares across its closed types.</summary>
internal static partial class ExceptionBoundaryMiddleware
{
    public static partial class Log
    {
        [LoggerMessage(EventId = 1004, EventName = "Unhandled", Level = LogLevel.Error, Message = "Unhandled {ExceptionType} in {RequestName}")]
        public static partial void Unhandled(ILogger logger, Exception exception, string exceptionType, string requestName);
    }
}
