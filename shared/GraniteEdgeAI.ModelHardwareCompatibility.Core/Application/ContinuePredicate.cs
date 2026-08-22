namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application;

/// <summary>
/// How the preceding hardware step ended. Unknown is the zero value so an
/// unread outcome cannot enable anything.
/// </summary>
internal enum HardwareOutcome
{
    Unknown = 0,
    Completed,
    CompletedWithWarnings,
    Failed
}

/// <summary>
/// The six conditions spec section 13 requires.
///
/// Each is a question that has been answered. An unanswered one is false,
/// because the predicate must never read "we did not check" as "it is fine" —
/// that is the difference between a step the user cannot complete being disabled
/// and being offered.
/// </summary>
internal sealed record ContinueConditions(
    bool HasCurrentValidModelHandoff,
    bool HasCurrentUsableHardwareHandoff,
    bool HasRegisteredAvailableRoute,
    bool HandoffsBoundToCurrentIdentities,

    /// <summary>
    /// False when the model handoff is stale, superseded, expired, consumed,
    /// ambiguously reissued, or associated with a failed rollback.
    /// </summary>
    bool ModelHandoffIsFresh,

    /// <summary>
    /// False when the navigation transaction is pending, failed, rolled back,
    /// duplicated or ambiguous.
    /// </summary>
    bool NavigationTransactionCompleted);

/// <summary>
/// Decides whether Continue is enabled.
///
/// Every condition is a conjunct: any false or unknown answer leaves Continue
/// visible-disabled rather than hidden, so the user can see the step exists and
/// be told why it is not yet available. A hidden step looks like a step that
/// never existed, which is a different and misleading claim.
/// </summary>
internal static class ContinuePredicate
{
    internal static bool IsEnabled(HardwareOutcome outcome, ContinueConditions conditions)
    {
        ArgumentNullException.ThrowIfNull(conditions);

        if (outcome is not (HardwareOutcome.Completed
            or HardwareOutcome.CompletedWithWarnings))
        {
            return false;
        }

        return conditions.HasCurrentValidModelHandoff
            && conditions.HasCurrentUsableHardwareHandoff
            && conditions.HasRegisteredAvailableRoute
            && conditions.HandoffsBoundToCurrentIdentities
            && conditions.ModelHandoffIsFresh
            && conditions.NavigationTransactionCompleted;
    }
}
