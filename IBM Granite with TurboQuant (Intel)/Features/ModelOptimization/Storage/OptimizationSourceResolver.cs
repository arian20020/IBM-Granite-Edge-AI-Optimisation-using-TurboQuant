using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using GraniteEdgeAI.Features.ModelInspection.SourceCustody;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;

namespace GraniteEdgeAI.Features.ModelOptimization.Storage;

internal sealed record OpenVinoSourceMember(
    string RelativePath,
    string Sha256,
    ulong LengthBytes);

internal sealed class OptimizationSourceResolver
{
    private readonly ModelSourceCustodyRegistry _custody;
    private readonly string _sourceRoot;

    internal OptimizationSourceResolver(
        ModelSourceCustodyRegistry custody,
        string appOwnedSourceRoot)
    {
        _custody = custody ?? throw new ArgumentNullException(nameof(custody));
        _sourceRoot = StoragePathGuard.RequireRoot(appOwnedSourceRoot, create: true);
    }

    internal async Task<StagedSourceSnapshot> ResolveGgufAsync(
        ModelSourceCustodyKey key,
        long generation,
        CancellationToken cancellationToken)
    {
        if (generation < 1 || key.Route != OptimizationRoute.Gguf)
        {
            throw new ArgumentException("A current GGUF source key and generation are required.");
        }
        if (!_custody.TryAcquire(key, out ModelSourceLease? lease) || lease is null)
        {
            throw new InvalidOperationException("The inspected source lease is no longer current.");
        }
        using (lease)
        {
            StoragePathGuard.RequireRegularFile(lease.SourcePath);
            string operationRoot = CreateOperationRoot();
            string destination = Path.Combine(operationRoot, "source.gguf");
            try
            {
                (string digest, ulong length) = await CopyAndHashAsync(
                    lease.SourcePath,
                    destination,
                    cancellationToken).ConfigureAwait(false);
                if (!string.Equals(digest, key.ModelSha256, StringComparison.Ordinal)
                    || length != (ulong)key.ModelLengthBytes)
                {
                    throw new InvalidDataException("The inspected source identity changed before sealing.");
                }
                SetReadOnly(destination);
                string identity = CreateIdentity("src", generation);
                return new StagedSourceSnapshot(
                    digest,
                    length,
                    identity,
                    destination,
                    integrityVerifier: () => FileMatches(lease.SourcePath, destination, digest, length),
                    cleanup: () => TryDeleteOwnedDirectory(operationRoot));
            }
            catch
            {
                TryDeleteOwnedDirectory(operationRoot);
                throw;
            }
        }
    }

