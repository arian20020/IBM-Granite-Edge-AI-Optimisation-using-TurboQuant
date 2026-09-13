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
using GraniteEdgeAI.GgufRuntime.Contracts.Failures;
using GraniteEdgeAI.GgufRuntime.WorkerClient;

namespace GraniteEdgeAI.Features.GgufRuntime.Services;

internal interface IGgufChatRuntimeSession : IAsyncDisposable
{
    IAsyncEnumerable<GgufRuntimeEvent> GenerateAsync(
        string prompt,
        CancellationToken cancellationToken);

    ValueTask StopAsync(CancellationToken cancellationToken);
}

internal interface IGgufChatTitleRuntimeSession
{
    IAsyncEnumerable<GgufRuntimeEvent> GenerateTitleAsync(string prompt, CancellationToken cancellationToken);
}

internal sealed class GgufChatSessionAdapter : IGgufChatSession, IGgufChatTitleSession
{
    private readonly Func<IReadOnlyList<GgufConversationTurn>, CancellationToken,
        Task<IGgufChatRuntimeSession>> startSession;
    private readonly SemaphoreSlim lifecycleGate = new(1, 1);
    private readonly SemaphoreSlim stopGate = new(1, 1);
    private readonly SemaphoreSlim sessionGate = new(1, 1);
    private IGgufChatRuntimeSession? session;
    private bool disposed;
    private bool titleQuarantined;

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
                    IGgufChatRuntimeSession candidate = await startSession(turns, cancellationToken)
                        .ConfigureAwait(false);
                    if (candidate is null)
                    {
                        throw new InvalidOperationException(
                            "The GGUF runtime session factory returned no session.");
                    }
                    if (cancellationToken.IsCancellationRequested)
                    {
                        if (!ReferenceEquals(candidate, session)) await candidate.DisposeAsync().ConfigureAwait(false);
                        cancellationToken.ThrowIfCancellationRequested();
                    }
                    IGgufChatRuntimeSession? previous = session;
                    session = candidate;
                    titleQuarantined = false;
                    if (previous is not null && !ReferenceEquals(previous, candidate))
                    {
                        try { await previous.DisposeAsync().ConfigureAwait(false); }
                        catch (Exception error) { System.Diagnostics.Trace.TraceError("Retired GGUF session cleanup failed: {0}", error.GetType().Name); }
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
        var operation = new ForegroundOperation();
        lock (foregroundSync)
        {
            if (foregroundOperation is not null) throw new InvalidOperationException("A response is already active.");
            foregroundOperation = operation;
        }
        bool entered = false;
        try
        {
            await lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            entered = true;
            await stopGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            stopGate.Release();
            if (titleQuarantined)
            {
                yield return new GgufChatFailed("title-restore-failed", new GgufTitleRestoreException().Message);
                yield break;
            }
            IGgufChatRuntimeSession active = await GetSessionAsync(cancellationToken)
                .ConfigureAwait(false);
            await foreach (GgufRuntimeEvent runtimeEvent in
                active.GenerateAsync(prompt, cancellationToken).ConfigureAwait(false))
            {
                if (runtimeEvent is ResponseStartedEvent) operation.Started.TrySetResult();
                if (runtimeEvent is ResponseCompletedEvent or ResponseStoppedEvent) operation.AcceptedTerminal = true;
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
                        failure.Failure.Category == GraniteEdgeAI.GgufRuntime.Contracts.Failures.GgufRuntimeFailureCategory.ContextLimitReached
                            ? "This conversation has reached the model's context limit. Start a new chat or choose a model with more context."
                            : "The local model could not complete this response."),
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
            lock (foregroundSync)
            {
                if (ReferenceEquals(foregroundOperation, operation)) foregroundOperation = null;
                if (entered) lifecycleGate.Release();
                operation.Finished.TrySetResult();
            }
        }
    }

    private readonly object foregroundSync = new();
    private ForegroundOperation? foregroundOperation;
    private sealed class ForegroundOperation
    {
        internal readonly TaskCompletionSource Started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource Finished = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal bool AcceptedTerminal;
    }

