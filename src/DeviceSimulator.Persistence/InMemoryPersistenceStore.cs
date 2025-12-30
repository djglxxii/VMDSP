using System.Collections.Concurrent;
using DeviceSimulator.Abstractions;

namespace DeviceSimulator.Persistence;

/// <summary>
/// In-memory persistence store (for testing and development).
/// </summary>
public class InMemoryPersistenceStore : IPersistenceStore
{
    private readonly ConcurrentDictionary<string, BehaviorPackManifest> _packs = new();
    private readonly ConcurrentDictionary<string, DeviceSession> _sessions = new();
    private readonly ConcurrentQueue<TraceEvent> _traceEvents = new();
    private readonly int _maxTraceEvents;

    public InMemoryPersistenceStore(int maxTraceEvents = 100000)
    {
        _maxTraceEvents = maxTraceEvents;
    }

    public Task SavePackAsync(BehaviorPackManifest manifest, CancellationToken cancellationToken = default)
    {
        _packs[manifest.Name] = manifest;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<BehaviorPackManifest>> GetPacksAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<BehaviorPackManifest>>(_packs.Values.ToList());
    }

    public Task SaveSessionAsync(DeviceSession session, CancellationToken cancellationToken = default)
    {
        _sessions[session.SessionId] = session;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<DeviceSession>> GetSessionsAsync(string groupId, CancellationToken cancellationToken = default)
    {
        var sessions = _sessions.Values
            .Where(s => s.GroupId == groupId)
            .ToList();
        return Task.FromResult<IReadOnlyList<DeviceSession>>(sessions);
    }

    public Task SaveTraceEventsAsync(IEnumerable<TraceEvent> events, CancellationToken cancellationToken = default)
    {
        foreach (var evt in events)
        {
            _traceEvents.Enqueue(evt);
        }

        // Trim if over capacity
        while (_traceEvents.Count > _maxTraceEvents)
        {
            _traceEvents.TryDequeue(out _);
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<TraceEvent>> QueryTraceEventsAsync(
        TraceEventFilter filter,
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        IEnumerable<TraceEvent> query = _traceEvents;

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

        var result = query.Take(limit).ToList();
        return Task.FromResult<IReadOnlyList<TraceEvent>>(result);
    }
}
