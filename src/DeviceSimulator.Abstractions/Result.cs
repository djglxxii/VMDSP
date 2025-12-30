namespace DeviceSimulator.Abstractions;

/// <summary>
/// Result type for operations that can fail.
/// </summary>
public record Result
{
    public bool IsSuccess { get; init; }
    public string? ErrorMessage { get; init; }
    public string? ErrorCode { get; init; }

    public static Result Success() => new() { IsSuccess = true };

    public static Result Failure(string message, string? code = null) =>
        new() { IsSuccess = false, ErrorMessage = message, ErrorCode = code };

    public static Result<T> Success<T>(T value) =>
        new() { IsSuccess = true, Value = value };

    public static Result<T> Failure<T>(string message, string? code = null) =>
        new() { IsSuccess = false, ErrorMessage = message, ErrorCode = code };
}

/// <summary>
/// Result type with a value.
/// </summary>
public record Result<T> : Result
{
    public T? Value { get; init; }

    public T GetValueOrThrow()
    {
        if (!IsSuccess || Value is null)
        {
            throw new InvalidOperationException(ErrorMessage ?? "Operation failed");
        }
        return Value;
    }
}

/// <summary>
/// Common error codes.
/// </summary>
public static class ErrorCodes
{
    public const string NotFound = "NOT_FOUND";
    public const string InvalidConfiguration = "INVALID_CONFIG";
    public const string ConnectionFailed = "CONNECTION_FAILED";
    public const string Timeout = "TIMEOUT";
    public const string ProtocolError = "PROTOCOL_ERROR";
    public const string InvalidState = "INVALID_STATE";
    public const string PackLoadFailed = "PACK_LOAD_FAILED";
    public const string ValidationFailed = "VALIDATION_FAILED";
}
