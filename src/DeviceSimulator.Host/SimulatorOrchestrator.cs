using System.Collections.Concurrent;
using DeviceSimulator.Abstractions;
using DeviceSimulator.BehaviorRuntime;
using DeviceSimulator.Observability;

namespace DeviceSimulator.Host;

/// <summary>
/// Main orchestrator for device simulation.
/// </summary>
public class SimulatorOrchestrator : ISimulatorOrchestrator
{
    private readonly IBehaviorPackProvider _packProvider;
    private readonly ITraceSink _traceSink;
    private readonly ConcurrentDictionary<string, BehaviorPack> _loadedPacks = new();
    private readonly ConcurrentDictionary<string, DeviceGroupInstance> _deviceGroups = new();

    public SimulatorOrchestrator(
        IBehaviorPackProvider packProvider,
        ITraceSink traceSink)
    {
        _packProvider = packProvider;
        _traceSink = traceSink;
    }

    public async Task<Result<BehaviorPackManifest>> LoadPackAsync(string path, CancellationToken cancellationToken = default)
    {
        var result = await _packProvider.LoadAsync(path, cancellationToken);

        if (!result.IsSuccess || result.Value == null)
        {
            return Result.Failure<BehaviorPackManifest>(result.ErrorMessage ?? "Failed to load pack", result.ErrorCode);
        }

        var pack = result.Value;
        _loadedPacks[pack.Manifest.Name] = pack;

        return Result.Success(pack.Manifest);
    }

    public IReadOnlyList<BehaviorPackManifest> GetLoadedPacks()
    {
        return _loadedPacks.Values.Select(p => p.Manifest).ToList();
    }

    public Task<Result<DeviceGroup>> StartDevicesAsync(string packName, int count, CancellationToken cancellationToken = default)
    {
        if (!_loadedPacks.TryGetValue(packName, out var pack))
        {
            return Task.FromResult(Result.Failure<DeviceGroup>($"Pack not found: {packName}", ErrorCodes.NotFound));
        }

        var groupId = $"grp-{Guid.NewGuid():N}"[..12];

        var group = new DeviceGroup(
            GroupId: groupId,
            PackName: packName,
            DeviceCount: count,
            StartedAt: DateTime.UtcNow,
            Status: DeviceGroupStatus.Running
        );

        var instance = new DeviceGroupInstance
        {
            Group = group,
            Pack = pack,
            Devices = new List<SimulatedDevice>()
        };

        // Create simulated devices (placeholder - not actually connecting)
        for (int i = 0; i < count; i++)
        {
            var deviceId = $"dev-{groupId}-{i:D3}";
            var device = new SimulatedDevice
            {
                DeviceId = deviceId,
                SessionId = Guid.NewGuid().ToString("N")[..8],
                StateMachine = CreateStateMachine(pack)
            };
            instance.Devices.Add(device);
        }

        _deviceGroups[groupId] = instance;

        return Task.FromResult(Result.Success(group));
    }

    public Task<Result> StopDevicesAsync(string groupId, CancellationToken cancellationToken = default)
    {
        if (!_deviceGroups.TryRemove(groupId, out var instance))
        {
            return Task.FromResult(Result.Failure($"Group not found: {groupId}", ErrorCodes.NotFound));
        }

        // Stop all devices in the group
        foreach (var device in instance.Devices)
        {
            device.StateMachine.Reset();
        }

        return Task.FromResult(Result.Success());
    }

    public IReadOnlyList<DeviceGroup> GetActiveGroups()
    {
        return _deviceGroups.Values.Select(i => i.Group).ToList();
    }

    public Task<Result> TriggerScenarioAsync(string groupId, string scenarioName, CancellationToken cancellationToken = default)
    {
        if (!_deviceGroups.TryGetValue(groupId, out var instance))
        {
            return Task.FromResult(Result.Failure($"Group not found: {groupId}", ErrorCodes.NotFound));
        }

        var scenario = instance.Pack.Scenarios.Scenarios
            .FirstOrDefault(s => s.Name == scenarioName);

        if (scenario == null)
        {
            return Task.FromResult(Result.Failure($"Scenario not found: {scenarioName}", ErrorCodes.NotFound));
        }

        // Placeholder - would trigger scenario on all devices
        return Task.FromResult(Result.Success());
    }

    private IStateMachine CreateStateMachine(BehaviorPack pack)
    {
        var stateMachine = new BehaviorStateMachine();
        stateMachine.Initialize(pack.Workflow);
        return stateMachine;
    }

    private class DeviceGroupInstance
    {
        public required DeviceGroup Group { get; init; }
        public required BehaviorPack Pack { get; init; }
        public required List<SimulatedDevice> Devices { get; init; }
    }

    private class SimulatedDevice
    {
        public required string DeviceId { get; init; }
        public required string SessionId { get; init; }
        public required IStateMachine StateMachine { get; init; }
    }
}
