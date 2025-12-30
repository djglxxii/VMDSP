namespace DeviceSimulator.Abstractions;

/// <summary>
/// Central orchestrator for managing simulated devices and behavior packs.
/// </summary>
public interface ISimulatorOrchestrator
{
    /// <summary>
    /// Loads a behavior pack from the specified path.
    /// </summary>
    Task<Result<BehaviorPackManifest>> LoadPackAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all loaded behavior packs.
    /// </summary>
    IReadOnlyList<BehaviorPackManifest> GetLoadedPacks();

    /// <summary>
    /// Starts a group of simulated devices using the specified behavior pack.
    /// </summary>
    Task<Result<DeviceGroup>> StartDevicesAsync(string packName, int count, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops all devices in the specified group.
    /// </summary>
    Task<Result> StopDevicesAsync(string groupId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all active device groups.
    /// </summary>
    IReadOnlyList<DeviceGroup> GetActiveGroups();

    /// <summary>
    /// Triggers a scenario on a device group.
    /// </summary>
    Task<Result> TriggerScenarioAsync(string groupId, string scenarioName, CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents a group of running simulated devices.
/// </summary>
public record DeviceGroup(
    string GroupId,
    string PackName,
    int DeviceCount,
    DateTime StartedAt,
    DeviceGroupStatus Status
);

public enum DeviceGroupStatus
{
    Starting,
    Running,
    Stopping,
    Stopped,
    Error
}
