using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.FitAssessment;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Estimation;

/// <summary>
/// The tunable numbers the estimator needs, held together so they version and
/// calibrate as one set.
/// </summary>
internal sealed record EstimatorTerms(
    ulong AllocationAlignment,
    decimal WeightOverheadFraction,
    ByteCount ComputeBufferFloor,
    ulong ComputeBufferBytesPerContextToken,
    ByteCount CpuBackendAllocation,
    ByteCount GpuBackendAllocation,
    decimal StagingBufferFraction,
    ByteCount StagingBufferFloor,
    ByteCount ApplicationOverhead);

/// <summary>
/// Versioned estimator constants with an explicit provenance. Kept separate from
/// SafetyPolicy because reserves and estimator overheads calibrate from different
/// datasets: recalibrating one must not bump the other's version and invalidate
/// its provenance claim.
///
/// Version one ships provisional values from the approved workflow documents.
/// They are documented defaults, not measurements, which is why every estimate
/// built on them records the UncalibratedEstimatorPolicy limitation.
/// </summary>
internal sealed record EstimatorPolicy
{
    private const ulong Mebibyte = 1024UL * 1024;

    private readonly EstimatorTerms? _terms;

    private EstimatorPolicy(
        PolicyProvenance provenance,
        string policyVersion,
        EstimatorTerms? terms)
    {
        Provenance = provenance;
        PolicyVersion = policyVersion;
        _terms = terms;
    }

    internal PolicyProvenance Provenance { get; }

    internal string PolicyVersion { get; }

    internal EstimatorTerms Terms =>
        _terms ?? throw new InvalidOperationException(
            "An absent estimator policy exposes no terms; the caller must report "
            + "NotEstablished rather than fall back to an invented constant.");

    /// <summary>
    /// Scratch memory the runtime needs while evaluating a graph. It grows with
    /// context and never drops below a floor, because even a tiny context still
    /// materialises full-width intermediates.
    /// </summary>
    internal ByteCount ComputeBufferFor(ContextTokenCount context)
    {
        EstimatorTerms terms = Terms;

        ByteCount scaled = ByteCount.FromBytes(
            checked((ulong)context.Tokens * terms.ComputeBufferBytesPerContextToken));

        return scaled > terms.ComputeBufferFloor ? scaled : terms.ComputeBufferFloor;
    }

    internal static EstimatorPolicy ProvisionalV1() => new(
        PolicyProvenance.Provisional,
        policyVersion: "estimator-policy-v1",
        terms: new EstimatorTerms(
            AllocationAlignment: 4096,
            WeightOverheadFraction: 0.03m,
            ComputeBufferFloor: ByteCount.FromBytes(128 * Mebibyte),
            ComputeBufferBytesPerContextToken: 32 * 1024,
            CpuBackendAllocation: ByteCount.FromBytes(64 * Mebibyte),
            GpuBackendAllocation: ByteCount.FromBytes(256 * Mebibyte),
            StagingBufferFraction: 0.05m,
            StagingBufferFloor: ByteCount.FromBytes(64 * Mebibyte),
            ApplicationOverhead: ByteCount.FromBytes(512 * Mebibyte)));

    internal static EstimatorPolicy Absent() => new(
        PolicyProvenance.Absent,
        policyVersion: "absent",
        terms: null);
}
