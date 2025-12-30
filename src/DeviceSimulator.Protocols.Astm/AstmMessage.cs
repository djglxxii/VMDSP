using DeviceSimulator.Abstractions;

namespace DeviceSimulator.Protocols.Astm;

/// <summary>
/// ASTM E1381 control message.
/// </summary>
public record AstmControlMessage : ProtocolMessage
{
    public override string MessageType => $"ASTM:CTRL:{ControlChar}";

    public required AstmControl ControlChar { get; init; }
}

/// <summary>
/// ASTM control characters.
/// </summary>
public enum AstmControl
{
    ENQ = 0x05,
    ACK = 0x06,
    NAK = 0x15,
    EOT = 0x04,
    STX = 0x02,
    ETX = 0x03,
    ETB = 0x17
}

/// <summary>
/// ASTM data frame message.
/// </summary>
public record AstmDataFrame : ProtocolMessage
{
    public override string MessageType => "ASTM:DATA";

    /// <summary>
    /// Frame number (1-7, wrapping).
    /// </summary>
    public int FrameNumber { get; init; }

    /// <summary>
    /// Frame content (without control chars and checksum).
    /// </summary>
    public required string Content { get; init; }

    /// <summary>
    /// Whether this is an intermediate frame (ETB) or final frame (ETX).
    /// </summary>
    public bool IsIntermediate { get; init; }

    /// <summary>
    /// Checksum bytes.
    /// </summary>
    public byte[]? Checksum { get; init; }
}

/// <summary>
/// ASTM record types.
/// </summary>
public enum AstmRecordType
{
    Header = 'H',
    Patient = 'P',
    TestOrder = 'O',
    Result = 'R',
    Comment = 'C',
    Request = 'Q',
    Terminator = 'L',
    Scientific = 'S',
    Manufacturer = 'M'
}
