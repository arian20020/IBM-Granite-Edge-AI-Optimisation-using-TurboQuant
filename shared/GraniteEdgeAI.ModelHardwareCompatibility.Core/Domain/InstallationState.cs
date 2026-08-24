namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;

/// <summary>
/// What the machine reports about a declared configuration. Verified means a
/// check ran and passed, not that a file was found on disk.
/// </summary>
internal enum InstallationState
{
    Unknown = 0,
    NotInstalled,
    InstalledAndVerified,

    /// <summary>
    /// Installed, verified, and the user has explicitly opted in to an
    /// experimental route. Opt-in is required because experimental routes may
    /// produce wrong output rather than merely failing.
    /// </summary>
    VerifiedAndOptedIn
}
