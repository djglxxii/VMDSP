namespace DeviceSimulator.Protocols.Astm;

/// <summary>
/// ASTM E1381 handshake state machine (establishment and termination phases).
/// </summary>
public class AstmHandshakeStateMachine
{
    private AstmConnectionState _state = AstmConnectionState.Idle;
    private int _frameNumber = 1;
    private int _retryCount;
    private const int MaxRetries = 6;

    public AstmConnectionState State => _state;
    public int FrameNumber => _frameNumber;

    public event EventHandler<AstmStateChangedEventArgs>? StateChanged;

    /// <summary>
    /// Process a received control character and return the response (if any).
    /// </summary>
    public AstmHandshakeResult ProcessReceived(AstmControl control)
    {
        var oldState = _state;
        AstmHandshakeResult result;

        switch (_state)
        {
            case AstmConnectionState.Idle:
                result = ProcessIdle(control);
                break;

            case AstmConnectionState.WaitingForAck:
                result = ProcessWaitingForAck(control);
                break;

            case AstmConnectionState.Receiving:
                result = ProcessReceiving(control);
                break;

            case AstmConnectionState.Transmitting:
                result = ProcessTransmitting(control);
                break;

            default:
                result = new AstmHandshakeResult { Success = false, ErrorMessage = "Unknown state" };
                break;
        }

        if (_state != oldState)
        {
            StateChanged?.Invoke(this, new AstmStateChangedEventArgs
            {
                OldState = oldState,
                NewState = _state,
                Trigger = control.ToString()
            });
        }

        return result;
    }

    private AstmHandshakeResult ProcessIdle(AstmControl control)
    {
        if (control == AstmControl.ENQ)
        {
            // Remote wants to transmit
            _state = AstmConnectionState.Receiving;
            _frameNumber = 1;
            return new AstmHandshakeResult
            {
                Success = true,
                Response = AstmControl.ACK
            };
        }

        return new AstmHandshakeResult
        {
            Success = false,
            ErrorMessage = $"Unexpected {control} in Idle state"
        };
    }

    private AstmHandshakeResult ProcessWaitingForAck(AstmControl control)
    {
        if (control == AstmControl.ACK)
        {
            // Ready to send data
            _state = AstmConnectionState.Transmitting;
            _retryCount = 0;
            return new AstmHandshakeResult { Success = true };
        }

        if (control == AstmControl.NAK)
        {
            _retryCount++;
            if (_retryCount >= MaxRetries)
            {
                _state = AstmConnectionState.Idle;
                return new AstmHandshakeResult
                {
                    Success = false,
                    ErrorMessage = "Max retries exceeded"
                };
            }

            // Retry ENQ
            return new AstmHandshakeResult
            {
                Success = true,
                Response = AstmControl.ENQ
            };
        }

        if (control == AstmControl.ENQ)
        {
            // Contention - remote also wants to transmit
            // We yield (send EOT and go idle)
            _state = AstmConnectionState.Idle;
            return new AstmHandshakeResult
            {
                Success = true,
                Response = AstmControl.EOT,
                Contention = true
            };
        }

        return new AstmHandshakeResult
        {
            Success = false,
            ErrorMessage = $"Unexpected {control} in WaitingForAck state"
        };
    }

    private AstmHandshakeResult ProcessReceiving(AstmControl control)
    {
        if (control == AstmControl.EOT)
        {
            // Transmission complete
            _state = AstmConnectionState.Idle;
            return new AstmHandshakeResult { Success = true };
        }

        // Other controls in receiving state are protocol errors
        return new AstmHandshakeResult
        {
            Success = false,
            ErrorMessage = $"Unexpected {control} in Receiving state"
        };
    }

    private AstmHandshakeResult ProcessTransmitting(AstmControl control)
    {
        if (control == AstmControl.ACK)
        {
            // Frame acknowledged, increment frame number (1-7, wrapping)
            _frameNumber = (_frameNumber % 7) + 1;
            _retryCount = 0;
            return new AstmHandshakeResult { Success = true };
        }

        if (control == AstmControl.NAK)
        {
            _retryCount++;
            if (_retryCount >= MaxRetries)
            {
                _state = AstmConnectionState.Idle;
                return new AstmHandshakeResult
                {
                    Success = false,
                    ErrorMessage = "Max frame retries exceeded",
                    Response = AstmControl.EOT
                };
            }

            // Need to retransmit last frame
            return new AstmHandshakeResult
            {
                Success = true,
                RetransmitRequired = true
            };
        }

        return new AstmHandshakeResult
        {
            Success = false,
            ErrorMessage = $"Unexpected {control} in Transmitting state"
        };
    }

    /// <summary>
    /// Initiates a transmission (sends ENQ).
    /// </summary>
    public AstmHandshakeResult InitiateTransmission()
    {
        if (_state != AstmConnectionState.Idle)
        {
            return new AstmHandshakeResult
            {
                Success = false,
                ErrorMessage = $"Cannot initiate transmission from state {_state}"
            };
        }

        _state = AstmConnectionState.WaitingForAck;
        _retryCount = 0;
        _frameNumber = 1;

        return new AstmHandshakeResult
        {
            Success = true,
            Response = AstmControl.ENQ
        };
    }

    /// <summary>
    /// Ends the current transmission.
    /// </summary>
    public AstmHandshakeResult EndTransmission()
    {
        if (_state != AstmConnectionState.Transmitting)
        {
            return new AstmHandshakeResult
            {
                Success = false,
                ErrorMessage = $"Cannot end transmission from state {_state}"
            };
        }

        _state = AstmConnectionState.Idle;
        return new AstmHandshakeResult
        {
            Success = true,
            Response = AstmControl.EOT
        };
    }

    /// <summary>
    /// Resets to idle state.
    /// </summary>
    public void Reset()
    {
        _state = AstmConnectionState.Idle;
        _frameNumber = 1;
        _retryCount = 0;
    }
}

public enum AstmConnectionState
{
    Idle,
    WaitingForAck,
    Receiving,
    Transmitting
}

public class AstmHandshakeResult
{
    public bool Success { get; init; }
    public AstmControl? Response { get; init; }
    public string? ErrorMessage { get; init; }
    public bool RetransmitRequired { get; init; }
    public bool Contention { get; init; }
}

public class AstmStateChangedEventArgs : EventArgs
{
    public required AstmConnectionState OldState { get; init; }
    public required AstmConnectionState NewState { get; init; }
    public required string Trigger { get; init; }
}
