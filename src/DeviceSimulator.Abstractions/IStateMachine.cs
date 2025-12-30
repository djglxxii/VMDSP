namespace DeviceSimulator.Abstractions;

/// <summary>
/// State machine for behavior execution.
/// </summary>
public interface IStateMachine
{
    /// <summary>
    /// Current state name.
    /// </summary>
    string CurrentState { get; }

    /// <summary>
    /// Initializes the state machine with a workflow definition.
    /// </summary>
    void Initialize(WorkflowDefinition workflow);

    /// <summary>
    /// Processes an event and transitions if applicable.
    /// </summary>
    Task<StateMachineResult> ProcessEventAsync(
        string eventName,
        string? payload,
        IStateMachineContext context,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resets to initial state.
    /// </summary>
    void Reset();

    /// <summary>
    /// Event raised on state transitions.
    /// </summary>
    event EventHandler<StateMachineTransitionEventArgs>? Transitioned;
}

/// <summary>
/// Result of state machine event processing.
/// </summary>
public record StateMachineResult
{
    public bool TransitionOccurred { get; init; }
    public string? FromState { get; init; }
    public string? ToState { get; init; }
    public List<ActionResult> ActionResults { get; init; } = new();
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Result of an action execution.
/// </summary>
public record ActionResult
{
    public required string ActionType { get; init; }
    public bool Success { get; init; }
    public string? Output { get; init; }
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Event args for state transitions.
/// </summary>
public class StateMachineTransitionEventArgs : EventArgs
{
    public required string FromState { get; init; }
    public required string ToState { get; init; }
    public required string Trigger { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Context provided to the state machine for action execution.
/// </summary>
public interface IStateMachineContext
{
    /// <summary>
    /// Device identifier.
    /// </summary>
    string DeviceId { get; }

    /// <summary>
    /// Session identifier.
    /// </summary>
    string SessionId { get; }

    /// <summary>
    /// Sends data through the transport.
    /// </summary>
    Task SendAsync(byte[] data, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a string (will be encoded as UTF-8).
    /// </summary>
    Task SendAsync(string data, CancellationToken cancellationToken = default);

    /// <summary>
    /// Trace sink for recording events.
    /// </summary>
    ITraceSink TraceSink { get; }

    /// <summary>
    /// Variables/context data.
    /// </summary>
    IDictionary<string, object> Variables { get; }
}
