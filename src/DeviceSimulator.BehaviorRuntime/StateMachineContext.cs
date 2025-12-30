using System.Text;
using DeviceSimulator.Abstractions;

namespace DeviceSimulator.BehaviorRuntime;

/// <summary>
/// Default implementation of state machine context.
/// </summary>
public class StateMachineContext : IStateMachineContext
{
    private readonly Func<byte[], CancellationToken, Task> _sendFunc;
    private readonly Dictionary<string, object> _variables = new();

    public string DeviceId { get; }
    public string SessionId { get; }
    public ITraceSink TraceSink { get; }
    public IDictionary<string, object> Variables => _variables;

    public StateMachineContext(
        string deviceId,
        string sessionId,
        ITraceSink traceSink,
        Func<byte[], CancellationToken, Task> sendFunc)
    {
        DeviceId = deviceId;
        SessionId = sessionId;
        TraceSink = traceSink;
        _sendFunc = sendFunc;
    }

    public Task SendAsync(byte[] data, CancellationToken cancellationToken = default)
    {
        // Record trace event
        TraceSink.Record(new RawBytesTraceEvent
        {
            DeviceId = DeviceId,
            SessionId = SessionId,
            Direction = ByteDirection.Sent,
            Data = data,
            HexDump = BitConverter.ToString(data).Replace("-", " ")
        });

        return _sendFunc(data, cancellationToken);
    }

    public Task SendAsync(string data, CancellationToken cancellationToken = default)
    {
        return SendAsync(Encoding.UTF8.GetBytes(data), cancellationToken);
    }
}
