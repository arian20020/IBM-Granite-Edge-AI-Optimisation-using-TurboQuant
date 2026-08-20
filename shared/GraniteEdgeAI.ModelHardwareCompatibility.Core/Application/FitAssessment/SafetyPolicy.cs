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

    private SafetyPolicy(
        PolicyProvenance provenance,
        string policyVersion,
        FitThresholds? thresholds,
        ByteCount osAllowance,
        ByteCount operationalReserve,
        ByteCount calibrationMarginFloor,
        decimal calibrationMarginFraction)
    {
        Provenance = provenance;
        PolicyVersion = policyVersion;
        _thresholds = thresholds;
        OsAllowance = osAllowance;
        OperationalReserve = operationalReserve;
        CalibrationMarginFloor = calibrationMarginFloor;
        CalibrationMarginFraction = calibrationMarginFraction;
    }

    internal PolicyProvenance Provenance { get; }

    internal string PolicyVersion { get; }

    /// <summary>Memory held back for Windows and its drivers.</summary>
    internal ByteCount OsAllowance { get; }

    /// <summary>Memory held back for this application and other running apps.</summary>
    internal ByteCount OperationalReserve { get; }

    internal ByteCount CalibrationMarginFloor { get; }

    internal decimal CalibrationMarginFraction { get; }

    internal FitThresholds Thresholds =>
        _thresholds ?? throw new InvalidOperationException(
            "An absent policy exposes no thresholds; evaluation must report NotEstablished "
            + "rather than fall back to an invented constant.");

    /// <summary>
    /// The margin added to a predicted peak to cover underprediction. It is
    /// always added and never subtracted, so an uncalibrated estimator errs
    /// toward reporting "does not fit" rather than crashing the machine.
    /// </summary>
    internal ByteCount CalibrationMarginFor(ByteCount predictedPeak)
    {
        ulong fromFraction = (ulong)Math.Ceiling(
            predictedPeak.Bytes * CalibrationMarginFraction);

        return fromFraction > CalibrationMarginFloor.Bytes
            ? ByteCount.FromBytes(fromFraction)
            : CalibrationMarginFloor;
    }

    internal static SafetyPolicy ProvisionalV1() => new(
        PolicyProvenance.Provisional,
        policyVersion: "fit-safety-policy-v1",
        thresholds: new FitThresholds(
            ComfortableCeiling: 0.75m,
            ModerateHeadroomCeiling: 0.90m,
            NarrowCeiling: 1.00m),
        osAllowance: ByteCount.FromBytes(2 * Gibibyte),
        operationalReserve: ByteCount.FromBytes(Gibibyte),
        calibrationMarginFloor: ByteCount.FromBytes(Gibibyte / 2),
        calibrationMarginFraction: 0.10m);

    internal static SafetyPolicy Absent() => new(
        PolicyProvenance.Absent,
        policyVersion: "absent",
        thresholds: null,
        osAllowance: ByteCount.Zero,
        operationalReserve: ByteCount.Zero,
        calibrationMarginFloor: ByteCount.Zero,
        calibrationMarginFraction: 0m);
}
