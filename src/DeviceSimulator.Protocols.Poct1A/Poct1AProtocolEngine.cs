using System.Runtime.CompilerServices;
using System.Text;
using DeviceSimulator.Abstractions;

namespace DeviceSimulator.Protocols.Poct1A;

/// <summary>
/// POCT1-A protocol engine (simplified session/topic handling).
/// </summary>
public class Poct1AProtocolEngine : IProtocolEngine
{
    private const char MessageDelimiter = '|';
    private const byte CR = 0x0D;
    private const byte LF = 0x0A;

    private readonly List<byte> _buffer = new();

    public string ProtocolId => "POCT1-A";

    public Result<ProtocolMessage> Decode(ReadOnlySpan<byte> data)
    {
        var content = Encoding.UTF8.GetString(data).Trim();

        if (string.IsNullOrEmpty(content))
        {
            return Result.Failure<ProtocolMessage>("Empty message", ErrorCodes.ProtocolError);
        }

        // Parse message code
        var parts = content.Split(MessageDelimiter);
        if (parts.Length == 0)
        {
            return Result.Failure<ProtocolMessage>("Invalid message format", ErrorCodes.ProtocolError);
        }

        if (Enum.TryParse<Poct1AMessageCode>(parts[0].Replace("-", "_"), ignoreCase: true, out var code))
        {
            return Result.Success<ProtocolMessage>(new Poct1AControlMessage
            {
                MessageCode = code,
                Data = parts.Length > 1 ? string.Join(MessageDelimiter, parts.Skip(1)) : null,
                RawBytes = data.ToArray()
            });
        }

        // Assume it's a data message if not a known code
        return Result.Success<ProtocolMessage>(new Poct1ADataMessage
        {
            Topic = parts[0],
            Content = parts.Length > 1 ? string.Join(MessageDelimiter, parts.Skip(1)) : "",
            RawBytes = data.ToArray()
        });
    }

    public Result<byte[]> Encode(ProtocolMessage message)
    {
        string content;

        if (message is Poct1AControlMessage ctrl)
        {
            content = ctrl.Data != null
                ? $"{ctrl.MessageCode.ToString().Replace("_", "-")}|{ctrl.Data}"
                : ctrl.MessageCode.ToString().Replace("_", "-");
        }
        else if (message is Poct1ADataMessage data)
        {
            content = $"{data.Topic}|{data.Content}";
        }
        else
        {
            return Result.Failure<byte[]>("Invalid message type for POCT1-A encoding", ErrorCodes.ProtocolError);
        }

        var bytes = Encoding.UTF8.GetBytes(content + "\r\n");
        return Result.Success(bytes);
    }

    public async IAsyncEnumerable<ProtocolMessage> OnBytesReceivedAsync(
        ReadOnlyMemory<byte> data,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Copy to array to avoid ref struct iteration in async method
        var bytes = data.ToArray();

        foreach (var b in bytes)
        {
            if (b == LF && _buffer.Count > 0)
            {
                // Remove trailing CR if present
                if (_buffer[^1] == CR)
                {
                    _buffer.RemoveAt(_buffer.Count - 1);
                }

                var messageData = _buffer.ToArray();
                _buffer.Clear();

                if (messageData.Length > 0)
                {
                    var result = Decode(messageData);
                    if (result.IsSuccess && result.Value != null)
                    {
                        yield return result.Value;
                    }
                }
            }
            else if (b != LF)
            {
                _buffer.Add(b);
            }
        }

        await Task.CompletedTask;
    }

    public void Reset()
    {
        _buffer.Clear();
    }
}

/// <summary>
/// POCT1-A session manager (placeholder).
/// </summary>
public class Poct1ASessionManager
{
    private readonly Dictionary<string, Poct1ASession> _sessions = new();

    /// <summary>
    /// Creates a new session.
    /// </summary>
    public Poct1ASession CreateSession(string deviceId)
    {
        var session = new Poct1ASession
        {
            SessionId = Guid.NewGuid().ToString("N")[..12],
            DeviceId = deviceId
        };
        _sessions[session.SessionId] = session;
        return session;
    }

    /// <summary>
    /// Gets a session by ID.
    /// </summary>
    public Poct1ASession? GetSession(string sessionId)
    {
        return _sessions.TryGetValue(sessionId, out var session) ? session : null;
    }

    /// <summary>
    /// Establishes a session (processes HEL).
    /// </summary>
    public Poct1AControlMessage HandleHello(Poct1ASession session, string? helloData)
    {
        session.State = Poct1ASessionState.Established;

        return new Poct1AControlMessage
        {
            MessageCode = Poct1AMessageCode.HEL_R,
            Data = session.SessionId
        };
    }

    /// <summary>
    /// Closes a session (processes EOT).
    /// </summary>
    public void CloseSession(string sessionId)
    {
        if (_sessions.TryGetValue(sessionId, out var session))
        {
            session.State = Poct1ASessionState.Closed;
            _sessions.Remove(sessionId);
        }
    }

    /// <summary>
    /// Subscribes to a topic.
    /// </summary>
    public Poct1AControlMessage HandleSubscribe(Poct1ASession session, string topic)
    {
        if (!session.SubscribedTopics.Contains(topic))
        {
            session.SubscribedTopics.Add(topic);
        }

        return new Poct1AControlMessage
        {
            MessageCode = Poct1AMessageCode.SUB_R,
            Data = $"OK|{topic}"
        };
    }
}
