namespace DeviceSimulator.Abstractions;

/// <summary>
/// Provides behavior packs for device simulation.
/// </summary>
public interface IBehaviorPackProvider
{
    /// <summary>
    /// Loads a behavior pack from the specified path.
    /// </summary>
    Task<Result<BehaviorPack>> LoadAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates a behavior pack.
    /// </summary>
    Result ValidatePack(BehaviorPack pack);
}

/// <summary>
/// Complete behavior pack containing all definitions.
/// </summary>
public record BehaviorPack(
    BehaviorPackManifest Manifest,
    ProtocolDefinition Protocol,
    WorkflowDefinition Workflow,
    QuirksDefinition Quirks,
    ScenariosDefinition Scenarios
);

/// <summary>
/// Behavior pack manifest with metadata.
/// </summary>
public record BehaviorPackManifest
{
    public required string Name { get; init; }
    public required string Vendor { get; init; }
    public required string Protocol { get; init; }
    public required string Version { get; init; }
    public string? Description { get; init; }
    public List<string> Supports { get; init; } = new();
    public string? BasePath { get; init; }
}

/// <summary>
/// Protocol-specific configuration.
/// </summary>
public record ProtocolDefinition
{
    public required string Type { get; init; }
    public Dictionary<string, object> Settings { get; init; } = new();
    public List<MessageTemplate> MessageTemplates { get; init; } = new();
}

/// <summary>
/// Message template for generating protocol messages.
/// </summary>
public record MessageTemplate
{
    public required string Name { get; init; }
    public required string Template { get; init; }
    public Dictionary<string, string> Placeholders { get; init; } = new();
}

/// <summary>
/// State machine workflow definition.
/// </summary>
public record WorkflowDefinition
{
    public required string InitialState { get; init; }
    public Dictionary<string, StateDefinition> States { get; init; } = new();
}

/// <summary>
/// Single state in the workflow.
/// </summary>
public record StateDefinition
{
    public string? Description { get; init; }
    public List<TransitionDefinition> Transitions { get; init; } = new();
    public List<ActionDefinition> OnEnter { get; init; } = new();
    public List<ActionDefinition> OnExit { get; init; } = new();
}

/// <summary>
/// State transition definition.
/// </summary>
public record TransitionDefinition
{
    public required string On { get; init; }
    public string? Match { get; init; }
    public required string Goto { get; init; }
    public List<ActionDefinition> Actions { get; init; } = new();
}

/// <summary>
/// Action to execute during state transition.
/// </summary>
public record ActionDefinition
{
    public required string Type { get; init; }
    public Dictionary<string, object> Parameters { get; init; } = new();
}

/// <summary>
/// Device quirks and timing configuration.
/// </summary>
public record QuirksDefinition
{
    public TimingQuirks Timing { get; init; } = new();
    public BehaviorQuirks Behavior { get; init; } = new();
}

public record TimingQuirks
{
    public int ResponseDelayMs { get; init; } = 0;
    public int TimeoutMs { get; init; } = 30000;
    public int RetryDelayMs { get; init; } = 1000;
    public int MaxRetries { get; init; } = 3;
}

public record BehaviorQuirks
{
    public bool SendCrLf { get; init; } = true;
    public bool StrictFraming { get; init; } = true;
    public Dictionary<string, object> Custom { get; init; } = new();
}

/// <summary>
/// Test scenarios definition.
/// </summary>
public record ScenariosDefinition
{
    public List<ScenarioDefinition> Scenarios { get; init; } = new();
}

/// <summary>
/// Single test scenario.
/// </summary>
public record ScenarioDefinition
{
    public required string Name { get; init; }
    public string? Description { get; init; }
    public List<ScenarioStep> Steps { get; init; } = new();
    public ScheduleDefinition? Schedule { get; init; }
}

/// <summary>
/// Scenario step.
/// </summary>
public record ScenarioStep
{
    public required string Action { get; init; }
    public Dictionary<string, object> Parameters { get; init; } = new();
    public int DelayMs { get; init; } = 0;
}

/// <summary>
/// Schedule for recurring scenarios.
/// </summary>
public record ScheduleDefinition
{
    public string? Cron { get; init; }
    public int? IntervalMs { get; init; }
    public int? RepeatCount { get; init; }
}
