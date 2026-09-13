using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace GraniteEdgeAI.Features.ModelInspection.Contracts;

/// <summary>
/// Holds tokenizer metadata and the bounded smoke-check result. Missing runtime
/// values remain null rather than being guessed from model names.
/// </summary>
internal sealed record ModelInspectionTokenizerEvidence
{
    /// <summary>
    /// creates immutable tokenizer evidence and copies all token identifiers
    /// </summary>
    internal ModelInspectionTokenizerEvidence(
        string? tokenizerModel,
        int? vocabularyCount,
        string? vocabularyType,
        bool? tokenizerSmokePassed,
        int? tokenizerSmokeTokenCount,
        IReadOnlyDictionary<string, int> knownSpecialTokenIds)
    {
        TokenizerModel = ModelInspectionContractValidation.RequireOptionalText(
            tokenizerModel,
            nameof(tokenizerModel));
        VocabularyCount = ModelInspectionContractValidation.RequireOptionalPositive(
            vocabularyCount,
            nameof(vocabularyCount));
        VocabularyType = ModelInspectionContractValidation.RequireOptionalText(
            vocabularyType,
            nameof(vocabularyType));

        // a token count has meaning only when a smoke check actually ran
        if (!tokenizerSmokePassed.HasValue &&
            tokenizerSmokeTokenCount.HasValue)
        {
            throw new ArgumentException(
                "Tokenizer token count requires an available smoke-check result.",
                nameof(tokenizerSmokeTokenCount));
        }

        if (tokenizerSmokeTokenCount.HasValue &&
            tokenizerSmokeTokenCount.Value < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(tokenizerSmokeTokenCount),
                tokenizerSmokeTokenCount,
                "Tokenizer token count must not be negative.");
        }

        if (tokenizerSmokePassed is true &&
            tokenizerSmokeTokenCount is not > 0)
        {
            throw new ArgumentException(
                "A successful tokenizer smoke check requires a positive token count.",
                nameof(tokenizerSmokeTokenCount));
        }

        ArgumentNullException.ThrowIfNull(knownSpecialTokenIds);

        // use ordinal keys and copy the source so callers cannot mutate evidence
        Dictionary<string, int> tokenIdCopy = new(StringComparer.Ordinal);
        foreach ((string key, int value) in knownSpecialTokenIds)
        {
            string validatedKey = ModelInspectionContractValidation.RequireText(
                key,
                nameof(knownSpecialTokenIds));
            if (value < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(knownSpecialTokenIds),
                    "Special token identifiers must not be negative.");
            }

            if (!tokenIdCopy.TryAdd(validatedKey, value))
            {
                throw new ArgumentException(
                    "Special token identifiers must use unique ordinal keys.",
                    nameof(knownSpecialTokenIds));
            }
        }

        TokenizerSmokePassed = tokenizerSmokePassed;
        TokenizerSmokeTokenCount = tokenizerSmokeTokenCount;
        KnownSpecialTokenIds =
            new ReadOnlyDictionary<string, int>(tokenIdCopy);
    }

    internal string? TokenizerModel { get; }

    internal int? VocabularyCount { get; }

    internal string? VocabularyType { get; }

    internal bool? TokenizerSmokePassed { get; }

    internal int? TokenizerSmokeTokenCount { get; }

    internal IReadOnlyDictionary<string, int> KnownSpecialTokenIds { get; }
}
