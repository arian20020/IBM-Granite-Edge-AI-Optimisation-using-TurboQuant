using GraniteEdgeAI.OpenVino.Contracts;

namespace GraniteEdgeAI.OpenVino.WorkerClient;

/// <summary>Identifies one controlled route-specific worker installation.</summary>
public sealed record OpenVinoWorkerInstallation(
    string ApprovedWorkerRoot,
    string WorkerExecutableRelativePath,
    string ExpectedProtocolId)
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
    }
}
