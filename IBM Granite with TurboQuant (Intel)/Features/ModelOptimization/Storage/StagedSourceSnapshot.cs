using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace GraniteEdgeAI.Features.ModelOptimization.Storage;

/// <summary>
/// Private, operation-owned copy of an inspected source. The path is never
/// surfaced as data: executors receive bounded read access through this lease.
/// </summary>
internal sealed class StagedSourceSnapshot : IDisposable
{
    private readonly string _snapshotPath;
    private readonly Func<bool>? _integrityVerifier;
    private readonly Action? _cleanup;
    private bool _disposed;

    internal StagedSourceSnapshot(
        string sourceSha256,
        ulong sourceLengthBytes,
        string sealedSnapshotIdentity,
        string snapshotPath,
        Func<bool>? integrityVerifier = null,
        Action? cleanup = null)
    {
        SourceSha256 = sourceSha256;
        SourceLengthBytes = sourceLengthBytes;
        SealedSnapshotIdentity = sealedSnapshotIdentity;
        _snapshotPath = snapshotPath;
        _integrityVerifier = integrityVerifier;
        _cleanup = cleanup;
        Validate();
    }

    // test-only compatibility constructor. it cannot provide source access
    internal StagedSourceSnapshot(
        string sourceSha256,
        ulong sourceLengthBytes,
        string sealedSnapshotIdentity)
        : this(sourceSha256, sourceLengthBytes, sealedSnapshotIdentity, string.Empty)
    {
    }

    internal string SourceSha256 { get; }
    internal ulong SourceLengthBytes { get; }
    internal string SealedSnapshotIdentity { get; }
    internal bool HasPrivateSource => _snapshotPath.Length != 0;

    internal StagedSourceSnapshot Validate()
    {
        if (SourceLengthBytes < 1
            || !IsCanonicalSha256(SourceSha256)
            || !IsOpaqueIdentity(SealedSnapshotIdentity)
            || (_snapshotPath.Length != 0 && !Path.IsPathFullyQualified(_snapshotPath)))
        {
            throw new ArgumentException("The staged source snapshot is not sealed.");
        }
        ObjectDisposedException.ThrowIf(_disposed, this);
        return this;
    }

    internal FileStream OpenReadVerified()
    {
        Validate();
        if (_snapshotPath.Length == 0)
        {
            throw new InvalidOperationException("This snapshot has no private source lease.");
        }
        StoragePathGuard.RequireRegularFile(_snapshotPath);
        return new FileStream(
            _snapshotPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            128 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
    }

    internal bool RehashMatches()
    {
        Validate();
        if (_integrityVerifier is not null)
        {
            return _integrityVerifier();
        }
        using FileStream stream = OpenReadVerified();
        string digest = Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
        return (ulong)stream.Length == SourceLengthBytes
            && string.Equals(digest, SourceSha256, StringComparison.Ordinal);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        _cleanup?.Invoke();
    }

    internal static bool IsCanonicalSha256(string? value) =>
        value is { Length: 64 }
        && value.All(character => character is >= '0' and <= '9'
            or >= 'a' and <= 'f');

    internal static bool IsOpaqueIdentity(string? value) =>
        value is { Length: >= 8 and <= 128 }
        && value.All(character => char.IsAsciiLetterOrDigit(character)
            || character is '-' or '_');
}

internal static class StoragePathGuard
{
    internal static string RequireRoot(string path, bool create)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path))
        {
            throw new ArgumentException("A fully qualified app-owned root is required.", nameof(path));
        }
        string full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        RequireNoReparseAncestors(full);
        if (create)
        {
            Directory.CreateDirectory(full);
            RequireNoReparseAncestors(full);
        }
        if (!Directory.Exists(full))
        {
            throw new DirectoryNotFoundException(
                "The app-owned root does not exist.");
        }
        return full;
    }

    internal static string RequireChild(string root, string path, bool mustExist)
    {
        string fullRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        string full = Path.GetFullPath(path);
        string prefix = fullRoot + Path.DirectorySeparatorChar;
        if (!full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The app-owned path escaped its verified root.");
        }
        if (mustExist && !File.Exists(full) && !Directory.Exists(full))
        {
            throw new FileNotFoundException("The app-owned path no longer exists.");
        }
        RequireNoReparseAncestors(fullRoot);
        RequireNoReparseAncestors(full);
        return full;
    }

    internal static void RequireRegularFile(string path)
    {
        RequireNoReparseAncestors(path);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("The sealed source is unavailable.");
        }
        FileAttributes attributes = File.GetAttributes(path);
        if ((attributes & (FileAttributes.Directory | FileAttributes.ReparsePoint)) != 0)
        {
            throw new InvalidOperationException("Only an ordinary local file is accepted.");
        }
    }

    internal static bool SameVolume(string first, string second) =>
        string.Equals(
            Path.GetPathRoot(Path.GetFullPath(first)),
            Path.GetPathRoot(Path.GetFullPath(second)),
            StringComparison.OrdinalIgnoreCase);

    internal static void RequireNoReparseAncestors(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path))
        {
            throw new ArgumentException(
                "A fully qualified local path is required.", nameof(path));
        }

        string full = Path.GetFullPath(path);
        string root = Path.GetPathRoot(full)
            ?? throw new ArgumentException(
                "A rooted local path is required.", nameof(path));
        if ((File.GetAttributes(root) & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidOperationException(
                "Reparse-point ancestry is not accepted.");
        }

        string cursor = root;
        string relative = Path.GetRelativePath(root, full);
        foreach (string component in relative.Split(
                     [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                     StringSplitOptions.RemoveEmptyEntries))
        {
            cursor = Path.Combine(cursor, component);
            if (!File.Exists(cursor) && !Directory.Exists(cursor))
            {
                return;
            }
            if ((File.GetAttributes(cursor) & FileAttributes.ReparsePoint) != 0)
            {
                throw new InvalidOperationException(
                    "Reparse-point ancestry is not accepted.");
            }
        }
    }
}
