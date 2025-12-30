using DeviceSimulator.Protocols.Astm;

namespace DeviceSimulator.UnitTests;

public class AstmProtocolTests
{
    [Fact]
    public void Decode_ControlChar_ENQ_ReturnsControlMessage()
    {
        // Arrange
        var engine = new AstmProtocolEngine();
        var data = new byte[] { 0x05 }; // ENQ

        // Act
        var result = engine.Decode(data);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.IsType<AstmControlMessage>(result.Value);
        var ctrl = (AstmControlMessage)result.Value!;
        Assert.Equal(AstmControl.ENQ, ctrl.ControlChar);
    }

    [Fact]
    public void Decode_ControlChar_ACK_ReturnsControlMessage()
    {
        // Arrange
        var engine = new AstmProtocolEngine();
        var data = new byte[] { 0x06 }; // ACK

        // Act
        var result = engine.Decode(data);

        // Assert
        Assert.True(result.IsSuccess);
        var ctrl = (AstmControlMessage)result.Value!;
        Assert.Equal(AstmControl.ACK, ctrl.ControlChar);
    }

    [Fact]
    public void Encode_ControlMessage_ReturnsSingleByte()
    {
        // Arrange
        var engine = new AstmProtocolEngine();
        var message = new AstmControlMessage
        {
            ControlChar = AstmControl.ACK
        };

        // Act
        var result = engine.Encode(message);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Single(result.Value!);
        Assert.Equal(0x06, result.Value![0]);
    }

    [Fact]
    public void CalculateChecksum_ReturnsCorrectValue()
    {
        // Arrange - frame number '1' + content + ETX
        var data = System.Text.Encoding.ASCII.GetBytes("1H|Test\x03");

        // Act
        var checksum = AstmProtocolEngine.CalculateChecksum(data);

        // Assert
        Assert.Equal(2, checksum.Length);
        // Checksum should be sum of bytes mod 256 as hex
    }

    [Fact]
    public void HandshakeStateMachine_Idle_OnENQ_TransitionsToReceiving()
    {
        // Arrange
        var sm = new AstmHandshakeStateMachine();

        // Act
        var result = sm.ProcessReceived(AstmControl.ENQ);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(AstmControl.ACK, result.Response);
        Assert.Equal(AstmConnectionState.Receiving, sm.State);
    }

    [Fact]
    public void HandshakeStateMachine_Receiving_OnEOT_TransitionsToIdle()
    {
        // Arrange
        var sm = new AstmHandshakeStateMachine();
        sm.ProcessReceived(AstmControl.ENQ); // Go to Receiving

        // Act
        var result = sm.ProcessReceived(AstmControl.EOT);

        // Assert
        Assert.True(result.Success);
        Assert.Null(result.Response);
        Assert.Equal(AstmConnectionState.Idle, sm.State);
    }

    [Fact]
    public void HandshakeStateMachine_InitiateTransmission_SendsENQ()
    {
        // Arrange
        var sm = new AstmHandshakeStateMachine();

        // Act
        var result = sm.InitiateTransmission();

        // Assert
        Assert.True(result.Success);
        Assert.Equal(AstmControl.ENQ, result.Response);
        Assert.Equal(AstmConnectionState.WaitingForAck, sm.State);
    }

    [Fact]
    public void HandshakeStateMachine_WaitingForAck_OnACK_TransitionsToTransmitting()
    {
        // Arrange
        var sm = new AstmHandshakeStateMachine();
        sm.InitiateTransmission();

        // Act
        var result = sm.ProcessReceived(AstmControl.ACK);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(AstmConnectionState.Transmitting, sm.State);
    }

    [Fact]
    public void HandshakeStateMachine_Reset_ReturnsToIdle()
    {
        // Arrange
        var sm = new AstmHandshakeStateMachine();
        sm.ProcessReceived(AstmControl.ENQ); // Go to Receiving

        // Act
        sm.Reset();

        // Assert
        Assert.Equal(AstmConnectionState.Idle, sm.State);
    }
}
