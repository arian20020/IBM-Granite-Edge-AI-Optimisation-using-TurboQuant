using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Contracts;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Capabilities;

/// <summary>
/// Projects machine facts onto the matrix, yielding one installation state per
/// entry.
///
/// Pure by design: keeping the projection free of I/O is what lets the
/// generator's admission rules be exercised without a machine, and it keeps the
/// rule "presence is not verification" in one readable place.
/// </summary>
internal static class CapabilityProjection
{
    internal static IReadOnlyDictionary<string, InstallationState> Project(
        SupportMatrix matrix,
        HardwareFacts facts,
        IReadOnlySet<string> optedInExperimentalEntryIds)
    {
        ArgumentNullException.ThrowIfNull(matrix);
        ArgumentNullException.ThrowIfNull(facts);
        ArgumentNullException.ThrowIfNull(optedInExperimentalEntryIds);

        Dictionary<string, InstallationState> projected = [];

        foreach (CompatibilitySupportEntry entry in matrix.Entries)
        {
            // Both must hold. A device the machine has, whose backend nothing has
            // verified, would fail at launch rather than at planning — the user
            // would be offered something that cannot start.
            bool runnable =
                facts.PresentDevices.Contains(entry.Device)
                && facts.VerifiedBackends.Contains(entry.Backend);

            if (!runnable)
            {
                projected[entry.EntryId] = InstallationState.NotInstalled;
                continue;
            }

            // Opting in to something that needs no opt-in must not upgrade it
            // past the check its own support level demands.
            projected[entry.EntryId] =
                entry.Level == SupportLevel.Experimental
                && optedInExperimentalEntryIds.Contains(entry.EntryId)
                    ? InstallationState.VerifiedAndOptedIn
                    : InstallationState.InstalledAndVerified;
        }

        return projected;
    }
}
