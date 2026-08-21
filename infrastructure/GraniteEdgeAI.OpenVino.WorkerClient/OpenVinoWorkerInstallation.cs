using GraniteEdgeAI.OpenVino.Contracts;

namespace GraniteEdgeAI.OpenVino.WorkerClient;

/// <summary>Closed COFF machine values accepted for one worker binary.</summary>
public enum OpenVinoWorkerBinaryMachine : ushort
{
    I386 = 0x014c,
    Amd64 = 0x8664
}

/// <summary>Identifies one controlled route-specific worker installation.</summary>
public sealed record OpenVinoWorkerInstallation(
    string ApprovedWorkerRoot,
    string WorkerExecutableRelativePath,
    string ExpectedProtocolId,
    OpenVinoBuildEvidence ExpectedBuildEvidence,
    IReadOnlyDictionary<string, OpenVinoWorkerBinaryMachine> ExpectedBinaryMachines)
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
        ArgumentNullException.ThrowIfNull(ExpectedBinaryMachines);
        string executable = WorkerExecutableRelativePath.Replace('\\', '/');
        if (ExpectedBinaryMachines.Count == 0 ||
            !ExpectedBinaryMachines.TryGetValue(executable, out
                OpenVinoWorkerBinaryMachine executableMachine) ||
            executableMachine != OpenVinoWorkerBinaryMachine.Amd64)
        {
            throw new ArgumentException(
                "The exact binary policy must include the AMD64 worker executable.",
                nameof(ExpectedBinaryMachines));
        }

        HashSet<string> binaries = new(StringComparer.Ordinal);
        foreach ((string binary, OpenVinoWorkerBinaryMachine machine) in
            ExpectedBinaryMachines)
        {
            if (string.IsNullOrWhiteSpace(binary) ||
                Path.IsPathRooted(binary) || binary.Contains('\\') ||
                binary.Contains('\0') || binary.Split('/').Any(segment =>
                    string.IsNullOrEmpty(segment) || segment is "." or "..") ||
                !binaries.Add(binary) ||
                machine is not OpenVinoWorkerBinaryMachine.I386 and
                    not OpenVinoWorkerBinaryMachine.Amd64)
            {
                throw new ArgumentException(
                    "The binary policy must be an exact relative path-to-machine map.",
                    nameof(ExpectedBinaryMachines));
            }
        }
    }
}
