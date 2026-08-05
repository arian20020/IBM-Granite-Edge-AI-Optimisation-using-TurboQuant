using System.Buffers;

namespace GraniteEdgeAI.ModelInspection.Transport;

/// <summary>
/// Reads strict UTF-8 protocol frames using a fixed-size read-ahead buffer and
/// a caller-supplied maximum payload size.
/// </summary>
public sealed class BoundedUtf8LineReader
{
    private const int ReadBufferSize = 4096;

    private readonly Stream _stream;
    private readonly int _maximumLineBytes;
    private readonly byte[] _readBuffer = new byte[ReadBufferSize];

    private int _readBufferOffset;
    private int _readBufferCount;

    /// <summary>
    /// Creates a reader for one LF-delimited protocol stream.
    /// </summary>
    /// <param name="stream">A readable stream owned by the caller.</param>
    /// <param name="maximumLineBytes">
    /// The maximum payload size in bytes, excluding the LF terminator.
    /// </param>
    public BoundedUtf8LineReader(Stream stream, int maximumLineBytes)
    {
        ProtocolStreamValidation.ValidateMaximumLineBytes(maximumLineBytes);

        _stream = ProtocolStreamValidation.RequireReadableStream(stream);
        _maximumLineBytes = maximumLineBytes;
    }

    /// <summary>
    /// Reads one complete LF-delimited payload.
    /// </summary>
    /// <param name="cancellationToken">
    /// Cancels a blocked read without converting cancellation into a protocol
    /// failure.
    /// </param>
    /// <returns>
    /// The payload bytes without LF, or <see langword="null"/> for clean EOF
    /// before any payload byte is received.
    /// </returns>
    /// <exception cref="ProtocolStreamException">
    /// The stream contains invalid, incomplete, or over-limit framing.
    /// </exception>
    public async ValueTask<byte[]?> ReadLineAsync(
        CancellationToken cancellationToken)
    {
        byte[] payloadBuffer = ArrayPool<byte>.Shared.Rent(_maximumLineBytes);
        int payloadLength = 0;

        try
        {
            while (true)
            {
                int next = await ReadBufferedByteAsync(cancellationToken)
                    .ConfigureAwait(false);

                if (next < 0)
                {
                    if (payloadLength == 0)
                    {
                        return null;
                    }

                    throw ProtocolStreamValidation.Create(
                        ProtocolStreamErrorKind.UnexpectedEndOfStream,
                        "The protocol stream ended before the LF terminator.");
                }

                if (next == '\n')
                {
                    if (payloadLength == 0)
                    {
                        throw ProtocolStreamValidation.Create(
                            ProtocolStreamErrorKind.EmptyLine,
                            "Protocol lines cannot be empty.");
                    }

                    ProtocolStreamValidation.ValidateInputPayload(
                        payloadBuffer,
                        payloadLength);

                    byte[] result = new byte[payloadLength];
                    Buffer.BlockCopy(
                        payloadBuffer,
                        0,
                        result,
                        0,
                        payloadLength);
                    return result;
                }

                if (next == '\r')
                {
                    throw ProtocolStreamValidation.Create(
                        ProtocolStreamErrorKind.CarriageReturnNotAllowed,
                        "Carriage-return bytes are not allowed; protocol framing is LF-only.");
                }

                if (payloadLength == _maximumLineBytes)
                {
                    throw ProtocolStreamValidation.Create(
                        ProtocolStreamErrorKind.LineTooLong,
                        "The protocol payload exceeds the configured byte limit.");
                }

                payloadBuffer[payloadLength++] = (byte)next;
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(payloadBuffer, clearArray: true);
        }
    }

    /// <summary>
    /// Returns one buffered byte, refilling from the stream only when necessary.
    /// The fixed read-ahead buffer limits memory use independently of line size.
    /// </summary>
    private async ValueTask<int> ReadBufferedByteAsync(
        CancellationToken cancellationToken)
    {
        if (_readBufferOffset >= _readBufferCount)
        {
            _readBufferCount = await _stream
                .ReadAsync(_readBuffer.AsMemory(), cancellationToken)
                .ConfigureAwait(false);
            _readBufferOffset = 0;

            if (_readBufferCount == 0)
            {
                return -1;
            }
        }

        return _readBuffer[_readBufferOffset++];
    }
}
