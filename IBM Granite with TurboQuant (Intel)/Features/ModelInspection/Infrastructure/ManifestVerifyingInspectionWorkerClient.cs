using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using GraniteEdgeAI.ModelInspection.Contracts;
using GraniteEdgeAI.ModelInspection.WorkerClient;

namespace GraniteEdgeAI.Features.ModelInspection.Infrastructure;

/// <summary>
/// Verifies the application-embedded worker manifest and the complete staged
/// worker closure immediately before delegating to the process client.
/// </summary>
internal sealed class ManifestVerifyingInspectionWorkerClient(
    string approvedApplicationRoot,
    ReadOnlyMemory<byte> trustedManifest,
    IInspectionWorkerClient innerClient) : IInspectionWorkerClient
{
    private const string IntegrityFailureMessage =
        "The installed Model Inspection worker package failed its integrity check.";
    private readonly string _approvedApplicationRoot =
        approvedApplicationRoot ??
        throw new ArgumentNullException(nameof(approvedApplicationRoot));
    private readonly byte[] _trustedManifest = trustedManifest.ToArray();
    private readonly IInspectionWorkerClient _innerClient = innerClient ??
        throw new ArgumentNullException(nameof(innerClient));

    public async Task<WorkerClientResult> ExecuteAsync(
        WorkerStartInspectionCommand command,
        IProgress<WorkerProgressMessage>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        command.Validate();
        cancellationToken.ThrowIfCancellationRequested();

        bool verified = await WorkerPackageManifestVerifier.VerifyAsync(
                _approvedApplicationRoot,
                _trustedManifest,
                cancellationToken)
            .ConfigureAwait(false);
        if (!verified)
        {
            WorkerClientResult failure = new(
                TerminalMessage: null,
                Failure: new WorkerClientFailure(
                    WorkerClientFailureCodes.WorkerPackageIntegrityFailed,
                    IntegrityFailureMessage),
                ExitCode: null,
                ForcedTermination: false,
                StandardErrorTruncated: false,
                RetainedStandardError: string.Empty,
                SecondaryDiagnostics: Array.Empty<string>());
            failure.Validate();
            return failure;
        }

        return await _innerClient.ExecuteAsync(
                command,
                progress,
                cancellationToken)
            .ConfigureAwait(false);
    }
}

internal static class WorkerPackageManifestVerifier
{
    private const int ExpectedFileCount = 44;
    private const int MaximumClosureDirectories = 64;
    private const int MaximumClosureItems = 128;
    private const int MaximumManifestBytes = 1024 * 1024;
    private const string ManifestRelativePath =
        @"ModelInspection\worker-manifest.json";
    private const string WorkerDirectoryRelativePath =
        @"ModelInspection\Worker";
    private const string WorkerExecutableRelativePath =
        "GraniteEdgeAI.ModelInspection.Worker.exe";

