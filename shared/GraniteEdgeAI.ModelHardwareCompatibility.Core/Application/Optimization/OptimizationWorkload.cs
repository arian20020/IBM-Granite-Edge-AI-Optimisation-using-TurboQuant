using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;

/// <summary>
/// What the user intends to do with the model, expressed as the floors a
/// candidate has to clear and the context lengths worth considering.
///
/// It is an input rather than a derivation. How long a conversation the user
/// expects, and how much quality they can tolerate, are facts about their
/// intent and the planner cannot infer them from a file.
/// </summary>
public sealed record OptimizationWorkload
{
    private OptimizationWorkload(
        string workloadId,
        int minimumContextTokens,
        OptimizationAssessment minimumQuality,
        IReadOnlyList<ContextTokenCount> candidateContexts)
    {
        WorkloadId = workloadId;
        MinimumContextTokens = minimumContextTokens;
        MinimumQuality = minimumQuality;
        CandidateContexts = candidateContexts;
    }

    /// <summary>Bound into the plan, so a result can be traced to what it was planned for.</summary>
    public string WorkloadId { get; }

    public int MinimumContextTokens { get; }

    /// <summary>
    /// The hard floor, not the preferred level. A candidate below this is not
    /// offered at any slider position, because Maximum efficiency still has to
    /// produce something usable.
    /// </summary>
    public OptimizationAssessment MinimumQuality { get; }

    /// <summary>Context lengths worth generating candidates at.</summary>
    public IReadOnlyList<ContextTokenCount> CandidateContexts { get; }

    public static OptimizationWorkload Create(
        string workloadId,
        int minimumContextTokens,
        OptimizationAssessment minimumQuality,
        IReadOnlyList<ContextTokenCount> candidateContexts)
    {
        ArgumentNullException.ThrowIfNull(candidateContexts);

        OptimizationIdentifier.Require(workloadId, nameof(workloadId), "A workload");

        if (minimumQuality == OptimizationAssessment.Unknown)
        {
            throw new ArgumentException(
                "A workload with no quality floor would admit anything, including a "
                + "configuration nobody could use.",
                nameof(minimumQuality));
        }

        if (minimumContextTokens < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(minimumContextTokens),
                minimumContextTokens,
                "A workload that needs no context is not a workload.");
        }

        if (candidateContexts.Count == 0)
        {
            throw new ArgumentException(
                "With no context lengths to consider, generation produces nothing "
                + "and reports it as a machine that fits nothing.",
                nameof(candidateContexts));
        }

        return new OptimizationWorkload(
            workloadId, minimumContextTokens, minimumQuality, [.. candidateContexts]);
    }
}
