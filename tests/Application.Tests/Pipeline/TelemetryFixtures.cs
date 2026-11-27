using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;
using YAF.Application;
using YAF.Core.Results;

namespace Application.Tests.Pipeline;

/// <summary>A command whose member holds a value that must never reach the telemetry.</summary>
internal sealed record RenameSpeakerCommand(string NewName) : ICommand<Result>;

/// <summary>A query.</summary>
internal sealed record FindSpeakerQuery : IQuery<Result<string>>;

/// <summary>One error of each kind the middleware treats differently.</summary>
internal abstract class SpeakerErrors : IErrorCatalog
{
    public static readonly ErrorDescriptor Unreachable = Error.Failure("Speaker {Name} cannot be reached.");

    public static readonly ErrorDescriptor NotFound = Error.NotFound("No such speaker.");

    public static readonly ErrorDescriptor NameRequired = Error.Validation("A name is required.");

    public static readonly ErrorDescriptor DirectoryDown = Error.Unavailable("The speaker directory is down.");
}

/// <summary>One log record: level, event, rendered message and the structured values.</summary>
internal sealed record LogRecord(LogLevel Level, EventId Id, string Message, IReadOnlyList<KeyValuePair<string, object?>> State);

/// <summary>A logger that keeps every record.</summary>
internal sealed class RecordingLogger<T> : ILogger<T>
{
    public List<LogRecord> Records { get; } = [];

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
        Records.Add(new LogRecord(logLevel, eventId, formatter(state, exception), [.. state as IReadOnlyList<KeyValuePair<string, object?>> ?? []]));
}

/// <summary>
/// Creates meters owned by itself, as the host's factory does, and collects this factory's <c>yaf.request.duration</c> measurements and
/// every stopped activity of source <c>YAF.Application</c>.
/// </summary>
internal sealed class TelemetryCapture : IMeterFactory
{
    private readonly List<Meter> _meters = [];
    private readonly ActivityListener _activities;
    private readonly MeterListener _measurements = new();

    public TelemetryCapture()
    {
        _activities = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "YAF.Application",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity =>
            {
                lock (Activities)
                {
                    Activities.Add(activity);
                }
            },
        };
        ActivitySource.AddActivityListener(_activities);
        _measurements.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Scope == this && instrument.Meter.Name == "YAF.Application" && instrument.Name == "yaf.request.duration")
            {
                Unit = instrument.Unit;
                listener.EnableMeasurementEvents(instrument);
            }
        };
        _measurements.SetMeasurementEventCallback<double>((_, value, tags, _) => Durations.Add((value, tags.ToArray())));
        _measurements.Start();
    }

    public List<Activity> Activities { get; } = [];

    public List<(double Value, KeyValuePair<string, object?>[] Tags)> Durations { get; } = [];

    public string? Unit { get; private set; }

    /// <summary>The activities of one request type, so that tests running in parallel do not see each other's.</summary>
    public Activity[] Of(string requestName)
    {
        lock (Activities)
        {
            return [.. Activities.Where(activity => activity.DisplayName == $"yaf.request {requestName}")];
        }
    }

    public Meter Create(MeterOptions options)
    {
        options.Scope = this;
        var meter = new Meter(options);
        _meters.Add(meter);
        return meter;
    }

    public void Dispose()
    {
        _activities.Dispose();
        _measurements.Dispose();
        _meters.ForEach(meter => meter.Dispose());
    }
}
