namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Estimation;

/// <summary>
/// A recorded caveat about how an established number was reached. These are what
/// pin an assessment to the Estimated evidence grade; nothing is presented as
/// measured that was not measured.
/// </summary>
internal enum EstimationLimitation
{
    Unspecified = 0,

    /// <summary>
    /// Weight memory came from the artifact's byte length plus overhead rather
    /// than from a tensor-level read. Derived from a measured quantity, but less
    /// precise than reading the tensor table.
    /// </summary>
    WeightsDerivedFromFileLength,

    /// <summary>
    /// Weight memory was scaled between two quantisations by average bits per
    /// weight. Embeddings and normalisation tensors do not scale linearly.
    /// </summary>
    WeightsScaledAcrossQuantisation,

    /// <summary>The estimator constants are provisional, not calibrated.</summary>
    UncalibratedEstimatorPolicy,

    /// <summary>
    /// One sequence was assumed. Nothing in a candidate declares parallel
    /// sequences yet, so a batched server workload is out of scope of this number.
    /// </summary>
    SingleSequenceAssumed
}
