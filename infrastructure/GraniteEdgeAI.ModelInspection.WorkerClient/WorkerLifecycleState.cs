namespace GraniteEdgeAI.ModelInspection.WorkerClient;

/// <summary>
/// Names each observable phase of one WorkerClient session. Later process code
/// uses this vocabulary to enforce legal transitions without exposing Process
/// objects, native handles, or raw protocol lines.
/// </summary>
public enum WorkerLifecycleState
{
    NotStarted,
    ResolvingExecutable,
    CreatingContainment,
    Starting,
    AwaitingHello,
    Ready,
    StartSent,
    Running,
    CancellationRequested,
    TerminalReceived,
    Exited,
    CleaningUp,
    Completed,
    Failed,
    Disposed
}
