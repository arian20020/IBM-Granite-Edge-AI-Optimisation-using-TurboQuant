using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GraniteEdgeAI.Features.GgufRuntime.History;
using GraniteEdgeAI.GgufRuntime.Contracts;
using GraniteEdgeAI.GgufRuntime.Contracts.Commands;
using GraniteEdgeAI.GgufRuntime.Contracts.Configuration;
using GraniteEdgeAI.GgufRuntime.Contracts.Events;
using GraniteEdgeAI.GgufRuntime.WorkerClient;

namespace GraniteEdgeAI.Features.GgufRuntime.Services;

internal interface IGgufChatRuntimeSession : IAsyncDisposable
{
    IAsyncEnumerable<GgufRuntimeEvent> GenerateAsync(
        string prompt,
        CancellationToken cancellationToken);

    ValueTask StopAsync(CancellationToken cancellationToken);
}

internal sealed class GgufChatSessionAdapter : IGgufChatSession
{
    private readonly Func<IReadOnlyList<GgufConversationTurn>, CancellationToken,
        Task<IGgufChatRuntimeSession>> startSession;
    private readonly SemaphoreSlim lifecycleGate = new(1, 1);
    private readonly SemaphoreSlim stopGate = new(1, 1);
    private readonly SemaphoreSlim sessionGate = new(1, 1);
    private IGgufChatRuntimeSession? session;
    private bool disposed;

