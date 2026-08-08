namespace GraniteEdgeAI.ModelInspection.LlamaSharp;

/// <summary>
/// Records the exact managed/native application runtime selected by ADR-001.
/// </summary>
public static class PinnedApplicationRuntime
{
    /// <summary>
    /// Gets the managed wrapper package name.
    /// </summary>
    public const string ManagedPackageName = "LLamaSharp";

    /// <summary>
    /// Gets the exact managed wrapper package version.
    /// </summary>
    public const string ManagedPackageVersion = "0.27.0";

    /// <summary>
    /// Gets the published CPU backend package name.
    /// </summary>
    public const string BackendPackageName = "LLamaSharp.Backend.Cpu";

    /// <summary>
    /// Gets the exact published CPU backend package version.
    /// </summary>
    public const string BackendPackageVersion = "0.27.0";

    /// <summary>
    /// Gets the LLamaSharp source tag corresponding to the selected packages.
    /// </summary>
    public const string LlamaSharpSourceTag = "v0.27.0";

    /// <summary>
    /// Gets the LLamaSharp release commit corresponding to the selected tag.
    /// </summary>
    public const string LlamaSharpReleaseCommit =
        "7cbbc45e421d55794d5050d126e0b96511007007";

    /// <summary>
    /// Gets the exact llama.cpp revision mapped by LLamaSharp 0.27.0.
    /// </summary>
    public const string ExpectedLlamaCppCommit =
        "3f7c29d318e317b63f54c558bc69803963d7d88c";

    /// <summary>
    /// Gets the intended first production runtime identifier.
    /// </summary>
    public const string IntendedProductionRuntimeIdentifier = "win-x64";

    /// <summary>
    /// Gets the separately retained standalone research runtime tag.
    /// </summary>
    public const string ResearchRuntimeTag = "b9870";

    /// <summary>
    /// Gets the separately retained standalone research runtime commit.
    /// </summary>
    public const string ResearchRuntimeCommit =
        "2d973636e292ee6f75fadcf08d29cb33511f509f";
}
