using HardwareInspection.LlmFitSpike.Candidate;

namespace HardwareInspection.LlmFitSpike.Command;

public static class LlmFitCommandBuilder
{
    private static readonly string[] SystemArguments = ["--no-dashboard", "--json", "system"];
    private static readonly string[] VersionArguments = ["--version"];

    public static LlmFitCommand BuildSystem(string candidateRoot, LlmFitCandidateManifest manifest)
    {
        return Build(candidateRoot, manifest, manifest?.Commands.System, SystemArguments);
    }

    public static LlmFitCommand BuildVersion(string candidateRoot, LlmFitCandidateManifest manifest)
    {
        return Build(candidateRoot, manifest, manifest?.Commands.Version, VersionArguments);
    }

    private static LlmFitCommand Build(
        string candidateRoot,
        LlmFitCandidateManifest manifest,
        IReadOnlyList<string>? manifestArguments,
        IReadOnlyList<string> approvedArguments)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(candidateRoot);
        ArgumentNullException.ThrowIfNull(manifest);

        if (string.IsNullOrWhiteSpace(manifest.Executable.RelativePath))
        {
            throw new InvalidDataException("The executable path must be a non-empty relative path.");
        }

        if (Path.IsPathRooted(manifest.Executable.RelativePath) || HasParentTraversal(manifest.Executable.RelativePath))
        {
            throw new InvalidDataException("The executable path must remain inside the candidate root.");
        }

        if (manifestArguments is null || !manifestArguments.SequenceEqual(approvedArguments, StringComparer.Ordinal))
        {
            throw new InvalidDataException("The command arguments are not an approved read-only invocation.");
        }

        string canonicalRoot = Path.GetFullPath(candidateRoot);
        string executablePath = Path.GetFullPath(Path.Combine(canonicalRoot, manifest.Executable.RelativePath));
        EnsureContained(canonicalRoot, executablePath);

        return new LlmFitCommand(executablePath, canonicalRoot, manifestArguments.ToArray());
    }

    private static void EnsureContained(string candidateRoot, string executablePath)
    {
        string relativePath = Path.GetRelativePath(candidateRoot, executablePath);
        if (string.IsNullOrWhiteSpace(relativePath) ||
            string.Equals(relativePath, ".", StringComparison.Ordinal) ||
            Path.IsPathRooted(relativePath) ||
            HasParentTraversal(relativePath))
        {
            throw new InvalidDataException("The executable path must remain inside the candidate root.");
        }
    }

    private static bool HasParentTraversal(string path)
    {
        return path.Split(['/', '\\'], StringSplitOptions.None)
            .Any(segment => string.Equals(segment, "..", StringComparison.Ordinal));
    }
}
