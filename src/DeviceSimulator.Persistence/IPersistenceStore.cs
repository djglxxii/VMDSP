using DeviceSimulator.Abstractions;

namespace DeviceSimulator.Persistence;

/// <summary>
/// Persistence store abstraction.
/// </summary>
public interface IPersistenceStore
{
    /// <summary>
    /// Saves a behavior pack reference.
    /// </summary>
    Task SavePackAsync(BehaviorPackManifest manifest, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all saved packs.
    /// </summary>
    Task<IReadOnlyList<BehaviorPackManifest>> GetPacksAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves a device session.
    /// </summary>
    Task SaveSessionAsync(DeviceSession session, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets sessions for a device group.
    /// </summary>
    Task<IReadOnlyList<DeviceSession>> GetSessionsAsync(string groupId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves trace events.
    /// </summary>
    Task SaveTraceEventsAsync(IEnumerable<TraceEvent> events, CancellationToken cancellationToken = default);

    /// <summary>
    /// Queries trace events.
    /// </summary>
    Task<IReadOnlyList<TraceEvent>> QueryTraceEventsAsync(
        TraceEventFilter filter,
        int limit = 100,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Device session record.
/// </summary>
public record DeviceSession
{
    public required string SessionId { get; init; }
    public required string DeviceId { get; init; }
    public required string GroupId { get; init; }
    public required string PackName { get; init; }
    public DateTime StartedAt { get; init; }
    public DateTime? EndedAt { get; init; }
    public DeviceSessionStatus Status { get; init; }
}

public enum DeviceSessionStatus
{
    Starting,
    Connected,
    Running,
    Disconnected,
    Error
}
