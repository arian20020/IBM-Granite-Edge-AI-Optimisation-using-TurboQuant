namespace GraniteEdgeAI.ModelInspection.Transport;

/// <summary>
/// Writes strict UTF-8 protocol payloads as serialized LF-delimited frames.
/// </summary>
public sealed class BoundedUtf8LineWriter
{
    private static readonly ReadOnlyMemory<byte> LineFeed =
        new byte[] { (byte)'\n' };

    private readonly Stream _stream;
    private readonly int _maximumLineBytes;
    private readonly SemaphoreSlim _writeGate = new(
        initialCount: 1,
        maxCount: 1);

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
    /// <exception cref="ProtocolStreamException">
    /// The payload is empty, malformed, contains framing bytes, or exceeds the
    /// configured byte limit.
    /// </exception>
    public async ValueTask WriteLineAsync(
        ReadOnlyMemory<byte> payload,
        CancellationToken cancellationToken)
    {
        ProtocolStreamValidation.ValidateOutputPayload(
            payload,
            _maximumLineBytes);

        await _writeGate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            await _stream.WriteAsync(payload, cancellationToken)
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
}
