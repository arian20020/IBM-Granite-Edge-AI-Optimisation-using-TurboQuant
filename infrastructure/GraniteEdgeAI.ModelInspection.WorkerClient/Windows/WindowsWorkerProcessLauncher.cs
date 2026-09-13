using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;
using GraniteEdgeAI.HardwareInspection.Foundation.Processes;

namespace GraniteEdgeAI.ModelInspection.WorkerClient.Windows;

/// <summary>
/// Launches one verified worker with both the Job Object and exact standard-
/// stream handle allowlist applied by STARTUPINFOEX during CreateProcessW.
/// There is deliberately no Process.Start, post-start assignment, or relaxed
/// fallback path.
/// </summary>
internal static class WindowsWorkerProcessLauncher
{
    private const string EnvironmentFailureMessage =
        "The Model Inspection worker environment could not be prepared.";
    private const string LaunchFailureMessage =
        "The Model Inspection worker process could not be started.";
    private const string ContainmentFailureMessage =
        "The Model Inspection worker process could not be contained.";
    private const string HandlePolicyFailureMessage =
        "The Model Inspection worker handle policy could not be applied.";

    internal static WorkerProcessSession Launch(
        WindowsProcessLaunchRequest request) =>
        Launch(request, WindowsWorkerProcessPlatform.Instance);

    /// <summary>
    /// Internal overload used by focused tests to make each Windows failure
    /// deterministic. The injected object represents operating-system calls,
    /// not worker behaviour or production scenario switches.
    /// </summary>
    internal static WorkerProcessSession Launch(
        WindowsProcessLaunchRequest request,
        IWindowsWorkerProcessPlatform platform)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(platform);

        WindowsJobObject? job = null;
        WindowsPipeSet? pipes = null;
        SafeProcessHandle? processHandle = null;
        SafeThreadHandle? threadHandle = null;
        FileStream? standardInput = null;
        FileStream? standardOutput = null;
        FileStream? standardError = null;
        bool processWasCreated = false;
        BoundedCleanupCoordinator cleanup = new(
            Sync(OwnedCleanupStage.StandardInput, () => standardInput?.Dispose()),
            new OwnedCleanupAction(OwnedCleanupStage.ProcessTree, async () =>
            {
                if (processWasCreated && job is not null)
                {
                    job.Terminate(exitCode: 1);
                    if (!await job.WaitUntilEmptyAsync(
                            TimeSpan.FromSeconds(5),
                            CancellationToken.None).ConfigureAwait(false))
                    {
                        throw new TimeoutException(
                            "The Model Inspection launch Job did not become empty.");
                    }
                }
            }),
            Sync(OwnedCleanupStage.StandardOutput, () => standardOutput?.Dispose()),
            Sync(OwnedCleanupStage.StandardError, () => standardError?.Dispose()),
            Sync(OwnedCleanupStage.Channel, () => pipes?.Dispose()),
            Sync(OwnedCleanupStage.ProcessHandle, () =>
            {
                threadHandle?.Dispose();
                processHandle?.Dispose();
            }),
            Sync(OwnedCleanupStage.Job, () => job?.Dispose()));

