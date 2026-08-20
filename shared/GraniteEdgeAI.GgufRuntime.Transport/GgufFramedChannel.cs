using GraniteEdgeAI.GgufRuntime.Contracts.Commands;
using GraniteEdgeAI.GgufRuntime.Contracts.Events;

namespace GraniteEdgeAI.GgufRuntime.Transport;

public sealed class GgufFramedChannel : IDisposable
{
    private readonly Stream _input;
    private readonly Stream _output;
    private readonly int _maxPendingWrites;
    private readonly SemaphoreSlim _readGate = new(1, 1);
    private readonly SemaphoreSlim _writeGate = new(1, 1);
    private int _pendingWrites;
    private bool _disposed;

    public GgufFramedChannel(Stream input, Stream output, int maxPendingWrites = 32)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxPendingWrites);
        _input = input;
        _output = output;
        _maxPendingWrites = maxPendingWrites;
    }

    public ValueTask WriteCommandAsync(
        GgufRuntimeCommand command,
        CancellationToken cancellationToken)
    {
        return WriteAsync(
            GgufProtocolSerializer.SerializeCommand(command),
            cancellationToken);
    }

    public ValueTask WriteEventAsync(
        GgufRuntimeEvent runtimeEvent,
        CancellationToken cancellationToken)
    {
        return WriteAsync(
            GgufProtocolSerializer.SerializeEvent(runtimeEvent),
            cancellationToken);
    }

    public async ValueTask<GgufRuntimeCommand> ReadCommandAsync(
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        await _readGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            byte[] payload = await GgufFrameReader.ReadAsync(
                _input,
                cancellationToken).ConfigureAwait(false);
            return GgufProtocolSerializer.DeserializeCommand(payload);
        }
        finally
        {
            _readGate.Release();
        }
    }

    public async ValueTask<GgufRuntimeEvent> ReadEventAsync(
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        await _readGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            byte[] payload = await GgufFrameReader.ReadAsync(
                _input,
                cancellationToken).ConfigureAwait(false);
            return GgufProtocolSerializer.DeserializeEvent(payload);
        }
        finally
        {
            _readGate.Release();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _readGate.Dispose();
        _writeGate.Dispose();
    }

    private async ValueTask WriteAsync(
        ReadOnlyMemory<byte> payload,
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        int pending = Interlocked.Increment(ref _pendingWrites);
        if (pending > _maxPendingWrites)
        {
            Interlocked.Decrement(ref _pendingWrites);
            throw new GgufTransportException("The pending write limit was exceeded.");
        }

        bool gateHeld = false;
        try
        {
            await _writeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            gateHeld = true;
            await GgufFrameWriter.WriteAsync(
                _output,
                payload,
                cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            if (gateHeld)
            {
                _writeGate.Release();
            }

            Interlocked.Decrement(ref _pendingWrites);
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
