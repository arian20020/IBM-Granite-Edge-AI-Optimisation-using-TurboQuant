using GraniteEdgeAI.GgufRuntime.Contracts;
using GraniteEdgeAI.GgufRuntime.Contracts.Commands;
using GraniteEdgeAI.GgufRuntime.Contracts.Configuration;
using GraniteEdgeAI.GgufRuntime.Contracts.Events;
using GraniteEdgeAI.GgufRuntime.Contracts.Session;
using GraniteEdgeAI.GgufRuntime.Transport;
using GraniteEdgeAI.GgufRuntime.WorkerClient.Windows;

namespace GraniteEdgeAI.GgufRuntime.WorkerClient;

public sealed class GgufRuntimeClient
{
    private readonly string _workerExecutable;
    private readonly GgufWorkerBootstrap _bootstrap;

    private GgufRuntimeClient(
        string workerExecutable,
        string cliExecutable,
        string modelFile,
        IReadOnlyList<string> cliArgumentsOverride)
    {
        _workerExecutable = workerExecutable;
        _bootstrap = new GgufWorkerBootstrap(
            cliExecutable,
            modelFile,
            cliArgumentsOverride);
    }

    internal static GgufRuntimeClient CreateForTestFixture(
        string workerExecutable,
        string cliExecutable,
        string modelFile,
        string scenario) => new(
            workerExecutable,
            cliExecutable,
            modelFile,
            ["--scenario", scenario]);

    public async Task<GgufRuntimeSession> StartAsync(
        GgufRuntimeConfiguration configuration,
        IReadOnlyList<GgufConversationTurn> initialTurns,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(initialTurns);
        IReadOnlyDictionary<string, string> environment =
            GgufWorkerEnvironmentPolicy.Create(ReadEnvironment());
        GgufWorkerProcessSession process = GgufWorkerProcessLauncher.Launch(
            _workerExecutable,
            [],
            environment,
            Path.GetDirectoryName(_workerExecutable)!);
        try
        {
            await GgufFrameWriter.WriteAsync(
                process.StandardInput,
                GgufWorkerBootstrapCodec.Serialize(_bootstrap),
                cancellationToken).ConfigureAwait(false);
            var channel = new GgufFramedChannel(
                process.StandardOutput,
                process.StandardInput);
            var session = new GgufRuntimeSession(
                process,
                channel,
                new GgufSessionId(Guid.NewGuid()));
            await session.StartAsync(configuration, initialTurns, cancellationToken)
                .ConfigureAwait(false);
            return session;
        }
        catch
        {
            await process.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    private static Dictionary<string, string?> ReadEnvironment()
    {
        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (System.Collections.DictionaryEntry entry in
            Environment.GetEnvironmentVariables())
        {
            values[(string)entry.Key] = entry.Value as string;
        }

        return values;
    }
}
