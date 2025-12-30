namespace DeviceSimulator.Abstractions;

/// <summary>
/// Protocol engine for encoding/decoding protocol-specific messages.
/// </summary>
public interface IProtocolEngine
{
    /// <summary>
    /// Protocol identifier (e.g., "HL7-MLLP", "ASTM-E1381", "POCT1A").
    /// </summary>
    string ProtocolId { get; }

    /// <summary>
    /// Decodes raw bytes into a protocol message.
    /// </summary>
    Result<ProtocolMessage> Decode(ReadOnlySpan<byte> data);

    /// <summary>
    /// Encodes a protocol message into raw bytes.
    /// </summary>
    Result<byte[]> Encode(ProtocolMessage message);

    /// <summary>
    /// Processes received bytes and emits decoded messages.
    /// Handles framing and buffering internally.
    /// </summary>
    IAsyncEnumerable<ProtocolMessage> OnBytesReceivedAsync(
        ReadOnlyMemory<byte> data,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resets internal state (buffer, framing state).
    /// </summary>
    void Reset();
}

/// <summary>
/// Base class for protocol messages.
/// </summary>
public abstract record ProtocolMessage
{
    /// <summary>
    /// Raw bytes of the message (for tracing).
    /// </summary>
    public byte[]? RawBytes { get; init; }

    /// <summary>
    /// Timestamp when the message was received/created.
    /// </summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Message type identifier.
    /// </summary>
    public abstract string MessageType { get; }
}

/// <summary>
/// Control character message (ENQ, ACK, NAK, EOT, etc.).
/// </summary>
public record ControlMessage(string Control) : ProtocolMessage
{
    public override string MessageType => $"CTRL:{Control}";
}

/// <summary>
/// Data frame message with content.
/// </summary>
public record DataMessage(string Content) : ProtocolMessage
{
    public override string MessageType => "DATA";
}
