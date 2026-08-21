using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace GraniteEdgeAI.Features.OpenVinoRoute;

internal sealed record OpenVinoSessionDescriptor(
    Guid InspectionRunId,
    string PackagePath,
    string PackageManifestDigest,
    string ModelSha256,
    long ModelLengthBytes);

public enum OpenVinoPromptEventKind
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

public enum OpenVinoTurnStatus
{
    Completed,
    Stopped,
    Failed
}

public sealed record OpenVinoPromptFailure(
    string SupportCode,
    string Message,
    string RecoveryAction);

public sealed record OpenVinoPromptEvent(
    OpenVinoPromptEventKind Kind,
    Guid OperationId,
    Guid SessionId,
    Guid? TurnId,
    string? Text,
    OpenVinoPromptFailure? Failure,
    string? RequestedDevice,
    IReadOnlyList<string> ActualExecutionDevices);

public sealed record OpenVinoTurnResult(
    OpenVinoTurnStatus Status,
    string Text,
    long PromptTokenCount,
    long GeneratedTokenCount,
    OpenVinoPromptFailure? Failure);

/// <summary>Owns one app-visible OpenVINO prompt session.</summary>
public sealed class OpenVinoRouteSession : IAsyncDisposable
{
    private readonly OpenVinoPromptAdapter adapter;

    private OpenVinoRouteSession(OpenVinoPromptAdapter adapter)
    {
        this.adapter = adapter;
    }

    public OpenVinoRouteSnapshot Snapshot => adapter.Snapshot;

    internal static async Task<OpenVinoRouteSession> StartAsync(
        IOpenVinoPromptChannelFactory channelFactory,
        OpenVinoRouteStateMachine stateMachine,
        OpenVinoSessionDescriptor descriptor,
        Action<OpenVinoPromptEvent> eventSink,
        CancellationToken cancellationToken)
    {
        OpenVinoPromptAdapter adapter = await OpenVinoPromptAdapter.CreateAsync(
            channelFactory,
            stateMachine,
            descriptor,
            eventSink,
            cancellationToken).ConfigureAwait(false);
        return new OpenVinoRouteSession(adapter);
    }

    public Task<OpenVinoTurnResult> GenerateAsync(
        string prompt,
        int requestedNewTokens,
        CancellationToken cancellationToken) =>
        adapter.GenerateAsync(prompt, requestedNewTokens, cancellationToken);

    public Task StopAsync(CancellationToken cancellationToken) =>
        adapter.StopAsync(cancellationToken);

    public Task CancelAsync(CancellationToken cancellationToken) =>
        adapter.CancelAsync(cancellationToken);

    public Task CloseAsync(CancellationToken cancellationToken) =>
        adapter.CloseAsync(cancellationToken);

    public ValueTask DisposeAsync() => adapter.DisposeAsync();
}
