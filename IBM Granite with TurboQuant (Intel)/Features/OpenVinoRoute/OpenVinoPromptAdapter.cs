using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using GraniteEdgeAI.OpenVino.Contracts;
using GraniteEdgeAI.OpenVino.WorkerClient;
using GraniteEdgeAI.Features.Prompting;

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
        Action<GenerationStartedEvent>? generationStarted,
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
    private readonly object teardownLock = new();
    private readonly OpenVinoRouteStateMachine stateMachine;
    private readonly IOpenVinoPromptChannel channel;
    private readonly Action<PromptEvent> eventSink;
    private bool disposed;
    private Task? terminalTeardownTask;
    private Task? channelDisposalTask;
    private Guid? workerConfirmedTurnId;
    private long nextSequence;

    private OpenVinoPromptAdapter(
        OpenVinoRouteStateMachine stateMachine,
        IOpenVinoPromptChannel channel,
        Action<PromptEvent> eventSink)
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
        Action<PromptEvent> eventSink,
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
            PromptEventKind.Loading,
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
        adapter.Emit(PromptEventKind.SessionReady, turnId: null,
            requestedDevice: OpenVinoRouteCapability.Device,
            actualExecutionDevices: CpuExecutionDevices);
        return adapter;
    }

    public async Task<PromptTurnResult> GenerateAsync(
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
            workerConfirmedTurnId = null;
        }
        Emit(PromptEventKind.GeneratingTurn, turnId);
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
                    generation => AcceptGenerationStarted(
                        operationId,
                        turnId,
                        generation),
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
                Emit(PromptEventKind.Cancelled, turnId: null);
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

            PromptTurnStatus status = completed.Disposition ==
                OpenVinoTurnDisposition.Stopped
                ? PromptTurnStatus.Stopped
                : PromptTurnStatus.Completed;
            Emit(PromptEventKind.TurnCompleted, turnId, text.ToString());
            if (!stateMachine.TryReturnToSessionReady(operationId))
            {
                return Fail(
                    operationId,
                    turnId,
                    OpenVinoSupportCode.RuntimeProtocolFailed);
            }
            Emit(PromptEventKind.SessionReady, turnId: null,
                requestedDevice: OpenVinoRouteCapability.Device,
                actualExecutionDevices: CpuExecutionDevices);
            return new PromptTurnResult(
                status,
                text.ToString(),
                completed.PromptTokenCount,
                completed.GeneratedTokenCount,
                Failure: null);
        }
        catch (OpenVinoRouteWorkerFailureException failure)
            when (failure.SupportCode == OpenVinoSupportCode.OperationCancelled)
        {
            stateMachine.TryBeginCancellation(operationId);
            if (stateMachine.TryMarkCancelled(operationId))
            {
                Emit(PromptEventKind.Cancelled, turnId: null);
            }
            throw new OperationCanceledException(
                "The OpenVINO prompt session was cancelled.",
                failure);
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
                if (stateMachine.TryMarkCancelled(operationId))
                {
                    Emit(PromptEventKind.Cancelled, turnId: null);
                }
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

        Emit(PromptEventKind.StoppingTurn, turnId);
        try
        {
            await channel.StopAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OpenVinoRouteWorkerFailureException failure)
        {
            Fail(snapshot.Identity.OperationId, turnId, failure.SupportCode);
        }
    }

    public Task CancelAsync(CancellationToken cancellationToken)
    {
        lock (teardownLock)
        {
            if (terminalTeardownTask is not null)
            {
                return terminalTeardownTask;
            }

            if (channelDisposalTask is not null)
            {
                return channelDisposalTask;
            }

            if (disposed)
            {
                return Task.CompletedTask;
            }

            terminalTeardownTask = CancelCoreAsync(
                cancellationAlreadyBegun: false,
                confirmedTurnId: null,
                cancellationToken);
            return terminalTeardownTask;
        }
    }

    public Task CancelActiveTurnAsync(
        Guid confirmedTurnId,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(confirmedTurnId, Guid.Empty);
        lock (teardownLock)
        {
            if (terminalTeardownTask is not null)
            {
                return terminalTeardownTask;
            }

            lock (stateLock)
            {
                OpenVinoRouteSnapshot snapshot = Snapshot;
                if (workerConfirmedTurnId != confirmedTurnId ||
                    snapshot.ActiveTurnId != confirmedTurnId ||
                    snapshot.State is not (
                        OpenVinoRouteState.GeneratingTurn or
                        OpenVinoRouteState.StoppingTurn) ||
                    !stateMachine.TryBeginCancellation(
                        snapshot.Identity.OperationId))
                {
                    throw new InvalidOperationException(
                        "The confirmed generation turn is no longer active.");
                }
            }

            terminalTeardownTask = CancelCoreAsync(
                cancellationAlreadyBegun: true,
                confirmedTurnId,
                cancellationToken);
            return terminalTeardownTask;
        }
    }

    private async Task CancelCoreAsync(
        bool cancellationAlreadyBegun,
        Guid? confirmedTurnId,
        CancellationToken cancellationToken)
    {
        Guid operationId = Snapshot.Identity.OperationId;
        if (!cancellationAlreadyBegun &&
            !stateMachine.TryBeginCancellation(operationId))
        {
            if (Snapshot.State is OpenVinoRouteState.Cancelled or
                OpenVinoRouteState.Failed or
                OpenVinoRouteState.SessionCompleted)
            {
                await DisposeChannelAsync().ConfigureAwait(false);
                return;
            }

            throw new InvalidOperationException(
                "The route session cannot be cancelled from its current state.");
        }

        Emit(PromptEventKind.CancellingSession, confirmedTurnId);
        PromptFailure? failure = null;
        try
        {
            await channel.CancelAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OpenVinoRouteWorkerFailureException workerFailure)
        {
            if (workerFailure.SupportCode != OpenVinoSupportCode.OperationCancelled)
            {
                failure = MapFailure(workerFailure.SupportCode);
            }
        }
        catch (OperationCanceledException)
        {
            // Caller cancellation still owns a terminal channel teardown.
        }
        catch (Exception)
        {
            failure = MapFailure(OpenVinoSupportCode.RuntimeProtocolFailed);
        }
        finally
        {
            lock (stateLock)
            {
                workerConfirmedTurnId = null;
            }
            bool publishTerminal;
            if (failure is null)
            {
                publishTerminal = stateMachine.TryMarkCancelled(operationId);
            }
            else
            {
                publishTerminal =
                    stateMachine.TryFail(operationId, failure.SupportCode);
            }

            // Resource retirement is authoritative. A presentation observer
            // cannot run before or preempt the exactly-once channel disposal.
            await DisposeChannelAsync().ConfigureAwait(false);

            if (publishTerminal && failure is null)
            {
                Emit(PromptEventKind.Cancelled, turnId: null);
            }
            else if (publishTerminal)
            {
                Emit(PromptEventKind.Failed, turnId: null, failure: failure);
            }
        }
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
        Emit(PromptEventKind.SessionCompleted, turnId: null);
        await DisposeChannelAsync().ConfigureAwait(false);
    }

    public ValueTask DisposeAsync()
    {
        lock (teardownLock)
        {
            if (terminalTeardownTask is not null)
            {
                return new ValueTask(terminalTeardownTask);
            }

            if (channelDisposalTask is not null)
            {
                return new ValueTask(channelDisposalTask);
            }

            OpenVinoRouteState state = Snapshot.State;
            if (state is not (
                    OpenVinoRouteState.SessionCompleted or
                    OpenVinoRouteState.Failed or
                    OpenVinoRouteState.Cancelled))
            {
                terminalTeardownTask = CancelCoreAsync(
                    cancellationAlreadyBegun: false,
                    confirmedTurnId: null,
                    CancellationToken.None);
                return new ValueTask(terminalTeardownTask);
            }

            channelDisposalTask = channel.DisposeAsync().AsTask();
            disposed = true;
            return new ValueTask(channelDisposalTask);
        }
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
            Emit(PromptEventKind.TextDelta, turnId, token.Text);
        }
    }

    private void AcceptGenerationStarted(
        Guid operationId,
        Guid turnId,
        GenerationStartedEvent generation)
    {
        lock (stateLock)
        {
            OpenVinoRouteSnapshot snapshot = Snapshot;
            if (snapshot.Identity.OperationId != operationId ||
                snapshot.Identity.SessionId != generation.SessionId ||
                snapshot.ActiveTurnId != turnId ||
                generation.TurnId != turnId ||
                snapshot.State != OpenVinoRouteState.GeneratingTurn)
            {
                throw new OpenVinoRouteWorkerFailureException(
                    OpenVinoSupportCode.RuntimeProtocolFailed,
                    "The worker confirmed a different generation turn.");
            }

            workerConfirmedTurnId = turnId;
            Emit(PromptEventKind.GenerationConfirmed, turnId);
        }
    }

    private PromptTurnResult Fail(
        Guid operationId,
        Guid turnId,
        OpenVinoSupportCode supportCode)
    {
        PromptFailure failure = MapFailure(supportCode);
        stateMachine.TryFail(operationId, failure.SupportCode);
        Emit(PromptEventKind.Failed, turnId, failure: failure);
        return new PromptTurnResult(
            PromptTurnStatus.Failed,
            string.Empty,
            0,
            0,
            failure);
    }

    private void Emit(
        PromptEventKind kind,
        Guid? turnId,
        string? text = null,
        PromptFailure? failure = null,
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
        Action<PromptEvent> sink,
        PromptEventKind kind,
        OpenVinoRouteSnapshot snapshot,
        Guid? turnId,
        string? text = null,
        PromptFailure? failure = null,
        string? requestedDevice = null,
        IReadOnlyList<string>? actualExecutionDevices = null)
    {
        try
        {
            sink(new PromptEvent(
                kind,
                snapshot.Identity.OperationId,
                snapshot.Identity.SessionId,
                turnId,
                text,
                failure,
                requestedDevice,
                actualExecutionDevices ?? Array.Empty<string>()));
        }
        catch (Exception)
        {
            // Event observers are presentation-only. Protocol state and
            // native resource ownership must never depend on their behavior.
        }
    }

    private Task DisposeChannelAsync()
    {
        lock (teardownLock)
        {
            if (channelDisposalTask is not null)
            {
                return channelDisposalTask;
            }

            disposed = true;
            channelDisposalTask = channel.DisposeAsync().AsTask();
            return channelDisposalTask;
        }
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

    internal static PromptFailure MapFailure(
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
        Action<GenerationStartedEvent>? generationStarted,
        CancellationToken cancellationToken)
    {
        try
        {
            return await conversation.PromptAsync(
                command,
                progress,
                generationStarted is null
                    ? null
                    : new InlineProgress<GenerationStartedEvent>(
                        generationStarted),
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

    private sealed class InlineProgress<T>(Action<T> handler) : IProgress<T>
    {
        public void Report(T value) => handler(value);
    }
}
