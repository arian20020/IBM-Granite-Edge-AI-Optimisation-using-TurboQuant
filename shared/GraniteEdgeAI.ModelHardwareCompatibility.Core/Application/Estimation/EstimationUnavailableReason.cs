namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Estimation;

/// <summary>
/// The single fact that stopped an estimate being produced. Stable codes, never
/// free-form text, so the reason can be shown without leaking anything.
/// </summary>
internal enum EstimationUnavailableReason
{
    None = 0,

    /// <summary>No versioned estimator constants are available.</summary>
    EstimatorPolicyUnavailable,

    /// <summary>
    /// Layers, heads or embedding size were not established, or were
    /// established but describe a shape this estimator cannot size - for
    /// example an embedding size that does not divide evenly by the attention
    /// head count. Rounding a shape like that would be inventing an
    /// architecture rather than reading one.
    /// </summary>
    UnknownArchitecture,

    /// <summary>
    /// The encoding of the imported file is unknown, so a different target
    /// weight format cannot be scaled from it.
    /// </summary>
    UnknownSourceQuantisation,

    /// <summary>
    /// Partial offload declares no layer count, so weights cannot be divided
    /// between system and device memory.
    /// </summary>
    UnknownOffloadSplit,

    /// <summary>The device and offload combination is not an admitted route.</summary>
    UnsupportedDeviceRoute,

    /// <summary>
    /// The KV-cache format has no recorded block encoding. A newly added format
    /// lands here rather than being sized as free.
    /// </summary>
    UnsupportedCacheFormat,

    /// <summary>
    /// The requested weight format has no canonical encoding. A newly added
    /// format lands here rather than being sized against a guessed bit width.
    /// </summary>
    UnsupportedWeightFormat,

    /// <summary>The arithmetic overflowed rather than wrapping to a smaller value.</summary>
    QuantitiesExceedRepresentableRange
}