        try
        {
            if (request.Executable.VerificationHandle.IsClosed)
            {
                throw PolicyFailure(
                    WorkerClientFailureCodes.WorkerLaunchFailed,
                    LaunchFailureMessage);
            }

            using WindowsEnvironmentBlock environment =
                CreateEnvironmentBlock(platform, request.Environment);

            job = CreateJob(platform);
            pipes = CreatePipes(platform);
            using SafeAttributeListBuffer attributes =
                CreateAttributeList(platform);

            ApplyHandleAllowlist(platform, attributes, pipes);
            ApplyJobContainment(platform, attributes, job);

            StartupInfoEx startupInfo = CreateStartupInfo(attributes, pipes);
            char[] commandLine = BuildWritableCommandLine(
                request.ApplicationName,
                request.TestOnlyArguments);

            ProcessInformation processInformation = CreateProcess(
                platform,
                request,
                commandLine,
                environment.Pointer,
                ref startupInfo);
            processWasCreated = true;

            processHandle = new SafeProcessHandle(
                processInformation.ProcessHandle,
                ownsHandle: true);
            threadHandle = new SafeThreadHandle(
                processInformation.ThreadHandle,
                ownsHandle: true);
            if (processHandle.IsInvalid || threadHandle.IsInvalid)
            {
                throw PolicyFailure(
                    WorkerClientFailureCodes.WorkerLaunchFailed,
                    LaunchFailureMessage);
            }

            // The primary thread handle is not needed after a successful
            // creation. Closing it here prevents an otherwise silent leak.
            threadHandle.Dispose();
            threadHandle = null;

            // The child inherited its endpoints during CreateProcessW. Closing
            // the parent copies now is required for correct EOF propagation.
            pipes.CloseChildEndpoints();

            standardInput = CreatePipeStream(
                pipes.TakeParentStandardInputWrite(),
                FileAccess.Write);
            standardOutput = CreatePipeStream(
                pipes.TakeParentStandardOutputRead(),
                FileAccess.Read);
            standardError = CreatePipeStream(
                pipes.TakeParentStandardErrorRead(),
                FileAccess.Read);

            WorkerProcessSession session = new(
                processInformation.ProcessId,
                processHandle,
                job,
                standardInput,
                standardOutput,
                standardError,
                request.CleanupTimeout);

            // Ownership has moved into the session. Clear every local owner so
            // the finally block cannot double-dispose returned resources.
            processHandle = null;
            job = null;
            standardInput = null;
            standardOutput = null;
            standardError = null;
            return session;
        }
        catch (WorkerClientPolicyException primaryFailure)
        {
            RequireLaunchCleanup(cleanup, primaryFailure);
            throw;
        }
        catch (Exception error) when (IsExpectedLaunchError(error))
        {
            WorkerClientPolicyException primaryFailure = PolicyFailure(
                WorkerClientFailureCodes.WorkerLaunchFailed,
                LaunchFailureMessage);
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

    private static WindowsEnvironmentBlock CreateEnvironmentBlock(
        IWindowsWorkerProcessPlatform platform,
        IReadOnlyDictionary<string, string> environment)
    {
        try
        {
            return platform.CreateEnvironmentBlock(environment);
        }
        catch (WorkerClientPolicyException)
        {
            throw;
        }
        catch (Exception error) when (IsExpectedLaunchError(error))
        {
            throw PolicyFailure(
                WorkerClientFailureCodes.WorkerEnvironmentPolicyFailed,
                EnvironmentFailureMessage);
        }
    }

    private static WindowsJobObject CreateJob(
        IWindowsWorkerProcessPlatform platform)
    {
        try
        {
            return platform.CreateJob();
        }
        catch (Exception error) when (IsExpectedLaunchError(error))
        {
            throw PolicyFailure(
                WorkerClientFailureCodes.WorkerContainmentFailed,
                ContainmentFailureMessage);
        }
    }

    private static WindowsPipeSet CreatePipes(
        IWindowsWorkerProcessPlatform platform)
    {
        try
        {
            return platform.CreatePipes();
        }
        catch (Exception error) when (IsExpectedLaunchError(error))
        {
            throw PolicyFailure(
                WorkerClientFailureCodes.WorkerHandlePolicyFailed,
                HandlePolicyFailureMessage);
        }
    }

    private static SafeAttributeListBuffer CreateAttributeList(
        IWindowsWorkerProcessPlatform platform)
    {
        try
        {
            return platform.CreateAttributeList();
        }
        catch (Exception error) when (IsExpectedLaunchError(error))
        {
            throw PolicyFailure(
                WorkerClientFailureCodes.WorkerHandlePolicyFailed,
                HandlePolicyFailureMessage);
        }
    }

    private static void ApplyHandleAllowlist(
        IWindowsWorkerProcessPlatform platform,
        SafeAttributeListBuffer attributes,
        WindowsPipeSet pipes)
    {
        try
        {
            platform.ApplyHandleAllowlist(attributes, pipes);
        }
        catch (Exception error) when (IsExpectedLaunchError(error))
        {
            throw PolicyFailure(
                WorkerClientFailureCodes.WorkerHandlePolicyFailed,
                HandlePolicyFailureMessage);
        }
    }

    private static void ApplyJobContainment(
        IWindowsWorkerProcessPlatform platform,
        SafeAttributeListBuffer attributes,
        WindowsJobObject job)
    {
        try
        {
            platform.ApplyJobContainment(attributes, job);
        }
        catch (Exception error) when (IsExpectedLaunchError(error))
        {
            throw PolicyFailure(
                WorkerClientFailureCodes.WorkerContainmentFailed,
                ContainmentFailureMessage);
        }
    }

    private static ProcessInformation CreateProcess(
        IWindowsWorkerProcessPlatform platform,
        WindowsProcessLaunchRequest request,
        char[] commandLine,
        IntPtr environment,
        ref StartupInfoEx startupInfo)
    {
        try
        {
            return platform.CreateProcess(
                request.ApplicationName,
                commandLine,
                environment,
                request.WorkingDirectory,
                ref startupInfo);
        }
        catch (Exception error) when (IsExpectedLaunchError(error))
        {
            throw PolicyFailure(
                WorkerClientFailureCodes.WorkerLaunchFailed,
                LaunchFailureMessage);
        }
    }

    private static StartupInfoEx CreateStartupInfo(
        SafeAttributeListBuffer attributes,
        WindowsPipeSet pipes) => new()
    {
        StartupInfo = new StartupInfo
        {
            Size = checked((uint)Marshal.SizeOf<StartupInfoEx>()),
            Flags = NativeConstants.StartUseStandardHandles,
            StandardInput = pipes.ChildStandardInputRead.DangerousGetHandle(),
            StandardOutput = pipes.ChildStandardOutputWrite.DangerousGetHandle(),
            StandardError = pipes.ChildStandardErrorWrite.DangerousGetHandle()
        },
        AttributeList = attributes.DangerousGetHandle()
    };

    private static FileStream CreatePipeStream(
        SafeFileHandle handle,
        FileAccess access)
    {
        try
        {
            // CreatePipe does not create overlapped handles, so FileStream must
            // use synchronous handle semantics. Higher layers drain both output
            // streams concurrently to prevent pipe-buffer deadlocks.
            return new FileStream(
                handle,
                access,
                bufferSize: 4096,
                isAsync: false);
        }
        catch
        {
            handle.Dispose();
            throw;
        }
    }

    private static char[] BuildWritableCommandLine(
        string executablePath,
        IReadOnlyList<string> arguments)
    {
        StringBuilder command = new();
        AppendQuotedArgument(command, executablePath);
        foreach (string argument in arguments)
        {
            command.Append(' ');
            AppendQuotedArgument(command, argument);
        }

        command.Append('\0');
        return command.ToString().ToCharArray();
    }

    private static void AppendQuotedArgument(
        StringBuilder command,
        string argument)
    {
        command.Append('"');
        int pendingBackslashes = 0;
        foreach (char character in argument)
        {
            if (character == '\\')
            {
                pendingBackslashes++;
                continue;
            }

            if (character == '"')
            {
                command.Append('\\', checked((pendingBackslashes * 2) + 1));
                command.Append('"');
                pendingBackslashes = 0;
                continue;
            }

            command.Append('\\', pendingBackslashes);
            pendingBackslashes = 0;
            command.Append(character);
        }

        // Backslashes immediately before the closing quote must be doubled so
        // Windows command-line parsing keeps them inside the final argument.
        command.Append('\\', checked(pendingBackslashes * 2));
        command.Append('"');
    }

    private static void RequireLaunchCleanup(
        BoundedCleanupCoordinator cleanup,
        Exception primaryFailure)
    {
        CleanupOutcome outcome = cleanup.ExecuteAsync().GetAwaiter().GetResult();
        if (!outcome.Succeeded)
        {
            throw new WorkerClientPolicyException(
                new WorkerClientFailure(
                    WorkerClientFailureCodes.WorkerCleanupFailed,
                    "The Model Inspection worker launch cleanup could not be verified."),
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

    private static bool IsExpectedLaunchError(Exception error) =>
        error is Win32Exception or
        IOException or
        UnauthorizedAccessException or
        ArgumentException or
        InvalidOperationException or
        ObjectDisposedException or
        OverflowException or
        OutOfMemoryException;

    private static WorkerClientPolicyException PolicyFailure(
        string code,
        string message) =>
        WorkerClientPolicyException.For(code, message);
}
