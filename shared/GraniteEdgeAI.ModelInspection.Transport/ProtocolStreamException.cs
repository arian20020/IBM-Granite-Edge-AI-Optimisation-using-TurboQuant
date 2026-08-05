using System.Text;

namespace GraniteEdgeAI.ModelInspection.Transport;

/// <summary>
/// Reports a controlled framing failure without exposing protocol payload data.
/// </summary>
public sealed class ProtocolStreamException : Exception
{
    /// <summary>
    /// Creates a general protocol stream exception for framework compatibility.
    /// </summary>
    public ProtocolStreamException()
        : this(
            ProtocolStreamErrorKind.InvalidConfiguration,
            "The protocol stream operation failed.")
    {
    }

    /// <summary>
    /// Creates a general protocol stream exception with a safe message.
    /// </summary>
    /// <param name="message">A message that contains no protocol payload.</param>
    public ProtocolStreamException(string message)
        : this(ProtocolStreamErrorKind.InvalidConfiguration, message)
    {
    }

    /// <summary>
    /// Creates a general protocol stream exception with a safe message and cause.
    /// </summary>
    /// <param name="message">A message that contains no protocol payload.</param>
    /// <param name="innerException">The underlying controlled exception.</param>
    public ProtocolStreamException(
        string message,
        Exception innerException)
        : this(
            ProtocolStreamErrorKind.InvalidConfiguration,
            message,
            innerException)
    {
    }

    /// <summary>
    /// Creates a typed protocol stream exception.
    /// </summary>
    /// <param name="errorKind">The stable framing failure category.</param>
    /// <param name="message">A message that contains no protocol payload.</param>
    public ProtocolStreamException(
        ProtocolStreamErrorKind errorKind,
        string message)
        : this(errorKind, message, null)
    {
    }

    /// <summary>
    /// Creates a typed protocol stream exception with an underlying cause.
    /// </summary>
    /// <param name="errorKind">The stable framing failure category.</param>
    /// <param name="message">A message that contains no protocol payload.</param>
    /// <param name="innerException">The underlying controlled exception.</param>
    public ProtocolStreamException(
        ProtocolStreamErrorKind errorKind,
        string message,
        Exception? innerException)
        : base(message, innerException)
    {
        if (!Enum.IsDefined(errorKind))
        {
            throw new ArgumentOutOfRangeException(nameof(errorKind));
        }

        ErrorKind = errorKind;
    }

    /// <summary>
    /// Gets the stable framing failure category.
    /// </summary>
    public ProtocolStreamErrorKind ErrorKind { get; }
}

/// <summary>
/// Applies the same fail-closed byte policy to input and output framing.
/// </summary>
internal static class ProtocolStreamValidation
{
    private static readonly UTF8Encoding StrictUtf8 = new(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);

    /// <summary>
    /// Rejects a limit that cannot enforce a useful positive boundary.
    /// </summary>
    public static void ValidateMaximumLineBytes(int maximumLineBytes)
    {
        if (maximumLineBytes <= 0)
        {
            throw Create(
                ProtocolStreamErrorKind.InvalidConfiguration,
                "The maximum protocol line length must be greater than zero.");
        }
    }

    /// <summary>
    /// Rejects a stream that cannot supply protocol input.
    /// </summary>
    public static Stream RequireReadableStream(Stream? stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        if (!stream.CanRead)
        {
            throw Create(
                ProtocolStreamErrorKind.InvalidConfiguration,
                "The protocol input stream must be readable.");
        }

        return stream;
    }

    /// <summary>
    /// Rejects a stream that cannot accept protocol output.
    /// </summary>
    public static Stream RequireWritableStream(Stream? stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        if (!stream.CanWrite)
        {
            throw Create(
                ProtocolStreamErrorKind.InvalidConfiguration,
                "The protocol output stream must be writable.");
        }

        return stream;
    }

    /// <summary>
    /// Validates one complete input payload after its LF has been consumed.
    /// </summary>
    public static void ValidateInputPayload(
        byte[] payloadBuffer,
        int payloadLength)
    {
        ArgumentNullException.ThrowIfNull(payloadBuffer);

        if (payloadLength <= 0 || payloadLength > payloadBuffer.Length)
        {
            throw Create(
                ProtocolStreamErrorKind.InvalidConfiguration,
                "The protocol input payload length is invalid.");
        }

        ValidateNoBom(payloadBuffer, payloadLength);
        ValidateStrictUtf8(payloadBuffer, payloadLength);
    }

    /// <summary>
    /// Validates one caller-supplied output payload before any byte is written.
    /// </summary>
    public static void ValidateOutputPayload(
        ReadOnlyMemory<byte> payload,
        int maximumLineBytes)
    {
        if (payload.IsEmpty)
        {
            throw Create(
                ProtocolStreamErrorKind.EmptyLine,
                "Protocol lines cannot be empty.");
        }

        if (payload.Length > maximumLineBytes)
        {
            throw Create(
                ProtocolStreamErrorKind.LineTooLong,
                "The protocol payload exceeds the configured byte limit.");
        }

        byte[] validatedCopy = payload.ToArray();
        ValidateNoBom(validatedCopy, validatedCopy.Length);

        for (int index = 0; index < validatedCopy.Length; index++)
        {
            switch (validatedCopy[index])
            {
                case (byte)'\r':
                    throw Create(
                        ProtocolStreamErrorKind.CarriageReturnNotAllowed,
                        "Carriage-return bytes are not allowed in protocol payloads.");
                case (byte)'\n':
                    throw Create(
                        ProtocolStreamErrorKind.LineFeedNotAllowed,
                        "Output payloads cannot contain the LF frame terminator.");
            }
        }

        ValidateStrictUtf8(validatedCopy, validatedCopy.Length);
    }

    /// <summary>
    /// Creates a stable framing exception without embedding untrusted bytes.
    /// </summary>
    public static ProtocolStreamException Create(
        ProtocolStreamErrorKind errorKind,
        string message,
        Exception? innerException = null)
    {
        return new ProtocolStreamException(errorKind, message, innerException);
    }

    private static void ValidateNoBom(byte[] payload, int payloadLength)
    {
        if (
            payloadLength >= 3 &&
            payload[0] == 0xEF &&
            payload[1] == 0xBB &&
            payload[2] == 0xBF)
        {
            throw Create(
                ProtocolStreamErrorKind.BomNotAllowed,
                "UTF-8 byte-order marks are not allowed in protocol payloads.");
        }
    }

    private static void ValidateStrictUtf8(byte[] payload, int payloadLength)
    {
        try
        {
            _ = StrictUtf8.GetCharCount(payload, 0, payloadLength);
        }
        catch (DecoderFallbackException error)
        {
            throw Create(
                ProtocolStreamErrorKind.InvalidUtf8,
                "The protocol payload is not valid UTF-8.",
                error);
        }
    }
}
