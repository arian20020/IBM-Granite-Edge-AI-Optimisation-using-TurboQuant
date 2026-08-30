using System.Runtime.ExceptionServices;
using GraniteEdgeAI.GgufQuantization.Capabilities;
using GraniteEdgeAI.GgufQuantization.Contracts;
using GraniteEdgeAI.HardwareInspection.Foundation.Processes;

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

        TrustedToolOperationEnvironment operationEnvironment =
            TrustedToolOperationEnvironment.CreateCurrent(includeDotnetRoots: false);
        if (!WindowsKillOnCloseJob.TryCreate(out WindowsKillOnCloseJob job))
        {
            operationEnvironment.Dispose();
            if (!operationEnvironment.CleanupSucceeded)
            {
                throw new InvalidOperationException(
                    "The GGUF quantizer startup cleanup could not be verified.");
            }

            return Failed(command, GgufQuantizationSupportCode.ProcessFailed, 0);
        }

        if (!WindowsSuspendedProcess.TryStartWithOutcome(
                package.ExecutablePath,
                BuildArguments(command, lease.SourcePath, lease.OutputPath),
                Path.GetDirectoryName(lease.OutputPath)!,
                operationEnvironment,
                job,
                out WindowsSuspendedProcess? launched,
                out CleanupOutcome startCleanup))
        {
            job.Dispose();
            if (!startCleanup.Succeeded)
            {
                throw new QuantizerCleanupException(startCleanup.Failures, null);
            }

            return Failed(command, GgufQuantizationSupportCode.ProcessFailed, 0);
        }

        WindowsSuspendedProcess running = launched!;
        if (!startCleanup.Succeeded)
        {
            CleanupOutcome abort = await new BoundedCleanupCoordinator(
                new OwnedCleanupAction(OwnedCleanupStage.ProcessTree, async () =>
                {
                    if (!job.TryTerminate() ||
                        !await job.WaitForEmptyAsync(TimeSpan.FromSeconds(5))
                            .ConfigureAwait(false))
                    {
                        throw new InvalidOperationException(
                            "The GGUF quantizer startup abort could not be verified.");
                    }
                }),
                new OwnedCleanupAction(OwnedCleanupStage.Session, () =>
                {
                    running.Dispose();
                    return ValueTask.CompletedTask;
                }),
                new OwnedCleanupAction(OwnedCleanupStage.Job, () =>
                {
                    job.Dispose();
                    return ValueTask.CompletedTask;
                })).ExecuteAsync().ConfigureAwait(false);
            throw new QuantizerCleanupException(
                startCleanup.Failures.Concat(abort.Failures).Take(16).ToArray(),
                null);
        }

        using var timeout = new CancellationTokenSource(_timeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        GgufQuantizationEvent? result = null;
        Exception? primaryFailure = null;
        try
        {
            Task output = DrainBoundedAsync(
                running.StandardOutput, package.StandardOutputMaximumBytes, linked.Token);
            Task error = DrainBoundedAsync(
                running.StandardError, package.StandardErrorMaximumBytes, linked.Token);
            Task processExit = running.Process.WaitForExitAsync(linked.Token);
            Task firstTerminal = await Task.WhenAny(processExit, output, error)
                .ConfigureAwait(false);
            if (firstTerminal == output)
            {
                // A bounded drain fault is terminal. Observe it immediately so
                // cleanup terminates the contained tree rather than waiting for
                // the process-wide timeout.
                await output.ConfigureAwait(false);
            }
            else if (firstTerminal == error)
            {
                await error.ConfigureAwait(false);
            }

            await Task.WhenAll(processExit, output, error).ConfigureAwait(false);

            if (!running.TryGetExitCode(out int exitCode) || exitCode != 0)
            {
                result = Failed(command, GgufQuantizationSupportCode.ProcessFailed, 0);
            }
            else if (!await job.WaitForEmptyAsync(TimeSpan.FromSeconds(5))
                         .ConfigureAwait(false))
            {
                result = Failed(command, GgufQuantizationSupportCode.ProcessFailed, 0);
            }
            else
            {
                lease.VerifySourceUnchanged();
                var outputFile = new FileInfo(lease.OutputPath);
                result = !outputFile.Exists
                    || outputFile.Length <= 0
                    || outputFile.Length > package.MaximumOutputBytes
                    || (outputFile.Attributes & FileAttributes.ReparsePoint) != 0
                    ? Failed(command, GgufQuantizationSupportCode.OutputInvalid, 0)
                    : GgufQuantizationEvent.Create(
                        command.CorrelationId,
                        command.OptimizationPlanId,
                        command.ConfigurationSha256,
                        GgufQuantizationEventKind.Completed,
                        100,
                        GgufQuantizationSupportCode.None,
                        command.OutputToken,
                        command.RequantizationAuthorizationSha256);
            }
        }
        catch (OperationCanceledException) when (linked.IsCancellationRequested)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                primaryFailure = new OperationCanceledException(cancellationToken);
            }
            else
            {
                result = Failed(command, GgufQuantizationSupportCode.TimedOut, 0);
            }
        }
        catch (InvalidDataException)
        {
            await linked.CancelAsync().ConfigureAwait(false);
            result = Failed(command, GgufQuantizationSupportCode.ProtocolViolation, 0);
        }
        catch (Exception error)
        {
            primaryFailure = error;
        }

        bool retainOutput = result?.Kind == GgufQuantizationEventKind.Completed;
        CleanupOutcome cleanup = await new BoundedCleanupCoordinator(
            new OwnedCleanupAction(OwnedCleanupStage.ProcessTree, async () =>
            {
                if (!job.TryTerminate() ||
                    !await job.WaitForEmptyAsync(TimeSpan.FromSeconds(5))
                        .ConfigureAwait(false))
                {
                    throw new InvalidOperationException(
                        "The GGUF quantizer process tree cleanup could not be verified.");
                }
            }),
            new OwnedCleanupAction(OwnedCleanupStage.Session, () =>
            {
                running.Dispose();
                if (!running.CleanupSucceeded)
                {
                    throw new InvalidOperationException(
                        "The GGUF quantizer session cleanup could not be verified.");
                }

                return ValueTask.CompletedTask;
            }),
            new OwnedCleanupAction(OwnedCleanupStage.Job, () =>
            {
                job.Dispose();
                return ValueTask.CompletedTask;
            }),
            new OwnedCleanupAction(OwnedCleanupStage.PendingOutput, () =>
            {
                if (!retainOutput)
                {
                    lease.DeletePendingOutput();
                }

                return ValueTask.CompletedTask;
            })).ExecuteAsync().ConfigureAwait(false);

        if (primaryFailure is OperationCanceledException &&
            cancellationToken.IsCancellationRequested)
        {
            if (!cleanup.Succeeded)
            {
                throw new OperationCanceledException(
                    "The GGUF quantization was cancelled and cleanup integrity failed.",
                    new QuantizerCleanupException(cleanup.Failures, primaryFailure),
                    cancellationToken);
            }

            throw new OperationCanceledException(cancellationToken);
        }

        if (!cleanup.Succeeded)
        {
            throw new QuantizerCleanupException(cleanup.Failures, primaryFailure);
        }

        if (primaryFailure is not null)
        {
            ExceptionDispatchInfo.Capture(primaryFailure).Throw();
        }

        return result ?? throw new InvalidOperationException(
            "The GGUF quantizer did not produce a bounded outcome.");
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
        Stream stream,
        int maximumBytes,
        CancellationToken cancellationToken)
    {
        byte[] buffer = new byte[4096];
        int total = 0;
        while (true)
        {
            int read = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                return;
            }
            total = checked(total + read);
            if (total > maximumBytes)
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

    internal sealed class QuantizerCleanupException : InvalidOperationException
    {
        internal QuantizerCleanupException(
            IReadOnlyList<CleanupFailureFact> failures,
            Exception? primaryFailure)
            : base(
                "The GGUF quantizer cleanup could not be verified.",
                primaryFailure)
        {
            Failures = failures.ToArray();
        }

        internal IReadOnlyList<CleanupFailureFact> Failures { get; }
    }
}
