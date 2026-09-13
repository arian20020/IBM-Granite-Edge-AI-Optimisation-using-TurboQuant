using System.Diagnostics;
using System.Text;
using GraniteEdgeAI.GgufRuntime.Contracts.Commands;
using GraniteEdgeAI.GgufRuntime.Contracts.Configuration;
using GraniteEdgeAI.GgufRuntime.Transport;

namespace GraniteEdgeAI.GgufRuntime.Worker.Session;

internal sealed class GgufCliSession : IGgufCliProcess
{
    private const int MaximumDiagnosticCharacters = 64 * 1024;
    private readonly string _executable;
    private readonly IReadOnlyList<string> _argumentsOverride;
    private readonly GgufWorkerBootstrap _bootstrap;
    private Process? _process;
    private StreamReader? _output;
    private StreamWriter? _input;
    private Task? _diagnosticDrain;

    internal GgufCliSession(
        string executable,
        IReadOnlyList<string> argumentsOverride,
        GgufWorkerBootstrap bootstrap)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executable);
        ArgumentNullException.ThrowIfNull(argumentsOverride);
        _executable = executable;
        _argumentsOverride = argumentsOverride;
        _bootstrap = bootstrap;
    }

    public async ValueTask StartAsync(
        string modelPath,
        GgufRuntimeConfiguration configuration,
        IReadOnlyList<GgufConversationTurn> initialTurns,
        CancellationToken cancellationToken)
    {
        if (_process is not null)
        {
            throw new InvalidOperationException("The CLI has already started.");
        }

        IReadOnlyList<string> arguments = _argumentsOverride.Count == 0
            ? GgufCliArgumentBuilder.Build(
                modelPath,
                configuration,
                GgufNativeRuntimeSelector.Select(_bootstrap, configuration))
            : _argumentsOverride;
        var startInfo = new ProcessStartInfo
        {
            FileName = _executable,
            WorkingDirectory = Path.GetDirectoryName(_executable)!,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardInputEncoding = new UTF8Encoding(false),
            StandardOutputEncoding = new UTF8Encoding(false, true),
            StandardErrorEncoding = new UTF8Encoding(false, true),
        };
        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        _process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("The CLI process did not start.");
        _input = _process.StandardInput;
        _output = _process.StandardOutput;
        _diagnosticDrain = DrainDiagnosticsAsync(
            _process.StandardError,
            CancellationToken.None);
        foreach (string frame in GgufAdapterProtocol.EncodeInitialTurns(initialTurns))
        {
            await _input.WriteLineAsync(frame.AsMemory(), cancellationToken)
                .ConfigureAwait(false);
        }

        await _input.FlushAsync(cancellationToken).ConfigureAwait(false);
        string? ready = await _output.ReadLineAsync(cancellationToken)
            .ConfigureAwait(false);
        if (ready is null)
        {
            throw new InvalidOperationException("The adapter did not become ready.");
        }

        GgufCliOutput startup = GgufCliOutputParser.Parse(
            ready,
            GgufCliOutputSource.StandardOutput);
        if (startup.Kind == GgufCliOutputKind.Failure)
        {
            throw new GgufCliStartupException(startup.FailureCode!);
        }

        if (startup.Kind != GgufCliOutputKind.Ready)
        {
            throw new InvalidOperationException("The adapter did not become ready.");
        }
    }

    public async ValueTask WritePromptAsync(
        string content,
        CancellationToken cancellationToken)
    {
        StreamWriter input = _input
            ?? throw new InvalidOperationException("The CLI is not running.");
        string frame = GgufAdapterProtocol.EncodePrompt(content);
        await input.WriteLineAsync(frame.AsMemory(), cancellationToken)
            .ConfigureAwait(false);
        await input.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask WriteTitleAsync(string content, CancellationToken cancellationToken)
    {
        StreamWriter input = _input ?? throw new InvalidOperationException("The CLI is not running.");
        string frame = GgufAdapterProtocol.EncodeTitle(content);
        await input.WriteLineAsync(frame.AsMemory(), cancellationToken).ConfigureAwait(false);
        await input.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<string?> ReadOutputLineAsync(
        CancellationToken cancellationToken)
    {
        StreamReader output = _output
            ?? throw new InvalidOperationException("The CLI is not running.");
        return await output.ReadLineAsync(cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<bool> TryInterruptAsync(CancellationToken cancellationToken)
    {
        if (_input is null || _process is null || _process.HasExited)
        {
            return false;
        }

        string frame = GgufAdapterProtocol.EncodeStop();
        await _input.WriteLineAsync(frame.AsMemory(), cancellationToken)
            .ConfigureAwait(false);
        await _input.FlushAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    public async ValueTask TerminateAsync(CancellationToken cancellationToken)
    {
        if (_process is null || _process.HasExited)
        {
            return;
        }

        _process.Kill(entireProcessTree: true);
        await _process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        if (_process is not null && !_process.HasExited)
        {
            _process.Kill(entireProcessTree: true);
            await _process.WaitForExitAsync().ConfigureAwait(false);
        }

        _input?.Dispose();
        _output?.Dispose();
        if (_diagnosticDrain is not null)
        {
            await _diagnosticDrain.ConfigureAwait(false);
        }

        _process?.Dispose();
    }

    private static async Task DrainDiagnosticsAsync(
        StreamReader error,
        CancellationToken cancellationToken)
    {
        char[] buffer = new char[2048];
        int retained = 0;
        while (await error.ReadAsync(buffer.AsMemory(), cancellationToken)
            .ConfigureAwait(false) is int read and > 0)
        {
            retained = Math.Min(MaximumDiagnosticCharacters, retained + read);
        }

        _ = retained;
    }
}
