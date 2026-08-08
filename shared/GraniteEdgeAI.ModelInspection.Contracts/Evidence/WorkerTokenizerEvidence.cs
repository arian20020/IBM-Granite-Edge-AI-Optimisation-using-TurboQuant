namespace GraniteEdgeAI.ModelInspection.Contracts;

/// <summary>
/// Summarises tokenizer inspection without returning vocabulary text.
/// </summary>
public sealed record WorkerTokenizerEvidence
{
    public int? VocabularyCount { get; init; }

    public string? VocabularyType { get; init; }

    public bool? TokenizerSmokePassed { get; init; }

    public int? TokenizerSmokeTokenCount { get; init; }

    public IReadOnlyDictionary<string, int> KnownSpecialTokenIds { get; init; } =
        new Dictionary<string, int>(StringComparer.Ordinal);

    /// <summary>
    /// Verifies that the public tokenizer collection contract is preserved.
    /// </summary>
    public void Validate()
    {
        WorkerProtocolValidation.RequireNotNull(
            KnownSpecialTokenIds,
            nameof(KnownSpecialTokenIds));
    }
}
