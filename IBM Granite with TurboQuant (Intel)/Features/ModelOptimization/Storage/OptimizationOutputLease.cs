using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace GraniteEdgeAI.Features.ModelOptimization.Storage;

internal sealed record SealedOptimizationCandidate
{
    private readonly string _candidateRoot;
    private readonly Guid _ownerToken;

    internal SealedOptimizationCandidate(
        string outputIdentity,
        string outputManifestSha256,
        ulong outputSizeBytes,
        string sealedStagingIdentity,
        string candidateRoot,
        Guid ownerToken)
    {
        OutputIdentity = outputIdentity;
        OutputManifestSha256 = outputManifestSha256;
        OutputSizeBytes = outputSizeBytes;
        SealedStagingIdentity = sealedStagingIdentity;
        _candidateRoot = candidateRoot;
        _ownerToken = ownerToken;
    }

    internal string OutputIdentity { get; }
    internal string OutputManifestSha256 { get; }
    internal ulong OutputSizeBytes { get; }
    internal string SealedStagingIdentity { get; }
    internal string CandidateRoot(Guid ownerToken)
    {
        if (ownerToken != _ownerToken)
        {
            throw new InvalidOperationException("The candidate was not issued by this output registry.");
        }
        return _candidateRoot;
    }
}

internal sealed class OptimizationOutputLease : IDisposable
{
    private readonly string _stagingRoot;
    private readonly string _candidateRoot;
    private readonly Guid _ownerToken;
    private readonly Action _release;
    private bool _sealed;
    private bool _disposed;

    internal OptimizationOutputLease(
        string stagingRoot,
        string candidateRoot,
        string sealedStagingIdentity,
        Guid ownerToken,
        Action release)
    {
        _stagingRoot = stagingRoot;
        _candidateRoot = candidateRoot;
        SealedStagingIdentity = sealedStagingIdentity;
        _ownerToken = ownerToken;
        _release = release;
    }

    internal string SealedStagingIdentity { get; }

    internal FileStream CreateFileForWrite(string relativePath)
    {
        string path = CreatePendingFilePath(relativePath);
        return new FileStream(
            path,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            128 * 1024,
            FileOptions.Asynchronous | FileOptions.WriteThrough);
    }

    /// <summary>
    /// Reserves a path inside this exact operation-owned candidate directory
    /// without creating the file. Native tools require an unused destination
    /// path and are never given the staging root itself.
    /// </summary>
    internal string CreatePendingFilePath(string relativePath)
    {
        ThrowIfUnavailable();
        ValidateRelativePath(relativePath);
        string path = StoragePathGuard.RequireChild(
            _stagingRoot,
            Path.Combine(_candidateRoot, relativePath),
            mustExist: false);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        StoragePathGuard.RequireChild(
            _stagingRoot,
            Path.GetDirectoryName(path)!,
            mustExist: true);
        if (File.Exists(path) || Directory.Exists(path))
        {
            throw new IOException("The pending output path is already in use.");
        }
        return path;
    }

    internal SealedOptimizationCandidate Seal(string outputIdentity)
        => SealCore(outputIdentity, null, null);

