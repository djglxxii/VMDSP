using System.Runtime.CompilerServices;
using System.Text;
using DeviceSimulator.Abstractions;

namespace DeviceSimulator.Protocols.Hl7;

/// <summary>
/// HL7 MLLP protocol engine.
/// </summary>
public class Hl7ProtocolEngine : IProtocolEngine
{
    private const byte VT = 0x0B;  // Start of MLLP frame
    private const byte FS = 0x1C;  // End of MLLP frame
    private const byte CR = 0x0D;  // Carriage return

    private readonly List<byte> _buffer = new();
    private bool _inFrame;

    public string ProtocolId => "HL7-MLLP";

    public Result<ProtocolMessage> Decode(ReadOnlySpan<byte> data)
    {
        // Try to unwrap MLLP
        if (data.Length < 3 || data[0] != VT || data[^2] != FS || data[^1] != CR)
        {
            return Result.Failure<ProtocolMessage>("Invalid MLLP framing", ErrorCodes.ProtocolError);
        }

        var content = Encoding.UTF8.GetString(data[1..^2]);
        var message = ParseHl7Message(content);
        message = message with { RawBytes = data.ToArray() };

        return Result.Success<ProtocolMessage>(message);
    }

    public Result<byte[]> Encode(ProtocolMessage message)
    {
        if (message is Hl7Message hl7)
        {
            var content = Encoding.UTF8.GetBytes(hl7.Content);
            var result = new byte[content.Length + 3];
            result[0] = VT;
            Array.Copy(content, 0, result, 1, content.Length);
            result[^2] = FS;
            result[^1] = CR;
            return Result.Success(result);
        }

        return Result.Failure<byte[]>("Invalid message type for HL7 encoding", ErrorCodes.ProtocolError);
    }

    public async IAsyncEnumerable<ProtocolMessage> OnBytesReceivedAsync(
        ReadOnlyMemory<byte> data,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Copy to array to avoid ref struct iteration in async method
        var bytes = data.ToArray();

        foreach (var b in bytes)
        {
            if (b == VT)
            {
                _buffer.Clear();
                _buffer.Add(b);
                _inFrame = true;
            }
            else if (_inFrame)
            {
                _buffer.Add(b);

                // Check for end of MLLP frame (FS + CR)
                if (_buffer.Count >= 3 && _buffer[^2] == FS && _buffer[^1] == CR)
                {
                    var frameData = _buffer.ToArray();
                    _buffer.Clear();
                    _inFrame = false;

                    var result = Decode(frameData);
                    if (result.IsSuccess && result.Value != null)
                    {
                        yield return result.Value;
                    }
                }
            }
        }

        await Task.CompletedTask; // Keep async signature
    }

    public void Reset()
    {
        _buffer.Clear();
        _inFrame = false;
    }

    private Hl7Message ParseHl7Message(string content)
    {
        var segments = new List<Hl7Segment>();
        var lines = content.Split('\r', StringSplitOptions.RemoveEmptyEntries);

        string messageType = "UNKNOWN";
        string? controlId = null;

        foreach (var line in lines)
        {
            var fields = line.Split('|');
            if (fields.Length > 0)
            {
                var segment = new Hl7Segment
                {
                    Name = fields[0],
                    Fields = fields.Skip(1).ToList()
                };
                segments.Add(segment);

                // Extract message type from MSH-9
                if (fields[0] == "MSH" && fields.Length > 9)
                {
                    messageType = fields[8]; // MSH-9 (0-indexed: MSH is 0, then |^~\& is 1, etc.)
                    if (fields.Length > 10)
                    {
                        controlId = fields[9]; // MSH-10
                    }
                }
            }
        }

        return new Hl7Message
        {
            Content = content,
            MessageTypeCode = messageType,
            MessageControlId = controlId,
            Segments = segments
        };
    }

    /// <summary>
    /// Builds an ACK message for a received HL7 message.
    /// </summary>
    public static Hl7AckMessage BuildAck(
        Hl7Message originalMessage,
        string ackCode = "AA",
        string? textMessage = null)
    {
        var now = DateTime.Now;
        var messageId = Guid.NewGuid().ToString("N")[..10];

        // Simple ACK format
        var content = $"MSH|^~\\&|ACK|SIMULATOR|{originalMessage.Segments.FirstOrDefault()?.GetField(2) ?? "SENDER"}|{originalMessage.Segments.FirstOrDefault()?.GetField(3) ?? "FACILITY"}|{now:yyyyMMddHHmmss}||ACK|{messageId}|P|2.5\r" +
                      $"MSA|{ackCode}|{originalMessage.MessageControlId ?? "0"}|{textMessage ?? "Message received"}\r";

        return new Hl7AckMessage
        {
            Content = content,
            MessageTypeCode = "ACK",
            MessageControlId = messageId,
            AcknowledgmentCode = ackCode,
            TextMessage = textMessage
        };
    }
}
