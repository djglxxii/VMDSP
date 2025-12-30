using DeviceSimulator.Abstractions;

namespace DeviceSimulator.Persistence;

/// <summary>
/// SQLite persistence store (placeholder - compiles but uses in-memory fallback).
/// Real implementation would use Microsoft.Data.Sqlite.
/// </summary>
public class SqlitePersistenceStore : IPersistenceStore
{
    private readonly string _connectionString;
    private readonly InMemoryPersistenceStore _fallback = new();

    public SqlitePersistenceStore(string databasePath = "device_simulator.db")
    {
        _connectionString = $"Data Source={databasePath}";

        // TODO: Initialize SQLite database
        // CreateTablesIfNotExist();
    }

    public Task SavePackAsync(BehaviorPackManifest manifest, CancellationToken cancellationToken = default)
    {
        // TODO: INSERT INTO packs (name, vendor, protocol, version, description, supports, base_path) VALUES (...)
        return _fallback.SavePackAsync(manifest, cancellationToken);
    }

    public Task<IReadOnlyList<BehaviorPackManifest>> GetPacksAsync(CancellationToken cancellationToken = default)
    {
        // TODO: SELECT * FROM packs
        return _fallback.GetPacksAsync(cancellationToken);
    }

    public Task SaveSessionAsync(DeviceSession session, CancellationToken cancellationToken = default)
    {
        // TODO: INSERT INTO sessions (...)
        return _fallback.SaveSessionAsync(session, cancellationToken);
    }

    public Task<IReadOnlyList<DeviceSession>> GetSessionsAsync(string groupId, CancellationToken cancellationToken = default)
    {
        // TODO: SELECT * FROM sessions WHERE group_id = @groupId
        return _fallback.GetSessionsAsync(groupId, cancellationToken);
    }

    public Task SaveTraceEventsAsync(IEnumerable<TraceEvent> events, CancellationToken cancellationToken = default)
    {
        // TODO: Bulk INSERT INTO trace_events (...)
        return _fallback.SaveTraceEventsAsync(events, cancellationToken);
    }

    public Task<IReadOnlyList<TraceEvent>> QueryTraceEventsAsync(
        TraceEventFilter filter,
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        // TODO: SELECT * FROM trace_events WHERE ... LIMIT @limit
        return _fallback.QueryTraceEventsAsync(filter, limit, cancellationToken);
    }

    // private void CreateTablesIfNotExist()
    // {
    //     const string sql = @"
    //         CREATE TABLE IF NOT EXISTS packs (
    //             name TEXT PRIMARY KEY,
    //             vendor TEXT,
    //             protocol TEXT,
    //             version TEXT,
    //             description TEXT,
    //             supports TEXT,
    //             base_path TEXT
    //         );
    //
    //         CREATE TABLE IF NOT EXISTS sessions (
    //             session_id TEXT PRIMARY KEY,
    //             device_id TEXT,
    //             group_id TEXT,
    //             pack_name TEXT,
    //             started_at TEXT,
    //             ended_at TEXT,
    //             status INTEGER
    //         );
    //
    //         CREATE TABLE IF NOT EXISTS trace_events (
    //             id TEXT PRIMARY KEY,
    //             timestamp TEXT,
    //             device_id TEXT,
    //             session_id TEXT,
    //             event_type INTEGER,
    //             data TEXT
    //         );
    //
    //         CREATE INDEX IF NOT EXISTS idx_sessions_group ON sessions(group_id);
    //         CREATE INDEX IF NOT EXISTS idx_trace_device ON trace_events(device_id);
    //         CREATE INDEX IF NOT EXISTS idx_trace_session ON trace_events(session_id);
    //         CREATE INDEX IF NOT EXISTS idx_trace_timestamp ON trace_events(timestamp);
    //     ";
    // }
}
