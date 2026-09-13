using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;

/// <summary>
/// Exact, path-free identity of the bytes a GGUF quantiser is permitted to read.
/// The journey records where the identity came from; precision records which
/// downward targets that evidence can honestly authorize.
/// </summary>
public sealed record GgufConversionSourceBinding
{
    private GgufConversionSourceBinding(
        WeightQuantisation precision, OptimizationJourneyBinding journey)
    {
        Precision = precision;
        Journey = journey;
    }

    public WeightQuantisation Precision { get; }

    public OptimizationJourneyBinding Journey { get; }

    public string SourceSha256 => Journey.ModelSha256;

    public ulong SourceLengthBytes => Journey.ModelLengthBytes;

    public static GgufConversionSourceBinding Create(
        WeightQuantisation precision, OptimizationJourneyBinding journey)
    {
        ArgumentNullException.ThrowIfNull(journey);

        if (!Enum.IsDefined(precision))
        {
            throw new ArgumentOutOfRangeException(
                nameof(precision), precision,
                "An undefined precision cannot bind conversion authority.");
        }

        if (precision == WeightQuantisation.Unknown)
        {
            throw new ArgumentException(
                "A conversion source must have an established precision. Unknown "
                + "cannot prove that a requested target is downward.",
                nameof(precision));
        }

        return new GgufConversionSourceBinding(precision, journey);
    }

    /// <summary>
    /// Creates conversion authority from the exact GGUF identifiers established
    /// by Model Inspection. Unknown versions and file types fail closed; callers
    /// never substitute a guessed source precision.
    /// </summary>
    public static bool TryCreateFromGgufInspection(
        int? fileType,
        int? quantisationVersion,
        OptimizationJourneyBinding journey,
        out GgufConversionSourceBinding? source)
    {
        ArgumentNullException.ThrowIfNull(journey);
        WeightQuantisation precision = WeightQuantisationMap.FromGgufFileType(
            fileType,
            quantisationVersion);
        if (precision == WeightQuantisation.Unknown)
        {
            source = null;
            return false;
        }

        source = new GgufConversionSourceBinding(precision, journey);
        return true;
    }

    internal bool IsAlreadyQuantised =>
        Precision is WeightQuantisation.Q8_0
            or WeightQuantisation.Q6_K
            or WeightQuantisation.Q5_K_M
            or WeightQuantisation.Q4_K_M
            or WeightQuantisation.Q3_K_M
            or WeightQuantisation.Q2_K
            or WeightQuantisation.TQ4_1S
            or WeightQuantisation.TQ3_1S;

    internal bool CanProduce(WeightQuantisation target) =>
        target != WeightQuantisation.Unknown
        && WeightQuantisationMap.BitsPerWeight(target)
            < WeightQuantisationMap.BitsPerWeight(Precision);
}
