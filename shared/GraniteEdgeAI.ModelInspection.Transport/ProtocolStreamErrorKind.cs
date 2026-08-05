namespace GraniteEdgeAI.ModelInspection.Transport;

/// <summary>
/// Identifies why strict protocol stream framing was rejected.
/// </summary>
public enum ProtocolStreamErrorKind
{
    /// <summary>
    /// The framing component received an unusable stream or byte limit.
    /// </summary>
    InvalidConfiguration,

    /// <summary>
    /// A line-feed terminator was received without a payload.
    /// </summary>
    EmptyLine,

    /// <summary>
    /// The payload started with a UTF-8 byte-order mark.
    /// </summary>
    BomNotAllowed,

    /// <summary>
    /// The payload contained a carriage-return byte.
    /// </summary>
    CarriageReturnNotAllowed,

    /// <summary>
    /// A caller-supplied output payload contained a line-feed byte.
    /// </summary>
    LineFeedNotAllowed,

    /// <summary>
    /// The payload was not valid strict UTF-8.
    /// </summary>
    InvalidUtf8,

    /// <summary>
    /// The payload exceeded the configured byte limit.
    /// </summary>
    LineTooLong,

    /// <summary>
    /// The stream ended after payload bytes but before the LF terminator.
    /// </summary>
    UnexpectedEndOfStream
}
