using System.Collections.Concurrent;
using DeviceSimulator.Abstractions;

namespace DeviceSimulator.Observability;

/// <summary>
/// In-memory trace sink with circular buffer.
/// </summary>
public class InMemoryTraceSink : ITraceSink
{
    private readonly ConcurrentQueue<TraceEvent> _events = new();
    private readonly int _maxEvents;

    public InMemoryTraceSink(int maxEvents = 10000)
    {
        _maxEvents = maxEvents;
    }

    public void Record(TraceEvent traceEvent)
    {
        _events.Enqueue(traceEvent);

        // Trim if over capacity
        while (_events.Count > _maxEvents)
        {
            _events.TryDequeue(out _);
        }
    }

    public Task RecordAsync(TraceEvent traceEvent, CancellationToken cancellationToken = default)
    {
        Record(traceEvent);
        return Task.CompletedTask;
    }

    public IReadOnlyList<TraceEvent> GetRecent(int count = 100, TraceEventFilter? filter = null)
    {
        IEnumerable<TraceEvent> query = _events.Reverse();

        if (filter != null)
        {
            if (!string.IsNullOrEmpty(filter.DeviceId))
            {
                query = query.Where(e => e.DeviceId == filter.DeviceId);
            }

            if (!string.IsNullOrEmpty(filter.SessionId))
            {
                query = query.Where(e => e.SessionId == filter.SessionId);
            }

            if (filter.EventType.HasValue)
            {
                query = query.Where(e => e.EventType == filter.EventType.Value);
            }

            if (filter.From.HasValue)
            {
                query = query.Where(e => e.Timestamp >= filter.From.Value);
            }

            if (filter.To.HasValue)
            {
                query = query.Where(e => e.Timestamp <= filter.To.Value);
            }
        }

        return query.Take(count).ToList();
    }

    /// <summary>
    /// Clears all events.
    /// </summary>
    public void Clear()
    {
        while (_events.TryDequeue(out _)) { }
    }

    /// <summary>
    /// Gets the total event count.
    /// </summary>
    public int Count => _events.Count;
}
