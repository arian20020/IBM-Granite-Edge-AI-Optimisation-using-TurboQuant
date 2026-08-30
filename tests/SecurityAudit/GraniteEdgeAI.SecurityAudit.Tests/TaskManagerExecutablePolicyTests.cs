using System.Diagnostics;
using GraniteEdgeAI.Features.ModelHardwareCompatibility.Infrastructure;

namespace GraniteEdgeAI.SecurityAudit.Tests;

[TestClass]
public sealed class TaskManagerExecutablePolicyTests
{
    [TestMethod]
    public void FixedRequestUsesExactSystem32ExecutableWithoutPathResolution()
    {
        ProcessStartInfo request =
            TaskManagerLaunchRequest.Fixed.CreateProcessStartInfo();
        string expected = Path.Combine(Environment.SystemDirectory, "Taskmgr.exe");

        Assert.AreEqual(expected, request.FileName);
        Assert.IsTrue(Path.IsPathFullyQualified(request.FileName));
        Assert.AreEqual(string.Empty, request.Arguments);
        Assert.AreEqual(string.Empty, request.Verb);
        Assert.IsTrue(request.UseShellExecute);
    }
}
