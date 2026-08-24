namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.ModeSelection;

/// <summary>
/// The four optimisation intents a user can express. "Use current model" is not
/// one of them: it is a separate action that creates a runtime profile rather
/// than choosing between alternatives.
/// </summary>
/// <remarks>
/// MIGRATION INPUT ONLY. Superseded by
/// <see cref="Optimization.OptimizationPreferenceSelection"/>, which carries
/// Automatic plus the five approved manual bands.
///
/// Kept, not deleted. The compatibility screens shipped against these four
/// members and they still decide what a user sees today; removing them would
/// break working behaviour to make room for a contract nothing consumes yet.
/// The approved sequence is additive: the new vocabulary lands alongside, the
/// executors move to it, and this is retired once nothing reads it.
///
/// What is already forbidden is serializing these names into anything durable.
/// A plan or result carrying Quality or Efficiency would put a vocabulary in
/// front of a user that the product has replaced, and
/// OptimizationCanonicalizer refuses it.
/// </remarks>
public enum CompatibilityMode
{
    Unspecified = 0,
    Automatic,
    Quality,
    Balanced,
    Efficiency
}
