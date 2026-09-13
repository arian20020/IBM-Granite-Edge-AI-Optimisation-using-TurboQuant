using System;

namespace GraniteEdgeAI.Features.ModelInspection.Contracts;

/// <summary>
/// Holds model-configuration evidence exposed by the approved lightweight
/// runtime path. Unavailable native values remain null rather than inferred.
/// </summary>
internal sealed record ModelInspectionConfigurationEvidence
{
    /// <summary>
    /// Creates immutable configuration evidence for the first GGUF runtime.
    /// </summary>
    internal ModelInspectionConfigurationEvidence(
        string format,
        uint? ggufVersion,
        string? modelName,
        string? architecture,
        int? fileType,
        int? quantisationVersion,
        ulong? declaredContextLength,
        ulong? embeddingSize,
        int? layerCount,
        int? attentionHeadCount,
        int? kvHeadCount,
        ulong? parameterCount)
    {
        // Gate 1 supports GGUF only; other formats require their own contract.
        string validatedFormat = ModelInspectionContractValidation.RequireText(
            format,
            nameof(format));
        if (!string.Equals(validatedFormat, "GGUF", StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Configuration format must be GGUF.",
                nameof(format));
        }

        if (ggufVersion.HasValue && ggufVersion.Value == 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(ggufVersion),
                ggufVersion,
                "An available GGUF version must be positive.");
        }

        Format = validatedFormat;
        GgufVersion = ggufVersion;
        ModelName = ModelInspectionContractValidation.RequireOptionalText(
            modelName,
            nameof(modelName));
        Architecture = ModelInspectionContractValidation.RequireOptionalText(
            architecture,
            nameof(architecture));
        FileType = ModelInspectionContractValidation.RequireOptionalNonNegative(
            fileType,
            nameof(fileType));
        QuantisationVersion =
            ModelInspectionContractValidation.RequireOptionalNonNegative(
                quantisationVersion,
                nameof(quantisationVersion));
        DeclaredContextLength =
            ModelInspectionContractValidation.RequireOptionalPositive(
                declaredContextLength,
                nameof(declaredContextLength));
        EmbeddingSize = ModelInspectionContractValidation.RequireOptionalPositive(
            embeddingSize,
            nameof(embeddingSize));
        LayerCount = ModelInspectionContractValidation.RequireOptionalPositive(
            layerCount,
            nameof(layerCount));
        AttentionHeadCount =
            ModelInspectionContractValidation.RequireOptionalPositive(
                attentionHeadCount,
                nameof(attentionHeadCount));
        KvHeadCount = ModelInspectionContractValidation.RequireOptionalPositive(
            kvHeadCount,
            nameof(kvHeadCount));
        ParameterCount = ModelInspectionContractValidation.RequireOptionalPositive(
            parameterCount,
            nameof(parameterCount));
    }

    internal string Format { get; }

    internal uint? GgufVersion { get; }

    internal string? ModelName { get; }

    internal string? Architecture { get; }

    internal int? FileType { get; }

    internal int? QuantisationVersion { get; }

    internal ulong? DeclaredContextLength { get; }

    internal ulong? EmbeddingSize { get; }

    internal int? LayerCount { get; }

    internal int? AttentionHeadCount { get; }

    internal int? KvHeadCount { get; }

    internal ulong? ParameterCount { get; }
}
