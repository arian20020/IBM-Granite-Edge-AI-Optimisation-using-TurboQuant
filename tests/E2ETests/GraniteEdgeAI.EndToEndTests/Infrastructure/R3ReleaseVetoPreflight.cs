using System.Security.Cryptography;

namespace GraniteEdgeAI.EndToEndTests.Infrastructure;

internal static class R3ReleaseVetoPreflight
{
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
}
