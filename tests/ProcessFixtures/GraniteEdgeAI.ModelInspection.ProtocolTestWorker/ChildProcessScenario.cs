using System.Diagnostics;

namespace GraniteEdgeAI.ModelInspection.ProtocolTestWorker;

/// <summary>
/// Creates only non-breakaway copies of this test fixture. Windows therefore
/// assigns every child to the launcher's existing Job Object automatically.
/// </summary>
internal static class ChildProcessScenario
{
    internal static Process StartWaitingChild()
    {
        string executable = Environment.ProcessPath ??
            throw new InvalidOperationException(
                "The fixture executable path is unavailable.");
        ProcessStartInfo startInfo = new()
        {
            FileName = executable,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add("--protocol");
        startInfo.ArgumentList.Add(
            "modelinspection.fixture.child-process-wait/1");
        return Process.Start(startInfo) ??
            throw new InvalidOperationException(
                "The fixture child process could not start.");
    }
}
