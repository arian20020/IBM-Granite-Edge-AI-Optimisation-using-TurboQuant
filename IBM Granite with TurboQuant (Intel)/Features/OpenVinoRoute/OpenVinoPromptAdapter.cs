using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using GraniteEdgeAI.OpenVino.Contracts;
using GraniteEdgeAI.OpenVino.WorkerClient;

namespace GraniteEdgeAI.Features.OpenVinoRoute;

internal interface IOpenVinoPromptChannelFactory
{
    Task<IOpenVinoPromptChannel> StartAsync(
        StartSessionCommand command,
        CancellationToken cancellationToken);
}

internal interface IOpenVinoPromptChannel : IAsyncDisposable
{
    Task<IOpenVinoEvent> PromptAsync(
        PromptCommand command,
        IProgress<TokenEvent>? progress,
        CancellationToken cancellationToken);

    Task StopAsync(CancellationToken cancellationToken);

    Task CancelAsync(CancellationToken cancellationToken);

    Task CloseAsync(CancellationToken cancellationToken);
}

public sealed class OpenVinoRouteWorkerFailureException : Exception
{
    public OpenVinoRouteWorkerFailureException(
        OpenVinoSupportCode supportCode,
        string privateDiagnostic)
        : base(privateDiagnostic)
    {
        supportCode.Validate();
        SupportCode = supportCode;
    }

    public OpenVinoSupportCode SupportCode { get; }
}

/// <summary>
/// Maps the official worker conversation into bounded route-local prompt
/// state without exposing native diagnostics or filesystem data.
/// </summary>
public sealed class OpenVinoPromptAdapter : IAsyncDisposable
{
    private static readonly IReadOnlyList<string> CpuExecutionDevices = ["CPU"];
    private readonly object stateLock = new();
    private readonly OpenVinoRouteStateMachine stateMachine;
    private readonly IOpenVinoPromptChannel channel;
    private readonly Action<OpenVinoPromptEvent> eventSink;
    private bool disposed;
    private long nextSequence;

    private OpenVinoPromptAdapter(
        OpenVinoRouteStateMachine stateMachine,
        IOpenVinoPromptChannel channel,
        Action<OpenVinoPromptEvent> eventSink)
    {
        this.stateMachine = stateMachine;
        this.channel = channel;
        this.eventSink = eventSink;
    }

    public OpenVinoRouteSnapshot Snapshot => stateMachine.Snapshot;

