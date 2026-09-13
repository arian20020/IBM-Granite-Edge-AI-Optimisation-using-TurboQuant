using System.ComponentModel;
using System.Runtime.InteropServices;

namespace GraniteEdgeAI.ModelInspection.WorkerClient.Windows;

/// <summary>
/// Performs the reviewed Windows calls used by the worker launcher.
/// </summary>
internal sealed class WindowsWorkerProcessPlatform :
    IWindowsWorkerProcessPlatform
{
    internal static WindowsWorkerProcessPlatform Instance { get; } = new();

    private WindowsWorkerProcessPlatform()
    {
    }

    public WindowsEnvironmentBlock CreateEnvironmentBlock(
        IReadOnlyDictionary<string, string> environment) =>
        WindowsEnvironmentBlock.Create(environment);

    public WindowsJobObject CreateJob() =>
        WindowsJobObject.CreateKillOnClose();

    public WindowsPipeSet CreatePipes() => WindowsPipeSet.Create();

    public SafeAttributeListBuffer CreateAttributeList() =>
        SafeAttributeListBuffer.Create(attributeCount: 2);

    public void ApplyHandleAllowlist(
        SafeAttributeListBuffer attributes,
        WindowsPipeSet pipes) =>
        attributes.UpdatePointerList(
            NativeConstants.ProcThreadAttributeHandleList,
            pipes.GetChildHandleAllowlist());

    public void ApplyJobContainment(
        SafeAttributeListBuffer attributes,
        WindowsJobObject job) =>
        attributes.UpdatePointerList(
            NativeConstants.ProcThreadAttributeJobList,
            [job.Handle.DangerousGetHandle()]);

    public ProcessInformation CreateProcess(
        string applicationName,
        char[] commandLine,
        IntPtr environment,
        string workingDirectory,
        ref StartupInfoEx startupInfo)
    {
        if (!NativeMethods.CreateProcess(
                applicationName,
                commandLine,
                IntPtr.Zero,
                IntPtr.Zero,
                inheritHandles: true,
                NativeConstants.RequiredCreationFlags,
                environment,
                workingDirectory,
                ref startupInfo,
                out ProcessInformation processInformation))
        {
            throw new Win32Exception(
                Marshal.GetLastPInvokeError(),
                "Windows could not create the worker process.");
        }

        return processInformation;
    }
}
