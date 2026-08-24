using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Capabilities;

/// <summary>
/// Turns a declared support level and an observed installation state into a
/// single answer.
///
/// Only three pairings admit anything. Everything else is unsupported, which is
/// what makes the absence of a claim mean "no" rather than "probably fine" — and
/// it is why a new enum member on either side lands on the safe answer without
/// anyone having to remember to handle it.
/// </summary>
internal static class SupportMatrixResolver
{
    internal static SupportAvailability Resolve(
        SupportLevel level,
        InstallationState installation) =>
        (level, installation) switch
        {
            (SupportLevel.DeclaredSupported, InstallationState.InstalledAndVerified) =>
                SupportAvailability.Available,

            (SupportLevel.DeclaredSupported, InstallationState.NotInstalled) =>
                SupportAvailability.Unavailable,

            // Opt-in is required, not merely installation: an experimental route
            // may produce wrong output rather than simply failing to start.
            (SupportLevel.Experimental, InstallationState.VerifiedAndOptedIn) =>
                SupportAvailability.ExperimentalAvailable,

            _ => SupportAvailability.Unsupported
        };
}
