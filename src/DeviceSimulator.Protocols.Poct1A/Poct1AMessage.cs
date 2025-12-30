using DeviceSimulator.Abstractions;

namespace DeviceSimulator.Protocols.Poct1A;

/// <summary>
/// POCT1-A control message.
/// </summary>
public record Poct1AControlMessage : ProtocolMessage
{
    public override string MessageType => $"POCT1A:{MessageCode}";

    public required Poct1AMessageCode MessageCode { get; init; }
    public string? Data { get; init; }
}

/// <summary>
/// POCT1-A message codes.
/// </summary>
public enum Poct1AMessageCode
{
    /// <summary>Hello - session establishment</summary>
    HEL,
    /// <summary>Hello response</summary>
    HEL_R,
    /// <summary>End of transmission</summary>
    EOT,
    /// <summary>Subscribe to topic</summary>
    SUB,
    /// <summary>Subscribe response</summary>
    SUB_R,
    /// <summary>Unsubscribe</summary>
    USB,
    /// <summary>Unsubscribe response</summary>
    USB_R,
    /// <summary>Publish</summary>
    PUB,
    /// <summary>Publish response</summary>
    PUB_R,
    /// <summary>Request</summary>
    REQ,
    /// <summary>Request response</summary>
    REQ_R,
    /// <summary>Error</summary>
    ERR,
    /// <summary>Acknowledgment</summary>
    ACK
}

/// <summary>
/// POCT1-A data message.
/// </summary>
public record Poct1ADataMessage : ProtocolMessage
{
    public override string MessageType => "POCT1A:DATA";

    public required string Topic { get; init; }
    public required string Content { get; init; }
    public Dictionary<string, string> Headers { get; init; } = new();
}

/// <summary>
/// POCT1-A session info.
/// </summary>
public record Poct1ASession
{
    public required string SessionId { get; init; }
    public required string DeviceId { get; init; }
    public DateTime EstablishedAt { get; init; } = DateTime.UtcNow;
    public Poct1ASessionState State { get; set; } = Poct1ASessionState.Pending;
    public List<string> SubscribedTopics { get; init; } = new();
}

public enum Poct1ASessionState
{
    Pending,
    Established,
    Closing,
    Closed,
    Error
}
