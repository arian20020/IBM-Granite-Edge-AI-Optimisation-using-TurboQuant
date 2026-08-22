using System.Diagnostics;
using System.Security.Cryptography;
using GraniteEdgeAI.HardwareInspection.Foundation.Processes;
using GraniteEdgeAI.HardwareInspection.Foundation.TrustedTools;

namespace GraniteEdgeAI.HardwareInspection.Foundation.Tests.Processes;

[TestClass]
public sealed class ExternalProcessRunnerTests
{
    [TestMethod]
    public async Task RunUsesOnlyManifestDeclaredVersionArguments()
    {
        using VerifiedFixture fixture = VerifiedFixture.Create("success");

        ExternalProcessResult result = await new ExternalProcessRunner().RunAsync(
            fixture.Tool,
            Request("version"),
            CancellationToken.None);

        Assert.AreEqual(ExternalProcessTerminationReason.Exited, result.TerminationReason);
        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual("llmfit 1.1.9", result.StandardOutput.Trim());
        Assert.AreEqual(string.Empty, result.StandardError);
    }

    [TestMethod]
    public async Task RunReturnsBoundedSuccessfulOutput()
    {
        using VerifiedFixture fixture = VerifiedFixture.Create("success");

        ExternalProcessResult result = await new ExternalProcessRunner().RunAsync(
            fixture.Tool,
            Request("system"),
            CancellationToken.None);

        Assert.AreEqual(ExternalProcessTerminationReason.Exited, result.TerminationReason);
        Assert.AreEqual(0, result.ExitCode);
        StringAssert.Contains(result.StandardOutput, "\"total_ram_gb\":32");
        Assert.AreEqual(string.Empty, result.StandardError);
    }

    [TestMethod]
    public async Task RunPreservesNonZeroExitWithoutConvertingItToStartFailure()
    {
        using VerifiedFixture fixture = VerifiedFixture.Create("nonzero");

        ExternalProcessResult result = await new ExternalProcessRunner().RunAsync(
            fixture.Tool,
            Request("system"),
            CancellationToken.None);

        Assert.AreEqual(ExternalProcessTerminationReason.Exited, result.TerminationReason);
        Assert.AreEqual(23, result.ExitCode);
        StringAssert.Contains(result.StandardError, "nonzero exit");
    }

    [TestMethod]
    public async Task RunKillsProcessWhenStandardOutputExceedsItsIndependentLimit()
    {
        using VerifiedFixture fixture = VerifiedFixture.Create("large-output");

        ExternalProcessResult result = await new ExternalProcessRunner().RunAsync(
            fixture.Tool,
            Request("system", stdoutLimit: 1024, stderrLimit: 3 * 1024 * 1024),
            CancellationToken.None);

        Assert.AreEqual(
            ExternalProcessTerminationReason.OutputLimitExceeded,
            result.TerminationReason);
        Assert.IsLessThanOrEqualTo(1024, System.Text.Encoding.UTF8.GetByteCount(result.StandardOutput));
        Assert.IsNull(result.ExitCode);
    }

    [TestMethod]
    public async Task RunKillsProcessWhenStandardErrorExceedsItsIndependentLimit()
    {
        using VerifiedFixture fixture = VerifiedFixture.Create("large-output");

        ExternalProcessResult result = await new ExternalProcessRunner().RunAsync(
            fixture.Tool,
            Request("system", stdoutLimit: 3 * 1024 * 1024, stderrLimit: 1024),
            CancellationToken.None);

        Assert.AreEqual(
            ExternalProcessTerminationReason.OutputLimitExceeded,
            result.TerminationReason);
        Assert.IsLessThanOrEqualTo(1024, System.Text.Encoding.UTF8.GetByteCount(result.StandardError));
        Assert.IsNull(result.ExitCode);
    }

