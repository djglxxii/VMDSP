namespace DeviceSimulator.Abstractions;

/// <summary>
/// Engine for scheduling and executing test scenarios.
/// </summary>
public interface IScenarioEngine
{
    /// <summary>
    /// Registers a scenario for execution.
    /// </summary>
    Task<Result<string>> RegisterScenarioAsync(
        ScenarioDefinition scenario,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Triggers immediate execution of a scenario.
    /// </summary>
    Task<Result> TriggerAsync(
        string scenarioId,
        IStateMachineContext context,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels a running or scheduled scenario.
    /// </summary>
    Task<Result> CancelAsync(string scenarioId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the status of a scenario.
    /// </summary>
    ScenarioStatus? GetStatus(string scenarioId);

    /// <summary>
    /// Event raised when scenario execution completes.
    /// </summary>
    event EventHandler<ScenarioCompletedEventArgs>? ScenarioCompleted;
}

public enum ScenarioStatus
{
    Pending,
    Running,
    Completed,
    Failed,
    Cancelled
}

public class ScenarioCompletedEventArgs : EventArgs
{
    public required string ScenarioId { get; init; }
    public required ScenarioStatus Status { get; init; }
    public Exception? Error { get; init; }
    public DateTime CompletedAt { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Fault injection abstraction for scenario testing.
/// </summary>
public interface IFaultInjector
{
    /// <summary>
    /// Injects a delay before the next operation.
    /// </summary>
    Task InjectDelayAsync(int delayMs, CancellationToken cancellationToken = default);

    /// <summary>
    /// Simulates a disconnection.
    /// </summary>
    Task InjectDisconnectAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Corrupts the next outgoing message.
    /// </summary>
    void InjectCorruption(CorruptionType type);

    /// <summary>
    /// Clears all pending fault injections.
    /// </summary>
    void Clear();
}

public enum CorruptionType
{
    None,
    TruncateMessage,
    InvalidChecksum,
    InvalidFraming,
    RandomBytes
}
