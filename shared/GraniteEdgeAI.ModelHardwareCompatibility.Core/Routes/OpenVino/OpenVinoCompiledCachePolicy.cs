namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.OpenVino;

/// <summary>
/// Whether the compiled model blob is cached on disk between runs.
///
/// Part of the configuration because it costs disk and changes first-run
/// behaviour, both of which the plan has to state before a user confirms it.
/// </summary>
public enum OpenVinoCompiledCachePolicy
{
    Unspecified = 0,
    Disabled = 1,
    Enabled = 2
}
