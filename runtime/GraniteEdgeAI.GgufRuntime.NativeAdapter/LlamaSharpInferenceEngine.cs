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
    private LLamaWeights? _weights;
    private LLamaContext? _context;
    private ChatSession? _session;

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
        var executor = new InteractiveExecutor(_context);
        _session = new ChatSession(executor, CreateHistory(initialHistory))
            .WithHistoryTransform(new PromptTemplateTransformer(
                _weights,
                withAssistant: true));
    }

    public async IAsyncEnumerable<string> GenerateAsync(
        string prompt,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ChatSession session = _session
            ?? throw new InvalidOperationException("The inference engine is not initialized.");
        var inference = new InferenceParams
        {
            MaxTokens = Math.Min(
                options.MaximumGeneratedTokens,
                checked((int)options.ContextSize / 2)),
            SamplingPipeline = new DefaultSamplingPipeline(),
        };
        var message = new ChatHistory.Message(AuthorRole.User, prompt);
        await foreach (string chunk in session.ChatAsync(
                           message,
                           inference,
                           cancellationToken).ConfigureAwait(false))
        {
            yield return chunk;
        }
    }

    public ValueTask DisposeAsync()
    {
        _context?.Dispose();
        _weights?.Dispose();
        _session = null;
        _context = null;
        _weights = null;
        return ValueTask.CompletedTask;
    }

    internal static ChatHistory CreateHistory(
        IReadOnlyList<GgufAdapterMessage> messages)
    {
        ArgumentNullException.ThrowIfNull(messages);
        var history = new ChatHistory();
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

    private static GGMLType MapCacheType(GgufAdapterCacheType cacheType) =>
        cacheType switch
        {
            GgufAdapterCacheType.F16 => GGMLType.GGML_TYPE_F16,
            GgufAdapterCacheType.Q8Zero => GGMLType.GGML_TYPE_Q8_0,
            GgufAdapterCacheType.Q4Zero => GGMLType.GGML_TYPE_Q4_0,
            _ => throw new ArgumentOutOfRangeException(nameof(cacheType)),
        };
}
