using System.Buffers;
using System.Text;

namespace GraniteEdgeAI.HardwareInspection.Foundation.Processes;

internal sealed class BoundedProcessOutput
{
    private static readonly Encoding SafeUtf8 = new UTF8Encoding(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: false);
    private readonly TaskCompletionSource _limitExceeded = new(
        TaskCreationOptions.RunContinuationsAsynchronously);

    private BoundedProcessOutput(Stream stream, int byteLimit)
    {
        Completion = ReadAsync(stream, byteLimit);
    }

    internal Task<BoundedProcessOutputResult> Completion { get; }

    internal Task LimitExceeded => _limitExceeded.Task;

    internal static BoundedProcessOutput Start(Stream stream, int byteLimit)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(byteLimit);
        return new BoundedProcessOutput(stream, byteLimit);
    }

    private async Task<BoundedProcessOutputResult> ReadAsync(Stream stream, int byteLimit)
    {
        byte[] retained = new byte[byteLimit];
        byte[] buffer = ArrayPool<byte>.Shared.Rent(81_920);
        int retainedCount = 0;
        bool exceeded = false;

        try
        {
            while (true)
            {
                int read = await stream.ReadAsync(buffer).ConfigureAwait(false);
                if (read == 0)
                {
                    break;
                }

                int copyLength = Math.Min(byteLimit - retainedCount, read);
                if (copyLength > 0)
                {
                    buffer.AsSpan(0, copyLength).CopyTo(retained.AsSpan(retainedCount));
                    retainedCount += copyLength;
                }

                if (copyLength < read)
                {
                    exceeded = true;
                    _limitExceeded.TrySetResult();
                }
            }

            return new BoundedProcessOutputResult(
                SafeUtf8.GetString(retained, 0, retainedCount),
                exceeded,
                Failed: false);
        }
        catch (Exception error) when (error is IOException or ObjectDisposedException)
        {
            return new BoundedProcessOutputResult(
                SafeUtf8.GetString(retained, 0, retainedCount),
                exceeded,
                Failed: true);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer, clearArray: true);
        }
    }
}

internal sealed record BoundedProcessOutputResult(string Text, bool LimitExceeded, bool Failed)
{
    internal static BoundedProcessOutputResult Empty { get; } = new(
        string.Empty,
        LimitExceeded: false,
        Failed: false);
}