    internal static async Task<OpenVinoPromptAdapter> CreateAsync(
        IOpenVinoPromptChannelFactory channelFactory,
        OpenVinoRouteStateMachine stateMachine,
        OpenVinoSessionDescriptor descriptor,
        Action<OpenVinoPromptEvent> eventSink,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(channelFactory);
        ArgumentNullException.ThrowIfNull(stateMachine);
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(eventSink);
        ValidateDescriptor(descriptor);

        OpenVinoRouteSnapshot snapshot = stateMachine.Snapshot;
        Guid operationId = snapshot.Identity.OperationId;
        if (!stateMachine.TryBeginLoading(operationId))
        {
            throw new InvalidOperationException(
                "The route is not awaiting an approved configuration.");
        }

        Publish(
            eventSink,
            OpenVinoPromptEventKind.Loading,
            stateMachine.Snapshot,
            turnId: null);
        StartSessionCommand command = new(
            snapshot.Identity.SessionId,
            descriptor.InspectionRunId,
            descriptor.PackagePath,
            descriptor.PackageManifestDigest,
            descriptor.ModelSha256,
            descriptor.ModelLengthBytes,
            new OpenVinoDeviceRequest(OpenVinoRouteCapability.Device),
            new OpenVinoGenerationLimits(
                OpenVinoRouteCapability.MaximumContextTokens,
                OpenVinoRouteCapability.MaximumRequestedNewTokens));
        IOpenVinoPromptChannel channel;
        try
        {
            channel = await channelFactory.StartAsync(command, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            string code = error is OpenVinoRouteWorkerFailureException failure
                ? failure.SupportCode.ToProtocolValue()
                : OpenVinoSupportCode.RuntimeLoadFailed.ToProtocolValue();
            stateMachine.TryFail(operationId, code);
            throw;
        }

        if (!stateMachine.TrySetSessionReady(operationId))
        {
            await channel.DisposeAsync().ConfigureAwait(false);
            throw new InvalidOperationException(
                "The route session became stale while loading.");
        }

        OpenVinoPromptAdapter adapter = new(
            stateMachine,
            channel,
            eventSink);
        adapter.Emit(OpenVinoPromptEventKind.SessionReady, turnId: null,
            requestedDevice: OpenVinoRouteCapability.Device,
            actualExecutionDevices: CpuExecutionDevices);
        return adapter;
    }

    public async Task<OpenVinoTurnResult> GenerateAsync(
        string prompt,
        int requestedNewTokens,
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        ValidatePrompt(prompt, requestedNewTokens);
        Guid operationId = Snapshot.Identity.OperationId;
        if (!stateMachine.TryBeginTurn(operationId, out Guid turnId))
        {
            throw new InvalidOperationException(
                "Only one turn may run in a ready session.");
        }

        lock (stateLock)
        {
            nextSequence = 0;
        }
        Emit(OpenVinoPromptEventKind.GeneratingTurn, turnId);
        StringBuilder text = new();
        var progress = new InlineProgress<TokenEvent>(token =>
            AcceptToken(operationId, turnId, token, text));
        try
        {
            IOpenVinoEvent terminal = await channel.PromptAsync(
                    new PromptCommand(
                        Snapshot.Identity.SessionId,
                        turnId,
                        prompt,
                        requestedNewTokens),
                    progress,
                    cancellationToken)
                .ConfigureAwait(false);

            if (Snapshot.State is OpenVinoRouteState.CancellingSession or
                OpenVinoRouteState.Cancelled)
            {
                throw new OperationCanceledException(
                    "The OpenVINO prompt session was cancelled.");
            }

            if (terminal is TurnFailedEvent turnFailed)
            {
                return Fail(operationId, turnId, turnFailed.SupportCode);
            }

            if (terminal is SessionCancelledEvent)
            {
                stateMachine.TryBeginCancellation(operationId);
                stateMachine.TryMarkCancelled(operationId);
                Emit(OpenVinoPromptEventKind.Cancelled, turnId: null);
                throw new OperationCanceledException(
                    "The OpenVINO prompt session was cancelled.");
            }

            if (terminal is not TurnCompletedEvent completed ||
                completed.SessionId != Snapshot.Identity.SessionId ||
                completed.TurnId != turnId ||
                !stateMachine.TryCompleteTurn(operationId, turnId))
            {
                return Fail(
                    operationId,
                    turnId,
                    OpenVinoSupportCode.RuntimeProtocolFailed);
            }

            OpenVinoTurnStatus status = completed.Disposition ==
                OpenVinoTurnDisposition.Stopped
                ? OpenVinoTurnStatus.Stopped
                : OpenVinoTurnStatus.Completed;
            Emit(OpenVinoPromptEventKind.TurnCompleted, turnId, text.ToString());
            if (!stateMachine.TryReturnToSessionReady(operationId))
            {
                return Fail(
                    operationId,
                    turnId,
                    OpenVinoSupportCode.RuntimeProtocolFailed);
            }
            Emit(OpenVinoPromptEventKind.SessionReady, turnId: null,
                requestedDevice: OpenVinoRouteCapability.Device,
                actualExecutionDevices: CpuExecutionDevices);
            return new OpenVinoTurnResult(
                status,
                text.ToString(),
                completed.PromptTokenCount,
                completed.GeneratedTokenCount,
                Failure: null);
        }
        catch (OpenVinoRouteWorkerFailureException failure)
        {
            return Fail(operationId, turnId, failure.SupportCode);
        }
        catch (OperationCanceledException)
        {
            if (Snapshot.State != OpenVinoRouteState.Cancelled)
            {
                stateMachine.TryBeginCancellation(operationId);
                stateMachine.TryMarkCancelled(operationId);
                Emit(OpenVinoPromptEventKind.Cancelled, turnId: null);
            }

            throw;
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        OpenVinoRouteSnapshot snapshot = Snapshot;
        if (snapshot.ActiveTurnId is not Guid turnId ||
            !stateMachine.TryBeginStopping(snapshot.Identity.OperationId, turnId))
        {
            return;
        }

        Emit(OpenVinoPromptEventKind.StoppingTurn, turnId);
        try
        {
            await channel.StopAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OpenVinoRouteWorkerFailureException failure)
        {
            Fail(snapshot.Identity.OperationId, turnId, failure.SupportCode);
        }
    }

    public async Task CancelAsync(CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        Guid operationId = Snapshot.Identity.OperationId;
        if (!stateMachine.TryBeginCancellation(operationId))
        {
            if (Snapshot.State == OpenVinoRouteState.Cancelled)
            {
                return;
            }

            throw new InvalidOperationException(
                "The route session cannot be cancelled from its current state.");
        }

        Emit(OpenVinoPromptEventKind.CancellingSession, turnId: null);
        try
        {
            await channel.CancelAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OpenVinoRouteWorkerFailureException failure)
            when (failure.SupportCode == OpenVinoSupportCode.OperationCancelled)
        {
            // The protected client reports the owned cancellation as a fixed
            // outcome after it has verified terminal cleanup.
        }

        stateMachine.TryMarkCancelled(operationId);
        Emit(OpenVinoPromptEventKind.Cancelled, turnId: null);
        await DisposeChannelAsync().ConfigureAwait(false);
    }

    public async Task CloseAsync(CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        Guid operationId = Snapshot.Identity.OperationId;
        if (Snapshot.State != OpenVinoRouteState.SessionReady)
        {
            throw new InvalidOperationException(
                "A session can close gracefully only while ready.");
        }

        await channel.CloseAsync(cancellationToken).ConfigureAwait(false);
        if (!stateMachine.TryCompleteSession(operationId))
        {
            throw new InvalidOperationException(
                "The session became stale while closing.");
        }
        Emit(OpenVinoPromptEventKind.SessionCompleted, turnId: null);
        await DisposeChannelAsync().ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        if (disposed)
        {
            return;
        }

        OpenVinoRouteState state = Snapshot.State;
        if (state is not (
                OpenVinoRouteState.SessionCompleted or
                OpenVinoRouteState.Failed or
                OpenVinoRouteState.Cancelled))
        {
            await CancelAsync(CancellationToken.None).ConfigureAwait(false);
            return;
        }

        await DisposeChannelAsync().ConfigureAwait(false);
    }

    private void AcceptToken(
        Guid operationId,
        Guid turnId,
        TokenEvent token,
        StringBuilder text)
    {
        lock (stateLock)
        {
            OpenVinoRouteSnapshot snapshot = Snapshot;
            if (snapshot.Identity.OperationId != operationId ||
                snapshot.ActiveTurnId != turnId ||
                snapshot.State is not (
                    OpenVinoRouteState.GeneratingTurn or
                    OpenVinoRouteState.StoppingTurn) ||
                token.SessionId != snapshot.Identity.SessionId ||
                token.TurnId != turnId)
            {
                return;
            }

            if (token.Sequence != nextSequence)
            {
                throw new OpenVinoRouteWorkerFailureException(
                    OpenVinoSupportCode.RuntimeProtocolFailed,
                    "The worker produced an out-of-order text fragment.");
            }

            checked
            {
                nextSequence++;
            }
            text.Append(token.Text);
            Emit(OpenVinoPromptEventKind.TextDelta, turnId, token.Text);
        }
    }

    private OpenVinoTurnResult Fail(
        Guid operationId,
        Guid turnId,
        OpenVinoSupportCode supportCode)
    {
        OpenVinoPromptFailure failure = MapFailure(supportCode);
        stateMachine.TryFail(operationId, failure.SupportCode);
        Emit(OpenVinoPromptEventKind.Failed, turnId, failure: failure);
        return new OpenVinoTurnResult(
            OpenVinoTurnStatus.Failed,
            string.Empty,
            0,
            0,
            failure);
    }

    private void Emit(
        OpenVinoPromptEventKind kind,
        Guid? turnId,
        string? text = null,
        OpenVinoPromptFailure? failure = null,
        string? requestedDevice = null,
        IReadOnlyList<string>? actualExecutionDevices = null) =>
        Publish(
            eventSink,
            kind,
            Snapshot,
            turnId,
            text,
            failure,
            requestedDevice,
            actualExecutionDevices);

    private static void Publish(
        Action<OpenVinoPromptEvent> sink,
        OpenVinoPromptEventKind kind,
        OpenVinoRouteSnapshot snapshot,
        Guid? turnId,
        string? text = null,
        OpenVinoPromptFailure? failure = null,
        string? requestedDevice = null,
        IReadOnlyList<string>? actualExecutionDevices = null) =>
        sink(new OpenVinoPromptEvent(
            kind,
            snapshot.Identity.OperationId,
            snapshot.Identity.SessionId,
            turnId,
            text,
            failure,
            requestedDevice,
            actualExecutionDevices ?? Array.Empty<string>()));

    private async Task DisposeChannelAsync()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        await channel.DisposeAsync().ConfigureAwait(false);
    }

    private void ThrowIfDisposed()
    {
        if (disposed)
        {
            throw new InvalidOperationException(
                "The OpenVINO route session is already closed.");
        }
    }

    private static void ValidatePrompt(string prompt, int requestedNewTokens)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prompt);
        int promptBytes = Encoding.UTF8.GetByteCount(prompt);
        if (promptBytes > OpenVinoRouteCapability.MaximumPromptUtf8Bytes)
        {
            throw new ArgumentException(
                "The prompt exceeds the product UTF-8 limit.",
                nameof(prompt));
        }

