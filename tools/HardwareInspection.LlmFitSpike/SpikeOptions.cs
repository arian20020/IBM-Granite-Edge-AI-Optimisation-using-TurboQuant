using System.Globalization;

namespace HardwareInspection.LlmFitSpike;

public sealed record SpikeOptions
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(15);

    private SpikeOptions(string candidateRoot, string outputDirectory, TimeSpan timeout)
    {
        CandidateRoot = candidateRoot;
        OutputDirectory = outputDirectory;
        Timeout = timeout;
    }

    public string CandidateRoot { get; }

    public string OutputDirectory { get; }

    public TimeSpan Timeout { get; }

    public static SpikeOptions Parse(string[] arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        string? candidateRootValue = null;
        string? outputValue = null;
        int? timeoutSeconds = null;
        var seen = new HashSet<string>(StringComparer.Ordinal);

        for (int index = 0; index < arguments.Length; index += 2)
        {
            if (index + 1 >= arguments.Length)
            {
                throw InvalidCommandLine();
            }

            string option = arguments[index];
            string value = arguments[index + 1];
            if (!seen.Add(option) || string.IsNullOrWhiteSpace(value))
            {
                throw InvalidCommandLine();
            }

            switch (option)
            {
                case "--candidate-root":
                    candidateRootValue = value;
                    break;
                case "--output":
                    outputValue = value;
                    break;
                case "--timeout-seconds":
                    if (!int.TryParse(
                            value,
                            NumberStyles.None,
                            CultureInfo.InvariantCulture,
                            out int parsedTimeout) ||
                        parsedTimeout is < 1 or > 120)
                    {
                        throw InvalidCommandLine();
                    }

                    timeoutSeconds = parsedTimeout;
                    break;
                default:
                    throw InvalidCommandLine();
            }
        }

        if (candidateRootValue is null || outputValue is null)
        {
            throw InvalidCommandLine();
        }

        try
        {
            EnsureLocalPath(candidateRootValue);
            EnsureLocalPath(outputValue);
            string candidateRoot = CanonicalizeDirectory(candidateRootValue);
            string outputDirectory = CanonicalizeDirectory(outputValue);
            EnsureLocalPath(candidateRoot);
            EnsureLocalPath(outputDirectory);
            EnsureExistingComponentsAreOrdinary(candidateRoot);
            EnsureExistingComponentsAreOrdinary(outputDirectory);
            EnsureExistingOrdinaryDirectory(candidateRoot);
            EnsureOutputIsAvailable(outputDirectory);

            if (IsSameOrDescendant(candidateRoot, outputDirectory) ||
                IsSameOrDescendant(outputDirectory, candidateRoot))
            {
                throw InvalidCommandLine();
            }

            EnsurePhysicallyDisjoint(candidateRoot, outputDirectory);

            return new SpikeOptions(
                candidateRoot,
                outputDirectory,
                TimeSpan.FromSeconds(timeoutSeconds ?? (int)DefaultTimeout.TotalSeconds));
        }
        catch (ArgumentException)
        {
            throw InvalidCommandLine();
        }
        catch (Exception exception) when (
            exception is IOException or NotSupportedException or UnauthorizedAccessException)
        {
            throw InvalidCommandLine();
        }
    }

    public void CreateOutputDirectory()
    {
        EnsureLocalPath(CandidateRoot);
        EnsureLocalPath(OutputDirectory);
        EnsureExistingComponentsAreOrdinary(CandidateRoot);
        EnsureExistingComponentsAreOrdinary(OutputDirectory);
        EnsureExistingOrdinaryDirectory(CandidateRoot);
        EnsureOutputIsAvailable(OutputDirectory);
        if (IsSameOrDescendant(CandidateRoot, OutputDirectory) ||
            IsSameOrDescendant(OutputDirectory, CandidateRoot))
        {
            throw InvalidCommandLine();
        }

        EnsurePhysicallyDisjoint(CandidateRoot, OutputDirectory);
        Directory.CreateDirectory(OutputDirectory);
        EnsureExistingComponentsAreOrdinary(OutputDirectory);
        EnsureExistingOrdinaryDirectory(OutputDirectory);
    }

    private static string CanonicalizeDirectory(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw InvalidCommandLine();
        }

        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(value));
    }

    private static void EnsureLocalPath(string path)
    {
        if (path.StartsWith(@"\\", StringComparison.Ordinal) ||
            path.StartsWith(@"\\?\", StringComparison.Ordinal) ||
            path.StartsWith(@"\\.\", StringComparison.Ordinal))
        {
            throw InvalidCommandLine();
        }
    }

    private static void EnsurePhysicallyDisjoint(
        string candidateRoot,
        string outputDirectory)
    {
        StableDirectoryIdentity candidateIdentity = StableDirectoryPath.Resolve(candidateRoot);
        string existingOutputAncestor = FindNearestExistingDirectory(outputDirectory);
        StableDirectoryIdentity outputAncestorIdentity =
            StableDirectoryPath.Resolve(existingOutputAncestor);
        EnsureLocalPath(candidateIdentity.FinalPath);
        EnsureLocalPath(outputAncestorIdentity.FinalPath);

        string relativeOutput = Path.GetRelativePath(
            existingOutputAncestor,
            outputDirectory);
        if (Path.IsPathRooted(relativeOutput))
        {
            throw InvalidCommandLine();
        }

        string stableOutputPath = string.Equals(
                relativeOutput,
                ".",
                StringComparison.OrdinalIgnoreCase)
            ? outputAncestorIdentity.FinalPath
            : CanonicalizeDirectory(Path.Combine(
                outputAncestorIdentity.FinalPath,
                relativeOutput));
        EnsureLocalPath(stableOutputPath);
        if (candidateIdentity.IsSameDirectory(outputAncestorIdentity) ||
            IsSameOrDescendant(candidateIdentity.FinalPath, stableOutputPath) ||
            IsSameOrDescendant(stableOutputPath, candidateIdentity.FinalPath))
        {
            throw InvalidCommandLine();
        }
    }

    private static string FindNearestExistingDirectory(string path)
    {
        string current = path;
        while (!Directory.Exists(current))
        {
            if (File.Exists(current))
            {
                throw InvalidCommandLine();
            }

            string? parent = Directory.GetParent(current)?.FullName;
            if (string.IsNullOrWhiteSpace(parent) ||
                string.Equals(parent, current, StringComparison.OrdinalIgnoreCase))
            {
                throw InvalidCommandLine();
            }

            current = parent;
        }

        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(current));
    }

    private static void EnsureOutputIsAvailable(string path)
    {
        if (File.Exists(path))
        {
            throw InvalidCommandLine();
        }

        if (Directory.Exists(path))
        {
            EnsureExistingOrdinaryDirectory(path);
        }
    }

    private static void EnsureExistingOrdinaryDirectory(string path)
    {
        FileAttributes attributes = File.GetAttributes(path);
        if ((attributes & FileAttributes.Directory) == 0 ||
            (attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw InvalidCommandLine();
        }
    }

    private static void EnsureExistingComponentsAreOrdinary(string path)
    {
        string fullPath = Path.GetFullPath(path);
        string? root = Path.GetPathRoot(fullPath);
        if (string.IsNullOrWhiteSpace(root))
        {
            throw InvalidCommandLine();
        }

        string current = Path.TrimEndingDirectorySeparator(root);
        foreach (string segment in fullPath[root.Length..].Split(
                     [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                     StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, segment);
            FileAttributes attributes;
            try
            {
                attributes = File.GetAttributes(current);
            }
            catch (FileNotFoundException)
            {
                return;
            }
            catch (DirectoryNotFoundException)
            {
                return;
            }

            if ((attributes & FileAttributes.ReparsePoint) != 0 ||
                (attributes & FileAttributes.Directory) == 0)
            {
                throw InvalidCommandLine();
            }
        }
    }

    private static bool IsSameOrDescendant(string parent, string candidate)
    {
        string relative = Path.GetRelativePath(parent, candidate);
        return string.Equals(relative, ".", StringComparison.OrdinalIgnoreCase) ||
            !Path.IsPathRooted(relative) &&
            !string.Equals(relative, "..", StringComparison.Ordinal) &&
            !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal) &&
            !relative.StartsWith(".." + Path.AltDirectorySeparatorChar, StringComparison.Ordinal);
    }

    private static ArgumentException InvalidCommandLine()
    {
        return new ArgumentException("The command line is invalid.");
    }
}
