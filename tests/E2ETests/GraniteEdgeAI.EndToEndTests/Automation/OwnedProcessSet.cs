using System.Diagnostics;

namespace GraniteEdgeAI.EndToEndTests.Automation;

internal sealed class OwnedProcessSet : IDisposable
{
    private readonly HashSet<int> processIds = [];

    internal void Add(int processId)
    {
        if (processId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(processId));
        }

        processIds.Add(processId);
    }

    public void Dispose()
    {
        foreach (int processId in processIds)
        {
            try
            {
                using Process process = Process.GetProcessById(processId);
                if (!process.HasExited)
                {
                    process.CloseMainWindow();
                    if (!process.WaitForExit(3000))
                    {
                        process.Kill(entireProcessTree: true);
                        process.WaitForExit(5000);
                    }
                }
            }
            catch (ArgumentException)
            {
                // The owned process already exited.
            }
        }
    }
}
