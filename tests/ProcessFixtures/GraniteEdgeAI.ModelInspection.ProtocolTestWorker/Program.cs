using System.Text;

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

        // WinExe has no interactive console. Open the three inherited standard
        // handles directly so the fixture remains a pipe-only child process.
        await using Stream input = Console.OpenStandardInput();
        await using Stream output = Console.OpenStandardOutput();
        using StreamReader reader = new(
            input,
            new UTF8Encoding(
                encoderShouldEmitUTF8Identifier: false,
                throwOnInvalidBytes: true),
            detectEncodingFromByteOrderMarks: false,
            bufferSize: 1024,
            leaveOpen: true);
        await using StreamWriter writer = new(
            output,
            new UTF8Encoding(
                encoderShouldEmitUTF8Identifier: false,
                throwOnInvalidBytes: true),
            bufferSize: 1024,
            leaveOpen: true)
        {
            AutoFlush = true,
            NewLine = "\n"
        };

        // Signal only after inherited streams are open, then remain alive until
        // the parent closes or writes one line to standard input.
        await writer.WriteLineAsync("fixture-ready").ConfigureAwait(false);
        _ = await reader.ReadLineAsync().ConfigureAwait(false);
        return 0;
    }
}
