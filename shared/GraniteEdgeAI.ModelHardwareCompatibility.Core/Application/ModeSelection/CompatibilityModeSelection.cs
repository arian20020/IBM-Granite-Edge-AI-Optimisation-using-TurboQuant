using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Candidates;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.ModeSelection;

/// <summary>
/// One mode's answer.
///
/// An unavailable mode carries a reason so presentation can disable it with an
/// explanation rather than hiding it — a hidden option looks like an option that
/// never existed, which is a different and misleading claim.
/// </summary>
internal sealed record CompatibilityModeSelection
{
    private CompatibilityModeSelection(
        CompatibilityMode mode,
        ModeAvailability availability,
        CandidateFingerprint? selectedFingerprint,
        ModeAdmissionReason reason,
        IReadOnlyList<SelectionFactor> factors)
    {
        Mode = mode;
        Availability = availability;
        SelectedFingerprint = selectedFingerprint;
        Reason = reason;
        Factors = factors;
    }

    internal CompatibilityMode Mode { get; }

    internal ModeAvailability Availability { get; }

    /// <summary>Set only when available.</summary>
    internal CandidateFingerprint? SelectedFingerprint { get; }

    internal ModeAdmissionReason Reason { get; }

    /// <summary>The ordering applied, in the order it was applied.</summary>
    internal IReadOnlyList<SelectionFactor> Factors { get; }

    internal static CompatibilityModeSelection Available(
        CompatibilityMode mode,
        CandidateFingerprint selectedFingerprint,
        IReadOnlyList<SelectionFactor> factors)
    {
        ArgumentNullException.ThrowIfNull(factors);
        RequireMode(mode);

        if (factors.Count == 0)
        {
            throw new ArgumentException(
                "An available mode must record the ordering that decided it, or a "
                + "screen cannot explain the choice without re-deriving it.",
                nameof(factors));
        }

        return new CompatibilityModeSelection(
            mode, ModeAvailability.Available, selectedFingerprint,
            ModeAdmissionReason.None, [.. factors]);
    }

    internal static CompatibilityModeSelection Unavailable(
        CompatibilityMode mode,
        ModeAdmissionReason reason)
    {
        RequireMode(mode);

        if (reason == ModeAdmissionReason.None)
        {
            throw new ArgumentException(
                "An unavailable mode must name why, so it can be disabled with an "
                + "accessible explanation rather than hidden.",
                nameof(reason));
        }

        return new CompatibilityModeSelection(
            mode, ModeAvailability.Unavailable, null, reason, []);
    }

    internal static CompatibilityModeSelection NotEstablished(CompatibilityMode mode)
    {
        RequireMode(mode);

        return new CompatibilityModeSelection(
            mode, ModeAvailability.NotEstablished, null, ModeAdmissionReason.None, []);
    }

    private static void RequireMode(CompatibilityMode mode)
    {
        if (mode == CompatibilityMode.Unspecified)
        {
            throw new ArgumentException(
                "A selection must name its mode.", nameof(mode));
        }
    }
}
