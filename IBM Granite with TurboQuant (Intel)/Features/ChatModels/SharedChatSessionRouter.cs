using GraniteEdgeAI.Features.GgufRuntime.History;
using GraniteEdgeAI.Features.GgufRuntime.Services;

namespace GraniteEdgeAI.Features.ChatModels;

/// <summary>Owns one active backend independently of the shared conversation and view.</summary>
internal sealed class SharedChatSessionRouter(IGgufChatSession initialSession) : IGgufChatSession, IBackgroundChatTitleSession
{
    private readonly SemaphoreSlim operationGate = new(1, 1);
    private readonly SemaphoreSlim stopGate = new(1, 1);
    private readonly CancellationTokenSource lifetime = new();
    private readonly object disposalSync = new();
    private IGgufChatSession active = initialSession ?? throw new ArgumentNullException(nameof(initialSession));
    private Task? disposal;
    private readonly object titleSync = new();
    private TitleOperation? titleOperation;
    private int foregroundWaiters;
    private ForegroundGeneration? foregroundGeneration;

    private sealed class ForegroundGeneration
    {
        internal bool Entered;
        internal bool StopRequested;
        internal readonly TaskCompletionSource AdapterEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource Finished = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private sealed class TitleOperation
    {
        internal readonly TaskCompletionSource Stop = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource Finished = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    public async Task<string?> TryGenerateTitleAsync(string prompt, CancellationToken cancellationToken)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, lifetime.Token);
        TitleOperation operation;
        IGgufChatTitleSession capability;
        lock (titleSync)
        {
            if (linked.IsCancellationRequested || foregroundWaiters != 0 || titleOperation is not null ||
                active is not IGgufChatTitleSession available || !operationGate.Wait(0)) return null;
            capability = available;
            operation = new TitleOperation();
            titleOperation = operation;
        }
        try
        {
            return await Task.Run(() => capability.GenerateTitleAsync(prompt, operation.Stop.Task, linked.Token),
                linked.Token).ConfigureAwait(false);
        }
        finally
        {
            lock (titleSync)
            {
                titleOperation = null;
                operationGate.Release();
                operation.Finished.TrySetResult();
            }
        }
    }

    private async Task EnterForegroundAsync(CancellationToken token)
    {
        Task? titleFinished;
        lock (titleSync)
        {
            foregroundWaiters++;
            titleOperation?.Stop.TrySetResult();
            titleFinished = titleOperation?.Finished.Task;
        }
        try
        {
            if (titleFinished is not null) await titleFinished.WaitAsync(token).ConfigureAwait(false);
            await operationGate.WaitAsync(token).ConfigureAwait(false);
        }
        finally { lock (titleSync) foregroundWaiters--; }
    }