    internal SealedOptimizationCandidate Seal(
        string outputIdentity,
        string expectedSha256,
        ulong expectedLengthBytes)
    {
        if (!IsCanonicalSha256(expectedSha256))
        {
            throw new ArgumentException(
                "A canonical lowercase validated output digest is required.",
                nameof(expectedSha256));
        }
        if (expectedLengthBytes == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(expectedLengthBytes));
        }
        return SealCore(outputIdentity, expectedSha256, expectedLengthBytes);
    }

    private SealedOptimizationCandidate SealCore(
        string outputIdentity,
        string? expectedSha256,
        ulong? expectedLengthBytes)
    {
        ThrowIfUnavailable();
        if (!StagedSourceSnapshot.IsOpaqueIdentity(outputIdentity))
        {
            throw new ArgumentException("A bounded opaque output identity is required.", nameof(outputIdentity));
        }
        string root = StoragePathGuard.RequireChild(_stagingRoot, _candidateRoot, mustExist: true);
        if (Directory.EnumerateDirectories(
                root,
                "*",
                SearchOption.TopDirectoryOnly).Any())
        {
            throw new InvalidDataException(
                "A GGUF candidate cannot contain directories or linked content.");
        }
        string[] members = Directory.GetFiles(
            root,
            "*",
            SearchOption.TopDirectoryOnly);
        if (members.Length != 1
            || !string.Equals(
                Path.GetExtension(members[0]),
                ".gguf",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "A GGUF candidate must contain exactly one model file.");
        }
        StoragePathGuard.RequireRegularFile(members[0]);
        ManifestIdentity computed = ComputeManifestIdentity(root);
        if (computed.FileCount == 0 || computed.SizeBytes == 0)
        {
            throw new InvalidOperationException("An empty output cannot be sealed.");
        }
        if (expectedSha256 is not null
            && (!string.Equals(
                    computed.SingleFileSha256,
                    expectedSha256,
                    StringComparison.Ordinal)
                || computed.SizeBytes != expectedLengthBytes))
        {
            throw new InvalidDataException(
                "The output changed after its final validation.");
        }
        _sealed = true;
        return new SealedOptimizationCandidate(
            outputIdentity,
            computed.ManifestSha256,
            computed.SizeBytes,
            SealedStagingIdentity,
            root,
            _ownerToken);
    }

    internal static (string ManifestSha256, ulong SizeBytes, int FileCount) ComputeManifest(
        string root)
    {
        ManifestIdentity identity = ComputeManifestIdentity(root);
        return (identity.ManifestSha256, identity.SizeBytes, identity.FileCount);
    }

    private static ManifestIdentity ComputeManifestIdentity(string root)
    {
        using var manifest = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        ulong total = 0;
        int count = 0;
        string? singleFileSha256 = null;
        foreach (string path in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .OrderBy(path => Path.GetRelativePath(root, path), StringComparer.Ordinal))
        {
            StoragePathGuard.RequireRegularFile(path);
            string relative = Path.GetRelativePath(root, path).Replace('\\', '/');
            using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            string digest = Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
            ulong length = (ulong)stream.Length;
            byte[] row = Encoding.UTF8.GetBytes(relative + "\0" + digest + "\0" + length + "\n");
            manifest.AppendData(row);
            total = checked(total + length);
            count++;
            singleFileSha256 = count == 1 ? digest : null;
        }
        return new ManifestIdentity(
            Convert.ToHexString(manifest.GetHashAndReset()).ToLowerInvariant(),
            total,
            count,
            count == 1 ? singleFileSha256 : null);
    }

    internal void MarkCommitted(Guid ownerToken)
    {
        if (ownerToken != _ownerToken || !_sealed)
        {
            throw new InvalidOperationException("Only the issuing registry can commit this lease.");
        }
        _disposed = true;
        _release();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        _release();
    }

    private void ThrowIfUnavailable()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_sealed)
        {
            throw new InvalidOperationException("The output lease is already sealed.");
        }
    }

    private static void ValidateRelativePath(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath)
            || Path.IsPathFullyQualified(relativePath)
            || relativePath.Contains(Path.DirectorySeparatorChar)
            || relativePath.Contains(Path.AltDirectorySeparatorChar)
            || relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Any(segment => segment is "" or "." or ".."))
        {
            throw new ArgumentException("The output member path is invalid.", nameof(relativePath));
        }
    }

    private static bool IsCanonicalSha256(string? value) =>
        value is { Length: 64 }
        && value.All(character => character is >= '0' and <= '9'
            or >= 'a' and <= 'f');

    private sealed record ManifestIdentity(
        string ManifestSha256,
        ulong SizeBytes,
        int FileCount,
        string? SingleFileSha256);
}
