using System.Threading.Channels;
using GraniteEdgeAI.Features.GgufRuntime.History;
using GraniteEdgeAI.Features.GgufRuntime.Services;
using GraniteEdgeAI.Features.Prompting;
using GraniteEdgeAI.OpenVino.Contracts;

namespace GraniteEdgeAI.Features.OpenVinoRoute;

/// <summary>Uses the verified OpenVINO session without maintaining a second transcript.</summary>
internal sealed class OpenVinoSharedChatSessionAdapter(
    Func<IReadOnlyList<OpenVinoInitialTurn>, Action<PromptEvent>, CancellationToken, Task<IPromptRouteSession>> startSession)
    : IGgufChatSession, IGgufChatTitleSession
{
    private IPromptRouteSession? session;
    private ChannelWriter<GgufChatEvent>? output;
    private object? eventScope;
    private Guid? activeTurn;
    private Guid? activeSession;
    private bool disposed;
    private bool titleQuarantined;
    private TaskCompletionSource<Guid>? titleStarted;
    private ForegroundOperation? foregroundOperation;
    private readonly SemaphoreSlim foregroundStopGate = new(1, 1);
    private sealed class ForegroundOperation
    {
        internal readonly TaskCompletionSource<Guid> Started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource Finished = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal PromptTurnResult? Result;
    }

    public async ValueTask PrepareConversationAsync(ChatConversation conversation, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        var turns = GraniteEdgeAI.Features.ChatModels.ChatHistoryReplayPolicy.GetCompletedTurns(conversation)
            .Select(message => new OpenVinoInitialTurn(message.Role switch
            {
                ChatMessageRole.User or ChatMessageRole.Control => "user",
                ChatMessageRole.Assistant => "assistant",
                _ => throw new InvalidOperationException("Unsupported conversation role.")
            }, message.Content)).ToArray();
        if (turns.Length > 512 || turns.Any(turn => System.Text.Encoding.UTF8.GetByteCount(turn.Content) > 65536)
            || turns.Sum(turn => (long)System.Text.Encoding.UTF8.GetByteCount(turn.Content)) > 512 * 1024)
            throw new GraniteEdgeAI.Features.ChatModels.ChatHistoryCapacityException();
        object candidateScope = new();
        IPromptRouteSession candidate = await startSession(turns, item => OnEvent(candidateScope, item), cancellationToken).ConfigureAwait(false);
        if (cancellationToken.IsCancellationRequested || disposed)
        {
            await candidate.DisposeAsync().ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            throw new ObjectDisposedException(nameof(OpenVinoSharedChatSessionAdapter));
        }
        IPromptRouteSession? previous = session;
        session = candidate;
        titleQuarantined = false;
        eventScope = candidateScope;
        if (previous is not null) await previous.DisposeAsync().ConfigureAwait(false);
    }

    public async IAsyncEnumerable<GgufChatEvent> GenerateAsync(string prompt,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (titleQuarantined)
        {
            yield return new GgufChatFailed("title-restore-failed", new GgufTitleRestoreException().Message);
            yield break;
        }
        IPromptRouteSession current = session ?? throw new InvalidOperationException("No prepared OpenVINO session.");
        await foregroundStopGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        foregroundStopGate.Release();
        var channel = Channel.CreateUnbounded<GgufChatEvent>(new() { SingleReader = true, SingleWriter = false });
        output = channel.Writer;
        activeTurn = null;
        activeSession = null;
        var operation = new ForegroundOperation();
        foregroundOperation = operation;
        Task generation = GenerateCoreAsync(current, prompt, channel.Writer, operation, cancellationToken);
        try
        {
            await foreach (var item in channel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false)) yield return item;
        }
        finally
        {
            try { await generation.ConfigureAwait(false); }
            finally
            {
                output = null; activeTurn = null; activeSession = null;
                if (ReferenceEquals(foregroundOperation, operation)) foregroundOperation = null;
                operation.Finished.TrySetResult();
            }
        }
    }

    private static async Task GenerateCoreAsync(IPromptRouteSession current, string prompt, ChannelWriter<GgufChatEvent> writer, ForegroundOperation operation, CancellationToken token)
    {
        try
        {
            PromptTurnResult result = await current.GenerateAsync(prompt, current.Capability.DefaultRequestedNewTokens, token).ConfigureAwait(false);
            operation.Result = result;
            writer.TryWrite(result.Status switch
            {
                PromptTurnStatus.Completed => new GgufChatCompleted(result.OutputLimitReached
                    ? GgufChatCompletionKind.Length : GgufChatCompletionKind.Stop),
                PromptTurnStatus.Stopped => new GgufChatStopped(false),
                _ => new GgufChatFailed(result.Failure?.SupportCode ?? "runtime_load_failed",
                    result.Failure?.SupportCode == "runtime_context_exceeded"
                        ? "This conversation has reached the model's context limit. Start a new chat or choose a model with more context."
                        : "The local model could not complete this response.")
            });
            writer.TryComplete();
        }
        catch (Exception error) { writer.TryComplete(error); }
    }

    private void OnEvent(object scope, PromptEvent item)
    {
        if (disposed || !ReferenceEquals(eventScope, scope)) return;
        if (item.Kind == PromptEventKind.GenerationConfirmed && item.TurnId is { } confirmed)
            titleStarted?.TrySetResult(confirmed);
        if (output is null) return;
        if (item.Kind == PromptEventKind.GenerationConfirmed && item.TurnId is { } foregroundTurn
            && item.TurnId == activeTurn && item.SessionId == activeSession)
            foregroundOperation?.Started.TrySetResult(foregroundTurn);
        if (item.Kind == PromptEventKind.GeneratingTurn && activeTurn is null)
        {
            activeTurn = item.TurnId;
            activeSession = item.SessionId;
        }
        if (item.Kind == PromptEventKind.TextDelta && item.Text is not null
            && activeTurn is not null && item.TurnId == activeTurn && item.SessionId == activeSession)
            output.TryWrite(new GgufChatDelta(item.Text));
    }

    public async Task<string?> GenerateTitleAsync(string prompt, Task stopRequested, CancellationToken token)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (stopRequested.IsCompleted) return null;
        if (titleQuarantined) throw new GgufTitleRestoreException();
        IPromptRouteSession current = session ?? throw new InvalidOperationException("No prepared OpenVINO session.");
        if (current is not ITransientPromptRouteSession capability) return null;
        var started = new TaskCompletionSource<Guid>(TaskCreationOptions.RunContinuationsAsynchronously);
        titleStarted = started;
        Task<PromptTurnResult>? generation = null;
        try
        {
            generation = capability.GenerateTitleAsync(prompt, token);
            if (ReferenceEquals(await Task.WhenAny(generation, stopRequested).ConfigureAwait(false), stopRequested) && !generation.IsCompleted)
            {
                await Task.WhenAny(started.Task, generation).ConfigureAwait(false);
                if (started.Task.IsCompletedSuccessfully && !generation.IsCompleted)
                {
                    try
                    {
                        await current.StopActiveTurnAsync(started.Task.Result, token).ConfigureAwait(false);
                    }
                    catch (InvalidOperationException)
                    {
                        // completion can clear the confirmed turn before its task finishes
                        // only that same successful terminal proves the title state was restored
                        PromptTurnResult completed = await generation.ConfigureAwait(false);
                        if (completed.Status is not (PromptTurnStatus.Completed or PromptTurnStatus.Stopped))
                            throw new GgufTitleRestoreException();
                    }
                }
            }
            PromptTurnResult result = await generation.ConfigureAwait(false);
            if (result.Status == PromptTurnStatus.Failed)
            {
                if (result.Failure?.SupportCode == "runtime_context_exceeded") return null;
                throw new GgufTitleRestoreException();
            }
            return stopRequested.IsCompleted || result.Status == PromptTurnStatus.Stopped ? null : result.Text;
        }
        catch (Exception) when (!token.IsCancellationRequested)
        {
            titleQuarantined = true;
            throw new GgufTitleRestoreException();
        }
        finally
        {
            try { if (generation is not null) await generation.ConfigureAwait(false); }
            catch { titleQuarantined = true; }
            finally { titleStarted = null; }
        }
    }

    public async ValueTask StopAsync(CancellationToken cancellationToken)
    {
        ForegroundOperation? operation = foregroundOperation;
        IPromptRouteSession? current = session;
        if (operation is null || current is null) return;
        await Task.WhenAny(operation.Started.Task, operation.Finished.Task).WaitAsync(cancellationToken).ConfigureAwait(false);
        await foregroundStopGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
        if (operation.Finished.Task.IsCompleted || !ReferenceEquals(foregroundOperation, operation)
            || !ReferenceEquals(session, current)) return;
        Guid confirmed = await operation.Started.Task.ConfigureAwait(false);
        try { await current.StopActiveTurnAsync(confirmed, cancellationToken).ConfigureAwait(false); }
        catch (InvalidOperationException)
        {
            await operation.Finished.Task.WaitAsync(TimeSpan.FromSeconds(2), cancellationToken).ConfigureAwait(false);
            if (operation.Result?.Status is not (PromptTurnStatus.Completed or PromptTurnStatus.Stopped)) throw;
        }
        }
        finally { foregroundStopGate.Release(); }
    }

    public async ValueTask DisposeAsync()
    {
        if (disposed) return;
        disposed = true;
        if (session is not null) await session.DisposeAsync().ConfigureAwait(false);
        session = null;
    }
}
