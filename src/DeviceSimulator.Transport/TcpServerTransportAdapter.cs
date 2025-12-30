using System.Net;
using System.Net.Sockets;
using DeviceSimulator.Abstractions;

namespace DeviceSimulator.Transport;

/// <summary>
/// TCP server transport adapter that listens for connections.
/// </summary>
public class TcpServerTransportAdapter : ITransportAdapter
{
    private readonly TcpServerOptions _options;
    private TcpListener? _listener;
    private TcpClient? _client;
    private NetworkStream? _stream;
    private CancellationTokenSource? _cts;
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

    public TcpServerTransportAdapter(TcpServerOptions options)
    {
        _options = options;
        Id = options.Id ?? Guid.NewGuid().ToString("N")[..8];
    }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (State != TransportState.Disconnected)
        {
            throw new InvalidOperationException($"Cannot start from state {State}");
        }

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        try
        {
            _listener = new TcpListener(IPAddress.Parse(_options.ListenAddress), _options.Port);
            _listener.Start();
            State = TransportState.Connecting; // Listening for connections

            _ = AcceptLoopAsync(_cts.Token);

            return Task.CompletedTask;
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

    private async Task AcceptLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested && _listener != null)
            {
                _client = await _listener.AcceptTcpClientAsync(cancellationToken);
                _stream = _client.GetStream();
                State = TransportState.Connected;

                await ReadLoopAsync(cancellationToken);

                // After disconnect, go back to listening
                if (State != TransportState.Disconnecting && State != TransportState.Disconnected)
                {
                    State = TransportState.Connecting;
                }
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
                    break; // Connection closed
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
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (State == TransportState.Disconnected)
        {
            return;
        }

        State = TransportState.Disconnecting;

        _cts?.Cancel();

        if (_stream != null)
        {
            await _stream.DisposeAsync();
            _stream = null;
        }

        _client?.Dispose();
        _client = null;

        _listener?.Stop();
        _listener = null;

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

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Options for TCP server.
/// </summary>
public record TcpServerOptions
{
    public string? Id { get; init; }
    public string ListenAddress { get; init; } = "0.0.0.0";
    public required int Port { get; init; }
    public int BufferSize { get; init; } = 4096;
}
