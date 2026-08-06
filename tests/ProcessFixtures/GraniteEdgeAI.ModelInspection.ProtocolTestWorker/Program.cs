using System.Globalization;
using System.Runtime.InteropServices;
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
        long handleValue = 0;
        bool isLaunchProbe =
            args.Length == 1 &&
            string.Equals(args[0], "launch-probe", StringComparison.Ordinal);
        bool isHandleProbe =
            args.Length == 2 &&
            string.Equals(
                args[0],
                "probe-unrelated-handle",
                StringComparison.Ordinal) &&
            long.TryParse(
                args[1],
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out handleValue);

        // Unknown or extra fixture arguments fail without touching model data.
        if (!isLaunchProbe && !isHandleProbe)
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

        string milestone;
        if (isHandleProbe)
        {
            // The parent deliberately marks this Event handle inheritable but
            // excludes it from PROC_THREAD_ATTRIBUTE_HANDLE_LIST. Signalling
            // must therefore fail in this child process.
            milestone = SetEvent(new IntPtr(handleValue))
                ? "handle-signaled"
                : "handle-unavailable";
        }
        else
        {
            milestone = "fixture-ready";
        }

        await writer.WriteLineAsync(milestone).ConfigureAwait(false);
        _ = await reader.ReadLineAsync().ConfigureAwait(false);
        return 0;
    }

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetEvent(IntPtr eventHandle);
}
