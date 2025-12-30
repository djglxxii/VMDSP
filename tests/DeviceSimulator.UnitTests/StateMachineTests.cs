using DeviceSimulator.Abstractions;
using DeviceSimulator.BehaviorRuntime;
using DeviceSimulator.Observability;

namespace DeviceSimulator.UnitTests;

public class StateMachineTests
{
    [Fact]
    public void Initialize_WithWorkflow_SetsInitialState()
    {
        // Arrange
        var stateMachine = new BehaviorStateMachine();
        var workflow = CreateTestWorkflow();

        // Act
        stateMachine.Initialize(workflow);

        // Assert
        Assert.Equal("Idle", stateMachine.CurrentState);
    }

    [Fact]
    public async Task ProcessEvent_WithConnect_TransitionsToConnected()
    {
        // Arrange
        var stateMachine = new BehaviorStateMachine();
        var workflow = CreateTestWorkflow();
        stateMachine.Initialize(workflow);
        var context = CreateTestContext();

        // Act
        var result = await stateMachine.ProcessEventAsync("connect", null, context);

        // Assert
        Assert.True(result.TransitionOccurred);
        Assert.Equal("Idle", result.FromState);
        Assert.Equal("Connected", result.ToState);
        Assert.Equal("Connected", stateMachine.CurrentState);
    }

    [Fact]
    public async Task ProcessEvent_WithENQ_TransitionsToReceiving()
    {
        // Arrange
        var stateMachine = new BehaviorStateMachine();
        var workflow = CreateTestWorkflow();
        stateMachine.Initialize(workflow);
        var context = CreateTestContext();

        // Act - first connect, then receive ENQ
        await stateMachine.ProcessEventAsync("connect", null, context);
        var result = await stateMachine.ProcessEventAsync("receive", "ENQ", context);

        // Assert
        Assert.True(result.TransitionOccurred);
        Assert.Equal("Connected", result.FromState);
        Assert.Equal("Receiving", result.ToState);
        Assert.Equal("Receiving", stateMachine.CurrentState);
    }

    [Fact]
    public async Task ProcessEvent_InReceiving_WithEOT_TransitionsToIdle()
    {
        // Arrange
        var stateMachine = new BehaviorStateMachine();
        var workflow = CreateTestWorkflow();
        stateMachine.Initialize(workflow);
        var context = CreateTestContext();

        // Act - connect -> receive ENQ -> receive EOT
        await stateMachine.ProcessEventAsync("connect", null, context);
        await stateMachine.ProcessEventAsync("receive", "ENQ", context);
        var result = await stateMachine.ProcessEventAsync("receive", "EOT", context);

        // Assert
        Assert.True(result.TransitionOccurred);
        Assert.Equal("Receiving", result.FromState);
        Assert.Equal("Idle", result.ToState);
        Assert.Equal("Idle", stateMachine.CurrentState);
    }

    [Fact]
    public async Task ProcessEvent_WithSendAction_ExecutesSend()
    {
        // Arrange
        var stateMachine = new BehaviorStateMachine();
        var workflow = CreateTestWorkflow();
        stateMachine.Initialize(workflow);

        var sentData = new List<byte[]>();
        var context = CreateTestContext(data => sentData.Add(data));

        // Act - receive ENQ which should send ACK
        var result = await stateMachine.ProcessEventAsync("receive", "ENQ", context);

        // Assert
        Assert.True(result.TransitionOccurred);
        Assert.NotEmpty(sentData);
        // ACK is 0x06
        Assert.Contains(sentData, d => d.Contains((byte)0x06));
    }

    [Fact]
    public async Task ProcessEvent_WithNoMatchingTransition_DoesNotTransition()
    {
        // Arrange
        var stateMachine = new BehaviorStateMachine();
        var workflow = CreateTestWorkflow();
        stateMachine.Initialize(workflow);
        var context = CreateTestContext();

        // Act - unknown event
        var result = await stateMachine.ProcessEventAsync("unknown_event", null, context);

        // Assert
        Assert.False(result.TransitionOccurred);
        Assert.Equal("Idle", stateMachine.CurrentState);
    }

    [Fact]
    public async Task Reset_AfterTransitions_ReturnsToInitialState()
    {
        // Arrange
        var stateMachine = new BehaviorStateMachine();
        var workflow = CreateTestWorkflow();
        stateMachine.Initialize(workflow);
        var context = CreateTestContext();

        // Act - make some transitions then reset
        await stateMachine.ProcessEventAsync("connect", null, context);
        stateMachine.Reset();

        // Assert
        Assert.Equal("Idle", stateMachine.CurrentState);
    }

