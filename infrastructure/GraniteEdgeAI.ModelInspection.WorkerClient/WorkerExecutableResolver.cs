using Microsoft.Win32.SafeHandles;

namespace GraniteEdgeAI.ModelInspection.WorkerClient;

/// <summary>
/// Resolves the one fixed production worker beneath a controlled installation
/// root and rejects any path, reparse-point, final-path or architecture drift.
/// </summary>
public sealed class WorkerExecutableResolver
{
    private const ushort Amd64Machine = 0x8664;
    private const string UntrustedMessage =
        "The Model Inspection worker executable could not be trusted.";
    private const string UnsupportedArchitectureMessage =
        "The Model Inspection worker architecture is not supported.";

    private readonly string _fixedRelativePath;
    private readonly IWorkerExecutableFileSystem _fileSystem;

    public WorkerExecutableResolver(string fixedRelativePath)
        : this(fixedRelativePath, new WindowsWorkerExecutableFileSystem())
    {
    }

    internal WorkerExecutableResolver(
        string fixedRelativePath,
        IWorkerExecutableFileSystem fileSystem)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fixedRelativePath);
        ArgumentNullException.ThrowIfNull(fileSystem);

        if (Path.IsPathRooted(fixedRelativePath) ||
            fixedRelativePath.Contains('\0'))
        {
            throw new ArgumentException(
                "The worker path must be a safe relative path.",
                nameof(fixedRelativePath));
        }

        _fixedRelativePath = fixedRelativePath;
        _fileSystem = fileSystem;
    }

    public VerifiedWorkerExecutable Resolve(string approvedRoot)
    {
        try
        {
            string root = RequireAbsoluteCanonicalRoot(approvedRoot);
            string candidate = Path.GetFullPath(
                Path.Combine(root, _fixedRelativePath));

            RequireContained(root, candidate);
            RequireExistingRoot(root);
            RequireNoReparsePoint(root, candidate);
            RequireExistingRegularFile(candidate);
            RequireAmd64(candidate);

            SafeFileHandle handle = _fileSystem.OpenReadHandle(candidate);
            try
            {
                string finalRoot = NormalizeFinalPath(
                    _fileSystem.GetFinalDirectoryPath(root));
                string finalExecutable = NormalizeFinalPath(
                    _fileSystem.GetFinalFilePath(handle));
                RequireContained(finalRoot, finalExecutable);

                return new VerifiedWorkerExecutable(
                    finalRoot,
                    finalExecutable,
                    handle);
            }
            catch
            {
                handle.Dispose();
                throw;
            }
        }
        catch (WorkerClientPolicyException)
        {
            throw;
        }
        catch (Exception error) when (
            error is ArgumentException or
            IOException or
            UnauthorizedAccessException or
            NotSupportedException)
        {
            // Expected filesystem failures can contain absolute paths in their
            // message. Replace them with the stable privacy-safe policy error.
            throw Untrusted();
        }
    }

    private static string RequireAbsoluteCanonicalRoot(string approvedRoot)
    {
        if (string.IsNullOrWhiteSpace(approvedRoot) ||
            approvedRoot.Contains('\0') ||
            !Path.IsPathFullyQualified(approvedRoot))
        {
            throw Untrusted();
        }

        return Path.TrimEndingDirectorySeparator(
            Path.GetFullPath(approvedRoot));
    }

    private void RequireExistingRoot(string root)
    {
        if (!_fileSystem.DirectoryExists(root))
        {
            throw Untrusted();
        }
    }

    private void RequireNoReparsePoint(string root, string candidate)
    {
        string? volumeRoot = Path.GetPathRoot(root);
        if (string.IsNullOrEmpty(volumeRoot))
        {
            throw Untrusted();
        }

        CheckPathComponents(volumeRoot, root);
        CheckPathComponents(root, candidate);
    }

    private void CheckPathComponents(string basePath, string fullPath)
    {
        string relative = Path.GetRelativePath(basePath, fullPath);
        string current = Path.TrimEndingDirectorySeparator(basePath);

        if (relative == ".")
        {
            CheckNotReparse(current);
            return;
        }

        foreach (string segment in relative.Split(
                     [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                     StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, segment);
            CheckNotReparse(current);
        }
    }

    private void CheckNotReparse(string path)
    {
        FileAttributes attributes = _fileSystem.GetAttributes(path);
        if ((attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw Untrusted();
        }
    }

    private void RequireExistingRegularFile(string candidate)
    {
        if (!_fileSystem.FileExists(candidate))
        {
            throw Untrusted();
        }

        FileAttributes attributes = _fileSystem.GetAttributes(candidate);
        if ((attributes & FileAttributes.Directory) != 0)
        {
            throw Untrusted();
        }
    }

    private void RequireAmd64(string candidate)
    {
        ushort machine;
        try
        {
            machine = _fileSystem.ReadPortableExecutableMachine(candidate);
        }
        catch (Exception error) when (
            error is IOException or
            UnauthorizedAccessException or
            InvalidDataException or
            ArgumentException)
        {
            throw Untrusted();
        }

        if (machine != Amd64Machine)
        {
            throw new WorkerClientPolicyException(
                new WorkerClientFailure(
                    WorkerClientFailureCodes.WorkerArchitectureUnsupported,
                    UnsupportedArchitectureMessage));
        }
    }

    private static void RequireContained(string root, string candidate)
    {
        string normalizedRoot = Path.TrimEndingDirectorySeparator(root);
        string normalizedCandidate = Path.TrimEndingDirectorySeparator(candidate);

        if (string.Equals(
                normalizedRoot,
                normalizedCandidate,
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        string qualifiedRoot = normalizedRoot + Path.DirectorySeparatorChar;
        if (!normalizedCandidate.StartsWith(
                qualifiedRoot,
                StringComparison.OrdinalIgnoreCase))
        {
            throw Untrusted();
        }
    }

    private static string NormalizeFinalPath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        const string extendedUncPrefix = @"\\?\UNC\";
        const string extendedPrefix = @"\\?\";

        string normalized = path.StartsWith(
            extendedUncPrefix,
            StringComparison.OrdinalIgnoreCase)
                ? @"\\" + path[extendedUncPrefix.Length..]
                : path.StartsWith(
                    extendedPrefix,
                    StringComparison.OrdinalIgnoreCase)
                    ? path[extendedPrefix.Length..]
                    : path;

        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(normalized));
    }

    private static WorkerClientPolicyException Untrusted() =>
        WorkerClientPolicyException.For(
            WorkerClientFailureCodes.WorkerExecutableUntrusted,
            UntrustedMessage);
}
