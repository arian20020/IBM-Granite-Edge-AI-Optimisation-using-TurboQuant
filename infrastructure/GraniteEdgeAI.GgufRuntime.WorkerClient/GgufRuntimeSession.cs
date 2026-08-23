using System.Runtime.CompilerServices;
using GraniteEdgeAI.GgufRuntime.Contracts;
using GraniteEdgeAI.GgufRuntime.Contracts.Commands;
using GraniteEdgeAI.GgufRuntime.Contracts.Configuration;
using GraniteEdgeAI.GgufRuntime.Contracts.Events;
using GraniteEdgeAI.GgufRuntime.Contracts.Session;
using GraniteEdgeAI.GgufRuntime.Transport;

namespace GraniteEdgeAI.GgufRuntime.WorkerClient;

public sealed class GgufRuntimeSession : IAsyncDisposable
{
    private readonly GgufWorkerProcessSession _process;
    private readonly GgufFramedChannel _channel;
    private readonly GgufSessionId _sessionId;
    private bool _closed;

    internal GgufRuntimeSession(
        GgufWorkerProcessSession process,
        GgufFramedChannel channel,
        GgufSessionId sessionId)
    {
        _process = process;
        _channel = channel;
        _sessionId = sessionId;
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
            }
        }

        _channel.Dispose();
        await _process.DisposeAsync().ConfigureAwait(false);
    }
}
