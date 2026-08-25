using System.Collections.ObjectModel;
using System.Security.Cryptography;

namespace GraniteEdgeAI.HardwareInspection.Foundation.TrustedTools;

public sealed class TrustedToolPackageVerifier
{
    private readonly Action? _afterDirectoryInspection;
    private readonly Action? _afterInitialInventory;

    public TrustedToolPackageVerifier()
    {
    }

    internal TrustedToolPackageVerifier(Action afterInitialInventory)
    {
        _afterInitialInventory = afterInitialInventory ??
            throw new ArgumentNullException(nameof(afterInitialInventory));
    }

    internal TrustedToolPackageVerifier(
        Action afterDirectoryInspection,
        Action? afterInitialInventory)
    {
        _afterDirectoryInspection = afterDirectoryInspection ??
            throw new ArgumentNullException(nameof(afterDirectoryInspection));
        _afterInitialInventory = afterInitialInventory;
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

        ReparseInspection directoryInspection = InspectDirectoryPath(
            approvedFullPath,
            packageFullPath);
        if (directoryInspection == ReparseInspection.Unavailable)
        {
            return Reject(TrustedToolVerificationFailure.FileUnavailable);
        }

        if (directoryInspection == ReparseInspection.ReparsePoint)
        {
            return Reject(TrustedToolVerificationFailure.ReparsePoint);
        }

        _afterDirectoryInspection?.Invoke();

        if (!TrustedToolCustody.TryAcquireDirectories(
                approvedFullPath,
                packageFullPath,
                out IReadOnlyList<Microsoft.Win32.SafeHandles.SafeFileHandle> directoryHandles,
                out CustodyOpenFailure directoryOpenFailure))
        {
            return Reject(directoryOpenFailure == CustodyOpenFailure.ReparsePoint
                ? TrustedToolVerificationFailure.ReparsePoint
                : TrustedToolVerificationFailure.FileUnavailable);
        }

        if (!TryCaptureFlatInventory(packageFullPath, out string[] initialInventory))
        {
            DisposeResources(directoryHandles);
            return Reject(TrustedToolVerificationFailure.FileUnavailable);
        }

        if (!InventoryMatches(initialInventory, manifest.RequiredMembers))
        {
            DisposeResources(directoryHandles);
            return Reject(TrustedToolVerificationFailure.InventoryMismatch);
        }

        Dictionary<string, string> actualMembers = initialInventory.ToDictionary(
            member => member,
            member => member,
            StringComparer.OrdinalIgnoreCase);
        if (actualMembers.Count != initialInventory.Length)
        {
            DisposeResources(directoryHandles);
            return Reject(TrustedToolVerificationFailure.InventoryMismatch);
        }

        foreach (string member in initialInventory)
        {
            string memberPath = Path.Combine(packageFullPath, member);
            ReparseInspection memberInspection = InspectReparsePoint(memberPath);
            if (memberInspection == ReparseInspection.Unavailable)
            {
                DisposeResources(directoryHandles);
                return Reject(TrustedToolVerificationFailure.FileUnavailable);
            }

            if (memberInspection == ReparseInspection.ReparsePoint)
            {
                DisposeResources(directoryHandles);
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
                if (!TrustedToolCustody.TryAcquireFile(
                        memberPath,
                        out FileStream? stream,
                        out _))
                {
                    DisposeResources(streams.Values);
                    DisposeResources(directoryHandles);
                    return Reject(TrustedToolVerificationFailure.PackageChangedDuringVerification);
                }

                streams.Add(requiredMember, stream!);
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            DisposeResources(streams.Values);
            DisposeResources(directoryHandles);
            return Reject(TrustedToolVerificationFailure.PackageChangedDuringVerification);
        }

        FileStream executable = streams[manifest.ExecutableRelativePath];
        byte[] actualHash;
        try
        {
            executable.Position = 0;
            actualHash = SHA256.HashData(executable);
        }
        catch (IOException)
        {
            DisposeResources(streams.Values);
            DisposeResources(directoryHandles);
            return Reject(TrustedToolVerificationFailure.FileUnavailable);
        }

        byte[] requiredHash = Convert.FromHexString(manifest.ExecutableSha256);
        if (!CryptographicOperations.FixedTimeEquals(actualHash, requiredHash))
        {
            DisposeResources(streams.Values);
            DisposeResources(directoryHandles);
            return Reject(TrustedToolVerificationFailure.HashMismatch);
        }

        TrustedToolVerificationFailure? peFailure =
            PeImageInspector.Inspect(executable, manifest.RequiredMachine);
        if (peFailure is not null)
        {
            DisposeResources(streams.Values);
            DisposeResources(directoryHandles);
            return Reject(peFailure.Value);
        }

        if (!TryCaptureFlatInventory(packageFullPath, out string[] finalInventory) ||
            !initialInventory.Order(StringComparer.Ordinal)
                .SequenceEqual(finalInventory.Order(StringComparer.Ordinal), StringComparer.Ordinal))
        {
            DisposeResources(streams.Values);
            DisposeResources(directoryHandles);
            return Reject(TrustedToolVerificationFailure.PackageChangedDuringVerification);
        }

        Dictionary<string, TrustedToolCommand> commands = manifest.Commands.ToDictionary(
            command => command.Identity,
            command => command,
            StringComparer.OrdinalIgnoreCase);
        TrustedToolCustody custody = new(
            directoryHandles.Cast<IDisposable>().Concat(streams.Values));
        VerifiedTrustedTool tool = new(
            manifest.ToolId,
            manifest.Version,
            packageFullPath,
            Path.Combine(packageFullPath, actualMembers[manifest.ExecutableRelativePath]),
            manifest.Disposition,
            new ReadOnlyDictionary<string, TrustedToolCommand>(commands),
            custody);
        return TrustedToolVerificationResult.Verified(tool);
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

    private static ReparseInspection InspectDirectoryPath(string approvedRoot, string packageRoot)
    {
        DirectoryInfo? current = new(approvedRoot);
        while (current is not null)
        {
            ReparseInspection inspection = InspectReparsePoint(current.FullName);
            if (inspection != ReparseInspection.Clear)
            {
                return inspection;
            }

            current = current.Parent;
        }

        string relative = Path.GetRelativePath(approvedRoot, packageRoot);
        string currentPath = approvedRoot;
        foreach (string segment in relative.Split(
                     [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                     StringSplitOptions.RemoveEmptyEntries))
        {
            currentPath = Path.Combine(currentPath, segment);
            ReparseInspection inspection = InspectReparsePoint(currentPath);
            if (inspection != ReparseInspection.Clear)
            {
                return inspection;
            }
        }

        return ReparseInspection.Clear;
    }

    private static ReparseInspection InspectReparsePoint(string path)
    {
        try
        {
            return (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0
                ? ReparseInspection.ReparsePoint
                : ReparseInspection.Clear;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return ReparseInspection.Unavailable;
        }
    }

    private static void DisposeResources(IEnumerable<IDisposable> resources)
    {
        foreach (IDisposable resource in resources)
        {
            resource.Dispose();
        }
    }

    private enum ReparseInspection
    {
        Clear,
        ReparsePoint,
        Unavailable,
    }
}
