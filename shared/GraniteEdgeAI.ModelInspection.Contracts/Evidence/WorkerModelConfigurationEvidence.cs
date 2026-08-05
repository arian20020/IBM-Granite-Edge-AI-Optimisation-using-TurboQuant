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
}
