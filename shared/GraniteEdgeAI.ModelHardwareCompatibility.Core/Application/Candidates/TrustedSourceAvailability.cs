namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Candidates;

/// <summary>
/// Whether a trusted higher-precision source exists to convert from.
///
/// Requantising an already-quantised file compounds loss, so spec section 10
/// forbids doing it by default. Without a declared source, only the imported
/// weights may be offered.
/// </summary>
internal sealed record TrustedSourceAvailability
{
    private TrustedSourceAvailability(bool hasHigherPrecisionSource) =>
        HasHigherPrecisionSource = hasHigherPrecisionSource;

    internal bool HasHigherPrecisionSource { get; }

    internal static TrustedSourceAvailability None() => new(false);

    internal static TrustedSourceAvailability HigherPrecisionAvailable() => new(true);
}
