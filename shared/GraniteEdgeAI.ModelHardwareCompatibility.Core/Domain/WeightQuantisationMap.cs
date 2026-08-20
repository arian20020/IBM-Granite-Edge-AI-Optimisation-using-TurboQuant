namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;

/// <summary>
/// Translates raw GGUF identifiers into C1's canonical quantisation, and states
/// the average bits per weight each encoding uses.
///
/// The K-quant figures are averages over a mixed encoding: different tensors in
/// one file use different block types. They are accurate enough to scale a file
/// length between formats and not precise enough to be presented as a
/// measurement, which is why every estimate derived from them carries the
/// WeightsScaledAcrossQuantisation limitation.
/// </summary>
internal static class WeightQuantisationMap
{
    private static readonly Dictionary<int, WeightQuantisation> FileTypes = new()
    {
        [0] = WeightQuantisation.F32,
        [1] = WeightQuantisation.F16,
        [7] = WeightQuantisation.Q8_0,
        [10] = WeightQuantisation.Q2_K,
        [12] = WeightQuantisation.Q3_K_M,
        [15] = WeightQuantisation.Q4_K_M,
        [17] = WeightQuantisation.Q5_K_M,
        [18] = WeightQuantisation.Q6_K,
        [32] = WeightQuantisation.BF16
    };

    private static readonly Dictionary<WeightQuantisation, decimal> Bits = new()
    {
        [WeightQuantisation.F32] = 32m,
        [WeightQuantisation.BF16] = 16m,
        [WeightQuantisation.F16] = 16m,
        [WeightQuantisation.Q8_0] = 8.5m,
        [WeightQuantisation.Q6_K] = 6.5625m,
        [WeightQuantisation.Q5_K_M] = 5.6875m,
        [WeightQuantisation.Q4_K_M] = 4.8125m,
        [WeightQuantisation.Q3_K_M] = 3.9062m,
        [WeightQuantisation.Q2_K] = 2.6250m
    };

    /// <summary>
    /// Resolves the encoding of the file as imported. Both identifiers are
    /// required: the version pins how the file type is to be read.
    /// </summary>
    internal static WeightQuantisation FromGgufFileType(
        int? fileType,
        int? quantisationVersion)
    {
        if (fileType is not { } type || quantisationVersion is null)
        {
            return WeightQuantisation.Unknown;
        }

        return FileTypes.TryGetValue(type, out WeightQuantisation quantisation)
            ? quantisation
            : WeightQuantisation.Unknown;
    }

    internal static decimal BitsPerWeight(WeightQuantisation quantisation) =>
        Bits.TryGetValue(quantisation, out decimal bits)
            ? bits
            : throw new ArgumentOutOfRangeException(
                nameof(quantisation),
                "An unknown encoding has no bit width; the caller must report "
                + "NotEstablished rather than substitute a default precision.");
}
