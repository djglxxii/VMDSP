namespace DeviceSimulator.Abstractions;

/// <summary>
/// Sink for trace events (observability).
/// </summary>
public interface ITraceSink
{
    /// <summary>
    /// Records a trace event.
    /// </summary>
    void Record(TraceEvent traceEvent);

    /// <summary>
    /// Records a trace event asynchronously.
    /// </summary>
    Task RecordAsync(TraceEvent traceEvent, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets recent trace events.
    /// </summary>
    IReadOnlyList<TraceEvent> GetRecent(int count = 100, TraceEventFilter? filter = null);
}

/// <summary>
/// Base class for trace events.
/// </summary>
public abstract record TraceEvent
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N")[..8];
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public required string DeviceId { get; init; }
    public required string SessionId { get; init; }
    public abstract TraceEventType EventType { get; }
}

public enum TraceEventType
{
    RawBytes,
    DecodedMessage,
    StateTransition,
    Error,
    ConnectionStateChange
}

/// <summary>
/// Trace event for raw bytes sent/received.
/// </summary>
public record RawBytesTraceEvent : TraceEvent
{
    public override TraceEventType EventType => TraceEventType.RawBytes;
    public required ByteDirection Direction { get; init; }
    public required byte[] Data { get; init; }
    public string? HexDump { get; init; }
}

public enum ByteDirection
{
    Received,
    Sent
}

/// <summary>
/// Trace event for decoded protocol messages.
/// </summary>
public record DecodedMessageTraceEvent : TraceEvent
{
    public override TraceEventType EventType => TraceEventType.DecodedMessage;
    public required string MessageType { get; init; }
    public required string Content { get; init; }
    public required ByteDirection Direction { get; init; }
}

/// <summary>
/// Trace event for state machine transitions.
/// </summary>
public record StateTransitionTraceEvent : TraceEvent
{
    public override TraceEventType EventType => TraceEventType.StateTransition;
    public required string FromState { get; init; }
    public required string ToState { get; init; }
    public required string Trigger { get; init; }
}

/// <summary>
/// Trace event for errors.
/// </summary>
public record ErrorTraceEvent : TraceEvent
{
    public override TraceEventType EventType => TraceEventType.Error;
    public required string ErrorMessage { get; init; }
    public string? StackTrace { get; init; }
    public string? ErrorCode { get; init; }
}

/// <summary>
/// Filter for querying trace events.
/// </summary>
public record TraceEventFilter
{
    public string? DeviceId { get; init; }
    public string? SessionId { get; init; }
    public TraceEventType? EventType { get; init; }
    public DateTime? From { get; init; }
    public DateTime? To { get; init; }
}
