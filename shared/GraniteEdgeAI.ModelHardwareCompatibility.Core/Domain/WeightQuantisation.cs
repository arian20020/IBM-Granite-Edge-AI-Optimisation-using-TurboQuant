namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;

/// <summary>
/// C1's canonical weight encoding, derived from the GGUF file type and
/// quantisation version rather than from any display string. Declared highest to
/// lowest quality.
/// </summary>
internal enum WeightQuantisation
{
    /// <summary>
    /// The encoding could not be established. This is a typed absence and is
    /// never treated as a default precision.
    /// </summary>
    Unknown = 0,
    F32,
    BF16,
    F16,
    Q8_0,
    Q6_K,
    Q5_K_M,
    Q4_K_M,
    Q3_K_M,
    Q2_K
}
