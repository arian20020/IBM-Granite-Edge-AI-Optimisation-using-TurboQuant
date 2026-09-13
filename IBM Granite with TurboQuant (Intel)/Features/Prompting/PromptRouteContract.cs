using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace GraniteEdgeAI.Features.Prompting;

public enum PromptRouteKind
{
    Gguf,
    OpenVino
}

public enum PromptEventKind
{
    Loading,
    SessionReady,
    GeneratingTurn,
    GenerationConfirmed,
    TextDelta,
    StoppingTurn,
    TurnCompleted,
    CancellingSession,
    SessionCompleted,
    Failed,
    Cancelled
}

public enum PromptTurnStatus
{
    Completed,
    Stopped,
    Failed
}

public sealed record PromptFailure(
    string SupportCode,
    string Message,
    string RecoveryAction);

public sealed record PromptEvent(
    PromptEventKind Kind,
    Guid OperationId,
    Guid SessionId,
    Guid? TurnId,
    string? Text,
    PromptFailure? Failure,
    string? RequestedDevice,
    IReadOnlyList<string> ActualExecutionDevices);

public sealed record PromptTurnResult(
    PromptTurnStatus Status,
    string Text,
    long PromptTokenCount,
    long GeneratedTokenCount,
    PromptFailure? Failure,
    bool OutputLimitReached = false);

public sealed record PromptRouteCapability(
    PromptRouteKind Kind,
    string RouteId,
    string ConfigurationId,
    string BackendLabel,
    string Device,
    string Maturity,
    int MaximumContextTokens,
    int DefaultRequestedNewTokens,
    int MaximumRequestedNewTokens);

public interface IPromptRouteActivation
{
    PromptRouteKind Kind { get; }
}

public sealed record PromptRoutePresentation(
    string CapabilitySummary,
    string ExecutionEvidence,
    string BuildEvidence,
    string ReadyAnnouncement);

public sealed record PromptRouteSessionActivation(
    IPromptRouteSession Session,
    PromptRoutePresentation Presentation);

public interface IPromptRouteSession : IAsyncDisposable
{
    PromptRouteCapability Capability { get; }

    Task<PromptTurnResult> GenerateAsync(
        string prompt,
        int requestedNewTokens,
        CancellationToken cancellationToken);

    Task StopAsync(CancellationToken cancellationToken);

    Task StopActiveTurnAsync(
        Guid workerConfirmedTurnId,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(
            workerConfirmedTurnId,
            Guid.Empty);
        return StopAsync(cancellationToken);
    }

    Task CancelAsync(CancellationToken cancellationToken);

    Task CancelActiveTurnAsync(
        Guid workerConfirmedTurnId,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(
            workerConfirmedTurnId,
            Guid.Empty);
        return CancelAsync(cancellationToken);
    }

    Task CloseAsync(CancellationToken cancellationToken);
}

public interface ITransientPromptRouteSession
{
    Task<PromptTurnResult> GenerateTitleAsync(string prompt, CancellationToken cancellationToken);
}

public interface IPromptRouteAdapter
{
    PromptRouteCapability Capability { get; }

    Task<PromptRouteSessionActivation> ActivateAsync(
        IPromptRouteActivation activation,
        Action<PromptEvent> eventSink,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException(
            $"Prompt route '{Capability.RouteId}' does not support session activation.");
}
