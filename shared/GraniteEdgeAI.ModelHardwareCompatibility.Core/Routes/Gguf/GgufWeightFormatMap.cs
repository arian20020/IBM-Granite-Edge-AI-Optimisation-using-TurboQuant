using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.Gguf;

/// <summary>
/// Translates a requested llama.cpp weight format into C1's canonical encoding.
///
/// This lives on the route side rather than in Domain because knowing what
/// "Q4KM" means is GGUF knowledge. Domain holds the canonical vocabulary and
/// must not depend on any route.
/// </summary>
internal static class GgufWeightFormatMap
{
    /// <summary>
    /// Imported has no canonical encoding of its own: it is whatever the file
    /// already contains, which only the file's own identifiers can answer.
    /// </summary>
    internal static WeightQuantisation ToCanonical(GgufWeightFormat format) => format switch
    {
        GgufWeightFormat.BF16 => WeightQuantisation.BF16,
        GgufWeightFormat.F16 => WeightQuantisation.F16,
        GgufWeightFormat.Q8_0 => WeightQuantisation.Q8_0,
        GgufWeightFormat.Q6K => WeightQuantisation.Q6_K,
        GgufWeightFormat.Q5KM => WeightQuantisation.Q5_K_M,
        GgufWeightFormat.Q4KM => WeightQuantisation.Q4_K_M,
        GgufWeightFormat.Q3KM => WeightQuantisation.Q3_K_M,
        GgufWeightFormat.Q2K => WeightQuantisation.Q2_K,
        _ => WeightQuantisation.Unknown
    };
}
