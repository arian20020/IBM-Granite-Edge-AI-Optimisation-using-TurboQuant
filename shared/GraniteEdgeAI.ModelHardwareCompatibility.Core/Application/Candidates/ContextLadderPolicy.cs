using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Candidates;

/// <summary>
/// Builds the ordered set of context lengths a candidate set may use.
/// Automatic extension beyond the model's trained limit is excluded, and the
/// ladder never rises above what the user actually asked for.
/// </summary>
internal static class ContextLadderPolicy
{
    private static readonly int[] StandardRungs =
        [1024, 2048, 4096, 8192, 16384, 32768];

    internal static IReadOnlyList<ContextTokenCount> Build(
        ContextTokenCount preservationTarget,
        ContextTokenCount? baseline,
        int modelLimitTokens,
        int entryMinimumTokens)
    {
        if (modelLimitTokens <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(modelLimitTokens),
                "A model context limit must be positive.");
        }

        if (entryMinimumTokens <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(entryMinimumTokens),
                "A support entry minimum context must be positive.");
        }

        List<ContextTokenCount> ladder = [];
        HashSet<int> seen = [];

        // The preservation target is always offered first, even when it exceeds
        // the model limit: the user asked for it, and admissibility is decided
        // later by support and fit rather than being silently removed here.
        if (seen.Add(preservationTarget.Tokens))
        {
            ladder.Add(preservationTarget);
        }

        if (baseline is { } baselineContext && seen.Add(baselineContext.Tokens))
        {
            ladder.Add(baselineContext);
        }

        // Lower rungs give the user something smaller to fall back to, in
        // descending order so the best remaining option comes first.
        foreach (int rung in StandardRungs.OrderByDescending(rung => rung))
        {
            if (rung > preservationTarget.Tokens ||
                rung > modelLimitTokens ||
                rung < entryMinimumTokens ||
                !seen.Add(rung))
            {
                continue;
            }

            ladder.Add(ContextTokenCount.FromTokens(rung));
        }

        return ladder;
    }
}