    [Fact]
    public async Task ProcessEvent_RaisesTransitionedEvent()
    {
        // Arrange
        var stateMachine = new BehaviorStateMachine();
        var workflow = CreateTestWorkflow();
        stateMachine.Initialize(workflow);
        var context = CreateTestContext();

        StateMachineTransitionEventArgs? eventArgs = null;
        stateMachine.Transitioned += (sender, args) => eventArgs = args;

        // Act
        await stateMachine.ProcessEventAsync("connect", null, context);

        // Assert
        Assert.NotNull(eventArgs);
        Assert.Equal("Idle", eventArgs.FromState);
        Assert.Equal("Connected", eventArgs.ToState);
        Assert.Equal("connect", eventArgs.Trigger);
    }

    [Fact]
    public async Task FullHandshake_Scenario_CompletesSuccessfully()
    {
        // Arrange - simulates: Connect -> ENQ -> ACK sent -> data received -> EOT -> back to Idle
        var stateMachine = new BehaviorStateMachine();
        var workflow = CreateTestWorkflow();
        stateMachine.Initialize(workflow);

        var sentData = new List<string>();
        var context = CreateTestContext(data =>
        {
            var str = System.Text.Encoding.UTF8.GetString(data)
                .Replace("\x05", "ENQ")
                .Replace("\x06", "ACK")
                .Replace("\x04", "EOT");
            sentData.Add(str);
        });

        // Act - full handshake
        await stateMachine.ProcessEventAsync("connect", null, context);
        Assert.Equal("Connected", stateMachine.CurrentState);

        await stateMachine.ProcessEventAsync("receive", "ENQ", context);
        Assert.Equal("Receiving", stateMachine.CurrentState);
        Assert.Contains(sentData, s => s.Contains("ACK"));

        await stateMachine.ProcessEventAsync("receive", "data-frame", context);
        Assert.Equal("Receiving", stateMachine.CurrentState);

        await stateMachine.ProcessEventAsync("receive", "EOT", context);
        Assert.Equal("Idle", stateMachine.CurrentState);
    }

    private static WorkflowDefinition CreateTestWorkflow()
    {
        return new WorkflowDefinition
        {
            InitialState = "Idle",
            States = new Dictionary<string, StateDefinition>
            {
                ["Idle"] = new StateDefinition
                {
                    Description = "Waiting for connection",
                    Transitions = new List<TransitionDefinition>
                    {
                        new TransitionDefinition
                        {
                            On = "connect",
                            Goto = "Connected"
                        },
                        new TransitionDefinition
                        {
                            On = "receive",
                            Match = "ENQ",
                            Goto = "Receiving",
                            Actions = new List<ActionDefinition>
                            {
                                new ActionDefinition
                                {
                                    Type = "send",
                                    Parameters = new Dictionary<string, object> { ["data"] = "ACK" }
                                }
                            }
                        }
                    }
                },
                ["Connected"] = new StateDefinition
                {
                    Description = "Connected, awaiting handshake",
                    Transitions = new List<TransitionDefinition>
                    {
                        new TransitionDefinition
                        {
                            On = "receive",
                            Match = "ENQ",
                            Goto = "Receiving",
                            Actions = new List<ActionDefinition>
                            {
                                new ActionDefinition
                                {
                                    Type = "send",
                                    Parameters = new Dictionary<string, object> { ["data"] = "ACK" }
                                }
                            }
                        },
                        new TransitionDefinition
                        {
                            On = "disconnect",
                            Goto = "Idle"
                        }
                    }
                },
                ["Receiving"] = new StateDefinition
                {
                    Description = "Receiving data",
                    Transitions = new List<TransitionDefinition>
                    {
                        new TransitionDefinition
                        {
                            On = "receive",
                            Match = "EOT",
                            Goto = "Idle"
                        },
                        new TransitionDefinition
                        {
                            On = "receive",
                            Goto = "Receiving",
                            Actions = new List<ActionDefinition>
                            {
                                new ActionDefinition
                                {
                                    Type = "send",
                                    Parameters = new Dictionary<string, object> { ["data"] = "ACK" }
                                }
                            }
                        }
                    }
                }
            }
        };
    }

    private static IStateMachineContext CreateTestContext(Action<byte[]>? onSend = null)
    {
        var traceSink = new InMemoryTraceSink();

        return new StateMachineContext(
            "test-device",
            "test-session",
            traceSink,
            (data, ct) =>
            {
                onSend?.Invoke(data);
                return Task.CompletedTask;
            }
        );
    }
}
