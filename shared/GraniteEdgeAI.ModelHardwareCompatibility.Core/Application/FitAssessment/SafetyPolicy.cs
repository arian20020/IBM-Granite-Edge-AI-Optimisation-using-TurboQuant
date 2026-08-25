using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.FitAssessment;

/// <summary>
/// Versioned safety numbers. Version one ships provisional values taken from
/// the approved workflow documents; they are not measurements, and every
/// result built on them is pinned to the Estimated evidence grade.
/// </summary>
internal sealed record SafetyPolicy
{
    private const ulong Gibibyte = 1024UL * 1024 * 1024;

    private readonly FitThresholds? _thresholds;
    private readonly SafetyTerms? _terms;

    private SafetyPolicy(
        PolicyProvenance provenance,
        string policyVersion,
        FitThresholds? thresholds,
        SafetyTerms? terms)
    {
        Provenance = provenance;
        PolicyVersion = policyVersion;
        _thresholds = thresholds;
        _terms = terms;
    }

    internal PolicyProvenance Provenance { get; }

    internal string PolicyVersion { get; }

    internal FitThresholds Thresholds =>
        _thresholds ?? throw new InvalidOperationException(
            "An absent policy exposes no thresholds; evaluation must report NotEstablished "
            + "rather than fall back to an invented constant.");

    private SafetyTerms Terms =>
        _terms ?? throw new InvalidOperationException(
            "An absent policy exposes no terms; evaluation must report NotEstablished "
            + "rather than fall back to an invented allowance.");

    /// <summary>Memory held back for Windows and its drivers.</summary>
    internal ByteCount OsAllowance => Terms.OsAllowance;

    /// <summary>Memory held back for this application and other running apps.</summary>
    internal ByteCount OperationalReserve => Terms.OperationalReserve;

    internal ByteCount CalibrationMarginFloor => Terms.CalibrationMarginFloor;

    internal decimal CalibrationMarginFraction => Terms.CalibrationMarginFraction;

    /// <summary>
    /// Memory held back from a fresh Windows available-memory reading.
    /// V1 preserves its fixed allowance for reproducibility; V2 scales the
    /// reserve with the pool that is actually available and applies a floor.
    /// </summary>
    internal ByteCount AvailableMemoryReserveFor(ByteCount availableMemory)
    {
        SafetyTerms terms = Terms;

        if (terms.AvailableMemoryReserveFraction == 0m)
        {
            return terms.OsAllowance.Add(terms.OperationalReserve);
        }

        ulong proportional = (ulong)Math.Ceiling(
            availableMemory.Bytes * terms.AvailableMemoryReserveFraction);
        return proportional > terms.AvailableMemoryReserveFloor.Bytes
            ? ByteCount.FromBytes(proportional)
            : terms.AvailableMemoryReserveFloor;
    }

    /// <summary>
    /// The margin added to a predicted peak to cover underprediction. It is
    /// always added and never subtracted, so an uncalibrated estimator errs
    /// toward reporting "does not fit" rather than crashing the machine.
    /// </summary>
    internal ByteCount CalibrationMarginFor(ByteCount predictedPeak)
    {
        SafetyTerms terms = Terms;

        ulong fromFraction = (ulong)Math.Ceiling(
            predictedPeak.Bytes * terms.CalibrationMarginFraction);

        return fromFraction > terms.CalibrationMarginFloor.Bytes
            ? ByteCount.FromBytes(fromFraction)
            : terms.CalibrationMarginFloor;
    }

    internal static SafetyPolicy ProvisionalV1() => new(
        PolicyProvenance.Provisional,
        policyVersion: "fit-safety-policy-v1",
        thresholds: new FitThresholds(
            ComfortableCeiling: 0.75m,
            ModerateHeadroomCeiling: 0.90m,
            NarrowCeiling: 1.00m),
        terms: new SafetyTerms(
            OsAllowance: ByteCount.FromBytes(2 * Gibibyte),
            OperationalReserve: ByteCount.FromBytes(Gibibyte),
            CalibrationMarginFloor: ByteCount.FromBytes(Gibibyte / 2),
            CalibrationMarginFraction: 0.10m,
            AvailableMemoryReserveFloor: ByteCount.Zero,
            AvailableMemoryReserveFraction: 0m));

    internal static SafetyPolicy ProportionalV2() => new(
        PolicyProvenance.Provisional,
        policyVersion: "fit-safety-policy-v2",
        thresholds: new FitThresholds(
            ComfortableCeiling: 0.75m,
            ModerateHeadroomCeiling: 0.90m,
            NarrowCeiling: 1.00m),
        terms: new SafetyTerms(
            // GlobalMemoryStatusEx already reports memory that is currently
            // available after Windows and running applications. Removing a
            // second OS/app allowance would count that usage twice.
            OsAllowance: ByteCount.Zero,
            OperationalReserve: ByteCount.Zero,
            CalibrationMarginFloor: ByteCount.FromBytes(Gibibyte / 2),
            CalibrationMarginFraction: 0.10m,
            AvailableMemoryReserveFloor: ByteCount.FromBytes(Gibibyte / 2),
            AvailableMemoryReserveFraction: 0.10m));

    internal static SafetyPolicy Absent() => new(
        PolicyProvenance.Absent,
        policyVersion: "absent",
        thresholds: null,
        terms: null);

    /// <summary>
    /// Bundles the byte allowances and margin terms so an absent policy can
    /// withhold all of them behind one nullable field and one throwing
    /// accessor, the same shape EstimatorPolicy uses for its terms. Without
    /// this, an absent policy would have to default each term to zero, which
    /// is a full-availability budget with no margin - "everything fits".
    /// </summary>
    private sealed record SafetyTerms(
        ByteCount OsAllowance,
        ByteCount OperationalReserve,
        ByteCount CalibrationMarginFloor,
        decimal CalibrationMarginFraction,
        ByteCount AvailableMemoryReserveFloor,
        decimal AvailableMemoryReserveFraction);
}