        ArgumentOutOfRangeException.ThrowIfLessThan(
            requestedNewTokens,
            1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(
            requestedNewTokens,
            OpenVinoRouteCapability.MaximumRequestedNewTokens);
    }

    private static void ValidateDescriptor(OpenVinoSessionDescriptor descriptor)
    {
        new StartSessionCommand(
            Guid.NewGuid(),
            descriptor.InspectionRunId,
            descriptor.PackagePath,
            descriptor.PackageManifestDigest,
            descriptor.ModelSha256,
            descriptor.ModelLengthBytes,
            new OpenVinoDeviceRequest(OpenVinoRouteCapability.Device),
            new OpenVinoGenerationLimits(
                OpenVinoRouteCapability.MaximumContextTokens,
                OpenVinoRouteCapability.MaximumRequestedNewTokens)).Validate();
    }

    internal static OpenVinoPromptFailure MapFailure(
        OpenVinoSupportCode supportCode)
    {
        supportCode.Validate();
        return supportCode switch
        {
            OpenVinoSupportCode.RuntimeLoadFailed => new(
                supportCode.ToProtocolValue(),
                "The OpenVINO runtime could not load the model package.",
                "Try loading the session again."),
            OpenVinoSupportCode.RuntimeContextExceeded => new(
                supportCode.ToProtocolValue(),
                "The prompt and requested response exceed the approved context limit.",
                "Shorten the prompt or request fewer new tokens."),
            OpenVinoSupportCode.RuntimeTimedOut => new(
                supportCode.ToProtocolValue(),
                "The local OpenVINO operation exceeded its time limit.",
                "Retry the operation."),
            OpenVinoSupportCode.OperationCancelled => new(
                supportCode.ToProtocolValue(),
                "The local OpenVINO operation was cancelled.",
                "Start a new session when ready."),
            _ => new(
                supportCode.ToProtocolValue(),
                "The local OpenVINO operation could not continue.",
                "Close the session and retry from model inspection.")
        };
    }

