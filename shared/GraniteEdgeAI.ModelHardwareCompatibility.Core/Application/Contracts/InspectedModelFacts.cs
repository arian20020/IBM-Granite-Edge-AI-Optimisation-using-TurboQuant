using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Contracts;

/// <summary>
/// The model facts the estimator consumes, owned by C1 and shaped by the
/// calculation rather than by any upstream contract. Every architectural fact is
/// optional because inspection may establish some and not others; absence is
/// null and never zero, so a missing figure collapses an estimate instead of
/// silently making the configuration cheaper.
/// </summary>
internal sealed record InspectedModelFacts
{
    private InspectedModelFacts(
        ByteCount fileLength,
        int? layerCount,
        int? embeddingSize,
        int? attentionHeadCount,
        int? keyValueHeadCount,
        int? declaredContextLimit,
        int? fileType,
        int? quantisationVersion)
    {
        FileLength = fileLength;
        LayerCount = layerCount;
        EmbeddingSize = embeddingSize;
        AttentionHeadCount = attentionHeadCount;
        KeyValueHeadCount = keyValueHeadCount;
        DeclaredContextLimit = declaredContextLimit;
        FileType = fileType;
        QuantisationVersion = quantisationVersion;
    }

    /// <summary>The measured artifact length. Required.</summary>
    internal ByteCount FileLength { get; }

    internal int? LayerCount { get; }

    internal int? EmbeddingSize { get; }

    internal int? AttentionHeadCount { get; }

    /// <summary>
    /// Key/value head count. Grouped-query models use fewer of these than
    /// attention heads, and substituting the attention head count would overstate
    /// the KV cache several times over.
    /// </summary>
    internal int? KeyValueHeadCount { get; }

    internal int? DeclaredContextLimit { get; }

    /// <summary>Raw GGUF file type, interpreted only through the canonical map.</summary>
    internal int? FileType { get; }

    internal int? QuantisationVersion { get; }

    internal static InspectedModelFacts Create(
        ByteCount fileLength,
        int? layerCount,
        int? embeddingSize,
        int? attentionHeadCount,
        int? keyValueHeadCount,
        int? declaredContextLimit,
        int? fileType,
        int? quantisationVersion)
    {
        if (fileLength == ByteCount.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(fileLength),
                "A model must have a measured length; the weight estimate rests on it.");
        }

        RequirePositiveWhenPresent(layerCount, nameof(layerCount));
        RequirePositiveWhenPresent(embeddingSize, nameof(embeddingSize));
        RequirePositiveWhenPresent(attentionHeadCount, nameof(attentionHeadCount));
        RequirePositiveWhenPresent(keyValueHeadCount, nameof(keyValueHeadCount));
        RequirePositiveWhenPresent(declaredContextLimit, nameof(declaredContextLimit));

        return new InspectedModelFacts(
            fileLength,
            layerCount,
            embeddingSize,
            attentionHeadCount,
            keyValueHeadCount,
            declaredContextLimit,
            fileType,
            quantisationVersion);
    }

    private static void RequirePositiveWhenPresent(int? value, string parameterName)
    {
        if (value is <= 0)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                "An established architectural fact must be positive. Absence is "
                + "expressed as null, never as zero.");
        }
    }
}
