using System.Runtime.CompilerServices;
using System.Text;
using DeviceSimulator.Abstractions;

namespace DeviceSimulator.Protocols.Astm;

/// <summary>
/// ASTM E1381/E1394 protocol engine.
/// </summary>
public class AstmProtocolEngine : IProtocolEngine
{
    private const byte STX = 0x02;
    private const byte ETX = 0x03;
    private const byte EOT = 0x04;
    private const byte ENQ = 0x05;
    private const byte ACK = 0x06;
    private const byte NAK = 0x15;
    private const byte ETB = 0x17;
    private const byte CR = 0x0D;
    private const byte LF = 0x0A;

    private readonly List<byte> _buffer = new();
    private AstmFrameState _frameState = AstmFrameState.Idle;

    public string ProtocolId => "ASTM-E1381";

    public Result<ProtocolMessage> Decode(ReadOnlySpan<byte> data)
    {
        if (data.Length == 0)
        {
            return Result.Failure<ProtocolMessage>("Empty data", ErrorCodes.ProtocolError);
        }

        // Single control character
        if (data.Length == 1)
        {
            var ctrl = data[0];
            if (IsControlChar(ctrl))
            {
                return Result.Success<ProtocolMessage>(new AstmControlMessage
                {
                    ControlChar = (AstmControl)ctrl,
                    RawBytes = data.ToArray()
                });
            }
        }

        // Data frame: <STX>FN...data...<ETX>CS<CR><LF>
        if (data[0] == STX)
        {
            return DecodeDataFrame(data);
        }

        return Result.Failure<ProtocolMessage>("Unrecognized ASTM data format", ErrorCodes.ProtocolError);
    }

    private Result<ProtocolMessage> DecodeDataFrame(ReadOnlySpan<byte> data)
    {
        // Minimum: STX + FN + ETX/ETB + CS(2) + CR + LF = 7 bytes
        if (data.Length < 7)
        {
            return Result.Failure<ProtocolMessage>("Frame too short", ErrorCodes.ProtocolError);
        }

        var frameNumber = data[1] - '0';
        var isIntermediate = false;
        var etxIndex = -1;

        for (int i = 2; i < data.Length; i++)
        {
            if (data[i] == ETX)
            {
                etxIndex = i;
                break;
            }
            if (data[i] == ETB)
            {
                etxIndex = i;
                isIntermediate = true;
                break;
            }
        }

        if (etxIndex == -1)
        {
            return Result.Failure<ProtocolMessage>("Missing ETX/ETB", ErrorCodes.ProtocolError);
        }

        var content = Encoding.ASCII.GetString(data[2..etxIndex]);
        var checksum = data.Length >= etxIndex + 3
            ? data[(etxIndex + 1)..(etxIndex + 3)].ToArray()
            : null;

        return Result.Success<ProtocolMessage>(new AstmDataFrame
        {
            FrameNumber = frameNumber,
            Content = content,
            IsIntermediate = isIntermediate,
            Checksum = checksum,
            RawBytes = data.ToArray()
        });
    }

    public Result<byte[]> Encode(ProtocolMessage message)
    {
        if (message is AstmControlMessage ctrl)
        {
            return Result.Success(new[] { (byte)ctrl.ControlChar });
        }

        if (message is AstmDataFrame frame)
        {
            return EncodeDataFrame(frame);
        }

        return Result.Failure<byte[]>("Invalid message type for ASTM encoding", ErrorCodes.ProtocolError);
    }

    private Result<byte[]> EncodeDataFrame(AstmDataFrame frame)
    {
        var content = Encoding.ASCII.GetBytes(frame.Content);
        var terminator = frame.IsIntermediate ? ETB : ETX;

        // Build frame for checksum calculation (FN + content + ETX/ETB)
        var forChecksum = new byte[1 + content.Length + 1];
        forChecksum[0] = (byte)('0' + frame.FrameNumber);
        Array.Copy(content, 0, forChecksum, 1, content.Length);
        forChecksum[^1] = terminator;

        var checksum = CalculateChecksum(forChecksum);

        // Full frame: STX + FN + content + ETX/ETB + CS + CR + LF
        var result = new byte[1 + forChecksum.Length + 2 + 2];
        result[0] = STX;
        Array.Copy(forChecksum, 0, result, 1, forChecksum.Length);
        Array.Copy(checksum, 0, result, 1 + forChecksum.Length, 2);
        result[^2] = CR;
        result[^1] = LF;

        return Result.Success(result);
    }

    public async IAsyncEnumerable<ProtocolMessage> OnBytesReceivedAsync(
        ReadOnlyMemory<byte> data,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Copy to array to avoid ref struct iteration in async method
        var bytes = data.ToArray();

        foreach (var b in bytes)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Single control characters are emitted immediately
            if (_frameState == AstmFrameState.Idle && IsControlChar(b) && b != STX)
            {
                yield return new AstmControlMessage
                {
                    ControlChar = (AstmControl)b,
                    RawBytes = new[] { b }
                };
                continue;
            }

            // Start of data frame
            if (b == STX && _frameState == AstmFrameState.Idle)
            {
                _buffer.Clear();
                _buffer.Add(b);
                _frameState = AstmFrameState.InFrame;
                continue;
            }

            // Collecting frame data
            if (_frameState == AstmFrameState.InFrame)
            {
                _buffer.Add(b);

                // Check for end of frame (ETX/ETB + CS + CR + LF)
                if (b == LF && _buffer.Count >= 7)
                {
                    var frameData = _buffer.ToArray();
                    _buffer.Clear();
                    _frameState = AstmFrameState.Idle;

                    var result = Decode(frameData);
                    if (result.IsSuccess && result.Value != null)
                    {
                        yield return result.Value;
                    }
                }
            }
        }

        await Task.CompletedTask;
    }

    public void Reset()
    {
        _buffer.Clear();
        _frameState = AstmFrameState.Idle;
    }

    private static bool IsControlChar(byte b)
    {
        return b == ENQ || b == ACK || b == NAK || b == EOT || b == STX;
    }

    /// <summary>
    /// Calculates ASTM checksum.
    /// Sum of bytes from frame number to and including ETX/ETB, modulo 256, as 2 hex chars.
    /// </summary>
    public static byte[] CalculateChecksum(ReadOnlySpan<byte> data)
    {
        var sum = 0;
        foreach (var b in data)
        {
            sum += b;
        }

        var hex = (sum % 256).ToString("X2");
        return Encoding.ASCII.GetBytes(hex);
    }

    /// <summary>
    /// Verifies checksum of a received frame.
    /// </summary>
    public static bool VerifyChecksum(AstmDataFrame frame)
    {
        if (frame.RawBytes == null || frame.Checksum == null)
        {
            return false;
        }

        // Find the range from frame number to ETX/ETB
        var stxIndex = Array.IndexOf(frame.RawBytes, STX);
        var etxIndex = Array.FindIndex(frame.RawBytes, b => b == ETX || b == ETB);

        if (stxIndex == -1 || etxIndex == -1)
        {
            return false;
        }

        var checksumData = frame.RawBytes[(stxIndex + 1)..(etxIndex + 1)];
        var expected = CalculateChecksum(checksumData);

        return frame.Checksum.SequenceEqual(expected);
    }
}

internal enum AstmFrameState
{
    Idle,
    InFrame
}
