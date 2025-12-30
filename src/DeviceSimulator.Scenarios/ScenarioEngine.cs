using DeviceSimulator.Abstractions;

namespace DeviceSimulator.Scenarios;

/// <summary>
/// Engine for scheduling and executing test scenarios.
/// </summary>
public class ScenarioEngine : IScenarioEngine
{
    private readonly Dictionary<string, ScenarioInstance> _scenarios = new();
    private readonly IFaultInjector _faultInjector;

    public event EventHandler<ScenarioCompletedEventArgs>? ScenarioCompleted;

    public ScenarioEngine(IFaultInjector? faultInjector = null)
    {
        _faultInjector = faultInjector ?? new NoOpFaultInjector();
    }

    public Task<Result<string>> RegisterScenarioAsync(
        ScenarioDefinition scenario,
        CancellationToken cancellationToken = default)
    {
        var id = $"{scenario.Name}-{Guid.NewGuid():N}"[..16];

        var instance = new ScenarioInstance
        {
            Id = id,
            Definition = scenario,
            Status = ScenarioStatus.Pending,
            RegisteredAt = DateTime.UtcNow
        };

        _scenarios[id] = instance;

        return Task.FromResult(Result.Success(id));
    }

    public async Task<Result> TriggerAsync(
        string scenarioId,
        IStateMachineContext context,
        CancellationToken cancellationToken = default)
    {
        if (!_scenarios.TryGetValue(scenarioId, out var instance))
        {
            return Result.Failure($"Scenario not found: {scenarioId}", ErrorCodes.NotFound);
        }

        if (instance.Status == ScenarioStatus.Running)
        {
            return Result.Failure("Scenario already running", ErrorCodes.InvalidState);
        }

        instance.Status = ScenarioStatus.Running;
        instance.StartedAt = DateTime.UtcNow;

        try
        {
            foreach (var step in instance.Definition.Steps)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (step.DelayMs > 0)
                {
                    await Task.Delay(step.DelayMs, cancellationToken);
                }

                await ExecuteStepAsync(step, context, cancellationToken);
            }

            instance.Status = ScenarioStatus.Completed;
            instance.CompletedAt = DateTime.UtcNow;

            ScenarioCompleted?.Invoke(this, new ScenarioCompletedEventArgs
            {
                ScenarioId = scenarioId,
                Status = ScenarioStatus.Completed
            });

            return Result.Success();
        }
        catch (OperationCanceledException)
        {
            instance.Status = ScenarioStatus.Cancelled;
            return Result.Failure("Scenario cancelled", ErrorCodes.Timeout);
        }
        catch (Exception ex)
        {
            instance.Status = ScenarioStatus.Failed;
            instance.Error = ex;

            ScenarioCompleted?.Invoke(this, new ScenarioCompletedEventArgs
            {
                ScenarioId = scenarioId,
                Status = ScenarioStatus.Failed,
                Error = ex
            });

            return Result.Failure(ex.Message, ErrorCodes.ProtocolError);
        }
    }

    public Task<Result> CancelAsync(string scenarioId, CancellationToken cancellationToken = default)
    {
        if (!_scenarios.TryGetValue(scenarioId, out var instance))
        {
            return Task.FromResult(Result.Failure($"Scenario not found: {scenarioId}", ErrorCodes.NotFound));
        }

        instance.Status = ScenarioStatus.Cancelled;
        return Task.FromResult(Result.Success());
    }

    public ScenarioStatus? GetStatus(string scenarioId)
    {
        return _scenarios.TryGetValue(scenarioId, out var instance) ? instance.Status : null;
    }

    private async Task ExecuteStepAsync(
        ScenarioStep step,
        IStateMachineContext context,
        CancellationToken cancellationToken)
    {
        switch (step.Action.ToLowerInvariant())
        {
            case "send":
                if (step.Parameters.TryGetValue("data", out var dataObj))
                {
                    await context.SendAsync(dataObj.ToString() ?? "", cancellationToken);
                }
                break;

            case "inject_delay":
                if (step.Parameters.TryGetValue("ms", out var msObj) &&
                    int.TryParse(msObj.ToString(), out var ms))
                {
                    await _faultInjector.InjectDelayAsync(ms, cancellationToken);
                }
                break;

            case "inject_disconnect":
                await _faultInjector.InjectDisconnectAsync(cancellationToken);
                break;

            case "inject_corruption":
                if (step.Parameters.TryGetValue("type", out var typeObj) &&
                    Enum.TryParse<CorruptionType>(typeObj.ToString(), out var corruptionType))
                {
                    _faultInjector.InjectCorruption(corruptionType);
                }
                break;

            default:
                // Unknown action - log and continue
                break;
        }
    }

    private class ScenarioInstance
    {
        public required string Id { get; init; }
        public required ScenarioDefinition Definition { get; init; }
        public ScenarioStatus Status { get; set; }
        public DateTime RegisteredAt { get; init; }
        public DateTime? StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public Exception? Error { get; set; }
    }
}

/// <summary>
/// No-op fault injector (placeholder).
/// </summary>
public class NoOpFaultInjector : IFaultInjector
{
    public Task InjectDelayAsync(int delayMs, CancellationToken cancellationToken = default)
    {
        return Task.Delay(delayMs, cancellationToken);
    }

    public Task InjectDisconnectAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public void InjectCorruption(CorruptionType type)
    {
        // No-op
    }

    public void Clear()
    {
        // No-op
    }
}
