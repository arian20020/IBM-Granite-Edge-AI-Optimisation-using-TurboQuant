using System;
using System.Buffers;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace GraniteEdgeAI.Features.ModelOptimization.Storage;

internal static class BoundedFileTransfer
{
    private const int BufferSize = 128 * 1024;

    internal static async Task<(string Sha256, ulong LengthBytes)> CopyAndHashAsync(
        string sourcePath,
        string destinationPath,
        ulong maximumBytes,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfZero(maximumBytes);
        StoragePathGuard.RequireRegularFile(sourcePath);
        FileInfo sourceInfo = new(sourcePath);
        ulong sourceLength = checked((ulong)sourceInfo.Length);
        if (sourceLength == 0 || sourceLength > maximumBytes)
        {
            throw new InvalidDataException("The output exceeds the bounded export size.");
        }

        byte[] buffer = ArrayPool<byte>.Shared.Rent(BufferSize);
        try
        {
            await using FileStream source = new(
                sourcePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                BufferSize,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            await using FileStream destination = new(
                destinationPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                BufferSize,
                FileOptions.Asynchronous | FileOptions.WriteThrough);
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            ulong copied = 0;
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int read = await source.ReadAsync(buffer.AsMemory(0, BufferSize), cancellationToken)
                    .ConfigureAwait(false);
                if (read == 0)
                {
                    break;
                }
                copied = checked(copied + (ulong)read);
                if (copied > maximumBytes || copied > sourceLength)
                {
                    throw new InvalidDataException("The source changed or exceeded the bounded export size.");
                }
                hash.AppendData(buffer, 0, read);
                await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken)
                    .ConfigureAwait(false);
            }
            if (copied != sourceLength)
            {
                throw new InvalidDataException("The source changed during export.");
            }
            await destination.FlushAsync(cancellationToken).ConfigureAwait(false);
            destination.Flush(flushToDisk: true);
            return (Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant(), copied);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer, clearArray: true);
        }
    }

    internal static async Task<(string Sha256, ulong LengthBytes)> HashAsync(
        string path,
        ulong maximumBytes,
        CancellationToken cancellationToken)
    {
        StoragePathGuard.RequireRegularFile(path);
        byte[] buffer = ArrayPool<byte>.Shared.Rent(BufferSize);
        try
        {
            await using FileStream stream = new(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                BufferSize,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            ulong expected = checked((ulong)stream.Length);
            if (expected == 0 || expected > maximumBytes)
            {
                throw new InvalidDataException("The output exceeds the bounded export size.");
            }
            ulong readTotal = 0;
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int read = await stream.ReadAsync(buffer.AsMemory(0, BufferSize), cancellationToken)
                    .ConfigureAwait(false);
                if (read == 0)
                {
                    break;
                }
                readTotal = checked(readTotal + (ulong)read);
                if (readTotal > maximumBytes || readTotal > expected)
                {
                    throw new InvalidDataException("The output changed while it was verified.");
                }
                hash.AppendData(buffer, 0, read);
            }
            if (readTotal != expected)
            {
                throw new InvalidDataException("The output changed while it was verified.");
            }
            return (Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant(), readTotal);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer, clearArray: true);
        }
    }
}
