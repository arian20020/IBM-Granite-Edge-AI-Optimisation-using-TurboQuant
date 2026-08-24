namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;

/// <summary>
/// The only way a plan comes into existence.
///
/// Every plan gets a fresh id. There is no amendment: a changed input produces
/// a new plan the user re-confirms, because the alternative is an executor
/// running something subtly different from what was agreed to while carrying
/// the id of what was.
///
/// The issuer is pure. It takes the time as an argument rather than reading a
/// clock, so the same inputs produce the same plan in a test and in production,
/// and so nothing here depends on when it happened to run.
/// </summary>
public static class OptimizationPlanIssuer
{
    /// <summary>
    /// Issues a plan for a candidate that was actually selected.
    ///
    /// The route of the candidate and the route of the capability snapshot must
    /// agree. They come from different places, and a mismatch means the
    /// candidate was planned against evidence describing a different executor -
    /// which is exactly the kind of thing that produces a confident plan nobody
    /// can run.
    /// </summary>
    public static OptimizationExecutionPlan Issue(
        OptimizationSelection selection,
        OptimizationCapabilitySnapshot capabilitySnapshot,
        OptimizationWorkload workload,
        OptimizationJourneyBinding binding,
        DateTimeOffset createdAtUtc)
    {
        ArgumentNullException.ThrowIfNull(selection);
        ArgumentNullException.ThrowIfNull(capabilitySnapshot);
        ArgumentNullException.ThrowIfNull(workload);
        ArgumentNullException.ThrowIfNull(binding);

        if (selection.Candidate.Route != capabilitySnapshot.Route)
        {
            throw new ArgumentException(
                $"The candidate is a {selection.Candidate.Route} configuration but the "
                + $"capability snapshot describes {capabilitySnapshot.Route}. A plan "
                + "bound to evidence about a different executor could not be verified "
                + "by either.",
                nameof(capabilitySnapshot));
        }

        if (createdAtUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException(
                "Plan timestamps are UTC. A local offset would make two plans issued "
                + "at the same instant compare as different.",
                nameof(createdAtUtc));
        }

        return new OptimizationExecutionPlan(
            Guid.NewGuid(),
            binding,
            capabilitySnapshot,
            workload,
            selection.Candidate,
            selection.Preference,
            selection.SharedWithAdjacentBand,
            OptimizationCanonicalizer.ConfigurationSha256(selection.Candidate),
            createdAtUtc);
    }
}
