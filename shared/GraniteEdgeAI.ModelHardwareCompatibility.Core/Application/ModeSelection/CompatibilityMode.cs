namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.ModeSelection;

/// <summary>
/// The four optimisation intents a user can express. "Use current model" is not
/// one of them: it is a separate action that creates a runtime profile rather
/// than choosing between alternatives.
/// </summary>
internal enum CompatibilityMode
{
    Unspecified = 0,
    Automatic,
    Quality,
    Balanced,
    Efficiency
}
