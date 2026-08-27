using System.Runtime.CompilerServices;
using LLama;
using LLama.Common;
using LLama.Native;
using LLama.Sampling;
using LLama.Transformers;

namespace GraniteEdgeAI.GgufRuntime.NativeAdapter;

internal sealed class LlamaSharpInferenceEngine(GgufAdapterOptions options)
    : IGgufInferenceEngine
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

    public async IAsyncEnumerable<GgufAdapterGenerationEvent> GenerateAsync(
        string prompt,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ChatSession session = _session
            ?? throw new InvalidOperationException("The inference engine is not initialized.");
        GraniteGenerationBoundaryObserver observer = _completionObserver
            ?? throw new InvalidOperationException("The inference engine is not initialized.");
        BeginGeneration(observer);
        InferenceParams inference = CreateInferenceParameters(options);
        var message = new ChatHistory.Message(AuthorRole.User, prompt);
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
            observer.Reason ?? GgufAdapterCompletionReason.Stop;
        if (RequiresSessionReplay(reason))
        {
            ReplaySession(session.History);
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
        GgufAdapterOptions configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        return new InferenceParams
        {
            MaxTokens = checked(VisibleTokenLimit(configuration) + 1),
            SamplingPipeline = new DefaultSamplingPipeline
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
            checked((int)configuration.ContextSize / 2));
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

    private void ReplaySession(ChatHistory history)
    {
        LLamaWeights weights = _weights
            ?? throw new InvalidOperationException("The inference engine is not initialized.");
        GraniteGenerationBoundaryObserver observer = _completionObserver
            ?? throw new InvalidOperationException("The inference engine is not initialized.");
        ChatHistory replayHistory = CloneHistory(history);
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

        _context?.Dispose();
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
