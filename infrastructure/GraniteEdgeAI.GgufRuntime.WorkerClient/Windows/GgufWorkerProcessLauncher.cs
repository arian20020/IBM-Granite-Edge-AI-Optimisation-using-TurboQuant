using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using GraniteEdgeAI.HardwareInspection.Foundation.Processes;

namespace GraniteEdgeAI.GgufRuntime.WorkerClient.Windows;

internal static class GgufWorkerProcessLauncher
{
    internal static GgufWorkerProcessSession Launch(
        string executablePath,
        IReadOnlyList<string> arguments,
        IReadOnlyDictionary<string, string> environment,
        string workingDirectory,
        TrustedToolOperationEnvironment operationEnvironment)
    {
        ValidateLaunchInput(executablePath, workingDirectory);
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(environment);

        GgufWorkerJob? job = null;
        GgufWorkerPipeSet? pipes = null;
        SafeFileHandle? processHandle = null;
        FileStream? standardInput = null;
        FileStream? standardOutput = null;
        FileStream? standardError = null;
        bool processCreated = false;
        BoundedCleanupCoordinator cleanup = new(
            Sync(OwnedCleanupStage.StandardInput, () => standardInput?.Dispose()),
            new OwnedCleanupAction(OwnedCleanupStage.ProcessTree, async () =>
            {
                if (processCreated && job is not null)
                {
                    job.Terminate();
                    if (!await job.WaitUntilEmptyAsync(TimeSpan.FromSeconds(5))
                            .ConfigureAwait(false))
                    {
                        throw new TimeoutException(
                            "The GGUF launch Job did not become empty.");
                    }
                }
            }),
            Sync(OwnedCleanupStage.StandardOutput, () => standardOutput?.Dispose()),
            Sync(OwnedCleanupStage.StandardError, () => standardError?.Dispose()),
            Sync(OwnedCleanupStage.Channel, () => pipes?.Dispose()),
            Sync(OwnedCleanupStage.ProcessHandle, () => processHandle?.Dispose()),
            Sync(OwnedCleanupStage.Job, () => job?.Dispose()));
        try
        {
            using GgufWindowsEnvironmentBlock environmentBlock =
                GgufWindowsEnvironmentBlock.Create(environment);
            job = GgufWorkerJob.Create();
            pipes = GgufWorkerPipeSet.Create();
            using GgufSafeAttributeList attributes = GgufSafeAttributeList.Create(2);
            attributes.AddHandleList(
                GgufNativeConstants.ProcThreadAttributeHandleList,
                [
                    pipes.ChildInput.DangerousGetHandle(),
                    pipes.ChildOutput.DangerousGetHandle(),
                    pipes.ChildError.DangerousGetHandle(),
                ]);
            attributes.AddHandleList(
                GgufNativeConstants.ProcThreadAttributeJobList,
                [job.Handle]);
            var startupInfo = new GgufStartupInfoEx
            {
                StartupInfo = new GgufStartupInfo
                {
                    Size = checked((uint)Marshal.SizeOf<GgufStartupInfoEx>()),
                    Flags = GgufNativeConstants.StartUseStandardHandles,
                    StandardInput = pipes.ChildInput.DangerousGetHandle(),
                    StandardOutput = pipes.ChildOutput.DangerousGetHandle(),
                    StandardError = pipes.ChildError.DangerousGetHandle(),
                },
                AttributeList = attributes.DangerousGetHandle(),
            };
            char[] commandLine = GgufWindowsCommandLine.Build(executablePath, arguments);
            if (!GgufNativeMethods.CreateProcess(
                    executablePath,
                    commandLine,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    inheritHandles: true,
                    GgufNativeConstants.RequiredCreationFlags,
                    environmentBlock.Pointer,
                    workingDirectory,
                    ref startupInfo,
                    out GgufProcessInformation processInformation))
            {
                throw new Win32Exception(
                    Marshal.GetLastPInvokeError(),
                    "The GGUF worker process could not be created.");
            }
            processCreated = true;

            processHandle = new SafeFileHandle(processInformation.ProcessHandle, ownsHandle: true);
            using var threadHandle = new SafeFileHandle(
                processInformation.ThreadHandle,
                ownsHandle: true);
            pipes.CloseChildHandles();
            standardInput = CreateStream(pipes.TakeParentInput(), FileAccess.Write);
            standardOutput = CreateStream(pipes.TakeParentOutput(), FileAccess.Read);
            standardError = CreateStream(pipes.TakeParentError(), FileAccess.Read);
            var session = new GgufWorkerProcessSession(
                processInformation.ProcessId,
                processHandle,
                job,
                standardInput,
                standardOutput,
                standardError,
                operationEnvironment);
            processHandle = null;
            job = null;
            standardInput = null;
            standardOutput = null;
            standardError = null;
            return session;
        }
        catch (GgufWorkerPolicyException primaryFailure)
        {
            RequireLaunchCleanup(cleanup, primaryFailure);
            throw;
        }
        catch (Exception exception) when (
            exception is Win32Exception or IOException or ArgumentException or
                UnauthorizedAccessException or InvalidOperationException)
        {
            GgufWorkerPolicyException primaryFailure = new(
                "worker-launch-failed",
                "The GGUF runtime worker could not be started.",
                exception);
            RequireLaunchCleanup(cleanup, primaryFailure);
            throw primaryFailure;
        }
        catch (Exception primaryFailure)
        {
            RequireLaunchCleanup(cleanup, primaryFailure);
            throw;
        }
        finally
        {
            _ = cleanup.ExecuteAsync().GetAwaiter().GetResult();
        }
    }

    private static void RequireLaunchCleanup(
        BoundedCleanupCoordinator cleanup,
        Exception primaryFailure)
    {
        CleanupOutcome outcome = cleanup.ExecuteAsync().GetAwaiter().GetResult();
        if (!outcome.Succeeded)
        {
            throw new GgufWorkerPolicyException(
                "worker-cleanup-failed",
                "The GGUF runtime worker launch cleanup could not be verified.",
                new CleanupIntegrityException(outcome.Failures, primaryFailure));
        }
    }

    private static OwnedCleanupAction Sync(
        OwnedCleanupStage stage,
        Action action) => new(stage, () =>
        {
            action();
            return ValueTask.CompletedTask;
        });

    private static void ValidateLaunchInput(string executablePath, string workingDirectory)
    {
        if (!Path.IsPathFullyQualified(executablePath) ||
            executablePath.StartsWith("\\\\", StringComparison.Ordinal) ||
            !File.Exists(executablePath) ||
            !Path.IsPathFullyQualified(workingDirectory) ||
            !Directory.Exists(workingDirectory))
        {
            throw new GgufWorkerPolicyException(
                "worker-launch-input-invalid",
                "The GGUF runtime worker location is invalid.");
        }
    }

    private static FileStream CreateStream(SafeFileHandle handle, FileAccess access)
    {
        try
        {
            return new FileStream(handle, access, 4096, isAsync: false);
        }
        catch
        {
            handle.Dispose();
            throw;
        }
    }
}
