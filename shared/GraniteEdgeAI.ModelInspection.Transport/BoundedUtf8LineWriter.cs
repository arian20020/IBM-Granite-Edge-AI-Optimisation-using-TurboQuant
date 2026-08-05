namespace GraniteEdgeAI.ModelInspection.Transport;

/// <summary>
/// Writes strict UTF-8 protocol payloads as serialized LF-delimited frames.
/// </summary>
/// <remarks>
/// The caller owns the supplied stream. Disposing this writer releases only its
/// internal serialization gate and must occur after all writer calls complete.
/// </remarks>
public sealed class BoundedUtf8LineWriter : IDisposable
{
    private static readonly ReadOnlyMemory<byte> LineFeed =
        new byte[] { (byte)'\n' };

    private readonly Stream _stream;
    private readonly int _maximumLineBytes;
    private readonly SemaphoreSlim _writeGate = new(
        initialCount: 1,
        maxCount: 1);

    private int _disposeState;

    /// <summary>
    /// Creates a writer for one LF-delimited protocol stream.
    /// </summary>
    /// <param name="stream">A writable stream owned by the caller.</param>
    /// <param name="maximumLineBytes">
    /// The maximum payload size in bytes, excluding the LF terminator.
    /// </param>
    public BoundedUtf8LineWriter(Stream stream, int maximumLineBytes)
    {
        ProtocolStreamValidation.ValidateMaximumLineBytes(maximumLineBytes);

        _stream = ProtocolStreamValidation.RequireWritableStream(stream);
        _maximumLineBytes = maximumLineBytes;
    }

    /// <summary>
    /// Validates and writes one complete payload, one LF terminator, and one
    /// flush as an indivisible transaction relative to other writer calls.
    /// </summary>
    /// <param name="payload">Strict UTF-8 payload bytes without framing.</param>
    /// <param name="cancellationToken">
    /// Cancels waiting, writing, or flushing without converting cancellation
    /// into a protocol failure.
    /// </param>
    /// <exception cref="ObjectDisposedException">
    /// The writer has already been disposed.
    /// </exception>
    /// <exception cref="ProtocolStreamException">
    /// The payload is empty, malformed, contains framing bytes, or exceeds the
    /// configured byte limit.
    /// </exception>
    public async ValueTask WriteLineAsync(
        ReadOnlyMemory<byte> payload,
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();

        // Validate the length before allocating so an untrusted caller cannot
        // force an output snapshot larger than the configured protocol limit.
        ProtocolStreamValidation.ValidateOutputPayloadLength(
            payload.Length,
            _maximumLineBytes);

        // ReadOnlyMemory can still wrap a mutable array. Take one bounded copy
        // before waiting so later caller mutations cannot alter the frame that
        // already passed validation.
        byte[] ownedPayload = payload.ToArray();
        ProtocolStreamValidation.ValidateOutputPayload(
            ownedPayload,
            _maximumLineBytes);

        await _writeGate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            await _stream.WriteAsync(ownedPayload, cancellationToken)
                .ConfigureAwait(false);
            await _stream.WriteAsync(LineFeed, cancellationToken)
                .ConfigureAwait(false);
            await _stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _writeGate.Release();
        }
    }

    /// <summary>
    /// Releases the internal serialization gate without closing the caller-owned
    /// protocol stream.
    /// </summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposeState, 1) == 0)
        {
            _writeGate.Dispose();
        }

        GC.SuppressFinalize(this);
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(
            Volatile.Read(ref _disposeState) != 0,
            this);
    }
}