    [TestMethod]
    public async Task RunTimesOutAndKillsRootProcess()
    {
        using VerifiedFixture fixture = VerifiedFixture.Create("sleep");

        ExternalProcessResult result = await new ExternalProcessRunner().RunAsync(
            fixture.Tool,
            Request("system", timeout: TimeSpan.FromMilliseconds(200)),
            CancellationToken.None);

        Assert.AreEqual(ExternalProcessTerminationReason.TimedOut, result.TerminationReason);
        Assert.IsNull(result.ExitCode);
        await AssertRecordedProcessExitedAsync(
            Path.Combine(fixture.PackageRoot, "owned-root-ready.txt"));
    }

    [TestMethod]
    public async Task RunCancellationKillsEntireProcessTree()
    {
        using VerifiedFixture fixture = VerifiedFixture.Create("spawn-child");
        using CancellationTokenSource cancellation = new();
        Task<ExternalProcessResult> execution = new ExternalProcessRunner().RunAsync(
            fixture.Tool,
            Request("system", timeout: TimeSpan.FromSeconds(10)),
            cancellation.Token);
        string childReady = Path.Combine(fixture.PackageRoot, "spawn-child-ready.txt");
        int childId = await WaitForProcessIdAsync(childReady);

        cancellation.Cancel();
        ExternalProcessResult result = await execution;

        Assert.AreEqual(ExternalProcessTerminationReason.Cancelled, result.TerminationReason);
        Assert.IsNull(result.ExitCode);
        await AssertProcessExitedAsync(childId);
    }

    [TestMethod]
    public async Task RunReturnsStartFailedWhenVerifiedExecutableDisappears()
    {
        using VerifiedFixture fixture = VerifiedFixture.Create("success");
        File.Delete(fixture.Tool.ExecutablePath);

        ExternalProcessResult result = await new ExternalProcessRunner().RunAsync(
            fixture.Tool,
            Request("system"),
            CancellationToken.None);

        Assert.AreEqual(ExternalProcessTerminationReason.StartFailed, result.TerminationReason);
        Assert.IsNull(result.ExitCode);
        Assert.AreEqual(string.Empty, result.StandardOutput);
        Assert.AreEqual(string.Empty, result.StandardError);
    }

    [TestMethod]
    public async Task RunReturnsCancelledWithoutStartingWhenCallerAlreadyCancelled()
    {
        using VerifiedFixture fixture = VerifiedFixture.Create("sleep");
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        ExternalProcessResult result = await new ExternalProcessRunner().RunAsync(
            fixture.Tool,
            Request("system"),
            cancellation.Token);

        Assert.AreEqual(ExternalProcessTerminationReason.Cancelled, result.TerminationReason);
        Assert.IsFalse(File.Exists(Path.Combine(fixture.PackageRoot, "owned-root-ready.txt")));
    }

    private static ExternalProcessRequest Request(
        string command,
        TimeSpan? timeout = null,
        int stdoutLimit = 64 * 1024,
        int stderrLimit = 64 * 1024) =>
        new(command, timeout ?? TimeSpan.FromSeconds(5), stdoutLimit, stderrLimit);

    private static async Task AssertRecordedProcessExitedAsync(string path)
    {
        int processId = await WaitForProcessIdAsync(path);
        await AssertProcessExitedAsync(processId);
    }

    private static async Task<int> WaitForProcessIdAsync(string path)
    {
        Stopwatch elapsed = Stopwatch.StartNew();
        while (elapsed.Elapsed < TimeSpan.FromSeconds(5))
        {
            if (File.Exists(path) && int.TryParse(await File.ReadAllTextAsync(path), out int processId))
            {
                return processId;
            }

            await Task.Delay(20);
        }

        Assert.Fail("The harmless fixture did not publish its bounded readiness marker.");
        return 0;
    }

    private static async Task AssertProcessExitedAsync(int processId)
    {
        Stopwatch elapsed = Stopwatch.StartNew();
        while (elapsed.Elapsed < TimeSpan.FromSeconds(5))
        {
            try
            {
                using Process process = Process.GetProcessById(processId);
                if (process.HasExited)
                {
                    return;
                }
            }
            catch (ArgumentException)
            {
                return;
            }

            await Task.Delay(20);
        }

        Assert.Fail("A harmless fixture process remained after bounded cleanup.");
    }

