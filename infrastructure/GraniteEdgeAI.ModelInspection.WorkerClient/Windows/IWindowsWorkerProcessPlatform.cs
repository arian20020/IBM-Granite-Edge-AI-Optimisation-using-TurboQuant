namespace GraniteEdgeAI.ModelInspection.WorkerClient.Windows;

/// <summary>
/// Defines the Windows operations used by one worker launch. Production uses
/// the real implementation while tests can provide deterministic failures.
/// </summary>
internal interface IWindowsWorkerProcessPlatform
{
    WindowsEnvironmentBlock CreateEnvironmentBlock(
        IReadOnlyDictionary<string, string> environment);

    WindowsJobObject CreateJob();

    WindowsPipeSet CreatePipes();

    SafeAttributeListBuffer CreateAttributeList();

    void ApplyHandleAllowlist(
        SafeAttributeListBuffer attributes,
        WindowsPipeSet pipes);

    void ApplyJobContainment(
        SafeAttributeListBuffer attributes,
        WindowsJobObject job);

    ProcessInformation CreateProcess(
        string applicationName,
        char[] commandLine,
        IntPtr environment,
        string workingDirectory,
        ref StartupInfoEx startupInfo);
}
