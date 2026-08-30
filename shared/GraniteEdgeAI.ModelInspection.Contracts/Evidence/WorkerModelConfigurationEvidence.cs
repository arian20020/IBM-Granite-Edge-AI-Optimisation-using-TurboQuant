namespace GraniteEdgeAI.ModelInspection.Contracts;

/// <summary>
/// Carries lightweight configuration values exposed by the safe native path.
/// Unavailable values remain null and are never guessed from the filename.
/// </summary>
public sealed record WorkerModelConfigurationEvidence
{
    public string? Architecture { get; init; }

    public string? ModelName { get; init; }

    public int? FileType { get; init; }

    public int? QuantisationVersion { get; init; }

    public string? TokenizerModel { get; init; }

    public ulong? DeclaredContextLength { get; init; }

    public ulong? EmbeddingSize { get; init; }

    public int? LayerCount { get; init; }

    public int? AttentionHeadCount { get; init; }

    public int? KvHeadCount { get; init; }

    public ulong? ParameterCount { get; init; }

    /// <summary>
    /// Verifies that available native values are usable and that unavailable
    /// values remain null rather than carrying placeholders.
    /// </summary>
    public void Validate()
    {
        WorkerProtocolValidation.RequireOptionalText(
            Architecture,
            nameof(Architecture));
        WorkerProtocolValidation.RequireOptionalText(
            ModelName,
            nameof(ModelName));
        WorkerProtocolValidation.RequireOptionalNonNegative(
            FileType,
            nameof(FileType));
        WorkerProtocolValidation.RequireOptionalNonNegative(
            QuantisationVersion,
            nameof(QuantisationVersion));
        WorkerProtocolValidation.RequireOptionalText(
            TokenizerModel,
            nameof(TokenizerModel));
        WorkerProtocolValidation.RequireOptionalPositive(
            DeclaredContextLength,
            nameof(DeclaredContextLength));
        WorkerProtocolValidation.RequireOptionalPositive(
            EmbeddingSize,
            nameof(EmbeddingSize));
        WorkerProtocolValidation.RequireOptionalPositive(
            LayerCount,
            nameof(LayerCount));
        WorkerProtocolValidation.RequireOptionalPositive(
            AttentionHeadCount,
            nameof(AttentionHeadCount));
        WorkerProtocolValidation.RequireOptionalPositive(
            KvHeadCount,
            nameof(KvHeadCount));
        WorkerProtocolValidation.RequireOptionalPositive(
            ParameterCount,
            nameof(ParameterCount));
    }
}
