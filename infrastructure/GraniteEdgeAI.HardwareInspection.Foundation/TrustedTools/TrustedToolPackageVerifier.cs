using System.Collections.ObjectModel;
using System.Security.Cryptography;

namespace GraniteEdgeAI.HardwareInspection.Foundation.TrustedTools;

public sealed class TrustedToolPackageVerifier
{
    private readonly Action? _afterInitialInventory;

    public TrustedToolPackageVerifier()
    {
    }

    internal TrustedToolPackageVerifier(Action afterInitialInventory)
    {
        _afterInitialInventory = afterInitialInventory ??
            throw new ArgumentNullException(nameof(afterInitialInventory));
    }

    public TrustedToolVerificationResult Verify(
        string approvedRoot,
        string packageRoot,
        TrustedToolPackageManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        if (!TryCanonicalizeExistingDirectory(approvedRoot, out string approvedFullPath))
        {
            return Reject(TrustedToolVerificationFailure.ApprovedRootInvalid);
        }

        if (!TryCanonicalize(packageRoot, out string packageFullPath))
        {
            return Reject(TrustedToolVerificationFailure.PackageRootInvalid);
        }

        string relativePath;
        try
        {
            relativePath = Path.GetRelativePath(approvedFullPath, packageFullPath);
        }
        catch (Exception error) when (error is ArgumentException or IOException)
        {
            return Reject(TrustedToolVerificationFailure.PackageRootInvalid);
        }

        if (Path.IsPathRooted(relativePath) ||
            relativePath.Equals("..", StringComparison.Ordinal) ||
            relativePath.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
            relativePath.StartsWith($"..{Path.AltDirectorySeparatorChar}", StringComparison.Ordinal))
        {
            return Reject(TrustedToolVerificationFailure.PathEscape);
        }

        if (!Directory.Exists(packageFullPath))
        {
            return Reject(TrustedToolVerificationFailure.PackageRootInvalid);
        }

        if (HasReparsePointInPath(approvedFullPath) ||
            HasReparsePointFromApprovedRoot(approvedFullPath, packageFullPath))
        {
            return Reject(TrustedToolVerificationFailure.ReparsePoint);
        }

        if (!TryCaptureFlatInventory(packageFullPath, out string[] initialInventory))
        {
            return Reject(TrustedToolVerificationFailure.FileUnavailable);
        }

        if (!InventoryMatches(initialInventory, manifest.RequiredMembers))
        {
            return Reject(TrustedToolVerificationFailure.InventoryMismatch);
        }

        Dictionary<string, string> actualMembers = initialInventory.ToDictionary(
            member => member,
            member => member,
            StringComparer.OrdinalIgnoreCase);
        if (actualMembers.Count != initialInventory.Length)
        {
            return Reject(TrustedToolVerificationFailure.InventoryMismatch);
        }

        foreach (string member in initialInventory)
        {
            string memberPath = Path.Combine(packageFullPath, member);
            if (IsReparsePoint(memberPath))
            {
                return Reject(TrustedToolVerificationFailure.ReparsePoint);
            }
        }

        _afterInitialInventory?.Invoke();

        Dictionary<string, FileStream> streams = new(StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (string requiredMember in manifest.RequiredMembers)
            {
                string actualMember = actualMembers[requiredMember];
                string memberPath = Path.Combine(packageFullPath, actualMember);
                if (IsReparsePoint(memberPath))
                {
                    DisposeStreams(streams.Values);
                    return Reject(TrustedToolVerificationFailure.PackageChangedDuringVerification);
                }

                streams.Add(
                    requiredMember,
                    new FileStream(
                        memberPath,
                        FileMode.Open,
                        FileAccess.Read,
                        FileShare.Read,
                        bufferSize: 4096,
                        FileOptions.SequentialScan));
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            DisposeStreams(streams.Values);
            return Reject(TrustedToolVerificationFailure.PackageChangedDuringVerification);
        }

        using (new StreamCollectionLease(streams.Values))
        {
            FileStream executable = streams[manifest.ExecutableRelativePath];
            byte[] actualHash;
            try
            {
                executable.Position = 0;
                actualHash = SHA256.HashData(executable);
            }
            catch (IOException)
            {
                return Reject(TrustedToolVerificationFailure.FileUnavailable);
            }

            byte[] requiredHash = Convert.FromHexString(manifest.ExecutableSha256);
            if (!CryptographicOperations.FixedTimeEquals(actualHash, requiredHash))
            {
                return Reject(TrustedToolVerificationFailure.HashMismatch);
            }

            TrustedToolVerificationFailure? peFailure =
                PeImageInspector.Inspect(executable, manifest.RequiredMachine);
            if (peFailure is not null)
            {
                return Reject(peFailure.Value);
            }

            if (!TryCaptureFlatInventory(packageFullPath, out string[] finalInventory) ||
                !initialInventory.Order(StringComparer.Ordinal)
                    .SequenceEqual(finalInventory.Order(StringComparer.Ordinal), StringComparer.Ordinal))
            {
                return Reject(TrustedToolVerificationFailure.PackageChangedDuringVerification);
            }

            Dictionary<string, TrustedToolCommand> commands = manifest.Commands.ToDictionary(
                command => command.Identity,
                command => command,
                StringComparer.OrdinalIgnoreCase);
            VerifiedTrustedTool tool = new(
                manifest.ToolId,
                manifest.Version,
                packageFullPath,
                Path.Combine(packageFullPath, actualMembers[manifest.ExecutableRelativePath]),
                manifest.Disposition,
                new ReadOnlyDictionary<string, TrustedToolCommand>(commands));
            return TrustedToolVerificationResult.Verified(tool);
        }
    }

    private static TrustedToolVerificationResult Reject(TrustedToolVerificationFailure failure) =>
        TrustedToolVerificationResult.Rejected(failure);

    private static bool TryCanonicalizeExistingDirectory(string? path, out string fullPath)
    {
        if (!TryCanonicalize(path, out fullPath))
        {
            return false;
        }

        return Directory.Exists(fullPath);
    }

    private static bool TryCanonicalize(string? path, out string fullPath)
    {
        fullPath = string.Empty;
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        try
        {
            fullPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
            return true;
        }
        catch (Exception error) when (error is ArgumentException or IOException or NotSupportedException)
        {
            return false;
        }
    }

    private static bool TryCaptureFlatInventory(string packageRoot, out string[] inventory)
    {
        inventory = [];
        try
        {
            string[] entries = Directory.GetFileSystemEntries(
                packageRoot,
                "*",
                SearchOption.TopDirectoryOnly);
            if (entries.Any(Directory.Exists))
            {
                return true;
            }

            inventory = entries.Select(entry => Path.GetFileName(entry)!).ToArray();
            return true;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static bool InventoryMatches(
        string[] actual,
        IReadOnlyCollection<string> required)
    {
        if (actual.Length != required.Count ||
            actual.Distinct(StringComparer.OrdinalIgnoreCase).Count() != actual.Length)
        {
            return false;
        }

        return new HashSet<string>(actual, StringComparer.OrdinalIgnoreCase)
            .SetEquals(required);
    }

    private static bool HasReparsePointInPath(string path)
    {
        DirectoryInfo? current = new(path);
        while (current is not null)
        {
            if (IsReparsePoint(current.FullName))
            {
                return true;
            }

            current = current.Parent;
        }

        return false;
    }

    private static bool HasReparsePointFromApprovedRoot(string approvedRoot, string packageRoot)
    {
        if (IsReparsePoint(approvedRoot))
        {
            return true;
        }

        string relative = Path.GetRelativePath(approvedRoot, packageRoot);
        string current = approvedRoot;
        foreach (string segment in relative.Split(
                     [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                     StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, segment);
            if (IsReparsePoint(current))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsReparsePoint(string path)
    {
        try
        {
            return (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static void DisposeStreams(IEnumerable<FileStream> streams)
    {
        foreach (FileStream stream in streams)
        {
            stream.Dispose();
        }
    }

    private sealed class StreamCollectionLease(IEnumerable<FileStream> streams) : IDisposable
    {
        public void Dispose() => DisposeStreams(streams);
    }
}
