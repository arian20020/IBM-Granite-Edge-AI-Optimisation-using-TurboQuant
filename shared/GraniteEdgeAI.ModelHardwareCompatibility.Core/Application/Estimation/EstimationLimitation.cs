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
    /// weight. Embeddings and normalisation tensors do not scale linearly, so
    /// this error is not symmetric: for a downward conversion (to fewer bits
    /// per weight) it runs toward under-estimating the target size, which is
    /// the false-safe direction. This is the one knowingly non-conservative
    /// path in the estimator; every other margin in this codebase is added to
    /// the requirement, never subtracted.
    /// </summary>
    WeightsScaledAcrossQuantisation,

    /// <summary>The estimator constants are provisional, not calibrated.</summary>
    UncalibratedEstimatorPolicy,

    /// <summary>
    /// One sequence was assumed. Nothing in a candidate declares parallel
    /// sequences yet, so a batched server workload is out of scope of this number.
    /// </summary>
    SingleSequenceAssumed,

    /// <summary>
    /// A weight-conversion candidate charges only the converted artifact's size
    /// to storage. The trusted higher-precision source it converts from is
    /// larger than both the imported file and the target, must exist on disk
    /// to be read, and coexists with the target for the duration of the
    /// conversion - so peak storage is understated by roughly the source's
    /// size. <see cref="TrustedSourceAvailability"/> carries no size today, so
    /// there is no figure to charge; this limitation names the omission rather
    /// than inventing one.
    /// </summary>
    ConversionSourceStorageNotCounted
}
