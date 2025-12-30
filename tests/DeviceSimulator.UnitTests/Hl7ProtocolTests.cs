using DeviceSimulator.Protocols.Hl7;

namespace DeviceSimulator.UnitTests;

public class Hl7ProtocolTests
{
    [Fact]
    public void Decode_ValidMllpMessage_ReturnsHl7Message()
    {
        // Arrange
        var engine = new Hl7ProtocolEngine();
        var hl7Content = "MSH|^~\\&|SENDER|FACILITY|RECEIVER|DEST|20231201120000||ORU^R01|12345|P|2.5\rPID|1||12345||Doe^John\r";
        var mllpFrame = WrapMllp(hl7Content);

        // Act
        var result = engine.Decode(mllpFrame);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.IsType<Hl7Message>(result.Value);
        var hl7 = (Hl7Message)result.Value!;
        Assert.Equal("ORU^R01", hl7.MessageTypeCode);
        Assert.Equal(hl7Content, hl7.Content);
    }

    [Fact]
    public void Decode_InvalidFraming_ReturnsFailure()
    {
        // Arrange
        var engine = new Hl7ProtocolEngine();
        var invalidData = System.Text.Encoding.UTF8.GetBytes("invalid data");

        // Act
        var result = engine.Decode(invalidData);

        // Assert
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void Encode_Hl7Message_ReturnsMllpFramedData()
    {
        // Arrange
        var engine = new Hl7ProtocolEngine();
        var message = new Hl7Message
        {
            Content = "MSH|^~\\&|TEST\r",
            MessageTypeCode = "TEST"
        };

        // Act
        var result = engine.Encode(message);

        // Assert
        Assert.True(result.IsSuccess);
        var data = result.Value!;

        // Verify MLLP framing
        Assert.Equal(0x0B, data[0]); // VT
        Assert.Equal(0x1C, data[^2]); // FS
        Assert.Equal(0x0D, data[^1]); // CR
    }

    [Fact]
    public void BuildAck_ReturnsValidAckMessage()
    {
        // Arrange
        var originalMessage = new Hl7Message
        {
            Content = "MSH|^~\\&|SENDER|FAC|RECV|DEST|20231201||ORU^R01|MSG001|P|2.5\r",
            MessageTypeCode = "ORU^R01",
            MessageControlId = "MSG001",
            Segments = new List<Hl7Segment>
            {
                new Hl7Segment
                {
                    Name = "MSH",
                    Fields = new List<string> { "^~\\&", "SENDER", "FAC", "RECV", "DEST" }
                }
            }
        };

        // Act
        var ack = Hl7ProtocolEngine.BuildAck(originalMessage);

        // Assert
        Assert.Equal("ACK", ack.MessageTypeCode);
        Assert.Equal("AA", ack.AcknowledgmentCode);
        Assert.Contains("MSA|AA|MSG001", ack.Content);
    }

    [Fact]
    public void Decode_ParsesSegments()
    {
        // Arrange
        var engine = new Hl7ProtocolEngine();
        var content = "MSH|^~\\&|SENDER|FAC|RECV|DEST|20231201||ORU^R01|12345|P|2.5\rPID|1||PAT001||Doe^John\rOBX|1|NM|GLU||95|mg/dL\r";
        var mllpFrame = WrapMllp(content);

        // Act
        var result = engine.Decode(mllpFrame);

        // Assert
        Assert.True(result.IsSuccess);
        var hl7 = (Hl7Message)result.Value!;
        Assert.Equal(3, hl7.Segments.Count);
        Assert.Equal("MSH", hl7.Segments[0].Name);
        Assert.Equal("PID", hl7.Segments[1].Name);
        Assert.Equal("OBX", hl7.Segments[2].Name);
    }

    private static byte[] WrapMllp(string content)
    {
        var contentBytes = System.Text.Encoding.UTF8.GetBytes(content);
        var result = new byte[contentBytes.Length + 3];
        result[0] = 0x0B; // VT - Start
        Array.Copy(contentBytes, 0, result, 1, contentBytes.Length);
        result[^2] = 0x1C; // FS
        result[^1] = 0x0D; // CR
        return result;
    }
}
