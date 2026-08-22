namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Contracts;

/// <summary>
/// What a finding is about. Stable codes only — section 14 forbids any free-form
/// payload reaching a result, and presentation owns the wording anyway.
/// </summary>
internal enum CompatibilityFindingCode
{
    Unspecified = 0,

    /// <summary>Mandatory whenever a policy in use is Provisional rather than Calibrated.</summary>
    UncalibratedEstimate,

    ModelFactsUnavailable,
    HardwareFactsUnavailable,
    FreshMemoryUnavailable,
    HandoffClaimFailed,
    SupportMatrixUnavailable,
    NoCandidateGenerated,
    PlanningContextNotEstablished,

    /// <summary>
    /// Candidates were generated but none could be sized — the model's
    /// architecture, its encoding, or the arithmetic itself defeated every one.
    /// Distinct from <see cref="NoSafeConfigurationFound"/>, which means every
    /// candidate WAS sized and none fit. Telling a user "nothing fits" when
    /// nothing was computed presents an unknown as a conclusion.
    /// </summary>
    NoCandidateCouldBeEstimated,

    /// <summary>
    /// Every candidate was sized and compared, and none fit the safe budget.
    /// </summary>
    NoSafeConfigurationFound,

    /// <summary>
    /// The as-imported configuration is not among the candidates. The assessment
    /// carries the exact reason; this finding only flags that it happened.
    /// </summary>
    BaselineConfigurationUnavailable
}

internal enum FindingSeverity
{
    Unspecified = 0,
    Information,
    Warning,
    Blocking
}

/// <summary>
/// One recorded observation about a run, carrying a code rather than a message.
/// </summary>
internal sealed record CompatibilityFinding
{
    private CompatibilityFinding(CompatibilityFindingCode code, FindingSeverity severity)
    {
        Code = code;
        Severity = severity;
    }

    internal CompatibilityFindingCode Code { get; }

    internal FindingSeverity Severity { get; }

    internal static CompatibilityFinding Create(
        CompatibilityFindingCode code,
        FindingSeverity severity)
    {
        if (code == CompatibilityFindingCode.Unspecified)
        {
            throw new ArgumentException(
                "A finding must name what it is about.", nameof(code));
        }

        if (severity == FindingSeverity.Unspecified)
        {
            throw new ArgumentException(
                "A finding must state how much it matters.", nameof(severity));
        }

        return new CompatibilityFinding(code, severity);
    }
}
