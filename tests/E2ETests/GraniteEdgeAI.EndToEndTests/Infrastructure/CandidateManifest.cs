using System.Security.Cryptography;
using System.Text.Json;

namespace GraniteEdgeAI.EndToEndTests.Infrastructure;

internal sealed record CandidateManifest(string SourceCommit, string SourceTree, string PackageFamilyName, string ApplicationId, string ExecutablePath, string ExecutableSha256, long ExecutableBytes)
{
    internal string Aumid => $"{PackageFamilyName}!{ApplicationId}";

    internal static CandidateManifest Load(
        string path,
        string expectedSourceCommit,
        string expectedSourceTree)
    {
        JsonContract.RequireGitObject(expectedSourceCommit, nameof(expectedSourceCommit));
        JsonContract.RequireGitObject(expectedSourceTree, nameof(expectedSourceTree));
        using JsonDocument document = JsonContract.Open(path);
        JsonElement root = document.RootElement;
        JsonContract.RequireOnly(root, "schemaVersion", "sourceCommit", "sourceTree", "packageFamilyName", "applicationId", "executablePath", "executableSha256", "executableBytes");
        if (JsonContract.RequiredInt64(root, "schemaVersion") != 1)
        {
            throw new InvalidDataException("Unsupported candidate manifest schemaVersion.");
        }

        string commit = JsonContract.RequiredString(root, "sourceCommit");
        string tree = JsonContract.RequiredString(root, "sourceTree");
        JsonContract.RequireGitObject(commit, "sourceCommit");
        JsonContract.RequireGitObject(tree, "sourceTree");
        if (!string.Equals(commit, expectedSourceCommit, StringComparison.Ordinal)
            || !string.Equals(tree, expectedSourceTree, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Candidate source commit/tree does not match the exact integrated candidate identity.");
        }

        string packageFamilyName = RequireIdentifier(
            JsonContract.RequiredString(root, "packageFamilyName"),
            "packageFamilyName",
            minimumLength: 3);
        string applicationId = RequireIdentifier(
            JsonContract.RequiredString(root, "applicationId"),
            "applicationId",
            minimumLength: 1);
        string executableValue = JsonContract.RequiredString(root, "executablePath");
        if (!Path.IsPathFullyQualified(executableValue))
        {
            throw new InvalidDataException(
                "Candidate executablePath must be fully qualified.");
        }
        string executablePath = Path.GetFullPath(executableValue);
        string expectedHash = JsonContract.RequiredString(root, "executableSha256");
        JsonContract.RequireSha256(expectedHash, "executableSha256");
        long expectedBytes = JsonContract.RequiredInt64(root, "executableBytes");
        FileInfo file = new(executablePath);
        if (!file.Exists
            || expectedBytes <= 0
            || file.Length != expectedBytes
            || (file.Attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidDataException("Candidate executable byte length does not match its manifest.");
        }

        string actualHash;
        using (FileStream stream = file.OpenRead())
        {
            actualHash = Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
            if (stream.Length != expectedBytes)
            {
                throw new InvalidDataException(
                    "Candidate executable changed while its manifest was verified.");
            }
        }

        if (!CryptographicOperations.FixedTimeEquals(Convert.FromHexString(expectedHash), Convert.FromHexString(actualHash)))
        {
            throw new InvalidDataException("Candidate executable SHA-256 does not match its manifest.");
        }

        return new CandidateManifest(
            commit,
            tree,
            packageFamilyName,
            applicationId,
            executablePath,
            expectedHash,
            expectedBytes);
    }

    private static string RequireIdentifier(
        string value,
        string name,
        int minimumLength)
    {
        if (value.Length < minimumLength
            || value.Length > 255
            || value.Any(character => !char.IsAsciiLetterOrDigit(character)
                                      && character is not ('.' or '_' or '-')))
        {
            throw new InvalidDataException(
                $"Candidate {name} contains unsupported characters or length.");
        }

        return value;
    }
}
