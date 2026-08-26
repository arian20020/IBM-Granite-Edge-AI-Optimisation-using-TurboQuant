using System.Diagnostics;
using GraniteEdgeAI.GgufQuantization.Capabilities;
using GraniteEdgeAI.GgufQuantization.Contracts;

namespace GraniteEdgeAI.GgufQuantization.WorkerClient;

public sealed class GgufQuantizationWorkerClient
{
    private readonly TimeSpan _timeout;

    public GgufQuantizationWorkerClient(TimeSpan timeout)
    {
        if (timeout <= TimeSpan.Zero || timeout > TimeSpan.FromHours(6))
        {
            throw new ArgumentOutOfRangeException(nameof(timeout));
        }
        _timeout = timeout;
    }

    public async Task<GgufQuantizationEvent> ExecuteAsync(
        GgufQuantizationCommand command,
        VerifiedGgufQuantizerPackage package,
        GgufQuantizationFileLease lease,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(package);
        ArgumentNullException.ThrowIfNull(lease);
        cancellationToken.ThrowIfCancellationRequested();

        package = GgufQuantizerPackageVerifier.Reverify(package);
        if (!string.Equals(command.ToolManifestSha256, package.ManifestSha256, StringComparison.Ordinal))
        {
            return Failed(command, GgufQuantizationSupportCode.PackageVerificationFailed, 0);
        }
        lease.Validate();
        if (lease.SourceLengthBytes > (ulong)package.MaximumSourceBytes)
        {
            return Failed(command, GgufQuantizationSupportCode.SourceChanged, 0);
        }

        var start = new ProcessStartInfo
        {
            FileName = package.ExecutablePath,
            WorkingDirectory = Path.GetDirectoryName(lease.OutputPath)!,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (string argument in BuildArguments(command, lease.SourcePath, lease.OutputPath))
        {
            start.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = start, EnableRaisingEvents = true };
        using var timeout = new CancellationTokenSource(_timeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        try
        {
            if (!process.Start())
            {
                return Failed(command, GgufQuantizationSupportCode.ProcessFailed, 0);
            }

            Task output = DrainBoundedAsync(
                process.StandardOutput, package.StandardOutputMaximumBytes, linked.Token);
            Task error = DrainBoundedAsync(
                process.StandardError, package.StandardErrorMaximumBytes, linked.Token);
            await Task.WhenAll(process.WaitForExitAsync(linked.Token), output, error)
                .ConfigureAwait(false);

            if (process.ExitCode != 0)
            {
                lease.DeletePendingOutput();
                return Failed(command, GgufQuantizationSupportCode.ProcessFailed, 0);
            }
            lease.VerifySourceUnchanged();
            var result = new FileInfo(lease.OutputPath);
            if (!result.Exists
                || result.Length <= 0
                || result.Length > package.MaximumOutputBytes
                || (result.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                lease.DeletePendingOutput();
                return Failed(command, GgufQuantizationSupportCode.OutputInvalid, 0);
            }

            return GgufQuantizationEvent.Create(
                command.CorrelationId,
                command.OptimizationPlanId,
                command.ConfigurationSha256,
                GgufQuantizationEventKind.Completed,
                100,
                GgufQuantizationSupportCode.None,
                command.OutputToken,
                command.RequantizationAuthorizationSha256);
        }
        catch (OperationCanceledException) when (linked.IsCancellationRequested)
        {
            TryKill(process);
            lease.DeletePendingOutput();
            if (cancellationToken.IsCancellationRequested)
            {
                throw new OperationCanceledException(cancellationToken);
            }
            return Failed(command, GgufQuantizationSupportCode.TimedOut, 0);
        }
        catch (InvalidDataException)
        {
            TryKill(process);
            lease.DeletePendingOutput();
            return Failed(command, GgufQuantizationSupportCode.ProtocolViolation, 0);
        }
        catch
        {
            TryKill(process);
            lease.DeletePendingOutput();
            throw;
        }
    }

    internal static string[] BuildArguments(
        GgufQuantizationCommand command,
        string sourcePath,
        string outputPath)
    {
        var arguments = new List<string>(4);
        if (command.RequantizationAuthorizationSha256 is not null)
        {
            arguments.Add("--allow-requantize");
        }
        arguments.Add(sourcePath);
        arguments.Add(outputPath);
        arguments.Add(GgufQuantizerFormatMap.ToToolToken(
            GgufQuantizerFormatMap.ToCore(command.TargetFormat)));
        return arguments.ToArray();
    }

    private static async Task DrainBoundedAsync(
        StreamReader reader,
        int maximumCharacters,
        CancellationToken cancellationToken)
    {
        char[] buffer = new char[1024];
        int total = 0;
        while (true)
        {
            int read = await reader.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                return;
            }
            total = checked(total + read);
            if (total > maximumCharacters)
            {
                throw new InvalidDataException("Quantizer output exceeded its closed bound.");
            }
        }
    }

    private static GgufQuantizationEvent Failed(
        GgufQuantizationCommand command,
        GgufQuantizationSupportCode code,
        int progress) =>
        GgufQuantizationEvent.Create(
            command.CorrelationId,
            command.OptimizationPlanId,
            command.ConfigurationSha256,
            GgufQuantizationEventKind.Failed,
            progress,
            code,
            requantizationAuthorizationSha256: command.RequantizationAuthorizationSha256);

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit(5000);
            }
        }
        catch
        {
            // Cleanup remains bounded to the process tree started by this client.
        }
    }
}
