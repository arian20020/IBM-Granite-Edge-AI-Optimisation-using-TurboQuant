namespace GraniteEdgeAI.GgufRuntime.NativeAdapter;

internal sealed class GgufAdapterHost(
    IGgufInferenceEngine engine,
    TextReader input,
    TextWriter output)
{
    internal async Task<int> RunAsync(CancellationToken cancellationToken)
    {
        var history = new List<GgufAdapterMessage>();
        try
        {
            while (await input.ReadLineAsync(cancellationToken).ConfigureAwait(false)
                   is { } frame)
            {
                GgufAdapterCommand command = GgufAdapterProtocol.Parse(frame);
                if (command.Type == GgufAdapterCommandType.Turn)
                {
                    history.Add(command.Message!);
                    continue;
                }

                if (command.Type != GgufAdapterCommandType.Start)
                {
                    throw new InvalidOperationException("Start was required.");
                }

                try
                {
                    await engine.InitializeAsync(history, cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (GgufUnsupportedChatTemplateException)
                {
                    await WriteFrameAsync(
                        "G1FAIL chat-template-unsupported",
                        CancellationToken.None).ConfigureAwait(false);
                    return 70;
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    await WriteFrameAsync(
                        "G1FAIL model-load-failed",
                        CancellationToken.None).ConfigureAwait(false);
                    return 70;
                }
                await WriteFrameAsync("G1READY", cancellationToken).ConfigureAwait(false);
                return await RunPromptsAsync(cancellationToken).ConfigureAwait(false);
            }

            throw new InvalidOperationException("Start was not received.");
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or ArgumentException or FormatException)
        {
            await WriteFrameAsync("G1FAIL protocol-violation", CancellationToken.None)
                .ConfigureAwait(false);
            return 65;
        }
    }

    private async Task<int> RunPromptsAsync(CancellationToken cancellationToken)
    {
        Task<string?>? pendingRead = null;
        while (await (pendingRead ?? input.ReadLineAsync(cancellationToken).AsTask()).ConfigureAwait(false)
               is { } frame)
        {
            pendingRead = null;
            GgufAdapterCommand command = GgufAdapterProtocol.Parse(frame);
            if (command.Type == GgufAdapterCommandType.Stop)
            {
                continue;
            }

            if (command.Type == GgufAdapterCommandType.Title)
            {
                pendingRead = await RunGenerationAsync(command.Content!, true, cancellationToken).ConfigureAwait(false);
                continue;
            }

            if (command.Type != GgufAdapterCommandType.Prompt)
            {
                throw new InvalidOperationException("A prompt was required.");
            }

            pendingRead = await RunGenerationAsync(command.Content!, false, cancellationToken).ConfigureAwait(false);
        }

        return 0;
    }

    private async Task<Task<string?>?> RunGenerationAsync(string prompt, bool isTitle, CancellationToken sessionToken)
    {
        if (isTitle && engine is not IGgufTitleInferenceEngine)
            throw new InvalidOperationException("Temporary title inference is unavailable.");
        await WriteFrameAsync("G1RESPONSE", sessionToken).ConfigureAwait(false);
        using var generationStop = CancellationTokenSource.CreateLinkedTokenSource(sessionToken);
        async Task<string> PumpAsync()
        {
            string? reason = null;
            try
            {
                var events = isTitle
                    ? ((IGgufTitleInferenceEngine)engine).GenerateTitleAsync(prompt, generationStop.Token)
                    : engine.GenerateAsync(prompt, generationStop.Token);
                await foreach (var item in events.ConfigureAwait(false))
                {
                    if (reason is not null) throw new InvalidOperationException("Data after title completion.");
                    if (item is GgufAdapterTextDelta { Text.Length: > 0 } delta)
                        await WriteFrameAsync(GgufAdapterProtocol.EncodeDelta(delta.Text), sessionToken).ConfigureAwait(false);
                    else if (item is GgufAdapterCompleted done)
                        reason = done.Reason switch
                        {
                            GgufAdapterCompletionReason.Length => "length",
                            GgufAdapterCompletionReason.Stop => "stop",
                            _ => throw new InvalidOperationException("Unsupported completion reason."),
                        };
                    else if (item is not GgufAdapterTextDelta)
                        throw new InvalidOperationException("The inference engine emitted an unsupported event.");
                }
                return reason ?? throw new InvalidOperationException("Title completion is missing.");
            }
            catch (OperationCanceledException) when (generationStop.IsCancellationRequested && !sessionToken.IsCancellationRequested)
            {
                // Iterator disposal has restored the original inference history.
                return "stop";
            }
            catch (GgufContextLimitException)
            {
                // The title iterator has restored the retained conversation before rejecting its budget.
                return "context-limit-reached";
            }
        }
        Task<string> generation = Task.Run(PumpAsync, sessionToken);
        // Console.In may synchronously block inside ReadLineAsync. The title must
        // still publish its terminal frame when no further command is entered.
        Task<string?> read = Task.Run(async () =>
            await input.ReadLineAsync(sessionToken).ConfigureAwait(false), sessionToken);
        Task winner = await Task.WhenAny((Task)generation, read).ConfigureAwait(false);
        if (ReferenceEquals(winner, read))
        {
            bool isStop;
            try
            {
                string? next = await read.ConfigureAwait(false);
                isStop = next is not null && GgufAdapterProtocol.Parse(next).Type == GgufAdapterCommandType.Stop;
            }
            catch
            {
                generationStop.Cancel();
                try { await generation.ConfigureAwait(false); }
                catch { /* Preserve the input failure after observing generation cleanup. */ }
                throw;
            }
            if (isStop || isTitle) generationStop.Cancel();
            string reason = await generation.ConfigureAwait(false);
            if (isTitle && !isStop)
                throw new InvalidOperationException("Only Stop is accepted during a title.");
            await WriteFrameAsync(reason == "context-limit-reached" ? "G1FAIL context-limit-reached" : $"G1DONE {reason}", sessionToken).ConfigureAwait(false);
            return isStop ? null : read;
        }
        string completedReason = await generation.ConfigureAwait(false);
        await WriteFrameAsync(completedReason == "context-limit-reached" ? "G1FAIL context-limit-reached" : $"G1DONE {completedReason}", sessionToken).ConfigureAwait(false);
        // Keep exactly this read: abandoning it could swallow the next prompt.
        return read;
    }

    private async Task WriteFrameAsync(
        string frame,
        CancellationToken cancellationToken)
    {
        await output.WriteLineAsync(frame.AsMemory(), cancellationToken)
            .ConfigureAwait(false);
        await output.FlushAsync(cancellationToken).ConfigureAwait(false);
    }
}
