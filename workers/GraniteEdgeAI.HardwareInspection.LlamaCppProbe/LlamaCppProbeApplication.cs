namespace GraniteEdgeAI.HardwareInspection.LlamaCppProbe;

internal static class LlamaCppProbeApplication
{
    private const int UsageExitCode = 64;
    private const int NativeUnavailableExitCode = 70;
    private const int OutputFailureExitCode = 74;

    internal static async Task<int> RunAsync(
        string[] args,
        Stream stdout,
        ILlamaCppNativeCapabilityApi nativeApi)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(stdout);
        ArgumentNullException.ThrowIfNull(nativeApi);

        bool identity = IsExactCommand(args, "identity");
        bool capabilities = IsExactCommand(args, "capabilities");
        if (!identity && !capabilities)
        {
            return UsageExitCode;
        }

        byte[] output;
        if (identity)
        {
            output = LlamaCppProbeProtocol.CreateIdentity();
        }
        else
        {
            LlamaCppNativeCapabilityResult result = nativeApi.Capture();
            if (!result.IsAvailable)
            {
                return NativeUnavailableExitCode;
            }

            output = LlamaCppProbeProtocol.CreateCapabilities(result.Devices);
        }

        int maximumOutputBytes = identity ? 4 * 1024 : 64 * 1024;
        if (output.Length > maximumOutputBytes)
        {
            return OutputFailureExitCode;
        }

        try
        {
            await stdout.WriteAsync(output).ConfigureAwait(false);
            return 0;
        }
        catch (Exception exception) when (exception is
            IOException or
            ObjectDisposedException or
            NotSupportedException or
            UnauthorizedAccessException)
        {
            return OutputFailureExitCode;
        }
    }

    private static bool IsExactCommand(string[] args, string command) =>
        args.Length == 3 &&
        string.Equals(args[0], command, StringComparison.Ordinal) &&
        string.Equals(args[1], "--format", StringComparison.Ordinal) &&
        string.Equals(args[2], "json-v1", StringComparison.Ordinal);
}
