using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Contracts;

/// <summary>
/// One run's immutable outcome.
///
/// Only a completed run carries an assessment. A cancelled or failed run must
/// not retain one: partial work that survived into a result would be read as a
/// conclusion about whether the model runs, which is precisely the claim the run
/// failed to make.
///
/// Recorded deviation from spec section 7. That section lists a separate
/// <c>Failure?</c> member alongside the findings. This carries the failure in
/// <see cref="Findings"/> instead, as a code with Blocking severity, because a
/// parallel member would give a reader two places to look for the same fact and
/// two ways for them to disagree. Every failed and not-established run is
/// required to carry at least one finding, so no failure can go unexplained.
/// </summary>
internal sealed record CompatibilityRunResult
{
    private CompatibilityRunResult(
        CompatibilityRunId runId,
        CompatibilityRunOutcome outcome,
        CompatibilityAssessment? assessment,
        IReadOnlyList<CompatibilityFinding> findings,
        IReadOnlyList<PolicyIdentity> policyIdentities,
        DateTimeOffset startedAtUtc,
        DateTimeOffset completedAtUtc)
    {
        RunId = runId;
        Outcome = outcome;
        Assessment = assessment;
        Findings = findings;
        PolicyIdentities = policyIdentities;
        StartedAtUtc = startedAtUtc;
        CompletedAtUtc = completedAtUtc;
    }

    internal CompatibilityRunId RunId { get; }

    internal CompatibilityRunOutcome Outcome { get; }

    /// <summary>Present only when the outcome is Completed.</summary>
    internal CompatibilityAssessment? Assessment { get; }

    internal IReadOnlyList<CompatibilityFinding> Findings { get; }

    internal IReadOnlyList<PolicyIdentity> PolicyIdentities { get; }

    internal DateTimeOffset StartedAtUtc { get; }

    internal DateTimeOffset CompletedAtUtc { get; }

    internal static CompatibilityRunResult Completed(
        CompatibilityRunId runId,
        CompatibilityAssessment assessment,
        IReadOnlyList<CompatibilityFinding> findings,
        IReadOnlyList<PolicyIdentity> policyIdentities,
        DateTimeOffset startedAtUtc,
        DateTimeOffset completedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(assessment);

        return Build(
            runId, CompatibilityRunOutcome.Completed, assessment,
            findings, policyIdentities, startedAtUtc, completedAtUtc);
    }

    internal static CompatibilityRunResult NotEstablished(
        CompatibilityRunId runId,
        IReadOnlyList<CompatibilityFinding> findings,
        IReadOnlyList<PolicyIdentity> policyIdentities,
        DateTimeOffset startedAtUtc,
        DateTimeOffset completedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(findings);

        // Screen 06 must list the exact missing evidence, so a silent
        // not-established would leave the user nothing to act on.
        if (findings.Count == 0)
        {
            throw new ArgumentException(
                "A not-established run must name what was missing.", nameof(findings));
        }

        return Build(
            runId, CompatibilityRunOutcome.NotEstablished, assessment: null,
            findings, policyIdentities, startedAtUtc, completedAtUtc);
    }

    internal static CompatibilityRunResult Failed(
        CompatibilityRunId runId,
        IReadOnlyList<CompatibilityFinding> findings,
        IReadOnlyList<PolicyIdentity> policyIdentities,
        DateTimeOffset startedAtUtc,
        DateTimeOffset completedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(findings);

        if (findings.Count == 0)
        {
            throw new ArgumentException(
                "A failure nobody can explain cannot be shown to a user or acted on.",
                nameof(findings));
        }

        return Build(
            runId, CompatibilityRunOutcome.Failed, assessment: null,
            findings, policyIdentities, startedAtUtc, completedAtUtc);
    }

    internal static CompatibilityRunResult Cancelled(
        CompatibilityRunId runId,
        IReadOnlyList<CompatibilityFinding> findings,
        IReadOnlyList<PolicyIdentity> policyIdentities,
        DateTimeOffset startedAtUtc,
        DateTimeOffset completedAtUtc) =>
        Build(
            runId, CompatibilityRunOutcome.Cancelled, assessment: null,
            findings, policyIdentities, startedAtUtc, completedAtUtc);

    private static CompatibilityRunResult Build(
        CompatibilityRunId runId,
        CompatibilityRunOutcome outcome,
        CompatibilityAssessment? assessment,
        IReadOnlyList<CompatibilityFinding> findings,
        IReadOnlyList<PolicyIdentity> policyIdentities,
        DateTimeOffset startedAtUtc,
        DateTimeOffset completedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(findings);
        ArgumentNullException.ThrowIfNull(policyIdentities);

        // A record struct cannot forbid default construction, so the factory's
        // empty-value rejection is not enough on its own. Every defaulted id
        // compares equal to every other, so a result carrying one would match any
        // other defaulted run — the confusion stale-run rejection exists to stop.
        if (runId.IsEmpty)
        {
            throw new ArgumentException(
                "A result must carry a run id that came from a factory; a defaulted "
                + "id matches every other defaulted id.",
                nameof(runId));
        }

        if (policyIdentities.Count == 0)
        {
            throw new ArgumentException(
                "Every figure rests on a versioned policy; a result naming none "
                + "cannot be audited.",
                nameof(policyIdentities));
        }

        if (completedAtUtc < startedAtUtc)
        {
            throw new ArgumentOutOfRangeException(
                nameof(completedAtUtc), "A run cannot complete before it started.");
        }

        return new CompatibilityRunResult(
            runId,
            outcome,
            assessment,
            [.. findings],
            [.. policyIdentities],
            startedAtUtc.ToUniversalTime(),
            completedAtUtc.ToUniversalTime());
    }
}
