using GraniteEdgeAI.GgufRuntime.Contracts;
using GraniteEdgeAI.GgufRuntime.Contracts.Commands;
using GraniteEdgeAI.GgufRuntime.Contracts.Configuration;
using GraniteEdgeAI.GgufRuntime.Contracts.Events;
using GraniteEdgeAI.GgufRuntime.Contracts.Session;
using GraniteEdgeAI.GgufRuntime.Transport;
using GraniteEdgeAI.GgufRuntime.Worker.Session;

namespace GraniteEdgeAI.GgufRuntime.Worker;

internal sealed class GgufWorkerHost : IAsyncDisposable
{
    private static readonly TimeSpan StopDeadline = TimeSpan.FromMilliseconds(250);
    private readonly GgufWorkerBootstrap _bootstrap;
    private readonly GgufFramedChannel _channel;
    private GgufCliSession? _cli;
    private StartSessionCommand? _start;
    private long _sequence;
    private bool _reloadRequired;

    internal GgufWorkerHost(GgufWorkerBootstrap bootstrap, GgufFramedChannel channel)
    {
        _bootstrap = bootstrap;
        _channel = channel;
    }

    internal async Task<int> RunAsync(CancellationToken cancellationToken)
    {
        GgufRuntimeCommand first = await _channel.ReadCommandAsync(cancellationToken)
            .ConfigureAwait(false);
        if (first is not StartSessionCommand start)
        {
            return 64;
        }

        _start = start;
        await _channel.WriteEventAsync(
            new SessionLoadingEvent(
                GgufProtocolVersion.Current,
                start.RequestId,
                start.SessionId,
                NextSequence(),
                "load-model"),
            cancellationToken).ConfigureAwait(false);
        await StartCliAsync(cancellationToken).ConfigureAwait(false);
        await _channel.WriteEventAsync(
            new SessionReadyEvent(
                GgufProtocolVersion.Current,
                start.RequestId,
                start.SessionId,
                NextSequence()),
            cancellationToken).ConfigureAwait(false);

        Task<GgufRuntimeCommand>? pendingRead = null;
        while (true)
        {
            GgufRuntimeCommand command = pendingRead is null
                ? await _channel.ReadCommandAsync(cancellationToken).ConfigureAwait(false)
                : await pendingRead.ConfigureAwait(false);
            pendingRead = null;
            if (command is CloseSessionCommand close)
            {
                await CloseCliAsync(cancellationToken).ConfigureAwait(false);
                await _channel.WriteEventAsync(
                    new SessionClosedEvent(
                        GgufProtocolVersion.Current,
                        close.RequestId,
                        close.SessionId,
                        NextSequence(),
                        true),
                    cancellationToken).ConfigureAwait(false);
                return 0;
            }

            if (command is not SubmitPromptCommand prompt)
            {
                return 65;
            }

            if (_reloadRequired)
            {
                await StartCliAsync(cancellationToken).ConfigureAwait(false);
                _reloadRequired = false;
            }

            await _cli!.WritePromptAsync(prompt.Content, cancellationToken)
                .ConfigureAwait(false);
            await _channel.WriteEventAsync(
                new ResponseStartedEvent(
                    GgufProtocolVersion.Current,
                    prompt.RequestId,
                    prompt.SessionId,
                    NextSequence()),
                cancellationToken).ConfigureAwait(false);

            using var generationCancellation =
                CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            Task generation = PumpGenerationAsync(prompt, generationCancellation.Token);
            Task<GgufRuntimeCommand> read = _channel.ReadCommandAsync(cancellationToken).AsTask();
            Task winner = await Task.WhenAny(generation, read).ConfigureAwait(false);
            if (ReferenceEquals(winner, generation))
            {
                await generation.ConfigureAwait(false);
                await _channel.WriteEventAsync(
                    new ResponseCompletedEvent(
                        GgufProtocolVersion.Current,
                        prompt.RequestId,
                        prompt.SessionId,
                        NextSequence()),
                    cancellationToken).ConfigureAwait(false);
                pendingRead = read;
                continue;
            }

            GgufRuntimeCommand duringGeneration = await read.ConfigureAwait(false);
            if (duringGeneration is CloseSessionCommand closeDuringGeneration)
            {
                generationCancellation.Cancel();
                await CloseCliAsync(CancellationToken.None).ConfigureAwait(false);
                await IgnoreGenerationFailureAsync(generation).ConfigureAwait(false);
                await _channel.WriteEventAsync(
                    new SessionClosedEvent(
                        GgufProtocolVersion.Current,
                        closeDuringGeneration.RequestId,
                        closeDuringGeneration.SessionId,
                        NextSequence(),
                        true),
                    CancellationToken.None).ConfigureAwait(false);
                return 0;
            }

            if (duringGeneration is not StopGenerationCommand)
            {
                return 66;
            }

            _ = await _cli.TryInterruptAsync(cancellationToken).ConfigureAwait(false);
            Task completed = await Task.WhenAny(
                generation,
                Task.Delay(StopDeadline, cancellationToken)).ConfigureAwait(false);
            GgufStopDisposition disposition;
            if (ReferenceEquals(completed, generation))
            {
                await generation.ConfigureAwait(false);
                disposition = GgufStopDisposition.Stopped;
            }
            else
            {
                generationCancellation.Cancel();
                await _cli.TerminateAsync(CancellationToken.None).ConfigureAwait(false);
                await IgnoreGenerationFailureAsync(generation).ConfigureAwait(false);
                await _cli.DisposeAsync().ConfigureAwait(false);
                _cli = null;
                _reloadRequired = true;
                disposition = GgufStopDisposition.StoppedNeedsReload;
            }

            await _channel.WriteEventAsync(
                new ResponseStoppedEvent(
                    GgufProtocolVersion.Current,
                    prompt.RequestId,
                    prompt.SessionId,
                    NextSequence(),
                    disposition),
                cancellationToken).ConfigureAwait(false);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await CloseCliAsync(CancellationToken.None).ConfigureAwait(false);
        _channel.Dispose();
    }

    private async Task StartCliAsync(CancellationToken cancellationToken)
    {
        StartSessionCommand start = _start
            ?? throw new InvalidOperationException("The session has not started.");
        _cli = new GgufCliSession(
            _bootstrap.CliExecutable,
            _bootstrap.CliArgumentsOverride);
        await _cli.StartAsync(
            _bootstrap.ModelFile,
            start.Configuration,
            start.InitialTurns,
            cancellationToken).ConfigureAwait(false);
    }

    private async Task PumpGenerationAsync(
        SubmitPromptCommand prompt,
        CancellationToken cancellationToken)
    {
        while (await _cli!.ReadOutputLineAsync(cancellationToken)
            .ConfigureAwait(false) is { } line)
        {
            GgufCliOutput output = GgufCliOutputParser.Parse(
                line,
                GgufCliOutputSource.StandardOutput);
            if (output.Kind == GgufCliOutputKind.TextDelta)
            {
                await _channel.WriteEventAsync(
                    new TextDeltaEvent(
                        GgufProtocolVersion.Current,
                        prompt.RequestId,
                        prompt.SessionId,
                        NextSequence(),
                        output.Text!),
                    cancellationToken).ConfigureAwait(false);
            }
            else if (output.Kind == GgufCliOutputKind.ResponseCompleted)
            {
                return;
            }
        }

        throw new IOException("The CLI output ended during generation.");
    }

    private async Task CloseCliAsync(CancellationToken cancellationToken)
    {
        if (_cli is null)
        {
            return;
        }

        await _cli.TerminateAsync(cancellationToken).ConfigureAwait(false);
        await _cli.DisposeAsync().ConfigureAwait(false);
        _cli = null;
    }

    private static async Task IgnoreGenerationFailureAsync(Task generation)
    {
        try
        {
            await generation.ConfigureAwait(false);
        }
        catch (Exception exception) when (
            exception is IOException or OperationCanceledException or InvalidOperationException)
        {
        }
    }

    private long NextSequence() => _sequence++;
}
