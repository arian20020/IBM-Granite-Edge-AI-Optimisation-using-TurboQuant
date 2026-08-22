using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Ports;

/// <summary>
/// The outcome of atomically claiming the paired handoffs.
///
/// This is the one place in the feature where strings originating outside it
/// enter a C1 type, so the identities are validated rather than merely trusted:
/// section 14 forbids a path, filename or model name entering any C1 type, and an
/// adapter written by another team is exactly where one would arrive. The check
/// is a shape check, not a guess at meaning — anything resembling a path
/// separator, a drive specifier or a file extension is refused at the boundary
/// rather than being carried inward and allowlisted later.
/// </summary>
internal sealed record HandoffClaim
{
    private static readonly char[] PathLike = ['\\', '/', ':'];

    private HandoffClaim(
        bool isClaimed,
        string modelInspectionRunId,
        string productHardwareRunId,
        PortUnavailableReason reason)
    {
        IsClaimed = isClaimed;
        ModelInspectionRunId = modelInspectionRunId;
        ProductHardwareRunId = productHardwareRunId;
        Reason = reason;
    }

    internal bool IsClaimed { get; }

    internal string ModelInspectionRunId { get; }

    internal string ProductHardwareRunId { get; }

    internal PortUnavailableReason Reason { get; }

    internal static HandoffClaim Claimed(
        string modelInspectionRunId,
        string productHardwareRunId)
    {
        RequireIdentity(modelInspectionRunId, nameof(modelInspectionRunId));
        RequireIdentity(productHardwareRunId, nameof(productHardwareRunId));

        return new HandoffClaim(
            true, modelInspectionRunId, productHardwareRunId, PortUnavailableReason.None);
    }

    internal static HandoffClaim Refused(PortUnavailableReason reason)
    {
        if (reason == PortUnavailableReason.None)
        {
            throw new ArgumentException(
                "A refused claim must name why.", nameof(reason));
        }

        return new HandoffClaim(false, string.Empty, string.Empty, reason);
    }

    private static void RequireIdentity(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(
                "A claimed handoff must carry the identity it claimed.", parameterName);
        }

        if (value.IndexOfAny(PathLike) >= 0 || value.Contains('.', StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "A run identity must not look like a path or filename; section 14 "
                + "forbids one entering this feature at all.",
                parameterName);
        }
    }
}

/// <summary>
/// Owns which handoffs are current, claims them atomically, rolls back, and
/// commits exactly one transfer when the user confirms.
///
/// The claim is atomic because a run that read one handoff and then had the other
/// replaced underneath it would produce an assessment about two different states
/// of the world.
/// </summary>
internal interface ICompatibilityInputGateway
{
    HandoffClaim Claim();

    /// <summary>Safe to call whether or not a claim succeeded.</summary>
    void Rollback();

    /// <summary>
    /// Commits the transfer for a run the user has confirmed.
    ///
    /// Two preconditions the type cannot enforce on its own.
    ///
    /// The id must be a real one. CompatibilityRunId is a struct, so default(T)
    /// sidesteps the guard in From and arrives as an empty Guid; an
    /// implementation that committed against it would commit against no run at
    /// all. Reject it - IsEmpty says so - rather than treating it as a run.
    ///
    /// Rollback and Commit are not ordered by this interface. A run always
    /// rolls back on the way out, including when it succeeded, because the
    /// transfer commits only when the user confirms a choice on a later screen.
    /// So a commit legitimately follows the rollback of the run that produced
    /// its result, and an implementation that treated rollback as final would
    /// make confirmation impossible.
    /// </summary>
    bool Commit(CompatibilityRunId runId);
}
