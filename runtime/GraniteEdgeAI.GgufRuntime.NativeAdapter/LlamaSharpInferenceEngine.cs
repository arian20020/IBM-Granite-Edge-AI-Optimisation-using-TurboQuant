using System.Runtime.CompilerServices;
using LLama;
using LLama.Common;
using LLama.Native;
using LLama.Sampling;
using LLama.Transformers;

namespace GraniteEdgeAI.GgufRuntime.NativeAdapter;

internal sealed class LlamaSharpInferenceEngine(GgufAdapterOptions options)
    : IGgufInferenceEngine, IGgufTitleInferenceEngine
{
    private const string SystemInstruction =
        "You are Granite Edge AI, a concise general-purpose assistant. " +
        "Answer the user's question directly and accurately. " +
        "Distinguish facts from uncertainty and state when you are unsure. " +
        "Check numerical claims and units before stating them. " +
        "Use only as much detail as needed unless the user asks for more.";

    private LLamaWeights? _weights;
    private LLamaContext? _context;
    private ChatSession? _session;
    private GraniteGenerationBoundaryObserver? _completionObserver;
    private bool _sessionReplayRequired;

    public async ValueTask InitializeAsync(
        IReadOnlyList<GgufAdapterMessage> initialHistory,
        CancellationToken cancellationToken)
    {
        if (_session is not null)
        {
            throw new InvalidOperationException("The inference engine is already initialized.");
        }

        ModelParams parameters = CreateModelParameters(options);
        _weights = await LLamaWeights.LoadFromFileAsync(parameters, cancellationToken)
            .ConfigureAwait(false);
        _context = _weights.CreateContext(parameters);
        ChatHistory history = CreateHistory(initialHistory);
        _completionObserver = new GraniteGenerationBoundaryObserver();
        _session = CreateSession(
            _weights,
            _context,
            history,
            _completionObserver);
    }

    public IAsyncEnumerable<GgufAdapterGenerationEvent> GenerateAsync(
        string prompt, CancellationToken cancellationToken) =>
        GenerateCoreAsync(prompt, options.MaximumGeneratedTokens, cancellationToken);

    public async IAsyncEnumerable<GgufAdapterGenerationEvent> GenerateTitleAsync(
        string prompt, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(prompt) || prompt.Length > 4096)
            throw new ArgumentOutOfRangeException(nameof(prompt));
        ChatHistory retained = CloneHistory((_session ?? throw new InvalidOperationException("The inference engine is not initialized.")).History);
        GgufAdapterCompleted? completion = null;
        try
        {
            ReplaySession(CreateHistory([]));
            _sessionReplayRequired = false;
            await foreach (var item in GenerateCoreAsync(prompt, 32, cancellationToken).ConfigureAwait(false))
            {
                if (item is GgufAdapterCompleted terminal) completion = terminal;
                else yield return item;
            }
        }
        finally
        {
            // Retain the loaded weights, but discard every temporary title token.
            // Restoration is not cancelled by foreground preemption.
            ReplaySession(retained);
            _sessionReplayRequired = false;
        }
        if (completion is not null) yield return completion;
    }

    private async IAsyncEnumerable<GgufAdapterGenerationEvent> GenerateCoreAsync(
        string prompt,
        int maximumGeneratedTokens,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (_sessionReplayRequired)
        {
            ReplaySession();
            _sessionReplayRequired = false;
        }

        ChatSession session = _session
            ?? throw new InvalidOperationException("The inference engine is not initialized.");
        GraniteGenerationBoundaryObserver observer = _completionObserver
            ?? throw new InvalidOperationException("The inference engine is not initialized.");
        BeginGeneration(observer);
        var message = new ChatHistory.Message(AuthorRole.User, prompt);
        ChatHistory candidate = CloneHistory(session.History);
        candidate.AddMessage(message.AuthorRole, message.Content);
        LLamaContext context = _context!;
        int promptTokens = context.Tokenize(session.HistoryTransform.HistoryToText(candidate), true, true).Length;
        int budget = RemainingBudget(maximumGeneratedTokens,
            checked((int)Math.Min(context.ContextSize, _weights!.ContextSize)), promptTokens);
        InferenceParams inference = CreateInferenceParameters(options, budget);
        var sampling = (ObservedSamplingPipeline)inference.SamplingPipeline;
        // A fresh executor must render the complete retained history on every turn;
        // incremental executors use a different MaxTokens accounting convention.
        _sessionReplayRequired = true;
        await foreach (string chunk in session.ChatAsync(
                           message,
                           inference,
                           cancellationToken).ConfigureAwait(false))
        {
            if (chunk.Length > 0)
            {
                yield return new GgufAdapterTextDelta(chunk);
            }
        }

        GgufAdapterCompletionReason reason =
            observer.Reason ?? (sampling.SawEndOfGeneration ? GgufAdapterCompletionReason.Stop :
                sampling.SampledCount >= budget ? GgufAdapterCompletionReason.Length : GgufAdapterCompletionReason.Stop);
        if (RequiresSessionReplay(reason))
        {
            _sessionReplayRequired = true;
        }

        yield return new GgufAdapterCompleted(reason);
    }

    public ValueTask DisposeAsync()
    {
        _context?.Dispose();
        _weights?.Dispose();
        _session = null;
        _completionObserver = null;
        _context = null;
        _weights = null;
        _sessionReplayRequired = false;
        return ValueTask.CompletedTask;
    }

    internal static ChatHistory CreateHistory(
        IReadOnlyList<GgufAdapterMessage> messages)
    {
        ArgumentNullException.ThrowIfNull(messages);
        var history = new ChatHistory();
        history.AddMessage(AuthorRole.System, SystemInstruction);
        foreach (GgufAdapterMessage message in messages)
        {
            AuthorRole role = message.Role switch
            {
                GgufAdapterRole.User => AuthorRole.User,
                GgufAdapterRole.Assistant => AuthorRole.Assistant,
                _ => throw new ArgumentOutOfRangeException(nameof(messages)),
            };
            history.AddMessage(role, message.Content);
        }

        return history;
    }

    internal static ModelParams CreateModelParameters(GgufAdapterOptions configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        return new ModelParams(configuration.ModelPath)
        {
            ContextSize = configuration.ContextSize,
            GpuLayerCount = configuration.GpuLayerCount,
            Threads = configuration.ThreadCount,
            BatchThreads = configuration.ThreadCount,
            BatchSize = configuration.BatchSize,
            TypeK = MapCacheType(configuration.KeyCacheType),
            TypeV = MapCacheType(configuration.ValueCacheType),
            FlashAttention = configuration.FlashAttention,
        };
    }

    internal static InferenceParams CreateInferenceParameters(
        GgufAdapterOptions configuration, int? effectiveBudget = null)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        return new InferenceParams
        {
            MaxTokens = effectiveBudget ?? VisibleTokenLimit(configuration),
            SamplingPipeline = new ObservedSamplingPipeline
            {
                Seed = 42,
                Temperature = 0.2f,
                RepeatPenalty = 1.1f,
            },
            AntiPrompts =
            [
                "\nUser:", "\nuser:",
                "\nAssistant:", "\nassistant:",
            ],
        };
    }

    internal static int VisibleTokenLimit(GgufAdapterOptions configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        return Math.Min(
            configuration.MaximumGeneratedTokens,
            checked((int)configuration.ContextSize));
    }

    internal static int RemainingBudget(int requested, int context, int promptTokens)
    {
        if (requested <= 0 || promptTokens < 0 || context <= promptTokens)
            throw new GgufContextLimitException();
        return Math.Min(requested, context - promptTokens);
    }

    private sealed class ObservedSamplingPipeline : DefaultSamplingPipeline
    {
        internal int SampledCount { get; private set; }
        internal bool SawEndOfGeneration { get; private set; }
        public override LLamaToken Sample(SafeLLamaContextHandle context, int index)
        {
            LLamaToken token = base.Sample(context, index);
            SampledCount++;
            SawEndOfGeneration |= token.IsEndOfGeneration(context.Vocab);
            return token;
        }
    }

    internal static void BeginGeneration(
        GraniteGenerationBoundaryObserver observer)
    {
        ArgumentNullException.ThrowIfNull(observer);
        observer.Reset();
    }

    internal static bool RequiresSessionReplay(
        GgufAdapterCompletionReason reason) =>
        reason == GgufAdapterCompletionReason.Length;

    private ChatSession CreateSession(
        LLamaWeights weights,
        LLamaContext context,
        ChatHistory history,
        GraniteGenerationBoundaryObserver observer)
    {
        var historyTransform = new PromptTemplateTransformer(
            weights,
            withAssistant: true);
        PreflightTemplate(historyTransform, history);
        return new ChatSession(new InteractiveExecutor(context), history)
            .WithHistoryTransform(historyTransform)
            .WithOutputTransform(new GraniteTurnBoundaryTextTransform(
                VisibleTokenLimit(options),
                observer));
    }

    private void ReplaySession(ChatHistory? replacement = null)
    {
        LLamaWeights weights = _weights
            ?? throw new InvalidOperationException("The inference engine is not initialized.");
        ChatSession? session = _session;
        GraniteGenerationBoundaryObserver observer = _completionObserver
            ?? throw new InvalidOperationException("The inference engine is not initialized.");
        ChatHistory replayHistory = replacement ?? CloneHistory((session
            ?? throw new InvalidOperationException("The inference engine is not initialized.")).History);
        _session = null;
        _context?.Dispose();
        _context = null;

        LLamaContext replayContext = weights.CreateContext(CreateModelParameters(options));
        ChatSession replaySession;
        try
        {
            replaySession = CreateSession(
                weights,
                replayContext,
                replayHistory,
                observer);
        }
        catch
        {
            replayContext.Dispose();
            throw;
        }

        _context = replayContext;
        _session = replaySession;
    }

    private static ChatHistory CloneHistory(ChatHistory source)
    {
        var clone = new ChatHistory();
        foreach (ChatHistory.Message message in source.Messages)
        {
            clone.AddMessage(message.AuthorRole, message.Content);
        }

        return clone;
    }

    private static void PreflightTemplate(
        PromptTemplateTransformer transform,
        ChatHistory history)
    {
        try
        {
            _ = transform.HistoryToText(history);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw new GgufUnsupportedChatTemplateException(exception);
        }
    }

    private static GGMLType MapCacheType(GgufAdapterCacheType cacheType) =>
        cacheType switch
        {
            GgufAdapterCacheType.F16 => GGMLType.GGML_TYPE_F16,
            GgufAdapterCacheType.Q8Zero => GGMLType.GGML_TYPE_Q8_0,
            GgufAdapterCacheType.Q4Zero => GGMLType.GGML_TYPE_Q4_0,
            _ => throw new ArgumentOutOfRangeException(nameof(cacheType)),
        };
}
