using System.Diagnostics;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Text;
using GraniteEdgeAI.HardwareInspection.Foundation.TrustedTools;

namespace GraniteEdgeAI.HardwareInspection.Foundation.Processes;

internal sealed class WindowsSuspendedProcess : IDisposable
{
    private WindowsSuspendedProcess(
        Process process,
        AnonymousPipeServerStream standardOutput,
        AnonymousPipeServerStream standardError)
    {
        Process = process;
        StandardOutput = standardOutput;
        StandardError = standardError;
    }

    internal Process Process { get; }

    internal Stream StandardOutput { get; }

    internal Stream StandardError { get; }

    internal static bool TryStart(
        VerifiedTrustedTool tool,
        TrustedToolCommand command,
        WindowsKillOnCloseJob job,
        out WindowsSuspendedProcess? launched)
    {
        ArgumentNullException.ThrowIfNull(tool);
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(job);

        launched = null;
        using AnonymousPipeServerStream standardInput = new(
            PipeDirection.Out,
            HandleInheritability.Inheritable);
        AnonymousPipeServerStream standardOutput = new(
            PipeDirection.In,
            HandleInheritability.Inheritable);
        AnonymousPipeServerStream standardError = new(
            PipeDirection.In,
            HandleInheritability.Inheritable);
        ProcessInformation processInformation = default;
        bool processCreated = false;
        try
        {
            StartupInformation startup = new()
            {
                Size = Marshal.SizeOf<StartupInformation>(),
                Flags = StartfUseStdHandles,
                StandardInput = standardInput.ClientSafePipeHandle.DangerousGetHandle(),
                StandardOutput = standardOutput.ClientSafePipeHandle.DangerousGetHandle(),
                StandardError = standardError.ClientSafePipeHandle.DangerousGetHandle(),
            };
            string commandLine = BuildCommandLine(tool.ExecutablePath, command.Arguments);
            if (commandLine.Length >= MaximumCommandLineLength)
            {
                return false;
            }

            processCreated = CreateProcess(
                tool.ExecutablePath,
                (commandLine + '\0').ToCharArray(),
                IntPtr.Zero,
                IntPtr.Zero,
                inheritHandles: true,
                CreateNoWindow | CreateSuspended,
                IntPtr.Zero,
                tool.PackageRoot,
                ref startup,
                out processInformation);
            standardInput.DisposeLocalCopyOfClientHandle();
            standardOutput.DisposeLocalCopyOfClientHandle();
            standardError.DisposeLocalCopyOfClientHandle();
            if (!processCreated)
            {
                return false;
            }

            if (!job.TryAssign(processInformation.Process))
            {
                TerminateAndWait(processInformation.Process);
                return false;
            }

            Process process = Process.GetProcessById(checked((int)processInformation.ProcessId));
            if (ResumeThread(processInformation.Thread) == uint.MaxValue)
            {
                process.Dispose();
                job.TryTerminate();
                TerminateAndWait(processInformation.Process);
                return false;
            }

            launched = new WindowsSuspendedProcess(process, standardOutput, standardError);
            standardOutput = null!;
            standardError = null!;
            return true;
        }
        catch (Exception error) when (
            error is InvalidOperationException or ArgumentException or OverflowException)
        {
            if (processCreated)
            {
                job.TryTerminate();
                TerminateAndWait(processInformation.Process);
            }

            return false;
        }
        finally
        {
            standardOutput?.Dispose();
            standardError?.Dispose();
            if (processInformation.Thread != IntPtr.Zero)
            {
                CloseHandle(processInformation.Thread);
            }

            if (processInformation.Process != IntPtr.Zero)
            {
                CloseHandle(processInformation.Process);
            }
        }
    }

    public void Dispose()
    {
        StandardOutput.Dispose();
        StandardError.Dispose();
        Process.Dispose();
    }

    private static string BuildCommandLine(
        string executablePath,
        IReadOnlyList<string> arguments)
    {
        var commandLine = new StringBuilder(QuoteArgument(executablePath));
        foreach (string argument in arguments)
        {
            commandLine.Append(' ');
            commandLine.Append(QuoteArgument(argument));
        }

        return commandLine.ToString();
    }

    private static string QuoteArgument(string argument)
    {
        if (argument.Length > 0 &&
            argument.IndexOfAny([' ', '\t', '\n', '\v', '"']) < 0)
        {
            return argument;
        }

        var quoted = new StringBuilder(argument.Length + 2);
        quoted.Append('"');
        int backslashes = 0;
        foreach (char character in argument)
        {
            if (character == '\\')
            {
                backslashes++;
                continue;
            }

            if (character == '"')
            {
                quoted.Append('\\', checked(backslashes * 2 + 1));
                quoted.Append('"');
                backslashes = 0;
                continue;
            }

            quoted.Append('\\', backslashes);
            backslashes = 0;
            quoted.Append(character);
        }

        quoted.Append('\\', checked(backslashes * 2));
        quoted.Append('"');
        return quoted.ToString();
    }

    private static void TerminateAndWait(IntPtr process)
    {
        if (process == IntPtr.Zero)
        {
            return;
        }

        TerminateProcess(process, 1);
        _ = WaitForSingleObject(process, 5_000);
    }

    private const int MaximumCommandLineLength = 32_767;
    private const uint CreateSuspended = 0x00000004;
    private const uint CreateNoWindow = 0x08000000;
    private const uint StartfUseStdHandles = 0x00000100;

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateProcess(
        string applicationName,
        [In, Out] char[] commandLine,
        IntPtr processAttributes,
        IntPtr threadAttributes,
        [MarshalAs(UnmanagedType.Bool)] bool inheritHandles,
        uint creationFlags,
        IntPtr environment,
        string currentDirectory,
        ref StartupInformation startupInformation,
        out ProcessInformation processInformation);

    [DllImport("kernel32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern uint ResumeThread(IntPtr thread);

    [DllImport("kernel32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool TerminateProcess(IntPtr process, uint exitCode);

    [DllImport("kernel32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern uint WaitForSingleObject(IntPtr handle, uint milliseconds);

    [DllImport("kernel32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr handle);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct StartupInformation
    {
        internal int Size;
        internal string? Reserved;
        internal string? Desktop;
        internal string? Title;
        internal uint X;
        internal uint Y;
        internal uint XSize;
        internal uint YSize;
        internal uint XCountChars;
        internal uint YCountChars;
        internal uint FillAttribute;
        internal uint Flags;
        internal short ShowWindow;
        internal short Reserved2Size;
        internal IntPtr Reserved2;
        internal IntPtr StandardInput;
        internal IntPtr StandardOutput;
        internal IntPtr StandardError;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessInformation
    {
        internal IntPtr Process;
        internal IntPtr Thread;
        internal uint ProcessId;
        internal uint ThreadId;
    }
}
