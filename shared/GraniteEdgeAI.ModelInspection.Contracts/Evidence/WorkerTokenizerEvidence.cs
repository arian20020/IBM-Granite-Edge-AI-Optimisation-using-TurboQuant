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
        WorkerProtocolValidation.RequireOptionalPositive(
            VocabularyCount,
            nameof(VocabularyCount));
        WorkerProtocolValidation.RequireOptionalText(
            VocabularyType,
            nameof(VocabularyType));
        WorkerProtocolValidation.Require(
            TokenizerSmokePassed.HasValue || !TokenizerSmokeTokenCount.HasValue,
            nameof(TokenizerSmokeTokenCount),
            "requires a smoke-check result when present");
        WorkerProtocolValidation.RequireOptionalNonNegative(
            TokenizerSmokeTokenCount,
            nameof(TokenizerSmokeTokenCount));
        WorkerProtocolValidation.Require(
            TokenizerSmokePassed is not true || TokenizerSmokeTokenCount is > 0,
            nameof(TokenizerSmokeTokenCount),
            "must be positive when the smoke check succeeds");

        IReadOnlyDictionary<string, int> tokenIds =
            WorkerProtocolValidation.RequireNotNull(
                KnownSpecialTokenIds,
                nameof(KnownSpecialTokenIds));
        foreach ((string key, int value) in tokenIds)
        {
            WorkerProtocolValidation.Require(
                !string.IsNullOrWhiteSpace(key),
                nameof(KnownSpecialTokenIds),
                "must use non-whitespace keys");
            WorkerProtocolValidation.Require(
                value >= 0,
                nameof(KnownSpecialTokenIds),
                "must use non-negative token identifiers");
            WorkerProtocolValidation.Require(
                !VocabularyCount.HasValue || value < VocabularyCount.Value,
                nameof(KnownSpecialTokenIds),
                "must use token identifiers inside the reported vocabulary");
        }
    }
}
