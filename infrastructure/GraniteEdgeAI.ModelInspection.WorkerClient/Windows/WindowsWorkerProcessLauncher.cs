using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace GraniteEdgeAI.ModelInspection.WorkerClient.Windows;

/// <summary>
/// Launches one verified worker with both the Job Object and exact standard-
/// stream handle allowlist applied by STARTUPINFOEX during CreateProcessW.
/// There is deliberately no Process.Start, post-start assignment, or relaxed
/// fallback path.
/// </summary>
internal static class WindowsWorkerProcessLauncher
{
    private const string LaunchFailureMessage =
        "The Model Inspection worker process could not be started.";
    private const string ContainmentFailureMessage =
        "The Model Inspection worker process could not be contained.";
    private const string HandlePolicyFailureMessage =
        "The Model Inspection worker handle policy could not be applied.";

    internal static WorkerProcessSession Launch(
        WindowsProcessLaunchRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ObjectDisposedException.ThrowIf(
            request.Executable.VerificationHandle.IsClosed,
            request.Executable);

        WindowsJobObject? job = null;
        WindowsPipeSet? pipes = null;
        SafeProcessHandle? processHandle = null;
        SafeThreadHandle? threadHandle = null;
        FileStream? standardInput = null;
        FileStream? standardOutput = null;
        FileStream? standardError = null;
        bool processWasCreated = false;

        try
        {
            using WindowsEnvironmentBlock environment =
                WindowsEnvironmentBlock.Create(request.Environment);

            job = CreateJob();
            pipes = CreatePipes();
            using SafeAttributeListBuffer attributes = CreateAttributeList();

            ApplyHandleAllowlist(attributes, pipes);
            ApplyJobContainment(attributes, job);

            StartupInfoEx startupInfo = CreateStartupInfo(attributes, pipes);
            char[] commandLine = BuildWritableCommandLine(
                request.Executable.ExecutableFinalPath,
                request.TestOnlyArguments);

            bool created = NativeMethods.CreateProcess(
                request.Executable.ExecutableFinalPath,
                commandLine,
                IntPtr.Zero,
                IntPtr.Zero,
                inheritHandles: true,
                NativeConstants.RequiredCreationFlags,
                environment.Pointer,
                request.WorkingDirectory,
                ref startupInfo,
                out ProcessInformation processInformation);
            int createError = Marshal.GetLastPInvokeError();
            if (!created)
            {
                _ = createError;
                throw PolicyFailure(
                    WorkerClientFailureCodes.WorkerLaunchFailed,
                    LaunchFailureMessage);
            }

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
                standardError);

            // Ownership has moved into the session. Clear every local owner so
            // the finally block cannot double-dispose returned resources.
            processHandle = null;
            job = null;
            standardInput = null;
            standardOutput = null;
            standardError = null;
            return session;
        }
        catch (WorkerClientPolicyException)
        {
            TerminatePartialProcess(job, processWasCreated);
            throw;
        }
        catch (Exception error) when (IsExpectedLaunchError(error))
        {
            TerminatePartialProcess(job, processWasCreated);
            throw PolicyFailure(
                WorkerClientFailureCodes.WorkerLaunchFailed,
                LaunchFailureMessage);
        }
        finally
        {
            threadHandle?.Dispose();
            standardInput?.Dispose();
            standardOutput?.Dispose();
            standardError?.Dispose();
            pipes?.Dispose();
            processHandle?.Dispose();

            // The Job Object is deliberately the final local resource closed.
            // KILL_ON_JOB_CLOSE remains effective through every earlier cleanup.
            job?.Dispose();
        }
    }

    private static WindowsJobObject CreateJob()
    {
        try
        {
            return WindowsJobObject.CreateKillOnClose();
        }
        catch (Exception error) when (IsExpectedLaunchError(error))
        {
            throw PolicyFailure(
                WorkerClientFailureCodes.WorkerContainmentFailed,
                ContainmentFailureMessage);
        }
    }

    private static WindowsPipeSet CreatePipes()
    {
        try
        {
            return WindowsPipeSet.Create();
        }
        catch (Exception error) when (IsExpectedLaunchError(error))
        {
            throw PolicyFailure(
                WorkerClientFailureCodes.WorkerHandlePolicyFailed,
                HandlePolicyFailureMessage);
        }
    }

    private static SafeAttributeListBuffer CreateAttributeList()
    {
        try
        {
            return SafeAttributeListBuffer.Create(attributeCount: 2);
        }
        catch (Exception error) when (IsExpectedLaunchError(error))
        {
            throw PolicyFailure(
                WorkerClientFailureCodes.WorkerHandlePolicyFailed,
                HandlePolicyFailureMessage);
        }
    }

    private static void ApplyHandleAllowlist(
        SafeAttributeListBuffer attributes,
        WindowsPipeSet pipes)
    {
        try
        {
            attributes.UpdatePointerList(
                NativeConstants.ProcThreadAttributeHandleList,
                pipes.GetChildHandleAllowlist());
        }
        catch (Exception error) when (IsExpectedLaunchError(error))
        {
            throw PolicyFailure(
                WorkerClientFailureCodes.WorkerHandlePolicyFailed,
                HandlePolicyFailureMessage);
        }
    }

    private static void ApplyJobContainment(
        SafeAttributeListBuffer attributes,
        WindowsJobObject job)
    {
        try
        {
            attributes.UpdatePointerList(
                NativeConstants.ProcThreadAttributeJobList,
                [job.Handle.DangerousGetHandle()]);
        }
        catch (Exception error) when (IsExpectedLaunchError(error))
        {
            throw PolicyFailure(
                WorkerClientFailureCodes.WorkerContainmentFailed,
                ContainmentFailureMessage);
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

    private static void TerminatePartialProcess(
        WindowsJobObject? job,
        bool processWasCreated)
    {
        if (!processWasCreated || job is null)
        {
            return;
        }

        try
        {
            job.Terminate(exitCode: 1);
        }
        catch (Exception error) when (IsExpectedLaunchError(error))
        {
            // The final Job Object close in the caller remains the fail-closed
            // containment fallback even when explicit termination races/fails.
        }
    }

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
