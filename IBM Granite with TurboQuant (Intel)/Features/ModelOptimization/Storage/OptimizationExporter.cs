using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;

namespace GraniteEdgeAI.Features.ModelOptimization.Storage;

internal sealed class GgufExportCleanupException : InvalidOperationException
{
    internal GgufExportCleanupException()
        : base("The temporary GGUF export could not be removed safely.") { }
}

internal static class OptimizationExporter
{
    internal static async Task<bool> ExportGgufAsync(
        OptimizationOutputRegistry registry,
        OptimizationExecutionResult result,
        string destinationPath,
        ulong maximumBytes,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(result);
        if (!registry.TryGetPublishedGgufFile(
                result,
                out string? sourcePath,
                out string? expectedSha256,
                out ulong expectedLength))
        {
            return false;
        }
        if (expectedLength == 0 || expectedLength > maximumBytes)
        {
            throw new InvalidDataException("The selected output exceeds the bounded export size.");
        }
        if (string.IsNullOrWhiteSpace(destinationPath)
            || !Path.IsPathFullyQualified(destinationPath))
        {
            throw new ArgumentException("A fully qualified export path is required.", nameof(destinationPath));
        }

        string destination = Path.GetFullPath(destinationPath);
        if (!string.Equals(
                Path.GetExtension(destination),
                ".gguf",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "A GGUF export must retain the .gguf model extension.",
                nameof(destinationPath));
        }
        string parent = Path.GetDirectoryName(destination)
            ?? throw new ArgumentException("The export path has no parent directory.", nameof(destinationPath));
        parent = StoragePathGuard.RequireRoot(parent, create: false);
        if (File.Exists(destination) || Directory.Exists(destination))
        {
            throw new IOException("The export destination already exists.");
        }
        string temporary = StoragePathGuard.RequireChild(
            parent,
            Path.Combine(parent, $".export-{Guid.NewGuid():N}.tmp"),
            mustExist: false);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            (string copiedSha256, ulong copiedLength) = await BoundedFileTransfer.CopyAndHashAsync(
                sourcePath!,
                temporary,
                maximumBytes,
                cancellationToken).ConfigureAwait(false);
            if (copiedLength != expectedLength
                || !string.Equals(copiedSha256, expectedSha256, StringComparison.Ordinal))
            {
                return false;
            }
            (string verifiedSha256, ulong verifiedLength) = await BoundedFileTransfer.HashAsync(
                temporary,
                maximumBytes,
                cancellationToken).ConfigureAwait(false);
            if (verifiedLength != expectedLength
                || !string.Equals(verifiedSha256, expectedSha256, StringComparison.Ordinal))
            {
                return false;
            }
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporary, destination, overwrite: false);
            return true;
        }
        finally
        {
            CleanupTemporary(temporary);
        }
    }

    private static void CleanupTemporary(string temporary)
    {
        if (!File.Exists(temporary))
        {
            return;
        }
        try
        {
            File.Delete(temporary);
        }
        catch (Exception exception) when (exception is IOException
                                          or UnauthorizedAccessException)
        {
            throw new GgufExportCleanupException();
        }
    }
}
