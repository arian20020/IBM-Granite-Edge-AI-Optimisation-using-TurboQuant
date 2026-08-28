using System.Security.Cryptography;
using System.Text.Json;

namespace GraniteEdgeAI.EndToEndTests.Infrastructure;

internal sealed record CandidateManifest(string SourceCommit, string SourceTree, string PackageFamilyName, string ApplicationId, string ExecutablePath, string ExecutableSha256, long ExecutableBytes)
{
    internal string Aumid => $"{PackageFamilyName}!{ApplicationId}";

    internal static CandidateManifest Load(string path)
    {
        using JsonDocument document = JsonContract.Open(path);
        JsonElement root = document.RootElement;
        JsonContract.RequireOnly(root, "schemaVersion", "sourceCommit", "sourceTree", "packageFamilyName", "applicationId", "executablePath", "executableSha256", "executableBytes");
        if (JsonContract.RequiredInt64(root, "schemaVersion") != 1)
        {
            throw new InvalidDataException("Unsupported candidate manifest schemaVersion.");
        }

        string commit = JsonContract.RequiredString(root, "sourceCommit");
        string tree = JsonContract.RequiredString(root, "sourceTree");
        if (commit != AuditIdentity.FrozenCommit || tree != AuditIdentity.FrozenTree)
        {
            throw new InvalidDataException("Candidate source commit/tree does not match the frozen audit identity.");
        }

        string executablePath = Path.GetFullPath(JsonContract.RequiredString(root, "executablePath"));
        string expectedHash = JsonContract.RequiredString(root, "executableSha256");
        JsonContract.RequireSha256(expectedHash, "executableSha256");
        long expectedBytes = JsonContract.RequiredInt64(root, "executableBytes");
        FileInfo file = new(executablePath);
        if (!file.Exists || file.Length != expectedBytes)
        {
            throw new InvalidDataException("Candidate executable byte length does not match its manifest.");
        }

        string actualHash;
        using (FileStream stream = file.OpenRead())
        {
            actualHash = Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
        }

        if (!CryptographicOperations.FixedTimeEquals(Convert.FromHexString(expectedHash), Convert.FromHexString(actualHash)))
        {
            throw new InvalidDataException("Candidate executable SHA-256 does not match its manifest.");
        }

        return new CandidateManifest(commit, tree, JsonContract.RequiredString(root, "packageFamilyName"), JsonContract.RequiredString(root, "applicationId"), executablePath, expectedHash, expectedBytes);
    }
}
