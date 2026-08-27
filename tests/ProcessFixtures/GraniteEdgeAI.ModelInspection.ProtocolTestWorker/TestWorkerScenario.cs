namespace GraniteEdgeAI.ModelInspection.ProtocolTestWorker;

/// <summary>
/// Enumerates deterministic fixture-only behaviours used to exercise the
/// protected worker client. None of these values exist in production projects.
/// </summary>
internal enum TestWorkerScenario
{
    LaunchProbe,
    HealthyControlledFailure,
    NoHello,
    MalformedHello,
    WrongProtocolVersion,
    WrongWorkerId,
    WrongWorkerProcessId,
    WrongRuntimeProfile,
    WrongArchitecture,
    TextBeforeHello,
    InvalidUtf8,
    Utf8Bom,
    OversizedStdoutLine,
    MalformedJson,
    DuplicateJsonProperty,
    WrongRequestId,
    ProgressBeforeStarted,
    NonMonotonicProgress,
    DuplicateTerminal,
    ExitWithoutTerminal,
    CrashBeforeHello,
    CrashAfterHello,
    CrashAfterStart,
    HangBeforeHello,
    HangAfterHello,
    HangAfterStart,
    CooperativeCancellation,
    IgnoreCancellation,
    SpawnChildAndWait,
    ExitRootWithLiveChild,
    FloodStdout,
    FloodStderr,
    TerminalExitMismatch,
    EchoEnvironmentKeys,
    ProbeUnrelatedHandle,
    ObserveParentIdentity,
    ChildProcessWait
}
