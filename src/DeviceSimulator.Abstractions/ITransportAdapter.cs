namespace DeviceSimulator.Abstractions;

/// <summary>
/// Abstraction for transport-level communication (TCP, etc.).
/// </summary>
public interface ITransportAdapter : IAsyncDisposable
{
    /// <summary>
    /// Unique identifier for this transport instance.
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Current connection state.
    /// </summary>
    TransportState State { get; }

    /// <summary>
    /// Starts the transport adapter (listen or connect).
    /// </summary>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops the transport adapter gracefully.
    /// </summary>
    Task StopAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends raw bytes to the remote endpoint.
    /// </summary>
    Task SendAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default);

    /// <summary>
    /// Event raised when bytes are received.
    /// </summary>
    event EventHandler<BytesReceivedEventArgs>? BytesReceived;

    /// <summary>
    /// Event raised when connection state changes.
    /// </summary>
    event EventHandler<TransportStateChangedEventArgs>? StateChanged;

    /// <summary>
    /// Event raised on transport errors.
    /// </summary>
    event EventHandler<TransportErrorEventArgs>? Error;
}

public enum TransportState
{
    Disconnected,
    Connecting,
    Connected,
    Disconnecting,
    Error
}

public class BytesReceivedEventArgs : EventArgs
{
    public required ReadOnlyMemory<byte> Data { get; init; }
    public required DateTime Timestamp { get; init; }
}

public class TransportStateChangedEventArgs : EventArgs
{
    public required TransportState OldState { get; init; }
    public required TransportState NewState { get; init; }
    public required DateTime Timestamp { get; init; }
}

public class TransportErrorEventArgs : EventArgs
{
    public required Exception Exception { get; init; }
    public required DateTime Timestamp { get; init; }
}