    internal GgufChatSessionAdapter(
        GgufRuntimeClient client,
        GgufRuntimeConfiguration configuration)
        : this(async (turns, cancellationToken) =>
        {
            try
            {
                return new RuntimeSession(await client.StartAsync(
                    configuration,
                    turns,
                    cancellationToken).ConfigureAwait(false));
            }
            catch (Exception exception) when (IsExpectedRuntimeFailure(exception))
            {
                throw TranslateExpectedRuntimeFailure(exception);
            }
        })
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(configuration);
    }

    internal GgufChatSessionAdapter(
        Func<IReadOnlyList<GgufConversationTurn>, CancellationToken,
            Task<IGgufChatRuntimeSession>> startSession)
    {
        this.startSession = startSession ??
            throw new ArgumentNullException(nameof(startSession));
    }

    public async ValueTask PrepareConversationAsync(
        ChatConversation conversation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(conversation);
        IReadOnlyList<GgufConversationTurn> turns = CreateInitialTurns(conversation);
        await lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await stopGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await sessionGate.WaitAsync(cancellationToken).ConfigureAwait(false);
                try
                {
                    ObjectDisposedException.ThrowIf(disposed, this);
                    if (session is not null)
                    {
                        await session.DisposeAsync().ConfigureAwait(false);
                        session = null;
                    }

                    session = await startSession(turns, cancellationToken)
                        .ConfigureAwait(false);
                    if (session is null)
                    {
                        throw new InvalidOperationException(
                            "The GGUF runtime session factory returned no session.");
                    }
                }
                finally
                {
                    sessionGate.Release();
                }
            }
            finally
            {
                stopGate.Release();
            }
        }
        finally
        {
            lifecycleGate.Release();
        }
    }

    public async IAsyncEnumerable<GgufChatEvent> GenerateAsync(
        string prompt,
        [System.Runtime.CompilerServices.EnumeratorCancellation]
        CancellationToken cancellationToken)
    {
        await lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            IGgufChatRuntimeSession active = await GetSessionAsync(cancellationToken)
                .ConfigureAwait(false);
            await foreach (GgufRuntimeEvent runtimeEvent in
                active.GenerateAsync(prompt, cancellationToken).ConfigureAwait(false))
            {
                GgufChatEvent? chatEvent = runtimeEvent switch
                {
                    TextDeltaEvent delta => new GgufChatDelta(delta.Text),
                    ResponseCompletedEvent completed => new GgufChatCompleted(
                        completed.Reason switch
                        {
                            GgufCompletionReason.Stop => GgufChatCompletionKind.Stop,
                            GgufCompletionReason.Length => GgufChatCompletionKind.Length,
                            _ => throw new ArgumentOutOfRangeException(
                                nameof(runtimeEvent)),
                        }),
                    ResponseStoppedEvent stopped => new GgufChatStopped(
                        stopped.Disposition == GgufStopDisposition.StoppedNeedsReload),
                    RuntimeFailureEvent failure => new GgufChatFailed(
                        failure.Failure.Code,
                        "The local model could not complete this response."),
                    _ => null,
                };
                if (chatEvent is not null)
                {
                    yield return chatEvent;
                }
            }
        }
        finally
        {
            lifecycleGate.Release();
        }
    }

    public async ValueTask StopAsync(CancellationToken cancellationToken)
    {
        await stopGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            IGgufChatRuntimeSession active = await GetSessionAsync(cancellationToken)
                .ConfigureAwait(false);
            await active.StopAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            stopGate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await lifecycleGate.WaitAsync().ConfigureAwait(false);
        try
        {
            await stopGate.WaitAsync().ConfigureAwait(false);
            try
            {
                await sessionGate.WaitAsync().ConfigureAwait(false);
                try
                {
                    if (disposed)
                    {
                        return;
                    }

                    disposed = true;
                    if (session is not null)
                    {
                        await session.DisposeAsync().ConfigureAwait(false);
                        session = null;
                    }
                }
                finally
                {
                    sessionGate.Release();
                }
            }
            finally
            {
                stopGate.Release();
            }
        }
        finally
        {
            lifecycleGate.Release();
        }
    }

    internal static IReadOnlyList<GgufConversationTurn> CreateInitialTurns(
        ChatConversation conversation)
    {
        ArgumentNullException.ThrowIfNull(conversation);
        return conversation.Messages
            .Where(message => !string.IsNullOrWhiteSpace(message.Content))
            .Select(message => new GgufConversationTurn(
                message.Role switch
                {
                    ChatMessageRole.User or ChatMessageRole.Control =>
                        GgufConversationRole.User,
                    ChatMessageRole.Assistant => GgufConversationRole.Assistant,
                    _ => throw new ArgumentOutOfRangeException(nameof(conversation)),
                },
                message.Content))
            .TakeLast(GgufProtocolLimits.MaxInitialTurns)
            .ToArray();
    }

    private async Task<IGgufChatRuntimeSession> GetSessionAsync(
        CancellationToken cancellationToken)
    {
        await sessionGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            return session ?? throw new InvalidOperationException(
                "The selected conversation has not prepared a runtime session.");
        }
        finally
        {
            sessionGate.Release();
        }
    }

    internal static GgufChatRuntimeUnavailableException
        TranslateExpectedRuntimeFailure(Exception exception)
    {
        if (!IsExpectedRuntimeFailure(exception))
        {
            throw new ArgumentException(
                "The failure is not an expected runtime availability failure.",
                nameof(exception));
        }

        return new GgufChatRuntimeUnavailableException();
    }

    internal static GgufChatRuntimeUnavailableException
        TranslateExpectedTeardownFailure(Exception exception)
    {
        if (!IsExpectedRuntimeFailure(exception) &&
            exception is not OperationCanceledException)
        {
            throw new ArgumentException(
                "The failure is not an expected runtime teardown failure.",
                nameof(exception));
        }

        return new GgufChatRuntimeUnavailableException();
    }

    private static bool IsExpectedRuntimeFailure(Exception exception) =>
        exception is IOException or UnauthorizedAccessException or
            GgufRuntimeStartupException or GgufWorkerPolicyException;

    private sealed class RuntimeSession(GgufRuntimeSession inner) :
        IGgufChatRuntimeSession
    {
        public async IAsyncEnumerable<GgufRuntimeEvent> GenerateAsync(
            string prompt,
            [System.Runtime.CompilerServices.EnumeratorCancellation]
            CancellationToken cancellationToken)
        {
            IAsyncEnumerable<GgufRuntimeEvent> events =
                inner.GenerateAsync(prompt, cancellationToken);
            await using IAsyncEnumerator<GgufRuntimeEvent> enumerator =
                events.GetAsyncEnumerator(cancellationToken);
            while (true)
            {
                bool hasNext;
                try
                {
                    hasNext = await enumerator.MoveNextAsync().ConfigureAwait(false);
                }
                catch (Exception exception) when (IsExpectedRuntimeFailure(exception))
                {
                    throw TranslateExpectedRuntimeFailure(exception);
                }

                if (!hasNext)
                {
                    yield break;
                }

                yield return enumerator.Current;
            }
        }

        public async ValueTask StopAsync(CancellationToken cancellationToken)
        {
            try
            {
                await inner.StopAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (IsExpectedRuntimeFailure(exception))
            {
                throw TranslateExpectedRuntimeFailure(exception);
            }
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                await inner.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception exception) when (
                IsExpectedRuntimeFailure(exception) ||
                exception is OperationCanceledException)
            {
                throw TranslateExpectedTeardownFailure(exception);
            }
        }
    }
}

internal sealed class GgufChatRuntimeUnavailableException : Exception
{
    internal GgufChatRuntimeUnavailableException()
        : base("The local GGUF runtime is temporarily unavailable.")
    {
    }
}
