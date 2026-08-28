using System.Diagnostics;

namespace GraniteEdgeAI.EndToEndTests.Automation;

internal sealed class OwnedProcessSet : IDisposable
{
    private readonly Dictionary<int, Process> processes = [];

    internal void Add(int processId)
    {
        if (processId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(processId));
        }

        if (!processes.ContainsKey(processId))
        {
            processes.Add(processId, Process.GetProcessById(processId));
        }
    }

    public void Dispose()
    {
        foreach (Process process in processes.Values)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.CloseMainWindow();
                    if (!process.WaitForExit(3000))
                    {
                        process.Kill(entireProcessTree: true);
                        if (!process.WaitForExit(5000))
                        {
                            throw new InvalidOperationException(
                                $"Owned process {process.Id} did not exit within the cleanup deadline.");
                        }
                    }
                }
            }
            catch (ArgumentException)
            {
                // The owned process already exited.
            }
            finally
            {
                process.Dispose();
            }
        }

        processes.Clear();
    }
}
