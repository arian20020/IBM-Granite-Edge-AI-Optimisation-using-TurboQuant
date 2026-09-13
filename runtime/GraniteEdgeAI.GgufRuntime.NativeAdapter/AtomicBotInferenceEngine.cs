using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

namespace GraniteEdgeAI.GgufRuntime.NativeAdapter;

internal sealed class AtomicBotInferenceEngine(GgufAdapterOptions options)
    : IGgufInferenceEngine, IGgufTitleInferenceEngine
{
    private static readonly TimeSpan StartupDeadline = TimeSpan.FromSeconds(30);
    private const int MaximumStreamCharacters = 4 * 1024 * 1024;
    private readonly List<GgufAdapterMessage> _history = [];
    private Process? _process;
    private HttpClient? _client;
    private Task? _diagnosticDrain;

    public async ValueTask InitializeAsync(
        IReadOnlyList<GgufAdapterMessage> initialHistory,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(initialHistory);
        if (_process is not null)
        {
            throw new InvalidOperationException("The inference engine is already initialized.");
        }

        int port = ReserveLoopbackPort();
        var startInfo = new ProcessStartInfo
        {
            FileName = options.NativeRuntimePath!,
            WorkingDirectory = Path.GetDirectoryName(options.NativeRuntimePath!)!,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = new UTF8Encoding(false, true),
            StandardErrorEncoding = new UTF8Encoding(false, true),
        };
        foreach (string argument in AtomicBotServerArguments.Build(options, port))
        {
            startInfo.ArgumentList.Add(argument);
        }

        _process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("The native runtime did not start.");
        _diagnosticDrain = Task.WhenAll(
            DrainAsync(_process.StandardOutput),
            DrainAsync(_process.StandardError));
        _client = new HttpClient(new SocketsHttpHandler
        {
            UseProxy = false,
            AllowAutoRedirect = false,
        })
        {
            BaseAddress = new Uri($"http://127.0.0.1:{port}/", UriKind.Absolute),
            Timeout = Timeout.InfiniteTimeSpan,
        };

        await WaitUntilHealthyAsync(cancellationToken).ConfigureAwait(false);
        _history.AddRange(initialHistory);
    }

    public IAsyncEnumerable<GgufAdapterGenerationEvent> GenerateAsync(
        string prompt, CancellationToken cancellationToken) =>
        GenerateCoreAsync(prompt, _history, options.MaximumGeneratedTokens, cancellationToken);

    public IAsyncEnumerable<GgufAdapterGenerationEvent> GenerateTitleAsync(
        string prompt, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(prompt) || prompt.Length > 4096)
            throw new ArgumentOutOfRangeException(nameof(prompt));
        // The same server/weights accept an independent full-history request.
        // Never append temporary title messages to the retained conversation.
        return GenerateCoreAsync(prompt, [], 32, cancellationToken);
    }

    private async IAsyncEnumerable<GgufAdapterGenerationEvent> GenerateCoreAsync(
        string prompt,
        List<GgufAdapterMessage> retainedHistory,
        int maximumGeneratedTokens,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        HttpClient client = _client
            ?? throw new InvalidOperationException("The inference engine is not initialized.");
        GgufAdapterMessage[] history = retainedHistory.ToArray();
        byte[] countBody = AtomicBotChatRequest.Build(history, prompt, maximumGeneratedTokens);
        using var countRequest = new HttpRequestMessage(HttpMethod.Post, "v1/chat/completions/input_tokens");
        countRequest.Content = new ByteArrayContent(countBody);
        countRequest.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        using HttpResponseMessage countResponse = await client.SendAsync(countRequest,
            HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        countResponse.EnsureSuccessStatusCode();
        // This pinned endpoint applies the identical chat template and tokenizer.
        // Missing/malformed evidence fails closed; never estimate from characters.
        await using Stream countStream = await countResponse.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        byte[] countBytes = new byte[4097];
        int received = 0;
        while (received < countBytes.Length)
        {
            int read = await countStream.ReadAsync(countBytes.AsMemory(received), cancellationToken).ConfigureAwait(false);
            if (read == 0) break;
            received += read;
        }
        if (received > 4096) throw new InvalidOperationException("Model token-count evidence is oversized.");
        using JsonDocument count = JsonDocument.Parse(countBytes.AsMemory(0, received));
        if (!count.RootElement.TryGetProperty("object", out JsonElement kind)
            || kind.ValueKind != JsonValueKind.String || kind.GetString() != "response.input_tokens"
            || !count.RootElement.TryGetProperty("input_tokens", out JsonElement tokens)
            || !tokens.TryGetInt32(out int promptTokens) || promptTokens < 1)
            throw new InvalidOperationException("Invalid model token-count evidence.");
        int budget = LlamaSharpInferenceEngine.RemainingBudget(maximumGeneratedTokens,
            checked((int)options.ContextSize - 1), promptTokens);
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            "v1/chat/completions");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        request.Content = new ByteArrayContent(
            AtomicBotChatRequest.Build(history, prompt, budget));
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

        using HttpResponseMessage response = await client.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken)
            .ConfigureAwait(false);
        using var reader = new StreamReader(stream, new UTF8Encoding(false, true));
        await foreach (GgufAdapterGenerationEvent generated in ReadResponseAsync(
                           reader,
                           prompt,
                           retainedHistory,
                           cancellationToken).ConfigureAwait(false))
        {
            yield return generated;
        }
    }

    internal static async IAsyncEnumerable<GgufAdapterGenerationEvent>
        ReadResponseAsync(
            TextReader reader,
            string prompt,
            ICollection<GgufAdapterMessage> history,
            [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(prompt);
        ArgumentNullException.ThrowIfNull(history);
        var assistant = new StringBuilder();
        int received = 0;
        GgufAdapterCompletionReason? completion = null;
        bool stopped = false;
        async ValueTask<string?> ReadLineAsync()
        {
            try { return await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false); }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                stopped = true;
                throw;
            }
        }
        try
        {
        while (await ReadLineAsync().ConfigureAwait(false) is { } line)
        {
            if (line.Length == 0)
            {
                continue;
            }

            received = checked(received + line.Length);
            if (received > MaximumStreamCharacters)
            {
                throw new IOException("The native runtime response exceeded its bound.");
            }

            AtomicBotSseEvent parsed = AtomicBotSseParser.Parse(line);
            if (!string.IsNullOrEmpty(parsed.Text))
            {
                assistant.Append(parsed.Text);
                yield return new GgufAdapterTextDelta(parsed.Text);
            }
            if (parsed.CompletionReason is not null)
            {
                completion = parsed.CompletionReason;
            }
            if (parsed.Done)
            {
                break;
            }
        }

        GgufAdapterCompletionReason reason = completion
            ?? throw new InvalidDataException(
                "The native runtime stream ended without a supported completion reason.");
        history.Add(new GgufAdapterMessage(GgufAdapterRole.User, prompt));
        history.Add(new GgufAdapterMessage(GgufAdapterRole.Assistant, assistant.ToString()));
        yield return new GgufAdapterCompleted(reason);
        }
        finally
        {
            // Retain only text already exposed to the caller on an explicit cancelled read.
            // Malformed/failed streams and cancellation before any output do not commit.
            if (stopped && assistant.Length > 0)
            {
                history.Add(new GgufAdapterMessage(GgufAdapterRole.User, prompt));
                history.Add(new GgufAdapterMessage(GgufAdapterRole.Assistant, assistant.ToString()));
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        _client?.Dispose();
        _client = null;
        if (_process is not null && !_process.HasExited)
        {
            _process.Kill(entireProcessTree: true);
            await _process.WaitForExitAsync().ConfigureAwait(false);
        }
        if (_diagnosticDrain is not null)
        {
            await _diagnosticDrain.ConfigureAwait(false);
        }
        _process?.Dispose();
        _process = null;
    }

    private async Task WaitUntilHealthyAsync(CancellationToken cancellationToken)
    {
        HttpClient client = _client!;
        Process process = _process!;
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(StartupDeadline);
        while (!deadline.IsCancellationRequested)
        {
            if (process.HasExited)
            {
                throw new InvalidOperationException("The native runtime exited during startup.");
            }
            try
            {
                using HttpResponseMessage response = await client.GetAsync(
                    "health",
                    deadline.Token).ConfigureAwait(false);
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    return;
                }
            }
            catch (HttpRequestException)
            {
            }
            await Task.Delay(TimeSpan.FromMilliseconds(100), deadline.Token)
                .ConfigureAwait(false);
        }
        throw new TimeoutException("The native runtime did not become healthy.");
    }

    private static int ReserveLoopbackPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            return ((IPEndPoint)listener.LocalEndpoint).Port;
        }
        finally
        {
            listener.Stop();
        }
    }

    private static async Task DrainAsync(StreamReader reader)
    {
        char[] buffer = new char[2048];
        while (await reader.ReadAsync(buffer).ConfigureAwait(false) > 0)
        {
        }
    }
}
