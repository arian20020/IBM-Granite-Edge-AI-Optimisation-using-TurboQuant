namespace GraniteEdgeAI.ModelInspection.WorkerClient;

/// <summary>
/// Defines the one package-relative production worker location. Production
/// composition cannot replace this value with a caller-selected executable.
/// </summary>
internal static class WorkerInstallationLayout
{
    internal const string WorkerExecutableRelativePath =
        @"ModelInspection\Worker\GraniteEdgeAI.ModelInspection.Worker.exe";
}
