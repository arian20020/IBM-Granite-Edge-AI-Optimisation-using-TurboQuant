namespace GraniteEdgeAI.ModelInspection.ProtocolTestWorker;

/// <summary>
/// Provides the smallest harmless process used to prove Task 6 launch
/// containment. Later abnormal protocol scenarios remain isolated in this
/// test-only executable and never enter the production worker.
/// </summary>
internal static class Program
{
    private const int UsageError = 64;

    internal static async Task<int> Main(string[] args)
    {
        // Accept exactly one fixture-only mode. Extra or unknown arguments
        // cannot accidentally become an unreviewed production launch path.
        if (args.Length != 1 ||
            !string.Equals(args[0], "launch-probe", StringComparison.Ordinal))
        {
            return UsageError;
        }

        // Signal only after standard streams are available, then remain alive
        // until the parent closes or writes one line to standard input.
        await Console.Out.WriteLineAsync("fixture-ready").ConfigureAwait(false);
        await Console.Out.FlushAsync().ConfigureAwait(false);
        _ = await Console.In.ReadLineAsync().ConfigureAwait(false);
        return 0;
    }
}
