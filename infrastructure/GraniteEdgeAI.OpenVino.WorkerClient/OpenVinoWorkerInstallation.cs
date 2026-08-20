using GraniteEdgeAI.OpenVino.Contracts;

namespace GraniteEdgeAI.OpenVino.WorkerClient;

/// <summary>Identifies one controlled route-specific worker installation.</summary>
public sealed record OpenVinoWorkerInstallation(
    string ApprovedWorkerRoot,
    string WorkerExecutableRelativePath,
    string ExpectedProtocolId,
    OpenVinoBuildEvidence ExpectedBuildEvidence,
    IReadOnlyList<string> ExpectedAmd64Binaries)
{
    public void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ApprovedWorkerRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(WorkerExecutableRelativePath);
        if (!Path.IsPathFullyQualified(ApprovedWorkerRoot))
        {
            throw new ArgumentException(
                "The approved worker root must be an absolute path.",
                nameof(ApprovedWorkerRoot));
        }

        if (Path.IsPathRooted(WorkerExecutableRelativePath) ||
            WorkerExecutableRelativePath.Contains('\0'))
        {
            throw new ArgumentException(
                "The worker executable must be package-relative.",
                nameof(WorkerExecutableRelativePath));
        }

        if (ExpectedProtocolId is not OpenVinoProtocol.OfficialProtocolId and
            not OpenVinoProtocol.TurboQuantProtocolId)
        {
            throw new ArgumentException(
                "The expected protocol must identify one approved OpenVINO route.",
                nameof(ExpectedProtocolId));
        }

        ArgumentNullException.ThrowIfNull(ExpectedBuildEvidence);
        ExpectedBuildEvidence.Validate();
        ArgumentNullException.ThrowIfNull(ExpectedAmd64Binaries);
        if (ExpectedAmd64Binaries.Count == 0 ||
            !ExpectedAmd64Binaries.Contains(
                WorkerExecutableRelativePath.Replace('\\', '/'),
                StringComparer.Ordinal))
        {
            throw new ArgumentException(
                "The AMD64 binary policy must include the worker executable.",
                nameof(ExpectedAmd64Binaries));
        }

        HashSet<string> binaries = new(StringComparer.Ordinal);
        foreach (string binary in ExpectedAmd64Binaries)
        {
            if (string.IsNullOrWhiteSpace(binary) ||
                Path.IsPathRooted(binary) || binary.Contains('\\') ||
                binary.Contains('\0') || binary.Split('/').Any(segment =>
                    string.IsNullOrEmpty(segment) || segment is "." or "..") ||
                !binaries.Add(binary))
            {
                throw new ArgumentException(
                    "The AMD64 binary policy must be a closed relative list.",
                    nameof(ExpectedAmd64Binaries));
            }
        }
    }
}