    internal static async Task<bool> VerifyAsync(
        string approvedApplicationRoot,
        ReadOnlyMemory<byte> trustedManifest,
        CancellationToken cancellationToken)
    {
        try
        {
            return await VerifyCoreAsync(
                    approvedApplicationRoot,
                    trustedManifest,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception error) when (IsExpectedIntegrityFailure(error))
        {
            return false;
        }
    }

    private static async Task<bool> VerifyCoreAsync(
        string approvedApplicationRoot,
        ReadOnlyMemory<byte> trustedManifest,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(approvedApplicationRoot) ||
            !Path.IsPathFullyQualified(approvedApplicationRoot) ||
            trustedManifest.IsEmpty ||
            trustedManifest.Length > MaximumManifestBytes)
        {
            return false;
        }

        string applicationRoot = Path.TrimEndingDirectorySeparator(
            Path.GetFullPath(approvedApplicationRoot));
        if (!Directory.Exists(applicationRoot) ||
            IsReparsePoint(applicationRoot))
        {
            return false;
        }

        string modelInspectionRoot = Path.GetFullPath(Path.Combine(
            applicationRoot,
            "ModelInspection"));
        string workerRoot = Path.GetFullPath(Path.Combine(
            applicationRoot,
            WorkerDirectoryRelativePath));
        string manifestPath = Path.GetFullPath(Path.Combine(
            applicationRoot,
            ManifestRelativePath));
        if (!IsStrictDescendant(workerRoot, applicationRoot) ||
            !IsStrictDescendant(manifestPath, applicationRoot) ||
            !Directory.Exists(modelInspectionRoot) ||
            !Directory.Exists(workerRoot) ||
            !File.Exists(manifestPath) ||
            IsReparsePoint(modelInspectionRoot) ||
            IsReparsePoint(workerRoot) ||
            IsReparsePoint(manifestPath))
        {
            return false;
        }

        byte[] detachedManifest = await ReadBoundedFileAsync(
                manifestPath,
                MaximumManifestBytes,
                cancellationToken)
            .ConfigureAwait(false);
        if (detachedManifest.Length != trustedManifest.Length ||
            !CryptographicOperations.FixedTimeEquals(
                SHA256.HashData(detachedManifest),
                SHA256.HashData(trustedManifest.Span)))
        {
            return false;
        }

        ManifestEntry[]? manifestEntries = ParseManifest(trustedManifest);
        if (manifestEntries is null)
        {
            return false;
        }

        string[] actualFiles = EnumerateClosureFiles(
            workerRoot,
            cancellationToken);
        if (actualFiles.Length != ExpectedFileCount)
        {
            return false;
        }

        string[] actualPaths = actualFiles
            .Select(path => ToManifestRelativePath(workerRoot, path))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
        if (actualPaths.Length != ExpectedFileCount ||
            !actualPaths.SequenceEqual(
                manifestEntries.Select(entry => entry.Path),
                StringComparer.Ordinal))
        {
            return false;
        }

        foreach (ManifestEntry entry in manifestEntries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string fullPath = Path.GetFullPath(Path.Combine(
                workerRoot,
                entry.Path.Replace('/', Path.DirectorySeparatorChar)));
            if (!IsStrictDescendant(fullPath, workerRoot) ||
                IsReparsePoint(fullPath))
            {
                return false;
            }

            await using FileStream stream = new(
                fullPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 81_920,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            long beforeLength = stream.Length;
            if (beforeLength != entry.Length)
            {
                return false;
            }

            byte[] digest = await SHA256.HashDataAsync(
                    stream,
                    cancellationToken)
                .ConfigureAwait(false);
            if (stream.Length != beforeLength ||
                !CryptographicOperations.FixedTimeEquals(
                    digest,
                    entry.Sha256))
            {
                return false;
            }
        }

        return true;
    }

    private static ManifestEntry[]? ParseManifest(
        ReadOnlyMemory<byte> manifestBytes)
    {
        using JsonDocument document = JsonDocument.Parse(
            manifestBytes,
            new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
                MaxDepth = 8
            });
        JsonElement root = document.RootElement;
        JsonElement schemaVersionProperty;
        JsonElement runtimeIdentifierProperty;
        JsonElement files;
        if (root.ValueKind != JsonValueKind.Object ||
            !HasExactProperties(
                root,
                "schemaVersion",
                "runtimeIdentifier",
                "files"))
        {
            return null;
        }

        schemaVersionProperty = root.GetProperty("schemaVersion");
        runtimeIdentifierProperty = root.GetProperty("runtimeIdentifier");
        files = root.GetProperty("files");
        if (schemaVersionProperty.ValueKind != JsonValueKind.Number ||
            runtimeIdentifierProperty.ValueKind != JsonValueKind.String ||
            !schemaVersionProperty.TryGetInt32(out int schemaVersion) ||
            schemaVersion != 1 ||
            runtimeIdentifierProperty.GetString() != "win-x64")
        {
            return null;
        }

        if (files.ValueKind != JsonValueKind.Array ||
            files.GetArrayLength() != ExpectedFileCount)
        {
            return null;
        }

        List<ManifestEntry> entries = new(ExpectedFileCount);
        string? previousPath = null;
        foreach (JsonElement file in files.EnumerateArray())
        {
            if (file.ValueKind != JsonValueKind.Object ||
                !HasExactProperties(file, "path", "length", "sha256"))
            {
                return null;
            }

            JsonElement pathProperty = file.GetProperty("path");
            JsonElement lengthProperty = file.GetProperty("length");
            JsonElement hashProperty = file.GetProperty("sha256");
            if (pathProperty.ValueKind != JsonValueKind.String ||
                lengthProperty.ValueKind != JsonValueKind.Number ||
                hashProperty.ValueKind != JsonValueKind.String ||
                !lengthProperty.TryGetInt64(out long length) ||
                length < 0)
            {
                return null;
            }

            string? path = pathProperty.GetString();
            string? hash = hashProperty.GetString();
            if (!IsSafeManifestPath(path) ||
                !TryParseLowercaseSha256(hash, out byte[] digest) ||
                (previousPath is not null &&
                    StringComparer.Ordinal.Compare(previousPath, path) >= 0))
            {
                return null;
            }

            entries.Add(new ManifestEntry(path!, length, digest));
            previousPath = path;
        }

        return entries.Count == ExpectedFileCount &&
            entries.Any(entry => StringComparer.Ordinal.Equals(
                entry.Path,
                WorkerExecutableRelativePath))
                ? entries.ToArray()
                : null;
    }

