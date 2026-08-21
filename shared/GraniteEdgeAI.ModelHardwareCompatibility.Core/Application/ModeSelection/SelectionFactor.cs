namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.ModeSelection;

/// <summary>
/// One comparison step in a mode's ordering, recorded in the order it was
/// applied. The list is what lets a screen say why this configuration won
/// without re-deriving the decision.
/// </summary>
internal enum SelectionFactor
{
    Unspecified = 0,
    PreservesRequestedContext,
    QualityTier,
    EvidenceGrade,
    NonExperimental,
    LeastDestructivePreparation,
    Performance,
    Headroom,
    WorstPoolPressureRatio,
    AddedStorage,
    MaximiseContext,
    DistanceFromImportedConfiguration,
    Fingerprint,

    /// <summary>
    /// Recorded when performance was part of the ordering but nothing measured
    /// it, so the factor separated nothing.
    /// </summary>
    PerformanceNotEstablished
}
