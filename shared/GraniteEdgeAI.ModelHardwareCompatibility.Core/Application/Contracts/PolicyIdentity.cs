using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.FitAssessment;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Contracts;

/// <summary>
/// Which versioned policy produced a number, and how much it is worth.
///
/// A result carries one of these per policy in use so a reader can tell whether
/// the figures rest on measurements or on documented defaults.
/// </summary>
internal sealed record PolicyIdentity
{
    private PolicyIdentity(string policyName, string version, PolicyProvenance provenance)
    {
        PolicyName = policyName;
        Version = version;
        Provenance = provenance;
    }

    internal string PolicyName { get; }

    internal string Version { get; }

    internal PolicyProvenance Provenance { get; }

    internal static PolicyIdentity Create(
        string policyName,
        string version,
        PolicyProvenance provenance)
    {
        if (string.IsNullOrWhiteSpace(policyName))
        {
            throw new ArgumentException(
                "A policy identity must name its policy.", nameof(policyName));
        }

        if (string.IsNullOrWhiteSpace(version))
        {
            throw new ArgumentException(
                "A policy identity must carry a version, or a reader cannot tell "
                + "which numbers produced this result.",
                nameof(version));
        }

        return new PolicyIdentity(policyName, version, provenance);
    }
}
