using System.Diagnostics;
using GraniteEdgeAI.ModelInspection.WorkerClient.Windows;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WorkerProcessHandle = GraniteEdgeAI.ModelInspection.WorkerClient.Windows.SafeProcessHandle;

namespace GraniteEdgeAI.ModelInspection.WorkerClient.Tests;

/// <summary>
/// Proves process waiting is exposed as cancellable asynchronous work without
/// wrapping a blocking native wait in Task.Run.
/// </summary>
[TestClass]
public sealed class WindowsProcessWaiterTests
{
    [TestMethod]
    public async Task WaitForExitAsyncCompletesForExitedProcess()
    {
        string commandInterpreter = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            "System32",
            "cmd.exe");
        ProcessStartInfo startInfo = new(commandInterpreter)
        {
            Arguments = "/d /s /c exit 23",
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("The test process did not start.");
        using WorkerProcessHandle handle = new(
            process.SafeHandle.DangerousGetHandle(),
            ownsHandle: false);

        await WindowsProcessWaiter.WaitForExitAsync(
            handle,
            CancellationToken.None);

        Assert.IsTrue(NativeMethods.GetExitCodeProcess(handle, out uint exitCode));
        Assert.AreEqual(23u, exitCode);
    }

    [TestMethod]
    public async Task WaitForExitAsyncHonorsPreCancelledToken()
    {
        using Process current = Process.GetCurrentProcess();
        using WorkerProcessHandle handle = new(
            current.SafeHandle.DangerousGetHandle(),
            ownsHandle: false);
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            () => WindowsProcessWaiter.WaitForExitAsync(
                handle,
                cancellation.Token));
    }
}
