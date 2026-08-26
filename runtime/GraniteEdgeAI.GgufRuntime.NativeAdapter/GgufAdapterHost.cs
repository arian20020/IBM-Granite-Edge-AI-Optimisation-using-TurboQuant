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
        while (await input.ReadLineAsync(cancellationToken).ConfigureAwait(false)
               is { } frame)
        {
            GgufAdapterCommand command = GgufAdapterProtocol.Parse(frame);
            if (command.Type == GgufAdapterCommandType.Stop)
            {
                continue;
            }

            if (command.Type != GgufAdapterCommandType.Prompt)
            {
                throw new InvalidOperationException("A prompt was required.");
            }

            await WriteFrameAsync("G1RESPONSE", cancellationToken).ConfigureAwait(false);
            GgufAdapterCompletionReason? completionReason = null;
            await foreach (GgufAdapterGenerationEvent generationEvent in engine.GenerateAsync(
                               command.Content!,
                               cancellationToken).ConfigureAwait(false))
            {
                if (completionReason is not null)
                {
                    throw new InvalidOperationException(
                        "The inference engine emitted data after completion.");
                }

                switch (generationEvent)
                {
                    case GgufAdapterTextDelta { Text.Length: > 0 } delta:
                        await WriteFrameAsync(
                            GgufAdapterProtocol.EncodeDelta(delta.Text),
                            cancellationToken).ConfigureAwait(false);
                        break;
                    case GgufAdapterTextDelta:
                        break;
                    case GgufAdapterCompleted completed:
                        completionReason = completed.Reason;
                        break;
                    default:
                        throw new InvalidOperationException(
                            "The inference engine emitted an unsupported event.");
                }
            }

            string reason = completionReason switch
            {
                GgufAdapterCompletionReason.Stop => "stop",
                GgufAdapterCompletionReason.Length => "length",
                _ => throw new InvalidOperationException(
                    "The inference engine omitted completion."),
            };
            await WriteFrameAsync($"G1DONE {reason}", cancellationToken)
                .ConfigureAwait(false);
        }

        return 0;
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
