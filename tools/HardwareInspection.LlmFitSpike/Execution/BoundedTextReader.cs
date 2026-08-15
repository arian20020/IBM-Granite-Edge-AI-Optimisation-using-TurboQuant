using System.Buffers;
using System.Text;

namespace HardwareInspection.LlmFitSpike.Execution;

internal static class BoundedTextReader
{
    private static readonly Encoding ReplacementUtf8 = new UTF8Encoding(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: false);

    internal static Task<BoundedTextCapture> ReadAsync(Stream stream, int maximumRetainedBytes)
    {
        return ReadAsync(stream, maximumRetainedBytes, ArrayPool<byte>.Shared);
    }

    internal static async Task<BoundedTextCapture> ReadAsync(
        Stream stream,
        int maximumRetainedBytes,
        ArrayPool<byte> bufferPool)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(bufferPool);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumRetainedBytes);

        byte[] retained = new byte[maximumRetainedBytes];
        byte[] buffer = bufferPool.Rent(81_920);
        int retainedCount = 0;
        bool truncated = false;

        try
        {
            while (true)
            {
                int read = await stream.ReadAsync(buffer).ConfigureAwait(false);
                if (read == 0)
                {
                    break;
                }

                int available = maximumRetainedBytes - retainedCount;
                int copyLength = Math.Min(available, read);
                if (copyLength > 0)
                {
                    buffer.AsSpan(0, copyLength).CopyTo(retained.AsSpan(retainedCount));
                    retainedCount += copyLength;
                }

                if (read > copyLength)
                {
                    truncated = true;
                }
            }

            string text = ReplacementUtf8.GetString(retained, 0, retainedCount);
            return new BoundedTextCapture(text, truncated);
        }
        finally
        {
            bufferPool.Return(buffer, clearArray: true);
        }
    }
}

internal sealed record BoundedTextCapture(string Text, bool Truncated)
{
    internal static BoundedTextCapture Empty { get; } = new(string.Empty, Truncated: false);
}
