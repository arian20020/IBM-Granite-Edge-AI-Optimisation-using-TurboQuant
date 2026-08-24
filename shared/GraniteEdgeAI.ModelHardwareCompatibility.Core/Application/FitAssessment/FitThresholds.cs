namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.FitAssessment;

/// <summary>
/// Ratios of required bytes to safe budget that separate the fit states.
/// </summary>
/// <param name="ComfortableCeiling">At or below this, headroom is generous.</param>
/// <param name="ModerateHeadroomCeiling">Upper bound of a still-safe result.</param>
/// <param name="NarrowCeiling">Upper bound before the budget is exceeded.</param>
internal sealed record FitThresholds(
    decimal ComfortableCeiling,
    decimal ModerateHeadroomCeiling,
    decimal NarrowCeiling);
