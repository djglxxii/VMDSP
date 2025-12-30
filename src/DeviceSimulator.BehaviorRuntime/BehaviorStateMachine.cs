using System.Text;
using DeviceSimulator.Abstractions;

namespace DeviceSimulator.BehaviorRuntime;

/// <summary>
/// Simple deterministic state machine for behavior execution.
/// </summary>
public class BehaviorStateMachine : IStateMachine
{
    private WorkflowDefinition? _workflow;
    private string _currentState = "Idle";

    public string CurrentState => _currentState;

    public event EventHandler<StateMachineTransitionEventArgs>? Transitioned;

    public void Initialize(WorkflowDefinition workflow)
    {
        _workflow = workflow ?? throw new ArgumentNullException(nameof(workflow));
        _currentState = workflow.InitialState;
    }

    public async Task<StateMachineResult> ProcessEventAsync(
        string eventName,
        string? payload,
        IStateMachineContext context,
        CancellationToken cancellationToken = default)
    {
        if (_workflow == null)
        {
            return new StateMachineResult
            {
                TransitionOccurred = false,
                ErrorMessage = "State machine not initialized"
            };
        }

        if (!_workflow.States.TryGetValue(_currentState, out var currentStateDefinition))
        {
            return new StateMachineResult
            {
                TransitionOccurred = false,
                ErrorMessage = $"Current state '{_currentState}' not found in workflow"
            };
        }

        // Find matching transition
        TransitionDefinition? matchingTransition = null;

        foreach (var transition in currentStateDefinition.Transitions)
        {
            if (transition.On == eventName)
            {
                // Check if match pattern is required
                if (!string.IsNullOrEmpty(transition.Match))
                {
                    if (payload != null && MatchesPattern(payload, transition.Match))
                    {
                        matchingTransition = transition;
                        break;
                    }
                }
                else
                {
                    matchingTransition = transition;
                    break;
                }
            }
        }

        if (matchingTransition == null)
        {
            return new StateMachineResult
            {
                TransitionOccurred = false,
                FromState = _currentState
            };
        }

        var fromState = _currentState;
        var toState = matchingTransition.Goto;

        // Execute onExit actions for current state
        var actionResults = new List<ActionResult>();

        foreach (var action in currentStateDefinition.OnExit)
        {
            var result = await ExecuteActionAsync(action, context, cancellationToken);
            actionResults.Add(result);
        }

        // Execute transition actions
        foreach (var action in matchingTransition.Actions)
        {
            var result = await ExecuteActionAsync(action, context, cancellationToken);
            actionResults.Add(result);
        }

        // Transition to new state
        _currentState = toState;

        // Execute onEnter actions for new state
        if (_workflow.States.TryGetValue(toState, out var newStateDefinition))
        {
            foreach (var action in newStateDefinition.OnEnter)
            {
                var result = await ExecuteActionAsync(action, context, cancellationToken);
                actionResults.Add(result);
            }
        }

        // Raise transition event
        Transitioned?.Invoke(this, new StateMachineTransitionEventArgs
        {
            FromState = fromState,
            ToState = toState,
            Trigger = eventName
        });

        return new StateMachineResult
        {
            TransitionOccurred = true,
            FromState = fromState,
            ToState = toState,
            ActionResults = actionResults
        };
    }

    public void Reset()
    {
        if (_workflow != null)
        {
            _currentState = _workflow.InitialState;
        }
    }

    private static bool MatchesPattern(string payload, string pattern)
    {
        // Simple token matching: exact match or control char names
        var normalizedPayload = NormalizeControlChars(payload);
        var normalizedPattern = pattern.Trim();

        return string.Equals(normalizedPayload, normalizedPattern, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeControlChars(string input)
    {
        // Convert control char bytes to names
        return input.Trim() switch
        {
            "\x05" => "ENQ",
            "\x06" => "ACK",
            "\x15" => "NAK",
            "\x04" => "EOT",
            "\x02" => "STX",
            "\x03" => "ETX",
            _ => input.Trim()
        };
    }

    private async Task<ActionResult> ExecuteActionAsync(
        ActionDefinition action,
        IStateMachineContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            switch (action.Type.ToLowerInvariant())
            {
                case "send":
                    return await ExecuteSendAsync(action, context, cancellationToken);

                case "log":
                    return ExecuteLog(action, context);

                case "delay":
                    return await ExecuteDelayAsync(action, cancellationToken);

                case "set":
                    return ExecuteSet(action, context);

                default:
                    return new ActionResult
                    {
                        ActionType = action.Type,
                        Success = false,
                        ErrorMessage = $"Unknown action type: {action.Type}"
                    };
            }
        }
        catch (Exception ex)
        {
            return new ActionResult
            {
                ActionType = action.Type,
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    private static async Task<ActionResult> ExecuteSendAsync(
        ActionDefinition action,
        IStateMachineContext context,
        CancellationToken cancellationToken)
    {
        if (!action.Parameters.TryGetValue("data", out var dataObj))
        {
            return new ActionResult
            {
                ActionType = "send",
                Success = false,
                ErrorMessage = "Missing 'data' parameter"
            };
        }

        var data = dataObj.ToString() ?? "";

        // Convert control char names to bytes
        var bytes = ConvertToBytes(data);

        await context.SendAsync(bytes, cancellationToken);

        return new ActionResult
        {
            ActionType = "send",
            Success = true,
            Output = data
        };
    }

    private static byte[] ConvertToBytes(string data)
    {
        // Replace control char names with actual bytes
        var result = data
            .Replace("<ENQ>", "\x05")
            .Replace("<ACK>", "\x06")
            .Replace("<NAK>", "\x15")
            .Replace("<EOT>", "\x04")
            .Replace("<STX>", "\x02")
            .Replace("<ETX>", "\x03")
            .Replace("<CR>", "\r")
            .Replace("<LF>", "\n")
            .Replace("ENQ", "\x05")
            .Replace("ACK", "\x06")
            .Replace("NAK", "\x15")
            .Replace("EOT", "\x04");

        return Encoding.UTF8.GetBytes(result);
    }

    private static ActionResult ExecuteLog(ActionDefinition action, IStateMachineContext context)
    {
        if (action.Parameters.TryGetValue("message", out var messageObj))
        {
            var message = messageObj.ToString() ?? "";
            // In a real implementation, this would use ILogger
            Console.WriteLine($"[{context.DeviceId}] {message}");
        }

        return new ActionResult
        {
            ActionType = "log",
            Success = true
        };
    }

    private static async Task<ActionResult> ExecuteDelayAsync(
        ActionDefinition action,
        CancellationToken cancellationToken)
    {
        if (action.Parameters.TryGetValue("ms", out var msObj) &&
            int.TryParse(msObj.ToString(), out var ms))
        {
            await Task.Delay(ms, cancellationToken);
        }

        return new ActionResult
        {
            ActionType = "delay",
            Success = true
        };
    }

    private static ActionResult ExecuteSet(ActionDefinition action, IStateMachineContext context)
    {
        if (action.Parameters.TryGetValue("key", out var keyObj) &&
            action.Parameters.TryGetValue("value", out var valueObj))
        {
            var key = keyObj.ToString() ?? "";
            context.Variables[key] = valueObj;
        }

        return new ActionResult
        {
            ActionType = "set",
            Success = true
        };
    }
}
