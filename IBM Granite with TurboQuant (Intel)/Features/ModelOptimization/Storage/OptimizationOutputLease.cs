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
        ThrowIfUnavailable();
        ValidateRelativePath(relativePath);
        string path = StoragePathGuard.RequireChild(
            _stagingRoot,
            Path.Combine(_candidateRoot, relativePath),
            mustExist: false);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        StoragePathGuard.RequireChild(_stagingRoot, Path.GetDirectoryName(path)!, mustExist: true);
        return new FileStream(
            path,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            128 * 1024,
            FileOptions.Asynchronous | FileOptions.WriteThrough);
    }

    internal SealedOptimizationCandidate Seal(string outputIdentity)
    {
        ThrowIfUnavailable();
        if (!StagedSourceSnapshot.IsOpaqueIdentity(outputIdentity))
        {
            throw new ArgumentException("A bounded opaque output identity is required.", nameof(outputIdentity));
        }
        string root = StoragePathGuard.RequireChild(_stagingRoot, _candidateRoot, mustExist: true);
        (string manifest, ulong size, int files) = ComputeManifest(root);
        if (files == 0 || size == 0)
        {
            throw new InvalidOperationException("An empty output cannot be sealed.");
        }
        _sealed = true;
        return new SealedOptimizationCandidate(
            outputIdentity,
            manifest,
            size,
            SealedStagingIdentity,
            root,
            _ownerToken);
    }

    internal static (string ManifestSha256, ulong SizeBytes, int FileCount) ComputeManifest(
        string root)
    {
        using var manifest = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        ulong total = 0;
        int count = 0;
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
        }
        return (Convert.ToHexString(manifest.GetHashAndReset()).ToLowerInvariant(), total, count);
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
            || relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Any(segment => segment is "" or "." or ".."))
        {
            throw new ArgumentException("The output member path is invalid.", nameof(relativePath));
        }
    }
}
