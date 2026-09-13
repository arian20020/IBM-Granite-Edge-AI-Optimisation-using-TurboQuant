using System.Diagnostics;
using GraniteEdgeAI.EndToEndTests.Automation;

namespace GraniteEdgeAI.EndToEndTests.Tests;

[TestClass]
public sealed class OwnedProcessSetTests
{
    [TestMethod]
    public void Add_rejects_non_process_identifiers()
    {
        using OwnedProcessSet processes = new();
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => processes.Add(0));
    }

    [TestMethod]
    public void Dispose_terminates_only_the_explicitly_owned_process()
    {
        using Process process = Process.Start(new ProcessStartInfo("cmd.exe", "/d /c ping 127.0.0.1 -n 30 >nul")
        {
            CreateNoWindow = true,
            UseShellExecute = false,
        })!;
        using (OwnedProcessSet processes = new())
        {
            processes.Add(process.Id);
        }

        Assert.IsTrue(process.WaitForExit(1000));
    }
}
