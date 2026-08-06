namespace GraniteEdgeAI.ModelInspection.ProtocolTestWorker;

/// <summary>
/// Composes the isolated abnormal-process fixture from inherited standard
/// streams. Exit 64 means the fixture command line was not exactly recognized.
/// </summary>
internal static class Program
{
    private const int UsageError = 64;

    internal static async Task<int> Main(string[] args)
    {
        if (!TestWorkerScenarioParser.TryParse(
                args,
                out TestWorkerScenarioRequest? request) ||
            request is null)
        {
            return UsageError;
        }

        await using Stream input = Console.OpenStandardInput();
        await using Stream output = Console.OpenStandardOutput();
        await using Stream error = Console.OpenStandardError();
        ProtocolScenarioRunner runner = new(input, output, error);
        return await runner.RunAsync(request).ConfigureAwait(false);
    }
}