    private sealed class VerifiedFixture : IDisposable
    {
        private VerifiedFixture(string root, string packageRoot, VerifiedTrustedTool tool)
        {
            Root = root;
            PackageRoot = packageRoot;
            Tool = tool;
        }

        internal string Root { get; }

        internal string PackageRoot { get; }

        internal VerifiedTrustedTool Tool { get; }

        internal static VerifiedFixture Create(string mode)
        {
            string sourceRoot = ResolveFakeToolRoot();
            string root = Path.Combine(Path.GetTempPath(), $"hi-runner-{Guid.NewGuid():N}");
            string approvedRoot = Path.Combine(root, "approved");
            string packageRoot = Path.Combine(approvedRoot, "package");
            Directory.CreateDirectory(packageRoot);
            foreach (string sourceFile in Directory.GetFiles(sourceRoot, "*", SearchOption.TopDirectoryOnly))
            {
                File.Copy(sourceFile, Path.Combine(packageRoot, Path.GetFileName(sourceFile)));
            }

            File.WriteAllText(Path.Combine(packageRoot, "fake-mode.txt"), mode);
            string executableName = "GraniteEdgeAI.HardwareInspection.LlmFitFakeTool.exe";
            string executablePath = Path.Combine(packageRoot, executableName);
            string hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(executablePath)))
                .ToLowerInvariant();
            string[] members = Directory.GetFiles(packageRoot)
                .Select(Path.GetFileName)
                .Select(name => name!)
                .ToArray();
            TrustedToolPackageManifest manifest = new(
                "llmfit-fake",
                "1.1.9",
                executableName,
                hash,
                members,
                PeMachine.Amd64,
                TrustedToolPackageDisposition.AcceptedForFunctionalEvaluation,
                [
                    new TrustedToolCommand("version", ["--version"]),
                    new TrustedToolCommand("system", ["--no-dashboard", "--json", "system"]),
                ]);
            TrustedToolVerificationResult verification = new TrustedToolPackageVerifier().Verify(
                approvedRoot,
                packageRoot,
                manifest);
            Assert.IsTrue(verification.IsVerified);
            return new VerifiedFixture(root, packageRoot, verification.Tool!);
        }

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }

        private static string ResolveFakeToolRoot()
        {
            string? controlled = Environment.GetEnvironmentVariable(
                "GRANITE_LLMFIT_FAKE_TOOL_ROOT");
            if (!string.IsNullOrWhiteSpace(controlled) && Directory.Exists(controlled))
            {
                return Path.GetFullPath(controlled);
            }

            string repositoryRoot = FindRepositoryRoot();
            string configuration = AppContext.BaseDirectory.Contains(
                $"{Path.DirectorySeparatorChar}Release{Path.DirectorySeparatorChar}",
                StringComparison.OrdinalIgnoreCase)
                ? "Release"
                : "Debug";
            string builtRoot = Path.Combine(
                repositoryRoot,
                "tests",
                "ProcessFixtures",
                "GraniteEdgeAI.HardwareInspection.LlmFitFakeTool",
                "bin",
                configuration,
                "net8.0-windows10.0.19041.0",
                "win-x64");
            if (!File.Exists(Path.Combine(
                    builtRoot,
                    "GraniteEdgeAI.HardwareInspection.LlmFitFakeTool.exe")))
            {
                throw new InvalidOperationException("The harmless fake tool fixture was not built.");
            }

            return builtRoot;
        }

        private static string FindRepositoryRoot()
        {
            DirectoryInfo? current = new(AppContext.BaseDirectory);
            while (current is not null)
            {
                if (File.Exists(Path.Combine(current.FullName, "global.json")))
                {
                    return current.FullName;
                }

                current = current.Parent;
            }

            throw new InvalidOperationException("Repository root was not found.");
        }
    }
}
