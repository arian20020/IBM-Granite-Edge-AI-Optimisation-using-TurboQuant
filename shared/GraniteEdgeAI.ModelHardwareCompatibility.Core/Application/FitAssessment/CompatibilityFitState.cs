namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.FitAssessment;

/// <summary>
/// How one candidate configuration relates to the safe memory budget.
/// NotEstablished is distinct from DoesNotFit: the first means the question
/// could not be answered, the second means it was answered negatively.
/// </summary>
public enum CompatibilityFitState
{
    Unspecified = 0,
    Safe,
    Narrow,
    DoesNotFit,
    Unsupported,
    NotEstablished
}