    private static bool HasExactProperties(
        JsonElement value,
        params string[] expectedNames)
    {
        string[] actualNames = value.EnumerateObject()
            .Select(property => property.Name)
            .ToArray();
        return actualNames.Length == expectedNames.Length &&
            expectedNames.All(expected => actualNames.Count(
                actual => StringComparer.Ordinal.Equals(actual, expected)) == 1);
    }

    private static bool IsSafeManifestPath(string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath) ||
            relativePath != relativePath.Trim() ||
            relativePath.Contains('\\') ||
            relativePath.StartsWith('/') ||
            Path.IsPathRooted(relativePath) ||
            relativePath.Any(char.IsControl))
        {
            return false;
        }

        string[] segments = relativePath.Split('/');
        return segments.All(segment =>
            !string.IsNullOrEmpty(segment) &&
            segment is not "." and not ".." &&
            segment == segment.Trim() &&
            !segment.EndsWith('.') &&
            segment.IndexOfAny(Path.GetInvalidFileNameChars()) < 0);
    }

    private static bool TryParseLowercaseSha256(
        string? value,
        out byte[] digest)
    {
        digest = Array.Empty<byte>();
        if (value is null || value.Length != 64 ||
            value.Any(character =>
                !char.IsAsciiHexDigit(character) || char.IsAsciiLetterUpper(character)))
        {
            return false;
        }

        digest = Convert.FromHexString(value);
        return digest.Length == 32;
    }

    private static string ToManifestRelativePath(
        string workerRoot,
        string fullPath)
    {
        string canonicalPath = Path.GetFullPath(fullPath);
        if (!IsStrictDescendant(canonicalPath, workerRoot))
        {
            throw new IOException(
                "The staged worker closure escaped its approved root.");
        }

        string relativePath = Path.GetRelativePath(workerRoot, canonicalPath)
            .Replace(Path.DirectorySeparatorChar, '/');
        if (!IsSafeManifestPath(relativePath))
        {
            throw new IOException(
                "The staged worker closure contains an unsafe path.");
        }

        return relativePath;
    }

    private static string[] EnumerateClosureFiles(
        string workerRoot,
        CancellationToken cancellationToken)
    {
        List<string> files = [];
        Stack<string> pendingDirectories = new();
        pendingDirectories.Push(workerRoot);
        int directoryCount = 1;
        int itemCount = 0;
        while (pendingDirectories.TryPop(out string? directory))
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (string item in Directory.EnumerateFileSystemEntries(
                         directory,
                         "*",
                         SearchOption.TopDirectoryOnly))
            {
                cancellationToken.ThrowIfCancellationRequested();
                itemCount++;
                if (itemCount > MaximumClosureItems)
                {
                    throw new IOException(
                        "The staged worker closure exceeds its item bound.");
                }

                if (IsReparsePoint(item))
                {
                    throw new IOException(
                        "The staged worker closure contains a reparse point.");
                }

                if (Directory.Exists(item))
                {
                    directoryCount++;
                    if (directoryCount > MaximumClosureDirectories)
                    {
                        throw new IOException(
                            "The staged worker closure exceeds its directory bound.");
                    }

                    pendingDirectories.Push(item);
                }
                else if (File.Exists(item))
                {
                    files.Add(item);
                    if (files.Count > ExpectedFileCount)
                    {
                        throw new IOException(
                            "The staged worker closure exceeds its file bound.");
                    }
                }
                else
                {
                    throw new IOException(
                        "The staged worker closure changed during enumeration.");
                }
            }
        }

        return files.ToArray();
    }

    private static async Task<byte[]> ReadBoundedFileAsync(
        string path,
        int maximumBytes,
        CancellationToken cancellationToken)
    {
        await using FileStream stream = new(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 4096,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        if (stream.Length <= 0 || stream.Length > maximumBytes)
        {
            throw new IOException("The integrity manifest is outside its bounds.");
        }

        byte[] content = GC.AllocateUninitializedArray<byte>(
            checked((int)stream.Length));
        await stream.ReadExactlyAsync(content, cancellationToken)
            .ConfigureAwait(false);
        if (stream.Length != content.LongLength)
        {
            throw new IOException("The integrity manifest changed while read.");
        }

        return content;
    }

    private static bool IsStrictDescendant(
        string candidate,
        string parent)
    {
        string prefix = Path.TrimEndingDirectorySeparator(parent) +
            Path.DirectorySeparatorChar;
        return candidate.StartsWith(
            prefix,
            StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsReparsePoint(string path) =>
        (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;

    private static bool IsExpectedIntegrityFailure(Exception error) =>
        error is IOException or
        UnauthorizedAccessException or
        ArgumentException or
        NotSupportedException or
        JsonException or
        CryptographicException or
        OverflowException;

    private sealed record ManifestEntry(
        string Path,
        long Length,
        byte[] Sha256);
}
