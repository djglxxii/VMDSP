using DeviceSimulator.Abstractions;

namespace DeviceSimulator.Protocols.Hl7;

/// <summary>
/// Represents an HL7 v2.x message.
/// </summary>
public record Hl7Message : ProtocolMessage
{
    public override string MessageType => $"HL7:{MessageTypeCode}";

    /// <summary>
    /// Message type code (e.g., "ORU^R01").
    /// </summary>
    public string MessageTypeCode { get; init; } = "UNKNOWN";

    /// <summary>
    /// Message control ID.
    /// </summary>
    public string? MessageControlId { get; init; }

    /// <summary>
    /// Raw message content.
    /// </summary>
    public required string Content { get; init; }

    /// <summary>
    /// Parsed segments (segment name -> list of fields).
    /// </summary>
    public List<Hl7Segment> Segments { get; init; } = new();
}

/// <summary>
/// Represents an HL7 segment.
/// </summary>
public record Hl7Segment
{
    public required string Name { get; init; }
    public List<string> Fields { get; init; } = new();

    public string? GetField(int index)
    {
        if (index >= 0 && index < Fields.Count)
        {
            return Fields[index];
        }
        return null;
    }
}

/// <summary>
/// HL7 ACK message.
/// </summary>
public record Hl7AckMessage : Hl7Message
{
    public required string AcknowledgmentCode { get; init; } // AA, AE, AR
    public string? TextMessage { get; init; }
}
