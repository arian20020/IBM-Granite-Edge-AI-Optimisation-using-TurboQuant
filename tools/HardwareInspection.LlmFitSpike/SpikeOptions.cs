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
            string candidateRoot = CanonicalizeDirectory(candidateRootValue);
            string outputDirectory = CanonicalizeDirectory(outputValue);
            EnsureLocalOutputPath(outputDirectory);
            EnsureExistingComponentsAreOrdinary(candidateRoot);
            EnsureExistingComponentsAreOrdinary(outputDirectory);
            EnsureExistingOrdinaryDirectory(candidateRoot);
            EnsureOutputIsAvailable(outputDirectory);

            if (IsSameOrDescendant(candidateRoot, outputDirectory) ||
                IsSameOrDescendant(outputDirectory, candidateRoot))
            {
                throw InvalidCommandLine();
            }

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
        EnsureLocalOutputPath(OutputDirectory);
        EnsureExistingComponentsAreOrdinary(OutputDirectory);
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

    private static void EnsureLocalOutputPath(string path)
    {
        if (path.StartsWith(@"\\", StringComparison.Ordinal) ||
            path.StartsWith(@"\\?\", StringComparison.Ordinal) ||
            path.StartsWith(@"\\.\", StringComparison.Ordinal))
        {
            throw InvalidCommandLine();
        }
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
