using DeviceSimulator.Abstractions;

namespace DeviceSimulator.Observability;

/// <summary>
/// OpenTelemetry trace sink placeholder.
/// Real implementation would use OpenTelemetry SDK.
/// </summary>
public class OpenTelemetryTraceSink : ITraceSink
{
    private readonly InMemoryTraceSink _fallback = new();
    private readonly string _serviceName;

    public OpenTelemetryTraceSink(string serviceName = "device-simulator")
    {
        _serviceName = serviceName;
    }

    public void Record(TraceEvent traceEvent)
    {
        // TODO: Integrate with OpenTelemetry SDK
        // For now, just use in-memory fallback
        _fallback.Record(traceEvent);

        // Placeholder: Would create OTel span here
        // using var span = tracer.StartSpan(traceEvent.EventType.ToString());
        // span.SetAttribute("device.id", traceEvent.DeviceId);
        // span.SetAttribute("session.id", traceEvent.SessionId);
    }

    public Task RecordAsync(TraceEvent traceEvent, CancellationToken cancellationToken = default)
    {
        Record(traceEvent);
        return Task.CompletedTask;
    }

    public IReadOnlyList<TraceEvent> GetRecent(int count = 100, TraceEventFilter? filter = null)
    {
        return _fallback.GetRecent(count, filter);
    }
}

/// <summary>
/// Factory for creating trace sinks.
/// </summary>
public static class TraceSinkFactory
{
    public static ITraceSink CreateInMemory(int maxEvents = 10000)
    {
        return new InMemoryTraceSink(maxEvents);
    }

    public static ITraceSink CreateOpenTelemetry(string serviceName = "device-simulator")
    {
        return new OpenTelemetryTraceSink(serviceName);
    }

    public static ITraceSink CreateComposite(params ITraceSink[] sinks)
    {
        return new CompositeTraceSink(sinks);
    }
}

/// <summary>
/// Composite trace sink that writes to multiple sinks.
/// </summary>
public class CompositeTraceSink : ITraceSink
{
    private readonly ITraceSink[] _sinks;

    public CompositeTraceSink(params ITraceSink[] sinks)
    {
        _sinks = sinks;
    }

    public void Record(TraceEvent traceEvent)
    {
        foreach (var sink in _sinks)
        {
            sink.Record(traceEvent);
        }
    }

    public async Task RecordAsync(TraceEvent traceEvent, CancellationToken cancellationToken = default)
    {
        var tasks = _sinks.Select(s => s.RecordAsync(traceEvent, cancellationToken));
        await Task.WhenAll(tasks);
    }

    public IReadOnlyList<TraceEvent> GetRecent(int count = 100, TraceEventFilter? filter = null)
    {
        // Return from first sink
        return _sinks.FirstOrDefault()?.GetRecent(count, filter) ?? Array.Empty<TraceEvent>();
    }
}
