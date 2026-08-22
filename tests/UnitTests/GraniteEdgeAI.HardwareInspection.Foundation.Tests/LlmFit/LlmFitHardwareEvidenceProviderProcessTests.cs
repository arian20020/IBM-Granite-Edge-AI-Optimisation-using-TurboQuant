using System.Diagnostics;
using GraniteEdgeAI.HardwareInspection.Foundation.LlmFit;
using GraniteEdgeAI.HardwareInspection.Foundation.Processes;
using GraniteEdgeAI.HardwareInspection.Foundation.Tests.Support;

namespace GraniteEdgeAI.HardwareInspection.Foundation.Tests.LlmFit;

[TestClass]
[DoNotParallelize]
public sealed class LlmFitHardwareEvidenceProviderProcessTests
{
    [TestMethod]
    public async Task RealBoundaryMapsCpuOnlySuccess()
    {
        using VerifiedLlmFitFixture fixture = VerifiedLlmFitFixture.Create("success");

        LlmFitHardwareEvidence evidence = await CreateProvider().CaptureAsync(
            fixture.Tool,
            CancellationToken.None);

        Assert.AreEqual(LlmFitEvidenceState.Available, evidence.State);
        Assert.AreEqual("Fixture CPU", evidence.CpuName);
        Assert.AreEqual(8, evidence.CpuLogicalProcessorCount);
        Assert.AreEqual(32, evidence.TotalRamGiB);
        Assert.AreEqual(16, evidence.AvailableRamGiB);
        Assert.AreEqual(LlmFitGpuDetectionState.NotReported, evidence.GpuState);
        Assert.HasCount(0, evidence.Diagnostics);
    }

    [TestMethod]
    public async Task RealBoundaryRejectsVersionMismatchBeforeSystem()
    {
        using VerifiedLlmFitFixture fixture = VerifiedLlmFitFixture.Create("version-mismatch");

        LlmFitHardwareEvidence evidence = await CreateProvider().CaptureAsync(
            fixture.Tool,
            CancellationToken.None);

        AssertUnavailable(evidence, LlmFitDiagnosticCode.VersionOutputMismatch);
    }

    [TestMethod]
    public async Task RealBoundaryMapsInvalidJsonWithoutPersistingOutput()
    {
        using VerifiedLlmFitFixture fixture = VerifiedLlmFitFixture.Create("invalid-json");
        string[] inventoryBefore = Directory.GetFiles(
            fixture.PackageRoot,
            "*",
            SearchOption.AllDirectories);

        LlmFitHardwareEvidence evidence = await CreateProvider().CaptureAsync(
            fixture.Tool,
            CancellationToken.None);

        Assert.AreEqual(LlmFitEvidenceState.Invalid, evidence.State);
        CollectionAssert.AreEqual(
            new[] { LlmFitDiagnosticCode.JsonInvalid },
            evidence.Diagnostics.ToArray());
        Assert.IsNotNull(evidence.RawOutputSha256);
        CollectionAssert.AreEquivalent(
            inventoryBefore,
            Directory.GetFiles(fixture.PackageRoot, "*", SearchOption.AllDirectories));
    }

    [TestMethod]
    public async Task RealBoundaryMapsNonzeroExit()
    {
        using VerifiedLlmFitFixture fixture = VerifiedLlmFitFixture.Create("nonzero");

        LlmFitHardwareEvidence evidence = await CreateProvider().CaptureAsync(
            fixture.Tool,
            CancellationToken.None);

        AssertUnavailable(evidence, LlmFitDiagnosticCode.SystemNonZeroExit);
    }

    [TestMethod]
    public async Task RealBoundaryMapsOutputOverflowAndKillsProcess()
    {
        using VerifiedLlmFitFixture fixture = VerifiedLlmFitFixture.Create("large-output");

        LlmFitHardwareEvidence evidence = await CreateProvider().CaptureAsync(
            fixture.Tool,
            CancellationToken.None);

        AssertUnavailable(evidence, LlmFitDiagnosticCode.SystemOutputLimitExceeded);
    }

    [TestMethod]
    public async Task RealBoundaryTimesOutAndKillsRootProcess()
    {
        using VerifiedLlmFitFixture fixture = VerifiedLlmFitFixture.Create("sleep");

        LlmFitHardwareEvidence evidence = await CreateProvider().CaptureAsync(
            fixture.Tool,
            CancellationToken.None);

        AssertUnavailable(evidence, LlmFitDiagnosticCode.SystemTimedOut);
        await AssertRecordedProcessExitedAsync(
            Path.Combine(fixture.PackageRoot, "owned-root-ready.txt"));
    }

    [TestMethod]
    public async Task RealBoundaryCancellationPropagatesAndKillsRootProcess()
    {
        using VerifiedLlmFitFixture fixture = VerifiedLlmFitFixture.Create("sleep");
        using CancellationTokenSource cancellation = new();
        Task<LlmFitHardwareEvidence> capture = CreateProvider().CaptureAsync(fixture.Tool, cancellation.Token);
        string marker = Path.Combine(fixture.PackageRoot, "owned-root-ready.txt");
        int processId = await WaitForProcessIdAsync(marker);

        cancellation.Cancel();
        OperationCanceledException error = await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            async () => await capture);

        Assert.AreEqual(cancellation.Token, error.CancellationToken);
        await AssertProcessExitedAsync(processId);
    }

    [TestMethod]
    public void AcceptedFixtureIsSyntheticAndOutsideRawCaptureEvidence()
    {
        string fixture = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "LlmFit",
            "valid-windows-intel.json");
        string json = File.ReadAllText(fixture);

        LlmFitSystemParseResult result = LlmFitSystemJsonParser.Parse(json);

        Assert.AreEqual(LlmFitEvidenceState.Available, result.State);
        Assert.AreEqual(31.72, result.TotalRamGiB);
        Assert.AreEqual(18.40, result.AvailableRamGiB);
        Assert.AreEqual(16, result.CpuLogicalProcessorCount);
        Assert.AreEqual("Fixture Intel CPU", result.CpuName);
        Assert.AreEqual("Fixture Intel Arc Graphics", result.Gpus.Single().Name);
        Assert.IsFalse(fixture.Contains("release-evidence", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(fixture.Contains("raw", StringComparison.OrdinalIgnoreCase));
    }

    private static LlmFitHardwareEvidenceProvider CreateProvider() =>
        new(new ExternalProcessRunner());

    private static void AssertUnavailable(
        LlmFitHardwareEvidence evidence,
        LlmFitDiagnosticCode diagnostic)
    {
        Assert.AreEqual(LlmFitEvidenceState.Unavailable, evidence.State);
        CollectionAssert.AreEqual(new[] { diagnostic }, evidence.Diagnostics.ToArray());
        Assert.IsNull(evidence.RawOutputSha256);
    }

    private static async Task AssertRecordedProcessExitedAsync(string marker)
    {
        int processId = await WaitForProcessIdAsync(marker);
        await AssertProcessExitedAsync(processId);
    }

    private static async Task<int> WaitForProcessIdAsync(string marker)
    {
        Stopwatch elapsed = Stopwatch.StartNew();
        while (elapsed.Elapsed < TimeSpan.FromSeconds(5))
        {
            if (File.Exists(marker) && int.TryParse(await File.ReadAllTextAsync(marker), out int processId))
            {
                return processId;
            }

            await Task.Delay(20);
        }

        Assert.Fail("The harmless fixture did not publish its bounded process marker.");
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
}
