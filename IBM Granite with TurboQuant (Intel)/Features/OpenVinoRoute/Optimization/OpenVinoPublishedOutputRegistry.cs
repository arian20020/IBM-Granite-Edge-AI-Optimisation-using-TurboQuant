using System.Collections.Concurrent;
using System.Buffers;
using System.Security.Cryptography;
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
            _ => throw new InvalidDataException(
                "The selected runtime configuration is not executable by this worker.")
        };
}

internal sealed class OpenVinoOptimizationChatTarget : IDisposable
{
    private readonly IDisposable? _sourceLease;

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

    public void Dispose() => _sourceLease?.Dispose();
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
        string publicationDirectory)
    {
        if (!TryValidate(result, publicationDirectory, out _, out string? evidenceDigest))
        {
            return false;
        }

        try
        {
            PublicationKey key = PublicationKey.From(result);
            var registration = new RegisteredPublication(
                RequireDirectChild(publicationDirectory),
                evidenceDigest!);
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
        publication = null;
        if (!IsEligible(result)
            || !_publications.TryGetValue(
                PublicationKey.From(result),
                out RegisteredPublication? registration))
        {
            return false;
        }

        try
        {
            if (!TryValidate(
                    result,
                    registration.Directory,
                    out publication,
                    out string? evidenceDigest)
                || !string.Equals(
                    evidenceDigest,
                    registration.EvidenceSha256,
                    StringComparison.Ordinal))
            {
                publication = null;
                return false;
            }

            return true;
        }
        catch (Exception failure) when (IsFileFailure(failure))
        {
            publication = null;
            return false;
        }
    }

    internal async Task<bool> ExportPersistentAsync(
        OptimizationExecutionResult result,
        string destinationDirectory,
        ulong maximumBytes,
        CancellationToken cancellationToken)
    {
        if (maximumBytes == 0 || maximumBytes > MaximumArtifactBytes)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumBytes));
        }
        if (!TryResolve(result, out OpenVinoPublishedOutput? publication)
            || publication is null
            || !publication.IsPersistentArtifact
            || publication.PersistentDirectory is null)
        {
            return false;
        }
        if (result.OutputSizeBytes > maximumBytes)
        {
            throw new InvalidDataException(
                "The selected OpenVINO package exceeds the bounded export size.");
        }

        string destination = RequireDirectChild(destinationDirectory);
        if (Directory.Exists(destination) || File.Exists(destination))
        {
            throw new IOException("The export destination already exists.");
        }
        string temporary = RequireDirectChild(Path.Combine(
            _root,
            $".export-{Guid.NewGuid():N}.tmp"));
        Directory.CreateDirectory(temporary);
        RequirePlainDirectory(temporary);
        try
        {
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
                    Path.Combine(temporary, file.Name),
                    checked((ulong)file.Length),
                    cancellationToken).ConfigureAwait(false);
            }
            if (artifactBytes != result.OutputSizeBytes)
            {
                return false;
            }

            var verifier = new OpenVinoPublishedOutputRegistry(_root);
            if (!verifier.TryRegister(result, temporary)
                || !verifier.TryResolve(result, out OpenVinoPublishedOutput? verified)
                || verified is null
                || !verified.IsPersistentArtifact)
            {
                return false;
            }
            cancellationToken.ThrowIfCancellationRequested();
            Directory.Move(temporary, destination);
            return true;
        }
        finally
        {
            TryDeleteTemporaryDirectory(temporary);
        }
    }

    private bool TryValidate(
        OptimizationExecutionResult result,
        string directory,
        out OpenVinoPublishedOutput? publication,
        out string? evidenceDigest)
    {
        publication = null;
        evidenceDigest = null;
        if (!IsEligible(result) || string.IsNullOrWhiteSpace(directory))
        {
            return false;
        }

        try
        {
            string fullPath = RequireDirectChild(directory);
            RequirePlainDirectory(fullPath);
            return result.ProducedPersistentArtifact
                ? TryValidatePersistent(
                    result,
                    fullPath,
                    out publication,
                    out evidenceDigest)
                : TryValidateRuntimeProfile(
                    result,
                    fullPath,
                    out publication,
                    out evidenceDigest);
        }
        catch (Exception failure) when (IsFileFailure(failure))
        {
            return false;
        }
    }

    private static bool TryValidateRuntimeProfile(
        OptimizationExecutionResult result,
        string directory,
        out OpenVinoPublishedOutput? publication,
        out string? evidenceDigest)
    {
        publication = null;
        evidenceDigest = null;
        if (!result.OutputIdentity!.StartsWith(
                "openvino-runtime-profile-v3-",
                StringComparison.Ordinal))
        {
            return false;
        }

        string profilePath = Path.Combine(
            directory,
            OpenVinoRuntimeOptimizationProfile.FileName);
        string[] files = Directory.GetFiles(directory, "*", SearchOption.AllDirectories);
        if (Directory.EnumerateDirectories(directory).Any()
            || files.Length != 1
            || !string.Equals(
                Path.GetFullPath(files[0]),
                Path.GetFullPath(profilePath),
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        FileInfo file = RequireRegularFile(profilePath, MaximumMetadataBytes);
        if (checked((ulong)file.Length) != result.OutputSizeBytes)
        {
            return false;
        }

        evidenceDigest = ComputeSha256(profilePath, MaximumMetadataBytes);
        if (!string.Equals(
                evidenceDigest,
                result.OutputManifestSha256,
                StringComparison.Ordinal)
            || !string.Equals(
                result.OutputIdentity,
                "openvino-runtime-profile-v3-" + evidenceDigest,
                StringComparison.Ordinal))
        {
            return false;
        }

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
            return false;
        }

        publication = new OpenVinoPublishedOutput(
            OpenVinoPublishedOutputKind.RuntimeConfiguration,
            PersistentDirectory: null,
            profile,
            profile.RuntimeConfiguration);
        return true;
    }

    private static bool TryValidatePersistent(
        OptimizationExecutionResult result,
        string directory,
        out OpenVinoPublishedOutput? publication,
        out string? evidenceDigest)
    {
        publication = null;
        evidenceDigest = null;
        if (!result.OutputIdentity!.StartsWith(
                "openvino-package-v2-",
                StringComparison.Ordinal))
        {
            return false;
        }

        string provenancePath = Path.Combine(
            directory,
            OpenVinoOptimizationProvenance.FileName);
        _ = RequireRegularFile(provenancePath, MaximumMetadataBytes);
        evidenceDigest = ComputeSha256(provenancePath, MaximumMetadataBytes);
        OpenVinoOptimizationProvenance provenance =
            OpenVinoOptimizationProvenance.Read(directory);
        if (Directory.EnumerateDirectories(directory).Any())
        {
            return false;
        }

        HashSet<string> expectedNames = provenance.OutputFiles
            .Select(static file => file.Path)
            .Append(OpenVinoOptimizationProvenance.FileName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        HashSet<string> actualNames = Directory.EnumerateFiles(directory)
            .Select(Path.GetFileName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase)!;
        if (!expectedNames.SetEquals(actualNames))
        {
            return false;
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
                return false;
            }

            liveFiles.Add(new OpenVinoOutputArtifact(
                expected.Path,
                file.Length,
                ComputeSha256(path, checked((long)MaximumArtifactBytes))));
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
            return false;
        }

        publication = new OpenVinoPublishedOutput(
            OpenVinoPublishedOutputKind.PersistentPackage,
            directory,
            RuntimeProfile: null,
            provenance.RuntimeConfiguration!);
        return true;
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
        DirectoryInfo directory = new(path);
        if (!directory.Exists
            || (directory.Attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidDataException("The publication directory is invalid.");
        }
    }

    private static FileInfo RequireRegularFile(string path, long maximumBytes)
    {
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

    private static string ComputeSha256(string path, long maximumBytes)
    {
        FileInfo file = RequireRegularFile(path, maximumBytes);
        byte[] buffer = GC.AllocateUninitializedArray<byte>(BufferBytes);
        using FileStream stream = new(
            file.FullName,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            BufferBytes,
            FileOptions.SequentialScan);
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        int read;
        while ((read = stream.Read(buffer, 0, buffer.Length)) != 0)
        {
            hash.AppendData(buffer, 0, read);
        }
        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
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

    private static void TryDeleteTemporaryDirectory(string temporary)
    {
        try
        {
            if (!Directory.Exists(temporary))
            {
                return;
            }
            RequirePlainDirectory(temporary);
            if (Directory.EnumerateDirectories(temporary).Any())
            {
                return;
            }
            foreach (string file in Directory.EnumerateFiles(temporary))
            {
                _ = RequireRegularFile(file, checked((long)MaximumArtifactBytes));
                File.Delete(file);
            }
            Directory.Delete(temporary, recursive: false);
        }
        catch (Exception failure) when (IsFileFailure(failure))
        {
            // Cleanup remains bounded to this exact app-created temporary child.
        }
    }

    private sealed record RegisteredPublication(
        string Directory,
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
