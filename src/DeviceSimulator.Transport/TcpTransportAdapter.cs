using System.Net;
using System.Net.Sockets;
using DeviceSimulator.Abstractions;

namespace DeviceSimulator.Transport;

/// <summary>
/// TCP transport adapter for client connections.
/// </summary>
public class TcpTransportAdapter : ITransportAdapter
{
    private readonly TcpTransportOptions _options;
    private TcpClient? _client;
    private NetworkStream? _stream;
    private CancellationTokenSource? _readCts;
    private TransportState _state = TransportState.Disconnected;

    public string Id { get; }
    public TransportState State
    {
        get => _state;
        private set
        {
            if (_state != value)
            {
                var oldState = _state;
                _state = value;
                StateChanged?.Invoke(this, new TransportStateChangedEventArgs
                {
                    OldState = oldState,
                    NewState = value,
                    Timestamp = DateTime.UtcNow
                });
            }
        }
    }

    public event EventHandler<BytesReceivedEventArgs>? BytesReceived;
    public event EventHandler<TransportStateChangedEventArgs>? StateChanged;
    public event EventHandler<TransportErrorEventArgs>? Error;

    public TcpTransportAdapter(TcpTransportOptions options)
    {
        _options = options;
        Id = options.Id ?? Guid.NewGuid().ToString("N")[..8];
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (State != TransportState.Disconnected)
        {
            throw new InvalidOperationException($"Cannot start from state {State}");
        }

        State = TransportState.Connecting;

        try
        {
            _client = new TcpClient();
            await _client.ConnectAsync(_options.Host, _options.Port, cancellationToken);
            _stream = _client.GetStream();
            State = TransportState.Connected;

            _readCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _ = ReadLoopAsync(_readCts.Token);
        }
        catch (Exception ex)
        {
            State = TransportState.Error;
            Error?.Invoke(this, new TransportErrorEventArgs
            {
                Exception = ex,
                Timestamp = DateTime.UtcNow
            });
            throw;
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (State == TransportState.Disconnected)
        {
            return;
        }

        State = TransportState.Disconnecting;

        _readCts?.Cancel();

        if (_stream != null)
        {
            await _stream.DisposeAsync();
            _stream = null;
        }

        _client?.Dispose();
        _client = null;

        State = TransportState.Disconnected;
    }

    public async Task SendAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default)
    {
        if (State != TransportState.Connected || _stream == null)
        {
            throw new InvalidOperationException("Not connected");
        }

        await _stream.WriteAsync(data, cancellationToken);
        await _stream.FlushAsync(cancellationToken);
    }

    private async Task ReadLoopAsync(CancellationToken cancellationToken)
    {
        var buffer = new byte[_options.BufferSize];

        try
        {
            while (!cancellationToken.IsCancellationRequested && _stream != null)
            {
                var bytesRead = await _stream.ReadAsync(buffer, cancellationToken);

                if (bytesRead == 0)
                {
                    // Connection closed
                    break;
                }

                var data = new byte[bytesRead];
                Array.Copy(buffer, data, bytesRead);

                BytesReceived?.Invoke(this, new BytesReceivedEventArgs
                {
                    Data = data,
                    Timestamp = DateTime.UtcNow
                });
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown
        }
        catch (Exception ex)
        {
            Error?.Invoke(this, new TransportErrorEventArgs
            {
                Exception = ex,
                Timestamp = DateTime.UtcNow
            });
        }
        finally
        {
            if (State == TransportState.Connected)
            {
                State = TransportState.Disconnected;
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Options for TCP transport.
/// </summary>
public record TcpTransportOptions
{
    public string? Id { get; init; }
    public required string Host { get; init; }
    public required int Port { get; init; }
    public int BufferSize { get; init; } = 4096;
    public int ConnectTimeoutMs { get; init; } = 5000;
}
