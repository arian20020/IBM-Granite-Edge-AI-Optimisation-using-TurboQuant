using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Estimation;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.FitAssessment;

/// <summary>
/// Compares a predicted peak against a conservative budget.
///
/// The bias is deliberately one-directional. A false-safe answer crashes the
/// user's machine; a false-unsafe answer is an inconvenience. So every unknown
/// collapses to NotEstablished, and the calibration margin is added to the
/// requirement rather than to the budget.
/// </summary>
internal static class FitPolicy
{
    internal static FitAssessment Assess(
        ResourcePeakProfile profile,
        AvailableResources available,
        SafetyPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(available);
        ArgumentNullException.ThrowIfNull(policy);

        if (policy.Provenance is PolicyProvenance.Absent or PolicyProvenance.Unspecified)
        {
            return NotEstablished(FitLimitingReason.SafetyPolicyUnavailable);
        }

        // A historical reading cannot satisfy the gate. Refusing here is what
        // stops a stale figure from producing a confident false-safe result.
        if (!available.IsFresh)
        {
            return NotEstablished(FitLimitingReason.FreshAvailabilityUnavailable);
        }

        // Shared device memory is already folded into system pressure, so this
        // single figure is the whole demand on physical RAM.
        ByteCount predictedPeak = profile.SystemMemoryPressure;
        ByteCount required = predictedPeak.Add(policy.CalibrationMarginFor(predictedPeak));

        // An exhausted budget is an expected answer, not an arithmetic error,
        // so the subtractions saturate at zero instead of throwing.
        ByteCount budget = ByteCount.Zero;
        if (available.SystemMemory.TrySubtract(policy.OsAllowance, out ByteCount afterOs))
        {
            afterOs.TrySubtract(policy.OperationalReserve, out budget);
        }

        if (budget == ByteCount.Zero)
        {
            return new FitAssessment(
                CompatibilityFitState.DoesNotFit,
                FitLimitingReason.InsufficientSystemMemory,
                ByteCount.Zero,
                required,
                ByteCount.Zero,
                PressureRatio: decimal.MaxValue);
        }

        decimal ratio = required.RatioAgainst(budget);
        _ = budget.TrySubtract(required, out ByteCount headroom);

        FitThresholds thresholds = policy.Thresholds;

        // Equality counts as fitting because every mandatory margin is already
        // inside "required" by this point.
        (CompatibilityFitState state, FitLimitingReason reason) = ratio switch
        {
            _ when ratio <= thresholds.ModerateHeadroomCeiling =>
                (CompatibilityFitState.Safe, FitLimitingReason.None),
            _ when ratio <= thresholds.NarrowCeiling =>
                (CompatibilityFitState.Narrow, FitLimitingReason.None),
            _ => (CompatibilityFitState.DoesNotFit,
                  FitLimitingReason.InsufficientSystemMemory)
        };

        return new FitAssessment(state, reason, budget, required, headroom, ratio);
    }

    private static FitAssessment NotEstablished(FitLimitingReason reason) =>
        new(CompatibilityFitState.NotEstablished,
            reason,
            ByteCount.Zero,
            ByteCount.Zero,
            ByteCount.Zero,
            PressureRatio: 0m);
}
