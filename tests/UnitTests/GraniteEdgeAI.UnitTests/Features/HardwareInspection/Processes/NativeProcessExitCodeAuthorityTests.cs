using GraniteEdgeAI.HardwareInspection.Foundation.Processes;

namespace GraniteEdgeAI.UnitTests.Features.HardwareInspection.Processes;

[TestClass]
public sealed class NativeProcessExitCodeAuthorityTests
{
    [TestMethod]
    public void CompletedNativeQueryReturnsTheExactExitCode()
    {
        Assert.IsTrue(NativeProcessExitCodeAuthority.TryInterpret(
            querySucceeded: true,
            nativeExitCode: 23,
            out int exitCode));
        Assert.AreEqual(23, exitCode);
    }

    [TestMethod]
    [DataRow(false, 0u)]
    [DataRow(true, 259u)]
    public void FailedOrStillActiveNativeQueryFailsClosedWithoutGuessing(
        bool querySucceeded,
        uint nativeExitCode)
    {
        Assert.IsFalse(NativeProcessExitCodeAuthority.TryInterpret(
            querySucceeded,
            nativeExitCode,
            out int exitCode));
        Assert.AreEqual(0, exitCode);
    }

    [TestMethod]
    public void ProcessRunnerUsesOwnedNativeExitCodeAuthorityNotManagedExitCode()
    {
        string repositoryRoot = FindRepositoryRoot();
        string runner = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "infrastructure",
            "GraniteEdgeAI.HardwareInspection.Foundation",
            "Processes",
            "ExternalProcessRunner.cs"));
        string launcher = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "infrastructure",
            "GraniteEdgeAI.HardwareInspection.Foundation",
            "Processes",
            "WindowsSuspendedProcess.cs"));
        string authority = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "infrastructure",
            "GraniteEdgeAI.HardwareInspection.Foundation",
            "Processes",
            "NativeProcessExitCodeAuthority.cs"));

        StringAssert.DoesNotContain(
            runner,
            "process.ExitCode",
            StringComparison.Ordinal);
        StringAssert.Contains(runner, "running.TryGetExitCode");
        StringAssert.Contains(launcher, "SafeProcessHandle");
        StringAssert.Contains(authority, "GetExitCodeProcess");
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(
                    directory.FullName,
                    "IBM Granite with TurboQuant (Intel).slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Repository root not found.");
    }
}
