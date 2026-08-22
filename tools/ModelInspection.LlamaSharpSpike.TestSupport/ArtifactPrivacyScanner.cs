namespace GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike.TestSupport;

/// <summary>
/// Describes one file that must not be uploaded as runtime-test evidence.
/// </summary>
public sealed record ArtifactPrivacyFinding(
    string RelativePath,
    string Reason);

/// <summary>
/// Contains the complete artifact-privacy scan result.
/// </summary>
public sealed record ArtifactPrivacyScanResult(
    IReadOnlyList<ArtifactPrivacyFinding> Findings)
{
    public bool Passed => Findings.Count == 0;
}

/// <summary>
/// Scans an evidence tree before upload and rejects model files, model-sized
/// files, unexpectedly large files, or files whose content hash matches the
/// controlled model.
/// </summary>
public static class ArtifactPrivacyScanner
{
    public const long DefaultMaximumEvidenceFileBytes =
        100L * 1024L * 1024L;

    public static async Task<ArtifactPrivacyScanResult> ScanAsync(
        string evidenceRoot,
        long prohibitedModelLengthBytes,
        string prohibitedModelSha256,
        CancellationToken cancellationToken,
        long maximumEvidenceFileBytes = DefaultMaximumEvidenceFileBytes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(evidenceRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(prohibitedModelSha256);

        if (!Directory.Exists(evidenceRoot))
        {
            throw new DirectoryNotFoundException(
                $"Evidence root does not exist: {evidenceRoot}");
        }

        if (prohibitedModelLengthBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(prohibitedModelLengthBytes));
        }

        if (maximumEvidenceFileBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumEvidenceFileBytes));
        }

        var findings = new List<ArtifactPrivacyFinding>();

        foreach (string filePath in Directory.EnumerateFiles(
            evidenceRoot,
            "*",
            SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var file = new FileInfo(filePath);
            string relativePath = Path.GetRelativePath(
                evidenceRoot,
                filePath);

            if (string.Equals(
                file.Extension,
                ".gguf",
                StringComparison.OrdinalIgnoreCase))
            {
                findings.Add(new ArtifactPrivacyFinding(
                    relativePath,
                    "GGUF model files must never be uploaded as test evidence."));
            }

            if (file.Length == prohibitedModelLengthBytes)
            {
                findings.Add(new ArtifactPrivacyFinding(
                    relativePath,
                    "File length matches the controlled model."));
            }

            if (file.Length > maximumEvidenceFileBytes)
            {
                findings.Add(new ArtifactPrivacyFinding(
                    relativePath,
                    $"Evidence file exceeds {maximumEvidenceFileBytes} bytes."));
            }

            // Evidence is expected to be small. Hash files within the bounded
            // evidence size so a renamed/copy-equivalent small prohibited input
            // is still detected without hashing arbitrary multi-gigabyte files.
            if (file.Length <= maximumEvidenceFileBytes)
            {
                string hash = await ModelFileHash.ComputeSha256Async(
                    filePath,
                    cancellationToken);

                if (string.Equals(
                    hash,
                    prohibitedModelSha256,
                    StringComparison.OrdinalIgnoreCase))
                {
                    findings.Add(new ArtifactPrivacyFinding(
                        relativePath,
                        "File SHA-256 matches the controlled model."));
                }
            }
        }

        return new ArtifactPrivacyScanResult(
            findings
                .OrderBy(finding => finding.RelativePath, StringComparer.Ordinal)
                .ThenBy(finding => finding.Reason, StringComparer.Ordinal)
                .ToArray());
    }
}
