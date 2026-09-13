using System.Reflection;
using GraniteEdgeAI.ModelInspection.LlamaSharp.ModelProbe;

namespace GraniteEdgeAI.ModelInspection.Worker;

/// <summary>
/// Composes the production worker from inherited standard streams and the
/// production VocabOnly engine. No command-line argument carries a model path.
/// </summary>
internal static class Program
{
    internal static async Task<int> Main()
    {
        await using Stream input = Console.OpenStandardInput();
        await using Stream output = Console.OpenStandardOutput();
        await using Stream error = Console.OpenStandardError();
        string version = typeof(Program).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion ??
            typeof(Program).Assembly.GetName().Version?.ToString() ??
            "0.0.0";

        await using WorkerHost host = new(
            input,
            output,
            error,
            () => CreateInspectionEngine(version),
            new ParentProcessMonitor(),
            Environment.ProcessId,
            version);
        return await host.RunAsync(CancellationToken.None)
            .ConfigureAwait(false);
    }

    internal static IWorkerInspectionEngine CreateInspectionEngine(
        string workerVersion) =>
        new LlamaSharpInspectionEngine(
            new VocabOnlyModelProbe(),
            workerVersion);
}
