namespace GraniteEdgeAI.ModelInspection.Worker;

/// <summary>
/// Provides an atomic one-winner gate for the terminal protocol message.
/// </summary>
internal sealed class WorkerTerminalCoordinator
{
    private int _terminalStarted;

    internal bool TryBeginTerminal() =>
        Interlocked.CompareExchange(
            ref _terminalStarted,
            value: 1,
            comparand: 0) == 0;
}
