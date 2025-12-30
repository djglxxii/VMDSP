using DeviceSimulator.Abstractions;

namespace DeviceSimulator.Scenarios;

/// <summary>
/// Schedules scenarios for execution (burst, repeating, cron).
/// </summary>
public class ScenarioScheduler : IDisposable
{
    private readonly IScenarioEngine _scenarioEngine;
    private readonly Dictionary<string, ScheduledScenario> _scheduledScenarios = new();
    private readonly CancellationTokenSource _cts = new();
    private bool _disposed;

    public ScenarioScheduler(IScenarioEngine scenarioEngine)
    {
        _scenarioEngine = scenarioEngine;
    }

    /// <summary>
    /// Schedules a scenario for repeated execution.
    /// </summary>
    public string ScheduleRepeating(
        string scenarioId,
        IStateMachineContext context,
        int intervalMs,
        int? repeatCount = null)
    {
        var scheduleId = Guid.NewGuid().ToString("N")[..8];

        var scheduled = new ScheduledScenario
        {
            ScheduleId = scheduleId,
            ScenarioId = scenarioId,
            Context = context,
            IntervalMs = intervalMs,
            RepeatCount = repeatCount,
            ExecutionCount = 0
        };

        _scheduledScenarios[scheduleId] = scheduled;

        // Start the execution loop
        _ = RunScheduledAsync(scheduled, _cts.Token);

        return scheduleId;
    }

    /// <summary>
    /// Triggers a burst of scenario executions.
    /// </summary>
    public async Task<string[]> TriggerBurstAsync(
        string scenarioId,
        IStateMachineContext context,
        int count,
        int delayBetweenMs = 0,
        CancellationToken cancellationToken = default)
    {
        var results = new List<string>();

        for (int i = 0; i < count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var result = await _scenarioEngine.TriggerAsync(scenarioId, context, cancellationToken);

            if (result.IsSuccess)
            {
                results.Add($"Execution {i + 1}: Success");
            }
            else
            {
                results.Add($"Execution {i + 1}: {result.ErrorMessage}");
            }

            if (delayBetweenMs > 0 && i < count - 1)
            {
                await Task.Delay(delayBetweenMs, cancellationToken);
            }
        }

        return results.ToArray();
    }

    /// <summary>
    /// Cancels a scheduled scenario.
    /// </summary>
    public void CancelScheduled(string scheduleId)
    {
        if (_scheduledScenarios.TryGetValue(scheduleId, out var scheduled))
        {
            scheduled.Cancelled = true;
            _scheduledScenarios.Remove(scheduleId);
        }
    }

    private async Task RunScheduledAsync(ScheduledScenario scheduled, CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested && !scheduled.Cancelled)
            {
                if (scheduled.RepeatCount.HasValue && scheduled.ExecutionCount >= scheduled.RepeatCount.Value)
                {
                    break;
                }

                await _scenarioEngine.TriggerAsync(scheduled.ScenarioId, scheduled.Context, cancellationToken);
                scheduled.ExecutionCount++;

                await Task.Delay(scheduled.IntervalMs, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal cancellation
        }
        finally
        {
            _scheduledScenarios.Remove(scheduled.ScheduleId);
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _cts.Cancel();
            _cts.Dispose();
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }

    private class ScheduledScenario
    {
        public required string ScheduleId { get; init; }
        public required string ScenarioId { get; init; }
        public required IStateMachineContext Context { get; init; }
        public required int IntervalMs { get; init; }
        public int? RepeatCount { get; init; }
        public int ExecutionCount { get; set; }
        public bool Cancelled { get; set; }
    }
}