    public async ValueTask StopAsync(CancellationToken cancellationToken)
    {
        ForegroundOperation? operation;
        lock (foregroundSync) operation = foregroundOperation;
        if (operation is null) return;
        await Task.WhenAny(operation.Started.Task, operation.Finished.Task).WaitAsync(cancellationToken).ConfigureAwait(false);
        if (operation.Finished.Task.IsCompleted) return;
        await stopGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            IGgufChatRuntimeSession active = await GetSessionAsync(cancellationToken)
                .ConfigureAwait(false);
            lock (foregroundSync)
                if (!ReferenceEquals(foregroundOperation, operation)) return;
            try { await active.StopAsync(cancellationToken).ConfigureAwait(false); }
            catch (InvalidOperationException)
            {
                await operation.Finished.Task.WaitAsync(TimeSpan.FromSeconds(2), cancellationToken).ConfigureAwait(false);
                if (!operation.AcceptedTerminal) throw;
            }
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
        GgufConversationTurn[] turns = GraniteEdgeAI.Features.ChatModels.ChatHistoryReplayPolicy.GetCompletedTurns(conversation)
            .Select(message => new GgufConversationTurn(
                message.Role switch
                {
                    ChatMessageRole.User or ChatMessageRole.Control =>
                        GgufConversationRole.User,
                    ChatMessageRole.Assistant => GgufConversationRole.Assistant,
                    _ => throw new ArgumentOutOfRangeException(nameof(conversation)),
                },
                message.Content))
            .ToArray();
        if (turns.Length > GgufProtocolLimits.MaxInitialTurns)
            throw new GgufChatRuntimeUnavailableException(new GgufRuntimeFailure(
                GgufRuntimeFailureCategory.ContextLimitReached, "chat-history-exceeds-replay-limit"));
        return turns;
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

        return exception is GgufRuntimeStartupException startup
            ? TranslateStartupFailure(startup.Failure)
            : new GgufChatRuntimeUnavailableException();
    }

    public async Task<string?> GenerateTitleAsync(string prompt, Task stopRequested, CancellationToken token)
    {
        await lifecycleGate.WaitAsync(token).ConfigureAwait(false);
        Task<string?>? running = null;
        try
        {
            if (stopRequested.IsCompleted) return null;
            if (titleQuarantined) throw new GgufTitleRestoreException();
            IGgufChatRuntimeSession active = await GetSessionAsync(token).ConfigureAwait(false);
            if (active is not IGgufChatTitleRuntimeSession capability) return null;
            var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            async Task<string?> PumpAsync()
            {
                var text = new System.Text.StringBuilder();
                bool oversized = false;
                await foreach (var item in capability.GenerateTitleAsync(prompt, token).ConfigureAwait(false))
                {
                    if (item is ResponseStartedEvent) started.TrySetResult();
                    if (item is TextDeltaEvent delta)
                    {
                        oversized |= text.Length + delta.Text.Length > 1024;
                        if (!oversized) text.Append(delta.Text);
                    }
                    if (item is ResponseStoppedEvent stopped)
                    {
                        if (stopped.Disposition == GgufStopDisposition.StoppedNeedsReload) throw new GgufTitleRestoreException();
                        return null;
                    }
                    if (item is RuntimeFailureEvent failure)
                    {
                        if (failure.Failure.Category == GgufRuntimeFailureCategory.ContextLimitReached) return null;
                        throw new GgufTitleRestoreException();
                    }
                    if (item is ResponseCompletedEvent) return oversized || stopRequested.IsCompleted ? null : text.ToString();
                }
                throw new GgufTitleRestoreException();
            }
            Task<string?> generation = PumpAsync();
            running = generation;
            if (ReferenceEquals(await Task.WhenAny(generation, stopRequested).ConfigureAwait(false), stopRequested) && !generation.IsCompleted)
            {
                await Task.WhenAny(started.Task, generation).ConfigureAwait(false);
                if (started.Task.IsCompleted && !generation.IsCompleted)
                    await active.StopAsync(token).ConfigureAwait(false);
            }
            return await generation.ConfigureAwait(false);
        }
        catch (Exception) when (!token.IsCancellationRequested)
        {
            titleQuarantined = true;
            throw new GgufTitleRestoreException();
        }
        finally
        {
            try { if (running is not null) await running.ConfigureAwait(false); }
            catch { titleQuarantined = true; }
            finally { lifecycleGate.Release(); }
        }
    }

    internal static GgufChatRuntimeUnavailableException TranslateStartupFailure(
        GgufRuntimeFailure failure)
    {
        ArgumentNullException.ThrowIfNull(failure);
        return new GgufChatRuntimeUnavailableException(failure);
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
        IGgufChatRuntimeSession, IGgufChatTitleRuntimeSession
    {
        public IAsyncEnumerable<GgufRuntimeEvent> GenerateTitleAsync(string prompt, CancellationToken token) =>
            inner.GenerateTitleAsync(prompt, token);
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
    internal GgufChatRuntimeUnavailableException(
        GgufRuntimeFailure? startupFailure = null)
        : base("The local GGUF runtime is temporarily unavailable.")
    {
        StartupFailure = startupFailure;
    }

    internal GgufRuntimeFailure? StartupFailure { get; }
}
