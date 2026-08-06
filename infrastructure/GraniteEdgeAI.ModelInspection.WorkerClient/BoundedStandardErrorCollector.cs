using System.Text;
using System.Text.RegularExpressions;

namespace GraniteEdgeAI.ModelInspection.WorkerClient;

/// <summary>
/// Drains stderr as raw bytes to EOF while retaining only a bounded, strictly
/// decoded, redacted prefix for infrastructure diagnostics.
/// </summary>
internal sealed class BoundedStandardErrorCollector
{
    private const int ReadBufferSize = 8192;
    private static readonly UTF8Encoding StrictUtf8 = new(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);
    private readonly int _maximumRetainedBytes;

    internal BoundedStandardErrorCollector(int maximumRetainedBytes)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(
            maximumRetainedBytes,
            0);
        _maximumRetainedBytes = maximumRetainedBytes;
    }

    internal async Task<StandardErrorSnapshot> DrainAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (!stream.CanRead)
        {
            throw new ArgumentException(
                "The stderr stream must be readable.",
                nameof(stream));
        }

        byte[] readBuffer = new byte[ReadBufferSize];
        using MemoryStream retained = new(_maximumRetainedBytes);
        bool discardedBytes = false;

        while (true)
        {
            int bytesRead = await stream.ReadAsync(
                    readBuffer.AsMemory(),
                    cancellationToken)
                .ConfigureAwait(false);
            if (bytesRead == 0)
            {
                break;
            }

            int remainingCapacity = checked(
                _maximumRetainedBytes - (int)retained.Length);
            int bytesToRetain = Math.Min(bytesRead, remainingCapacity);
            if (bytesToRetain > 0)
            {
                retained.Write(readBuffer.AsSpan(0, bytesToRetain));
            }

            // Reading continues even after this flag becomes true. That is the
            // deadlock-prevention guarantee: discarded bytes are still consumed.
            if (bytesToRetain < bytesRead)
            {
                discardedBytes = true;
            }
        }

        string decodedText;
        try
        {
            decodedText = StrictUtf8.GetString(retained.ToArray());
        }
        catch (DecoderFallbackException)
        {
            // Invalid bytes are never converted with replacement characters or
            // exposed as hexadecimal content; the raw pipe has already drained.
            return new StandardErrorSnapshot(
                retainedText: string.Empty,
                isTruncated: discardedBytes,
                invalidUtf8Detected: true);
        }

        string redactedText = StandardErrorRedactor.Redact(decodedText);
        string boundedText = LimitUtf8(
            redactedText,
            _maximumRetainedBytes,
            out bool redactionExpansionWasTruncated);

        return new StandardErrorSnapshot(
            boundedText,
            discardedBytes || redactionExpansionWasTruncated,
            invalidUtf8Detected: false);
    }

    private static string LimitUtf8(
        string value,
        int maximumBytes,
        out bool wasTruncated)
    {
        if (StrictUtf8.GetByteCount(value) <= maximumBytes)
        {
            wasTruncated = false;
            return value;
        }

        byte[] boundedBytes = new byte[maximumBytes];
        Encoder encoder = StrictUtf8.GetEncoder();
        encoder.Convert(
            value.AsSpan(),
            boundedBytes.AsSpan(),
            flush: true,
            out _,
            out int bytesUsed,
            out _);

        wasTruncated = true;
        return StrictUtf8.GetString(boundedBytes.AsSpan(0, bytesUsed));
    }
}

/// <summary>
/// Applies a closed redaction policy to already bounded stderr text. A regular-
/// expression timeout fails closed to one fixed marker.
/// </summary>
internal static partial class StandardErrorRedactor
{
    private const string RedactedMarker = "[REDACTED]";

    [GeneratedRegex(
        "\\b(?:token|password|secret|api[_-]?key|authorization)\\s*[:=]\\s*[^\\s,;]+",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        100)]
    private static partial Regex SecretAssignmentRegex();

    [GeneratedRegex(
        "(?<![A-Za-z0-9])(?:[A-Za-z]:\\\\|\\\\\\\\)[^\\r\\n\\t ]+",
        RegexOptions.CultureInvariant,
        100)]
    private static partial Regex WindowsPathRegex();

    [GeneratedRegex(
        "\\b[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}\\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        100)]
    private static partial Regex GuidRegex();

    [GeneratedRegex(
        "\\b[^\\s\\\\/:*?\"<>|]+\\.(?:gguf|bin|xml)\\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        100)]
    private static partial Regex ModelFileRegex();

    internal static string Redact(string input)
    {
        ArgumentNullException.ThrowIfNull(input);

        try
        {
            string redacted = SecretAssignmentRegex().Replace(
                input,
                RedactedMarker);
            redacted = WindowsPathRegex().Replace(
                redacted,
                RedactedMarker);
            redacted = GuidRegex().Replace(
                redacted,
                RedactedMarker);
            return ModelFileRegex().Replace(
                redacted,
                RedactedMarker);
        }
        catch (RegexMatchTimeoutException)
        {
            return RedactedMarker;
        }
    }
}
