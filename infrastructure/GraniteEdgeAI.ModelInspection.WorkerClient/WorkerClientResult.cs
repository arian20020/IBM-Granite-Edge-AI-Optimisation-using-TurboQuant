using GraniteEdgeAI.ModelInspection.Contracts;

namespace GraniteEdgeAI.ModelInspection.WorkerClient;

/// <summary>
/// Represents the single unambiguous outcome of one worker process execution.
/// A result contains either one trusted worker terminal message or one
/// application-side infrastructure failure, never both.
/// </summary>
public sealed record WorkerClientResult
{
    /// <summary>
    /// Creates an owned result snapshot. Secondary diagnostics are copied so a
    /// caller cannot mutate the recorded execution after construction.
    /// </summary>
    public WorkerClientResult(
        WorkerCompletedMessage? TerminalMessage,
        WorkerClientFailure? Failure,
        int? ExitCode,
        bool ForcedTermination,
        bool StandardErrorTruncated,
        string RetainedStandardError,
        IReadOnlyList<string> SecondaryDiagnostics,
        bool StandardErrorInvalidUtf8Detected = false)
    {
        ArgumentNullException.ThrowIfNull(RetainedStandardError);
        ArgumentNullException.ThrowIfNull(SecondaryDiagnostics);

        this.TerminalMessage = TerminalMessage;
        this.Failure = Failure;
        this.ExitCode = ExitCode;
        this.ForcedTermination = ForcedTermination;
        this.StandardErrorTruncated = StandardErrorTruncated;
        this.RetainedStandardError = RetainedStandardError;
        this.SecondaryDiagnostics = Array.AsReadOnly(
            SecondaryDiagnostics.ToArray());
        this.StandardErrorInvalidUtf8Detected =
            StandardErrorInvalidUtf8Detected;
    }

    public WorkerCompletedMessage? TerminalMessage { get; }

    public WorkerClientFailure? Failure { get; }

    public int? ExitCode { get; }

    public bool ForcedTermination { get; }

    public bool StandardErrorTruncated { get; }

    /// <summary>
    /// Indicates that at least one invalid UTF-8 sequence was observed anywhere
    /// in the fully drained stderr stream. Invalid bytes are never retained.
    /// </summary>
    public bool StandardErrorInvalidUtf8Detected { get; }

    public string RetainedStandardError { get; }

    public IReadOnlyList<string> SecondaryDiagnostics { get; }

    /// <summary>
    /// Validates the exclusive outcome rules before the result is returned to a
    /// higher application layer.
    /// </summary>
    public void Validate()
    {
        bool hasTerminal = TerminalMessage is not null;
        bool hasFailure = Failure is not null;

        if (hasTerminal == hasFailure)
        {
            throw new InvalidOperationException(
                "Exactly one trusted terminal message or infrastructure failure is required.");
        }

        if (ForcedTermination && hasTerminal)
        {
            throw new InvalidOperationException(
                "Forced termination cannot produce a trusted worker terminal result.");
        }

        TerminalMessage?.Validate();
        Failure?.Validate();
    }
}
