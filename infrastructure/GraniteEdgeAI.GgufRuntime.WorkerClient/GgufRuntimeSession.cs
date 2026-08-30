using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using GraniteEdgeAI.GgufRuntime.Contracts;
using GraniteEdgeAI.GgufRuntime.Contracts.Commands;
using GraniteEdgeAI.GgufRuntime.Contracts.Configuration;
using GraniteEdgeAI.GgufRuntime.Contracts.Events;
using GraniteEdgeAI.GgufRuntime.Contracts.Session;
using GraniteEdgeAI.GgufRuntime.Transport;
using GraniteEdgeAI.HardwareInspection.Foundation.Processes;

namespace GraniteEdgeAI.GgufRuntime.WorkerClient;

public sealed class GgufRuntimeSession : IAsyncDisposable
{
    private readonly GgufWorkerProcessSession _process;
    private readonly GgufFramedChannel _channel;
    private readonly GgufSessionId _sessionId;
    private readonly BoundedCleanupCoordinator _cleanup;
    private bool _closed;

    internal GgufRuntimeSession(
        GgufWorkerProcessSession process,
        GgufFramedChannel channel,
        GgufSessionId sessionId)
    {
        _process = process;
        _channel = channel;
        _sessionId = sessionId;
        _cleanup = new BoundedCleanupCoordinator(
            new OwnedCleanupAction(OwnedCleanupStage.Channel, () =>
            {
                _channel.Dispose();
                return ValueTask.CompletedTask;
            }),
            new OwnedCleanupAction(OwnedCleanupStage.Session, () =>
                _process.DisposeAsync()));
    }

    internal uint ActiveProcessCount => _process.ActiveProcessCount;

    internal async Task StartAsync(
        GgufRuntimeConfiguration configuration,
        IReadOnlyList<GgufConversationTurn> initialTurns,
        CancellationToken cancellationToken)
    {
        Guid requestId = Guid.NewGuid();
        await _channel.WriteCommandAsync(
            new StartSessionCommand(
                GgufProtocolVersion.Current,
                requestId,
                _sessionId,
                configuration,
                initialTurns),
            cancellationToken).ConfigureAwait(false);
        GgufRuntimeEvent loading = await _channel.ReadEventAsync(cancellationToken)
            .ConfigureAwait(false);
        GgufRuntimeEvent ready = await _channel.ReadEventAsync(cancellationToken)
            .ConfigureAwait(false);
        ValidateStartupEvents(loading, ready);
    }

    internal static void ValidateStartupEvents(
        GgufRuntimeEvent loading,
        GgufRuntimeEvent terminal)
    {
        ArgumentNullException.ThrowIfNull(loading);
        ArgumentNullException.ThrowIfNull(terminal);
        if (loading is SessionLoadingEvent && terminal is RuntimeFailureEvent failure)
        {
            throw new GgufRuntimeStartupException(failure.Failure);
        }

        if (loading is not SessionLoadingEvent || terminal is not SessionReadyEvent)
        {
            throw new GgufTransportException("The worker did not become ready.");
        }
    }

    public async IAsyncEnumerable<GgufRuntimeEvent> GenerateAsync(
        string prompt,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_closed, this);
        Guid requestId = Guid.NewGuid();
        await _channel.WriteCommandAsync(
            new SubmitPromptCommand(
                GgufProtocolVersion.Current,
                requestId,
                _sessionId,
                prompt),
            cancellationToken).ConfigureAwait(false);
        while (true)
        {
            GgufRuntimeEvent runtimeEvent = await _channel.ReadEventAsync(cancellationToken)
                .ConfigureAwait(false);
            if (runtimeEvent.RequestId != requestId)
            {
                throw new GgufTransportException("The worker event correlation is invalid.");
            }

            yield return runtimeEvent;
            if (runtimeEvent is ResponseCompletedEvent or ResponseStoppedEvent or RuntimeFailureEvent)
            {
                yield break;
            }
        }
    }

    public ValueTask StopAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_closed, this);
        return _channel.WriteCommandAsync(
            new StopGenerationCommand(
                GgufProtocolVersion.Current,
                Guid.NewGuid(),
                _sessionId),
            cancellationToken);
    }

    public async Task CloseAsync(CancellationToken cancellationToken)
    {
        if (_closed)
        {
            return;
        }

        _closed = true;
        await _channel.WriteCommandAsync(
            new CloseSessionCommand(
                GgufProtocolVersion.Current,
                Guid.NewGuid(),
                _sessionId),
            cancellationToken).ConfigureAwait(false);
        _ = await _channel.ReadEventAsync(cancellationToken).ConfigureAwait(false);
        await _process.TerminateAndVerifyEmptyAsync(TimeSpan.FromSeconds(5))
            .ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        Exception? primaryFailure = null;
        if (!_closed)
        {
            try
            {
                await CloseAsync(CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception exception) when (
                exception is IOException or GgufTransportException or
                    GgufWorkerPolicyException or OperationCanceledException)
            {
                primaryFailure = exception;
            }
        }

        CleanupOutcome cleanup = await _cleanup.ExecuteAsync().ConfigureAwait(false);
        if (!cleanup.Succeeded)
        {
            Exception integrity = CleanupIntegrityException.PreserveCancellation(
                cleanup.Failures,
                primaryFailure);
            if (integrity is OperationCanceledException cancellation)
            {
                throw cancellation;
            }

            throw new GgufWorkerPolicyException(
                "worker-cleanup-failed",
                "The GGUF runtime worker cleanup could not be verified.",
                integrity);
        }

        if (primaryFailure is not null)
        {
            ExceptionDispatchInfo.Capture(primaryFailure).Throw();
        }
    }

    internal static async ValueTask DisposePreservingFirstFailureAsync(
        Func<Task> close,
        Action disposeChannel,
        Func<ValueTask> disposeProcess)
    {
        ArgumentNullException.ThrowIfNull(close);
        ArgumentNullException.ThrowIfNull(disposeChannel);
        ArgumentNullException.ThrowIfNull(disposeProcess);
        Exception? firstFailure = null;
        try
        {
            await close().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            firstFailure = exception;
        }

        try
        {
            disposeChannel();
        }
        catch (Exception exception)
        {
            firstFailure ??= exception;
        }

        try
        {
            await disposeProcess().ConfigureAwait(false);
        }
        catch (GgufWorkerPolicyException)
        {
            throw;
        }
        catch (Exception exception)
        {
            firstFailure ??= exception;
        }

        if (firstFailure is not null)
        {
            ExceptionDispatchInfo.Capture(firstFailure).Throw();
        }
    }
}
