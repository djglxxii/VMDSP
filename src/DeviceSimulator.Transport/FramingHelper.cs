namespace DeviceSimulator.Transport;

/// <summary>
/// Helper utilities for protocol framing.
/// </summary>
public static class FramingHelper
{
    /// <summary>
    /// MLLP (Minimal Lower Layer Protocol) start byte.
    /// </summary>
    public const byte MllpStartByte = 0x0B; // VT (Vertical Tab)

    /// <summary>
    /// MLLP end byte 1.
    /// </summary>
    public const byte MllpEndByte1 = 0x1C; // FS (File Separator)

    /// <summary>
    /// MLLP end byte 2 (CR).
    /// </summary>
    public const byte MllpEndByte2 = 0x0D; // CR

    /// <summary>
    /// ASTM control characters.
    /// </summary>
    public const byte STX = 0x02; // Start of Text
    public const byte ETX = 0x03; // End of Text
    public const byte EOT = 0x04; // End of Transmission
    public const byte ENQ = 0x05; // Enquiry
    public const byte ACK = 0x06; // Acknowledge
    public const byte NAK = 0x15; // Negative Acknowledge
    public const byte ETB = 0x17; // End of Transmission Block
    public const byte LF = 0x0A;  // Line Feed
    public const byte CR = 0x0D;  // Carriage Return

    /// <summary>
    /// Wraps data in MLLP framing.
    /// </summary>
    public static byte[] WrapMllp(byte[] data)
    {
        var result = new byte[data.Length + 3];
        result[0] = MllpStartByte;
        Array.Copy(data, 0, result, 1, data.Length);
        result[^2] = MllpEndByte1;
        result[^1] = MllpEndByte2;
        return result;
    }

    /// <summary>
    /// Unwraps MLLP framing from data.
    /// </summary>
    public static byte[]? UnwrapMllp(byte[] data)
    {
        if (data.Length < 3)
        {
            return null;
        }

        if (data[0] != MllpStartByte || data[^2] != MllpEndByte1 || data[^1] != MllpEndByte2)
        {
            return null;
        }

        var result = new byte[data.Length - 3];
        Array.Copy(data, 1, result, 0, result.Length);
        return result;
    }

    /// <summary>
    /// Calculates ASTM checksum for a frame.
    /// </summary>
    public static byte[] CalculateAstmChecksum(byte[] frameData)
    {
        var sum = 0;
        foreach (var b in frameData)
        {
            sum += b;
        }

        // Checksum is sum mod 256, represented as 2 hex digits
        var checksum = (sum % 256).ToString("X2");
        return System.Text.Encoding.ASCII.GetBytes(checksum);
    }

    /// <summary>
    /// Converts bytes to hex string for display.
    /// </summary>
    public static string ToHexString(ReadOnlySpan<byte> data)
    {
        return BitConverter.ToString(data.ToArray()).Replace("-", " ");
    }

    /// <summary>
    /// Converts bytes to printable string with control char escaping.
    /// </summary>
    public static string ToPrintableString(ReadOnlySpan<byte> data)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var b in data)
        {
            sb.Append(b switch
            {
                STX => "<STX>",
                ETX => "<ETX>",
                EOT => "<EOT>",
                ENQ => "<ENQ>",
                ACK => "<ACK>",
                NAK => "<NAK>",
                ETB => "<ETB>",
                LF => "<LF>",
                CR => "<CR>",
                MllpStartByte => "<VT>",
                MllpEndByte1 => "<FS>",
                >= 0x20 and <= 0x7E => ((char)b).ToString(),
                _ => $"<0x{b:X2}>"
            });
        }
        return sb.ToString();
    }
}