    internal async Task<StagedSourceSnapshot> ResolveOpenVinoAsync(
        ModelSourceCustodyKey key,
        IReadOnlyList<OpenVinoSourceMember> members,
        long generation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(members);
        if (generation < 1 || key.Route != OptimizationRoute.OpenVino)
        {
            throw new ArgumentException("A current OpenVINO source key and generation are required.");
        }
        if (members.Count == 0 || members.Select(member => member.RelativePath)
            .Distinct(StringComparer.Ordinal).Count() != members.Count)
        {
            throw new ArgumentException("An exact, duplicate-free package manifest is required.", nameof(members));
        }
        if (!_custody.TryAcquire(key, out ModelSourceLease? lease) || lease is null)
        {
            throw new InvalidOperationException("The inspected package lease is no longer current.");
        }
        using (lease)
        {
            string packageRoot = StoragePathGuard.RequireRoot(lease.SourcePath, create: false);
            string operationRoot = CreateOperationRoot();
            try
            {
                using var packageDigest = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                ulong total = 0;
                foreach (OpenVinoSourceMember member in members.OrderBy(value => value.RelativePath, StringComparer.Ordinal))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    ValidateRelativePath(member.RelativePath);
                    if (!StagedSourceSnapshot.IsCanonicalSha256(member.Sha256) || member.LengthBytes == 0)
                    {
                        throw new InvalidDataException("The package manifest contains an invalid member identity.");
                    }
                    string source = StoragePathGuard.RequireChild(
                        packageRoot,
                        Path.Combine(packageRoot, member.RelativePath),
                        mustExist: true);
                    StoragePathGuard.RequireRegularFile(source);
                    string destination = Path.Combine(operationRoot, member.RelativePath);
                    Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                    (string digest, ulong length) = await CopyAndHashAsync(source, destination, cancellationToken)
                        .ConfigureAwait(false);
                    if (!string.Equals(digest, member.Sha256, StringComparison.Ordinal)
                        || length != member.LengthBytes)
                    {
                        throw new InvalidDataException("A package member changed before sealing.");
                    }
                    SetReadOnly(destination);
                    AppendCanonicalMember(packageDigest, member.RelativePath, digest, length);
                    total = checked(total + length);
                }
                string packageSha = Convert.ToHexString(packageDigest.GetHashAndReset()).ToLowerInvariant();
                if (!string.Equals(packageSha, key.ModelSha256, StringComparison.Ordinal)
                    || total != (ulong)key.ModelLengthBytes)
                {
                    throw new InvalidDataException("The sealed package does not match the inspected package identity.");
                }
                return new StagedSourceSnapshot(
                    packageSha,
                    total,
                    CreateIdentity("pkg", generation),
                    Path.Combine(operationRoot, members.OrderBy(value => value.RelativePath, StringComparer.Ordinal).First().RelativePath),
                    integrityVerifier: () => PackageMatches(packageRoot, operationRoot, members, packageSha, total),
                    cleanup: () => TryDeleteOwnedDirectory(operationRoot));
            }
            catch
            {
                TryDeleteOwnedDirectory(operationRoot);
                throw;
            }
        }
    }

    private string CreateOperationRoot()
    {
        string path = Path.Combine(_sourceRoot, $"source-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return StoragePathGuard.RequireChild(_sourceRoot, path, mustExist: true);
    }

    private static async Task<(string Digest, ulong Length)> CopyAndHashAsync(
        string source,
        string destination,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        await using var input = new FileStream(
            source, FileMode.Open, FileAccess.Read, FileShare.Read,
            128 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
        await using var output = new FileStream(
            destination, FileMode.CreateNew, FileAccess.Write, FileShare.None,
            128 * 1024, FileOptions.Asynchronous | FileOptions.WriteThrough);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        byte[] buffer = ArrayPool<byte>.Shared.Rent(128 * 1024);
        ulong length = 0;
        try
        {
            int read;
            while ((read = await input.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken)
                .ConfigureAwait(false)) != 0)
            {
                hash.AppendData(buffer, 0, read);
                await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                length = checked(length + (ulong)read);
            }
            await output.FlushAsync(cancellationToken).ConfigureAwait(false);
            output.Flush(flushToDisk: true);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer, clearArray: true);
        }
        return (Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant(), length);
    }

    private static void AppendCanonicalMember(
        IncrementalHash hash,
        string relativePath,
        string digest,
        ulong length)
    {
        byte[] bytes = System.Text.Encoding.UTF8.GetBytes(
            relativePath.Replace('\\', '/') + "\0" + digest + "\0" + length + "\n");
        hash.AppendData(bytes);
    }

    private static void ValidateRelativePath(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath)
            || Path.IsPathFullyQualified(relativePath)
            || relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Any(segment => segment is "" or "." or ".."))
        {
            throw new InvalidDataException("A package manifest path is invalid.");
        }
    }

    private static string CreateIdentity(string prefix, long generation) =>
        $"{prefix}-{generation}-{Guid.NewGuid():N}";

    private static void SetReadOnly(string path) =>
        File.SetAttributes(path, File.GetAttributes(path) | FileAttributes.ReadOnly);

    private static bool FileMatches(
        string original,
        string staged,
        string expectedDigest,
        ulong expectedLength)
    {
        try
        {
            return HashFile(original) == (expectedDigest, expectedLength)
                && HashFile(staged) == (expectedDigest, expectedLength);
        }
        catch
        {
            return false;
        }
    }

    private static bool PackageMatches(
        string originalRoot,
        string stagedRoot,
        IReadOnlyList<OpenVinoSourceMember> members,
        string expectedDigest,
        ulong expectedLength)
    {
        try
        {
            return HashPackage(originalRoot, members) == (expectedDigest, expectedLength)
                && HashPackage(stagedRoot, members) == (expectedDigest, expectedLength);
        }
        catch
        {
            return false;
        }
    }

    private static (string Digest, ulong Length) HashPackage(
        string root,
        IReadOnlyList<OpenVinoSourceMember> members)
    {
        using var packageDigest = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        ulong total = 0;
        foreach (OpenVinoSourceMember member in members.OrderBy(value => value.RelativePath, StringComparer.Ordinal))
        {
            string path = StoragePathGuard.RequireChild(
                root,
                Path.Combine(root, member.RelativePath),
                mustExist: true);
            (string digest, ulong length) = HashFile(path);
            if (!string.Equals(digest, member.Sha256, StringComparison.Ordinal)
                || length != member.LengthBytes)
            {
                return (string.Empty, 0);
            }
            AppendCanonicalMember(packageDigest, member.RelativePath, digest, length);
            total = checked(total + length);
        }
        return (Convert.ToHexString(packageDigest.GetHashAndReset()).ToLowerInvariant(), total);
    }

    private static (string Digest, ulong Length) HashFile(string path)
    {
        StoragePathGuard.RequireRegularFile(path);
        using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        string digest = Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
        return (digest, (ulong)stream.Length);
    }

    private void TryDeleteOwnedDirectory(string path)
    {
        try
        {
            string owned = StoragePathGuard.RequireChild(_sourceRoot, path, mustExist: true);
            foreach (string file in Directory.EnumerateFiles(owned, "*", SearchOption.AllDirectories))
            {
                File.SetAttributes(file, FileAttributes.Normal);
            }
            Directory.Delete(owned, recursive: true);
        }
        catch
        {
            // Cleanup is best-effort; no external path is ever selected.
        }
    }
}
