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
    PromptFailure? Failure);

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

public interface IPromptRouteSession : IAsyncDisposable
{
    PromptRouteCapability Capability { get; }

    Task<PromptTurnResult> GenerateAsync(
        string prompt,
        int requestedNewTokens,
        CancellationToken cancellationToken);

    Task StopAsync(CancellationToken cancellationToken);

    Task CancelAsync(CancellationToken cancellationToken);

    Task CloseAsync(CancellationToken cancellationToken);
}

public interface IPromptRouteAdapter
{
    PromptRouteCapability Capability { get; }
}
