using System.Collections.Concurrent;
using System.Buffers;
using System.Security.Cryptography;
using GraniteEdgeAI.Features.ModelOptimization.Storage;
using GraniteEdgeAI.Features.OpenVinoRoute.Conversion;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;
using GraniteEdgeAI.OpenVino.Contracts;

namespace GraniteEdgeAI.Features.OpenVinoRoute.Optimization;

internal enum OpenVinoPublishedOutputKind
{
    PersistentPackage,
    RuntimeConfiguration
}

internal sealed record OpenVinoPublishedOutput(
    OpenVinoPublishedOutputKind Kind,
    string? PersistentDirectory,
    OpenVinoRuntimeOptimizationProfile? RuntimeProfile,
    OpenVinoRuntimeTechnicalConfiguration RuntimeConfiguration)
{
    internal bool IsPersistentArtifact =>
        Kind == OpenVinoPublishedOutputKind.PersistentPackage;

    internal OpenVinoRuntimeOptions RuntimeOptions =>
        RuntimeConfiguration.KvCachePrecision switch
        {
            OpenVinoKvCachePrecision.ReleasedDefault =>
                OpenVinoRuntimeOptions.ReleasedDefault,
            OpenVinoKvCachePrecision.U8 => OpenVinoRuntimeOptions.U8,
            OpenVinoKvCachePrecision.U4 => OpenVinoRuntimeOptions.U4,
            OpenVinoKvCachePrecision.Tbq4 => OpenVinoRuntimeOptions.Tbq4,
            OpenVinoKvCachePrecision.Tbq3 => OpenVinoRuntimeOptions.Tbq3,
            _ => throw new InvalidDataException(
                "The selected runtime configuration is not executable by this worker.")
        };
}

internal sealed class OpenVinoOptimizationChatTarget : IDisposable
{
    private readonly IDisposable? _sourceLease;
    private readonly object _custodySync = new();
    private int _custodyReferences = 1;
    private bool _ownerReleased;

    internal OpenVinoOptimizationChatTarget(
        OptimizationExecutionResult result,
        OpenVinoPublishedOutputKind kind,
        string packageDirectory,
        OpenVinoRuntimeOptions runtimeOptions,
        IDisposable? sourceLease)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentException.ThrowIfNullOrWhiteSpace(packageDirectory);
        ArgumentNullException.ThrowIfNull(runtimeOptions);
        if (!result.IsSuccessful || result.Route != OptimizationRoute.OpenVino)
        {
            throw new ArgumentException(
                "Only a successful OpenVINO execution can create a Chat target.",
                nameof(result));
        }
        if ((kind == OpenVinoPublishedOutputKind.RuntimeConfiguration)
            != (sourceLease is not null))
        {
            throw new ArgumentException(
                "A runtime-only target must retain its exact source lease.",
                nameof(sourceLease));
        }

        ExecutionId = result.ExecutionId;
        OptimizationPlanId = result.OptimizationPlanId;
        ConfigurationSha256 = result.ConfigurationSha256;
        SourceSha256 = result.SourceSha256;
        HardwareSnapshotSha256 = result.HardwareSnapshotSha256;
        Kind = kind;
        PackageDirectory = packageDirectory;
        RuntimeOptions = runtimeOptions;
        _sourceLease = sourceLease;
    }

    internal Guid ExecutionId { get; }
    internal Guid OptimizationPlanId { get; }
    internal string ConfigurationSha256 { get; }
    internal string SourceSha256 { get; }
    internal string HardwareSnapshotSha256 { get; }
    internal OpenVinoPublishedOutputKind Kind { get; }
    internal string PackageDirectory { get; }
    internal OpenVinoRuntimeOptions RuntimeOptions { get; }
    internal bool CanExportModel =>
        Kind == OpenVinoPublishedOutputKind.PersistentPackage;

    internal IDisposable RetainVerifiedCustody()
    {
        lock (_custodySync)
        {
            ObjectDisposedException.ThrowIf(_ownerReleased, this);
            checked { _custodyReferences++; }
            return new RetainedCustody(this);
        }
    }

    public void Dispose()
    {
        lock (_custodySync)
        {
            if (_ownerReleased) return;
            _ownerReleased = true;
        }
        ReleaseCustody();
    }

    private void ReleaseCustody()
    {
        bool release;
        lock (_custodySync) release = --_custodyReferences == 0;
        if (release) _sourceLease?.Dispose();
    }

    private sealed class RetainedCustody(OpenVinoOptimizationChatTarget owner) : IDisposable
    {
        private OpenVinoOptimizationChatTarget? _owner = owner;
        public void Dispose() => Interlocked.Exchange(ref _owner, null)?.ReleaseCustody();
    }
}

