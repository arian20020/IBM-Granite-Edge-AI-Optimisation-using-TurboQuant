using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.FitAssessment;

/// <summary>
/// The result of comparing one candidate's predicted peak against the safe
/// budget, together with the figures that produced it so the reasoning can be
/// shown to the user rather than asserted.
/// </summary>
internal sealed record FitAssessment(
    CompatibilityFitState State,
    FitLimitingReason LimitingReason,
    ByteCount SafeBudget,
    ByteCount RequiredBytes,
    ByteCount Headroom,
    decimal PressureRatio,
    ByteCount DedicatedSafeBudget = default,
    ByteCount DedicatedRequiredBytes = default,
    ByteCount DedicatedHeadroom = default);