    private sealed class InlineProgress<T>(Action<T> handler) : IProgress<T>
    {
        public void Report(T value) => handler(value);
    }
}

internal sealed class OpenVinoWorkerPromptChannelFactory(
    IOpenVinoWorkerClient workerClient) : IOpenVinoPromptChannelFactory
{
    public async Task<IOpenVinoPromptChannel> StartAsync(
        StartSessionCommand command,
        CancellationToken cancellationToken)
    {
        try
        {
            OpenVinoConversation conversation = await workerClient
                .StartSessionAsync(command, cancellationToken)
                .ConfigureAwait(false);
            return new OpenVinoWorkerPromptChannel(conversation);
        }
        catch (OpenVinoWorkerClientException failure)
        {
            throw new OpenVinoRouteWorkerFailureException(
                failure.SupportCode,
                failure.Message);
        }
    }
}

internal sealed class OpenVinoWorkerPromptChannel(
    OpenVinoConversation conversation) : IOpenVinoPromptChannel
{
    public async Task<IOpenVinoEvent> PromptAsync(
        PromptCommand command,
        IProgress<TokenEvent>? progress,
        CancellationToken cancellationToken)
    {
        try
        {
            return await conversation.PromptAsync(
                command,
                progress,
                cancellationToken).ConfigureAwait(false);
        }
        catch (OpenVinoWorkerClientException failure)
        {
            throw Map(failure);
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        try
        {
            await conversation.StopAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OpenVinoWorkerClientException failure)
        {
            throw Map(failure);
        }
    }

    public async Task CancelAsync(CancellationToken cancellationToken)
    {
        try
        {
            await conversation.CancelAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OpenVinoWorkerClientException failure)
        {
            throw Map(failure);
        }
    }

    public async Task CloseAsync(CancellationToken cancellationToken)
    {
        try
        {
            await conversation.CloseAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OpenVinoWorkerClientException failure)
        {
            throw Map(failure);
        }
    }

    public ValueTask DisposeAsync() => conversation.DisposeAsync();

    private static OpenVinoRouteWorkerFailureException Map(
        OpenVinoWorkerClientException failure) => new(
            failure.SupportCode,
            failure.Message);
}