/// <summary>
/// Resolves only the bytes/configuration attested by one exact successful
/// result. Runtime-only results expose a configuration object, never a path.
/// </summary>
internal sealed class OpenVinoPublishedOutputRegistry
{
    private const long MaximumMetadataBytes = 4L * 1024 * 1024;
    private const ulong MaximumArtifactBytes = 1UL << 40;
    private const int BufferBytes = 128 * 1024;

    private readonly string _root;
    private readonly ConcurrentDictionary<PublicationKey, RegisteredPublication>
        _publications = new();

    internal OpenVinoPublishedOutputRegistry(string root)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        _root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        Directory.CreateDirectory(_root);
        RequirePlainDirectory(_root);
    }

    internal bool TryRegister(
        OptimizationExecutionResult result,
        string publicationDirectory) =>
        RegisterAsync(result, publicationDirectory, CancellationToken.None)
            .GetAwaiter().GetResult();

    internal async Task<bool> RegisterAsync(
        OptimizationExecutionResult result,
        string publicationDirectory,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidatedPublication? validated = await ValidateAsync(
            result,
            publicationDirectory,
            cancellationToken).ConfigureAwait(false);
        if (validated is null)
        {
            return false;
        }

        try
        {
            PublicationKey key = PublicationKey.From(result);
            var registration = new RegisteredPublication(
                RequireDirectChild(publicationDirectory),
                validated.EvidenceSha256);
            return _publications.TryAdd(key, registration);
        }
        catch (Exception failure) when (IsFileFailure(failure))
        {
            return false;
        }
    }

    internal bool TryResolve(
        OptimizationExecutionResult result,
        out OpenVinoPublishedOutput? publication)
    {
        publication = ResolveAsync(result, CancellationToken.None)
            .GetAwaiter().GetResult();
        return publication is not null;
    }

    internal async Task<OpenVinoPublishedOutput?> ResolveAsync(
        OptimizationExecutionResult result,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!IsEligible(result)
            || !_publications.TryGetValue(
                PublicationKey.From(result),
                out RegisteredPublication? registration))
        {
            return null;
        }

        try
        {
            ValidatedPublication? validated = await ValidateAsync(
                result,
                registration.Directory,
                cancellationToken).ConfigureAwait(false);
            if (validated is null
                || !string.Equals(
                    validated.EvidenceSha256,
                    registration.EvidenceSha256,
                    StringComparison.Ordinal))
            {
                return null;
            }

            return validated.Publication;
        }
        catch (Exception failure) when (IsFileFailure(failure))
        {
            return null;
        }
    }

    internal async Task<OpenVinoExportResult> ExportPersistentAsync(
        OptimizationExecutionResult result,
        string destinationDirectory,
        ulong maximumBytes,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (maximumBytes == 0 || maximumBytes > MaximumArtifactBytes)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumBytes));
        }
        cancellationToken.ThrowIfCancellationRequested();
        OpenVinoPublishedOutput? publication = await ResolveAsync(
            result,
            cancellationToken).ConfigureAwait(false);
        if (publication is null)
        {
            return OpenVinoExportResult.For(
                result,
                OpenVinoExportDisposition.ResultRejected);
        }
        if (!publication.IsPersistentArtifact
            || publication.PersistentDirectory is null)
        {
            return OpenVinoExportResult.For(
                result,
                OpenVinoExportDisposition.RuntimeOnly);
        }
        if (result.OutputSizeBytes > maximumBytes)
        {
            throw new InvalidDataException(
                "The selected OpenVINO package exceeds the bounded export size.");
        }

        ExportDestinationPaths paths;
        try
        {
            paths = ExportDestinationGuard.RequireAbsentDirectory(
                destinationDirectory);
        }
        catch (IOException)
        {
            return OpenVinoExportResult.For(
                result,
                OpenVinoExportDisposition.DestinationExists);
        }
        catch (Exception failure) when (failure is ArgumentException
                                        or InvalidOperationException
                                        or UnauthorizedAccessException)
        {
            return OpenVinoExportResult.For(
                result,
                OpenVinoExportDisposition.DestinationRejected);
        }

        bool promoted = false;
        bool cancelled = false;
        try
        {
            Directory.CreateDirectory(paths.Temporary);
            RequirePlainDirectory(paths.Temporary);
            ulong copied = 0;
            ulong artifactBytes = 0;
            foreach (string source in Directory.EnumerateFiles(
                         publication.PersistentDirectory,
                         "*",
                         SearchOption.TopDirectoryOnly))
            {
                cancellationToken.ThrowIfCancellationRequested();
                FileInfo file = RequireRegularFile(
                    source,
                    checked((long)MaximumArtifactBytes));
                copied = checked(copied + (ulong)file.Length);
                if (!string.Equals(
                        file.Name,
                        OpenVinoOptimizationProvenance.FileName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    artifactBytes = checked(artifactBytes + (ulong)file.Length);
                }
                if (copied > maximumBytes || artifactBytes > result.OutputSizeBytes)
                {
                    throw new InvalidDataException(
                        "The OpenVINO package changed or exceeded the export bound.");
                }
                await CopyFileAsync(
                    file.FullName,
                    Path.Combine(paths.Temporary, file.Name),
                    checked((ulong)file.Length),
                    cancellationToken).ConfigureAwait(false);
            }
            if (artifactBytes != result.OutputSizeBytes)
            {
                return OpenVinoExportResult.For(
                    result,
                    OpenVinoExportDisposition.IdentityMismatch);
            }

            var verifier = new OpenVinoPublishedOutputRegistry(
                Path.GetDirectoryName(paths.Temporary)!);
            if (!await verifier.RegisterAsync(
                    result,
                    paths.Temporary,
                    cancellationToken).ConfigureAwait(false))
            {
                return OpenVinoExportResult.For(
                    result,
                    OpenVinoExportDisposition.IdentityMismatch);
            }
            OpenVinoPublishedOutput? verified = await verifier.ResolveAsync(
                result,
                cancellationToken).ConfigureAwait(false);
            if (verified is null || !verified.IsPersistentArtifact)
            {
                return OpenVinoExportResult.For(
                    result,
                    OpenVinoExportDisposition.IdentityMismatch);
            }
            cancellationToken.ThrowIfCancellationRequested();
            StoragePathGuard.RequireNoReparseAncestors(paths.Destination);
            if (Directory.Exists(paths.Destination) || File.Exists(paths.Destination))
            {
                return OpenVinoExportResult.For(
                    result,
                    OpenVinoExportDisposition.DestinationExists);
            }
            Directory.Move(paths.Temporary, paths.Destination);
            promoted = true;
            return OpenVinoExportResult.For(
                result,
                OpenVinoExportDisposition.Succeeded);
        }
        catch (OperationCanceledException)
        {
            cancelled = true;
            throw;
        }
        finally
        {
            if (!promoted)
            {
                TemporaryExportDirectory.Cleanup(paths.Temporary, cancelled);
            }
        }
    }

    private async Task<ValidatedPublication?> ValidateAsync(
        OptimizationExecutionResult result,
        string directory,
        CancellationToken cancellationToken)
    {
        if (!IsEligible(result) || string.IsNullOrWhiteSpace(directory))
        {
            return null;
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            string fullPath = RequireDirectChild(directory);
            RequirePlainDirectory(fullPath);
            return result.ProducedPersistentArtifact
                ? await ValidatePersistentAsync(
                    result,
                    fullPath,
                    cancellationToken).ConfigureAwait(false)
                : await ValidateRuntimeProfileAsync(
                    result,
                    fullPath,
                    cancellationToken).ConfigureAwait(false);
        }
        catch (Exception failure) when (IsFileFailure(failure))
        {
            return null;
        }
    }

    private static async Task<ValidatedPublication?> ValidateRuntimeProfileAsync(
        OptimizationExecutionResult result,
        string directory,
        CancellationToken cancellationToken)
    {
        if (!result.OutputIdentity!.StartsWith(
                "openvino-runtime-profile-v3-",
                StringComparison.Ordinal))
        {
            return null;
        }

        string profilePath = Path.Combine(
            directory,
            OpenVinoRuntimeOptimizationProfile.FileName);
        string? discoveredFile = null;
        int entryCount = 0;
        foreach (string entry in Directory.EnumerateFileSystemEntries(
                     directory,
                     "*",
                     SearchOption.TopDirectoryOnly))
        {
            cancellationToken.ThrowIfCancellationRequested();
            entryCount++;
            FileAttributes attributes = File.GetAttributes(entry);
            if ((attributes &
                (FileAttributes.Directory | FileAttributes.ReparsePoint)) != 0)
            {
                return null;
            }
            discoveredFile = entry;
        }
        if (entryCount != 1
            || !string.Equals(
                Path.GetFullPath(discoveredFile!),
                Path.GetFullPath(profilePath),
                StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        FileInfo file = RequireRegularFile(profilePath, MaximumMetadataBytes);
        if (checked((ulong)file.Length) != result.OutputSizeBytes)
        {
            return null;
        }

        string evidenceDigest = await ComputeSha256Async(
            profilePath,
            MaximumMetadataBytes,
            cancellationToken).ConfigureAwait(false);
        if (!string.Equals(
                evidenceDigest,
                result.OutputManifestSha256,
                StringComparison.Ordinal)
            || !string.Equals(
                result.OutputIdentity,
                "openvino-runtime-profile-v3-" + evidenceDigest,
                StringComparison.Ordinal))
        {
            return null;
        }

        cancellationToken.ThrowIfCancellationRequested();
        OpenVinoRuntimeOptimizationProfile profile =
            OpenVinoRuntimeOptimizationProfile.Read(directory);
        if (profile.OptimizationPlanId != result.OptimizationPlanId
            || profile.ExecutionId != result.ExecutionId
            || !string.Equals(
                profile.ConfigurationSha256,
                result.ConfigurationSha256,
                StringComparison.Ordinal)
            || !string.Equals(
                profile.ModelSha256,
                result.SourceSha256,
                StringComparison.Ordinal)
            || profile.ModelLengthBytes != result.SourceLengthBytes
            || !string.Equals(
                profile.ProductHardwareRunId,
                result.ProductHardwareRunId,
                StringComparison.Ordinal)
            || !string.Equals(
                profile.HardwareSnapshotSha256,
                result.HardwareSnapshotSha256,
                StringComparison.Ordinal))
        {
            return null;
        }

        return new ValidatedPublication(
            new OpenVinoPublishedOutput(
                OpenVinoPublishedOutputKind.RuntimeConfiguration,
                PersistentDirectory: null,
                profile,
                profile.RuntimeConfiguration),
            evidenceDigest);
    }

    private static async Task<ValidatedPublication?> ValidatePersistentAsync(
        OptimizationExecutionResult result,
        string directory,
        CancellationToken cancellationToken)
    {
        if (!result.OutputIdentity!.StartsWith(
                "openvino-package-v2-",
                StringComparison.Ordinal))
        {
            return null;
        }

        string provenancePath = Path.Combine(
            directory,
            OpenVinoOptimizationProvenance.FileName);
        _ = RequireRegularFile(provenancePath, MaximumMetadataBytes);
        string evidenceDigest = await ComputeSha256Async(
            provenancePath,
            MaximumMetadataBytes,
            cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        OpenVinoOptimizationProvenance provenance =
            OpenVinoOptimizationProvenance.Read(directory);
        HashSet<string> actualNames = new(StringComparer.OrdinalIgnoreCase);
        foreach (string entry in Directory.EnumerateFileSystemEntries(
                     directory,
                     "*",
                     SearchOption.TopDirectoryOnly))
        {
            cancellationToken.ThrowIfCancellationRequested();
            FileAttributes attributes = File.GetAttributes(entry);
            if ((attributes &
                (FileAttributes.Directory | FileAttributes.ReparsePoint)) != 0)
            {
                return null;
            }
            actualNames.Add(Path.GetFileName(entry));
        }

        HashSet<string> expectedNames = provenance.OutputFiles
            .Select(static file => file.Path)
            .Append(OpenVinoOptimizationProvenance.FileName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!expectedNames.SetEquals(actualNames))
        {
            return null;
        }

        List<OpenVinoOutputArtifact> liveFiles = [];
        ulong total = 0;
        foreach (OpenVinoOutputArtifact expected in provenance.OutputFiles)
        {
            string path = Path.Combine(directory, expected.Path);
            FileInfo file = RequireRegularFile(path, checked((long)MaximumArtifactBytes));
            ulong length = checked((ulong)file.Length);
            total = checked(total + length);
            if (total > MaximumArtifactBytes)
            {
                return null;
            }

            liveFiles.Add(new OpenVinoOutputArtifact(
                expected.Path,
                file.Length,
                await ComputeSha256Async(
                    path,
                    checked((long)MaximumArtifactBytes),
                    cancellationToken).ConfigureAwait(false)));
        }

        string digest = OpenVinoProvenance.ComputeOutputManifestDigest(liveFiles);
        if (provenance.OptimizationPlanId != result.OptimizationPlanId
            || provenance.OperationId != result.ExecutionId
            || !string.Equals(
                provenance.ConfigurationSha256,
                result.ConfigurationSha256,
                StringComparison.Ordinal)
            || !string.Equals(
                provenance.ModelSha256,
                result.SourceSha256,
                StringComparison.Ordinal)
            || provenance.ModelLengthBytes != result.SourceLengthBytes
            || !string.Equals(
                provenance.ProductHardwareRunId,
                result.ProductHardwareRunId,
                StringComparison.Ordinal)
            || !string.Equals(
                provenance.HardwareSnapshotSha256,
                result.HardwareSnapshotSha256,
                StringComparison.Ordinal)
            || !string.Equals(
                provenance.OutputManifestSha256,
                digest,
                StringComparison.Ordinal)
            || !string.Equals(
                result.OutputManifestSha256,
                digest,
                StringComparison.Ordinal)
            || !string.Equals(
                result.OutputIdentity,
                "openvino-package-v2-" + digest,
                StringComparison.Ordinal)
            || total != result.OutputSizeBytes)
        {
            return null;
        }

        return new ValidatedPublication(
            new OpenVinoPublishedOutput(
                OpenVinoPublishedOutputKind.PersistentPackage,
                directory,
                RuntimeProfile: null,
                provenance.RuntimeConfiguration!),
            evidenceDigest);
    }

    private string RequireDirectChild(string directory)
    {
        string fullPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory));
        string relative = Path.GetRelativePath(_root, fullPath);
        if (relative is "." or ".."
            || Path.IsPathRooted(relative)
            || relative.StartsWith(
                ".." + Path.DirectorySeparatorChar,
                StringComparison.Ordinal)
            || relative.Contains(Path.DirectorySeparatorChar)
            || relative.Contains(Path.AltDirectorySeparatorChar))
        {
            throw new ArgumentException(
                "The OpenVINO publication must be a direct child of its output root.",
                nameof(directory));
        }

        return fullPath;
    }

    private static void RequirePlainDirectory(string path)
    {
        StoragePathGuard.RequireNoReparseAncestors(path);
        DirectoryInfo directory = new(path);
        if (!directory.Exists
            || (directory.Attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidDataException("The publication directory is invalid.");
        }
    }

    private static FileInfo RequireRegularFile(string path, long maximumBytes)
    {
        StoragePathGuard.RequireNoReparseAncestors(path);
        FileInfo file = new(path);
        if (!file.Exists
            || file.Length <= 0
            || file.Length > maximumBytes
            || (file.Attributes & (FileAttributes.ReparsePoint | FileAttributes.Directory)) != 0)
        {
            throw new InvalidDataException("The publication file is invalid.");
        }
        return file;
    }

    private static async Task<string> ComputeSha256Async(
        string path,
        long maximumBytes,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        FileInfo file = RequireRegularFile(path, maximumBytes);
        long expectedLength = file.Length;
        byte[] buffer = ArrayPool<byte>.Shared.Rent(BufferBytes);
        try
        {
            await using FileStream stream = new(
                file.FullName,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                BufferBytes,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            if (stream.Length != expectedLength)
            {
                throw new InvalidDataException(
                    "The publication file changed during identity validation.");
            }

            using IncrementalHash hash = IncrementalHash.CreateHash(
                HashAlgorithmName.SHA256);
            long readTotal = 0;
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int read = await stream.ReadAsync(
                    buffer.AsMemory(0, BufferBytes),
                    cancellationToken).ConfigureAwait(false);
                if (read == 0)
                {
                    break;
                }
                readTotal = checked(readTotal + read);
                if (readTotal > expectedLength || readTotal > maximumBytes)
                {
                    throw new InvalidDataException(
                        "The publication file changed or exceeded its identity bound.");
                }
                hash.AppendData(buffer, 0, read);
            }
            if (readTotal != expectedLength)
            {
                throw new InvalidDataException(
                    "The publication file changed during identity validation.");
            }
            return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer, clearArray: true);
        }
    }

    private static async Task CopyFileAsync(
        string sourcePath,
        string destinationPath,
        ulong expectedLength,
        CancellationToken cancellationToken)
    {
        byte[] buffer = ArrayPool<byte>.Shared.Rent(BufferBytes);
        try
        {
            await using FileStream source = new(
                sourcePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                BufferBytes,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            if (checked((ulong)source.Length) != expectedLength)
            {
                throw new InvalidDataException("The OpenVINO package changed during export.");
            }
            await using FileStream destination = new(
                destinationPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                BufferBytes,
                FileOptions.Asynchronous | FileOptions.WriteThrough);
            ulong copied = 0;
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int read = await source.ReadAsync(
                    buffer.AsMemory(0, BufferBytes),
                    cancellationToken).ConfigureAwait(false);
                if (read == 0)
                {
                    break;
                }
                copied = checked(copied + (ulong)read);
                if (copied > expectedLength)
                {
                    throw new InvalidDataException(
                        "The OpenVINO package changed during export.");
                }
                await destination.WriteAsync(
                    buffer.AsMemory(0, read),
                    cancellationToken).ConfigureAwait(false);
            }
            if (copied != expectedLength)
            {
                throw new InvalidDataException("The OpenVINO package changed during export.");
            }
            await destination.FlushAsync(cancellationToken).ConfigureAwait(false);
            destination.Flush(flushToDisk: true);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer, clearArray: true);
        }
    }

    private sealed record RegisteredPublication(
        string Directory,
        string EvidenceSha256);

    private sealed record ValidatedPublication(
        OpenVinoPublishedOutput Publication,
        string EvidenceSha256);

    private static bool IsEligible(OptimizationExecutionResult? result) =>
        result is not null
        && result.IsSuccessful
        && result.Route == OptimizationRoute.OpenVino
        && result.SourceUnchanged
        && result.OutputIdentity is not null
        && result.OutputManifestSha256 is not null
        && result.OutputSizeBytes > 0;

    private static bool IsFileFailure(Exception failure) => failure is
        IOException
        or UnauthorizedAccessException
        or InvalidDataException
        or ArgumentException
        or InvalidOperationException
        or OverflowException
        or CryptographicException;

    private readonly record struct PublicationKey(
        OptimizationExecutionStatus Status,
        Guid OptimizationPlanId,
        Guid ExecutionId,
        string ConfigurationSha256,
        string SourceSha256,
        ulong SourceLengthBytes,
        string ProductHardwareRunId,
        string HardwareSnapshotSha256,
        string OutputIdentity,
        string OutputManifestSha256,
        ulong OutputSizeBytes)
    {
        internal static PublicationKey From(OptimizationExecutionResult result) => new(
            result.Status,
            result.OptimizationPlanId,
            result.ExecutionId,
            result.ConfigurationSha256,
            result.SourceSha256,
            result.SourceLengthBytes,
            result.ProductHardwareRunId,
            result.HardwareSnapshotSha256,
            result.OutputIdentity!,
            result.OutputManifestSha256!,
            result.OutputSizeBytes);
    }
}