    internal async Task SwitchAsync(IGgufChatSession candidate, ChatConversation conversation, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(conversation);
        if (ReferenceEquals(candidate, active)) throw new ArgumentException("A replacement session must be independent.", nameof(candidate));
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, lifetime.Token);
        bool entered = false;
        bool committed = false;
        try
        {
            await EnterForegroundAsync(linked.Token).ConfigureAwait(false);
            entered = true;
            if (ReferenceEquals(candidate, active)) throw new ArgumentException("The candidate is already active.", nameof(candidate));
            // Adapters can perform synchronous package verification before their
            // first await. Session activation must never borrow the UI thread.
            await Task.Run(async () => await candidate.PrepareConversationAsync(conversation, linked.Token)
                .ConfigureAwait(false), linked.Token).ConfigureAwait(false);
            await stopGate.WaitAsync(linked.Token).ConfigureAwait(false);
            try
            {
                linked.Token.ThrowIfCancellationRequested();
                IGgufChatSession previous = active;
                active = candidate;
                committed = true;
                // Teardown failure cannot turn an already committed switch into a reported rollback.
                try { await previous.DisposeAsync().ConfigureAwait(false); }
                catch (Exception error) { System.Diagnostics.Trace.TraceError("Retired chat session cleanup failed: {0}", error.GetType().Name); }
            }
            finally { stopGate.Release(); }
        }
        finally
        {
            try
            {
                if (!committed && !ReferenceEquals(candidate, active)) await candidate.DisposeAsync().ConfigureAwait(false);
            }
            finally { if (entered) operationGate.Release(); }
        }
    }

    public async ValueTask PrepareConversationAsync(ChatConversation conversation, CancellationToken cancellationToken)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, lifetime.Token);
        await EnterForegroundAsync(linked.Token).ConfigureAwait(false);
        try
        {
            // Preparation may verify large packages synchronously before its first await.
            // keep that work off the dispatcher while retaining the session operation gate
            await Task.Run(async () => await active.PrepareConversationAsync(conversation, linked.Token)
                .ConfigureAwait(false), linked.Token).ConfigureAwait(false);
        }
        finally { operationGate.Release(); }
    }

    public async IAsyncEnumerable<GgufChatEvent> GenerateAsync(string prompt,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, lifetime.Token);
        var operation = new ForegroundGeneration();
        lock (titleSync)
        {
            if (foregroundGeneration is not null) throw new InvalidOperationException("A foreground response is already pending.");
            foregroundGeneration = operation;
        }
        bool entered = false;
        try
        {
            await EnterForegroundAsync(linked.Token).ConfigureAwait(false);
            entered = true;
            // Finish any previous turn's outstanding control call before admission.
            await stopGate.WaitAsync(linked.Token).ConfigureAwait(false);
            stopGate.Release();
            bool stopped;
            lock (titleSync)
            {
                stopped = operation.StopRequested;
                operation.Entered = true;
            }
            if (stopped)
            {
                // The queued prompt never entered the model. Title restoration has finished.
                yield return new GgufChatStopped(false);
                yield break;
            }
            await using var stream = active.GenerateAsync(prompt, linked.Token).GetAsyncEnumerator(linked.Token);
            ValueTask<bool> next = stream.MoveNextAsync();
            operation.AdapterEntered.TrySetResult();
            while (await next.ConfigureAwait(false))
            {
                yield return stream.Current;
                next = stream.MoveNextAsync();
            }
        }
        finally
        {
            lock (titleSync)
            {
                if (ReferenceEquals(foregroundGeneration, operation)) foregroundGeneration = null;
                if (entered) operationGate.Release();
                operation.Finished.TrySetResult();
            }
        }
    }

    public async ValueTask StopAsync(CancellationToken cancellationToken)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, lifetime.Token);
        ForegroundGeneration? operation;
        bool queued;
        lock (titleSync)
        {
            operation = foregroundGeneration;
            queued = operation is not null && !operation.Entered;
            if (queued) operation!.StopRequested = true;
        }
        if (operation is null)
        {
            // Preserve idle/retirement Stop behavior without targeting a title or
            // another operation that has acquired native ownership in the meantime
            if (!operationGate.Wait(0)) return;
            try
            {
                await stopGate.WaitAsync(linked.Token).ConfigureAwait(false);
                try { await active.StopAsync(linked.Token).ConfigureAwait(false); }
                finally { stopGate.Release(); }
            }
            finally { operationGate.Release(); }
            return;
        }
        if (queued)
        {
            await operation.Finished.Task.WaitAsync(linked.Token).ConfigureAwait(false);
            return;
        }
        await stopGate.WaitAsync(linked.Token).ConfigureAwait(false);
        try
        {
            await Task.WhenAny(operation.AdapterEntered.Task, operation.Finished.Task).WaitAsync(linked.Token).ConfigureAwait(false);
            lock (titleSync)
                if (!ReferenceEquals(foregroundGeneration, operation)) return;
            await active.StopAsync(linked.Token).ConfigureAwait(false);
        }
        finally { stopGate.Release(); }
    }

    public ValueTask DisposeAsync()
    {
        lock (disposalSync)
        {
            if (disposal is null) { lifetime.Cancel(); disposal = DisposeCoreAsync(); }
            return new ValueTask(disposal);
        }
    }

    private async Task DisposeCoreAsync()
    {
        await operationGate.WaitAsync().ConfigureAwait(false);
        try
        {
            await stopGate.WaitAsync().ConfigureAwait(false);
            try { await active.DisposeAsync().ConfigureAwait(false); }
            finally { stopGate.Release(); }
        }
        finally { operationGate.Release(); }
    }
}
