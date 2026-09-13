using GraniteEdgeAI.GgufQuantization.Contracts;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Execution;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.Gguf;

namespace GraniteEdgeAI.GgufQuantization.Capabilities;

public static class GgufQuantizerFormatMap
{
    public static string ToToolToken(GgufWeightFormat format) => format switch
    {
        GgufWeightFormat.Q2K => "Q2_K",
        GgufWeightFormat.Q3KM => "Q3_K_M",
        GgufWeightFormat.Q4KM => "Q4_K_M",
        GgufWeightFormat.Q5KM => "Q5_K_M",
        GgufWeightFormat.Q6K => "Q6_K",
        GgufWeightFormat.Q8_0 => "Q8_0",
        _ => throw new ArgumentOutOfRangeException(
            nameof(format), format, "This weight format is not an approved persistent quantizer target."),
    };

    public static string ToToolToken(GgufCacheType format) =>
        throw new ArgumentOutOfRangeException(
            nameof(format), format, "Runtime cache formats are never persistent weight formats.");

    public static GgufQuantizationFormat ToContract(GgufWeightFormat format) => format switch
    {
        GgufWeightFormat.BF16 => GgufQuantizationFormat.BF16,
        GgufWeightFormat.F16 => GgufQuantizationFormat.F16,
        GgufWeightFormat.Q8_0 => GgufQuantizationFormat.Q8_0,
        GgufWeightFormat.Q6K => GgufQuantizationFormat.Q6K,
        GgufWeightFormat.Q5KM => GgufQuantizationFormat.Q5KM,
        GgufWeightFormat.Q4KM => GgufQuantizationFormat.Q4KM,
        GgufWeightFormat.Q3KM => GgufQuantizationFormat.Q3KM,
        GgufWeightFormat.Q2K => GgufQuantizationFormat.Q2K,
        _ => throw new ArgumentOutOfRangeException(nameof(format), format, "No exact contract format exists."),
    };

    public static GgufWeightFormat ToCore(GgufQuantizationFormat format) => format switch
    {
        GgufQuantizationFormat.BF16 => GgufWeightFormat.BF16,
        GgufQuantizationFormat.F16 => GgufWeightFormat.F16,
        GgufQuantizationFormat.Q8_0 => GgufWeightFormat.Q8_0,
        GgufQuantizationFormat.Q6K => GgufWeightFormat.Q6K,
        GgufQuantizationFormat.Q5KM => GgufWeightFormat.Q5KM,
        GgufQuantizationFormat.Q4KM => GgufWeightFormat.Q4KM,
        GgufQuantizationFormat.Q3KM => GgufWeightFormat.Q3KM,
        GgufQuantizationFormat.Q2K => GgufWeightFormat.Q2K,
        _ => throw new ArgumentOutOfRangeException(nameof(format), format, "No exact planning format exists."),
    };

    public static WeightQuantisation ToCanonicalPrecision(GgufQuantizationFormat format) => format switch
    {
        GgufQuantizationFormat.F32 => WeightQuantisation.F32,
        GgufQuantizationFormat.BF16 => WeightQuantisation.BF16,
        GgufQuantizationFormat.F16 => WeightQuantisation.F16,
        GgufQuantizationFormat.Q8_0 => WeightQuantisation.Q8_0,
        GgufQuantizationFormat.Q6K => WeightQuantisation.Q6_K,
        GgufQuantizationFormat.Q5KM => WeightQuantisation.Q5_K_M,
        GgufQuantizationFormat.Q4KM => WeightQuantisation.Q4_K_M,
        GgufQuantizationFormat.Q3KM => WeightQuantisation.Q3_K_M,
        GgufQuantizationFormat.Q2K => WeightQuantisation.Q2_K,
        _ => throw new ArgumentOutOfRangeException(nameof(format), format, "Unknown format."),
    };
}
