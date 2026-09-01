using System.Security.Cryptography;
using System.Text.Json;

namespace GraniteEdgeAI.EndToEndTests.Infrastructure;

internal static class R3ReleaseVetoPreflight
{
    internal static R4TwoPhaseIssueEvidence VerifyTwoPhase(
        string repositoryRoot,
        string closureManifestPath,
        string closureManifestRelativePath,
        string candidateCommit,
        string candidateTree,
        string candidateRemote,
        string candidateRemoteRef,
        CancellationToken cancellationToken = default)
    {
        FileInfo closure = RequireRegularFile(closureManifestPath, "R4 closure manifest");
        GitEvidenceVerifier.VerifyBlob(
            repositoryRoot,
            candidateCommit,
            candidateTree,
            closureManifestRelativePath,
            Sha256(closure),
            closure.Length,
            cancellationToken);
        GitEvidenceVerifier.VerifyPushedRef(
            repositoryRoot,
            candidateRemote,
            candidateRemoteRef,
            candidateCommit,
            cancellationToken);

        using JsonDocument document = JsonContract.Open(closure.FullName);
        JsonElement binding = document.RootElement.GetProperty("evidenceCatalog");
        string subjectCommit = JsonContract.RequiredString(binding, "evidenceSubjectCommit");
        string subjectTree = JsonContract.RequiredString(binding, "evidenceSubjectTree");
        JsonElement blob = binding.GetProperty("evidenceBlob");
        string relativePath = JsonContract.RequiredString(blob, "path");
        string expectedSha = JsonContract.RequiredString(blob, "sha256");
        long expectedBytes = JsonContract.RequiredInt64(blob, "bytes");
        JsonContract.RequireGitObject(subjectCommit, "evidenceSubjectCommit");
        JsonContract.RequireGitObject(subjectTree, "evidenceSubjectTree");
        JsonContract.RequireSha256(expectedSha, "sha256");
        if (Path.IsPathFullyQualified(relativePath) || relativePath.Contains('\\')
            || relativePath.Split('/').Any(segment => segment is "" or "." or ".."))
        {
            throw new InvalidDataException("R4 catalog path is not repository-relative.");
        }
        string root = Path.GetFullPath(repositoryRoot).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        string catalogPath = Path.GetFullPath(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        if (!catalogPath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("R4 catalog path escapes the repository.");
        }
        FileInfo catalog = RequireRegularFile(catalogPath, "R4 evidence catalog");
        if (catalog.Length != expectedBytes || Sha256(catalog) != expectedSha)
        {
            throw new InvalidDataException("R4 evidence catalog hash or byte count differs from closure binding.");
        }
        GitEvidenceVerifier.VerifyBlob(
            repositoryRoot,
            subjectCommit,
            subjectTree,
            relativePath,
            expectedSha,
            expectedBytes,
            cancellationToken);
        return R3IssueEvidenceVerifier.VerifyTwoPhase(closure.FullName, catalog.FullName, repositoryRoot);
    }

    internal static IReadOnlyList<R3IssueEvidence> Verify(
        string repositoryRoot,
        string closureManifestPath,
        string closureManifestRelativePath,
        string candidateCommit,
        string candidateTree,
        string candidateRemote,
        string candidateRemoteRef,
        CancellationToken cancellationToken = default)
    {
        FileInfo manifest = new(Path.GetFullPath(closureManifestPath));
        if (!manifest.Exists
            || manifest.Length <= 0
            || manifest.Length > 1024 * 1024
            || (manifest.Attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidDataException("R3 closure manifest violates its closed regular-file bound.");
        }

        string manifestSha;
        using (FileStream stream = manifest.OpenRead())
        {
            manifestSha = Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
        }
        GitEvidenceVerifier.VerifyBlob(
            repositoryRoot,
            candidateCommit,
            candidateTree,
            closureManifestRelativePath,
            manifestSha,
            manifest.Length,
            cancellationToken);
        GitEvidenceVerifier.VerifyPushedRef(
            repositoryRoot,
            candidateRemote,
            candidateRemoteRef,
            candidateCommit,
            cancellationToken);

        IReadOnlyList<R3IssueEvidence> issues = R3IssueEvidenceVerifier.Verify(manifest.FullName);
        foreach (R3IssueEvidence issue in issues.DistinctBy(issue => new
                 {
                     issue.EvidenceSubjectCommit,
                     issue.EvidenceSubjectTree,
                     issue.EvidenceBlob.Path,
                     issue.EvidenceBlob.Sha256,
                     issue.EvidenceBlob.Bytes,
                 }))
        {
            GitEvidenceVerifier.VerifyBlob(
                repositoryRoot,
                issue.EvidenceSubjectCommit,
                issue.EvidenceSubjectTree,
                issue.EvidenceBlob.Path,
                issue.EvidenceBlob.Sha256,
                issue.EvidenceBlob.Bytes,
                cancellationToken);
        }
        return issues;
    }

    private static FileInfo RequireRegularFile(string path, string description)
    {
        FileInfo file = new(Path.GetFullPath(path));
        if (!file.Exists || file.Length <= 0 || file.Length > 1024 * 1024
            || (file.Attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidDataException($"{description} violates its closed regular-file bound.");
        }
        return file;
    }

    private static string Sha256(FileInfo file)
    {
        using FileStream stream = file.OpenRead();
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }
}
