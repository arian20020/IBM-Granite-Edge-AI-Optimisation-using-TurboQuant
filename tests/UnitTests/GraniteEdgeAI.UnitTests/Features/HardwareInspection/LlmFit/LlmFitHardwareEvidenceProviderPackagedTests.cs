using System.Diagnostics;
using GraniteEdgeAI.HardwareInspection.Foundation.LlmFit;
using GraniteEdgeAI.HardwareInspection.Foundation.Processes;
using GraniteEdgeAI.UnitTests.Features.HardwareInspection.Support;

namespace GraniteEdgeAI.UnitTests.Features.HardwareInspection.LlmFit;

[TestClass]
[DoNotParallelize]
public sealed class LlmFitHardwareEvidenceProviderPackagedTests
{
    [TestMethod]
    [TestCategory("HardwareInspectionProcessAcceptance")]
    public async Task RealBoundaryMapsCpuOnlySuccess()
    {
        using VerifiedPackagedToolFixture fixture = CreateFixture("success");

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
    [TestCategory("HardwareInspectionProcessAcceptance")]
    public async Task RealBoundaryRejectsVersionMismatchBeforeSystem()
    {
        using VerifiedPackagedToolFixture fixture = CreateFixture("version-mismatch");

        LlmFitHardwareEvidence evidence = await CreateProvider().CaptureAsync(
            fixture.Tool,
            CancellationToken.None);

        AssertUnavailable(evidence, LlmFitDiagnosticCode.VersionOutputMismatch);
    }

    [TestMethod]
    [TestCategory("HardwareInspectionProcessAcceptance")]
    public async Task RealBoundaryMapsInvalidJsonWithoutPersistingOutput()
    {
        using VerifiedPackagedToolFixture fixture = CreateFixture("invalid-json");
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
    [TestCategory("HardwareInspectionProcessAcceptance")]
    public async Task RealBoundaryMapsNonzeroExit()
    {
        using VerifiedPackagedToolFixture fixture = CreateFixture("nonzero");

        LlmFitHardwareEvidence evidence = await CreateProvider().CaptureAsync(
            fixture.Tool,
            CancellationToken.None);

        AssertUnavailable(evidence, LlmFitDiagnosticCode.SystemNonZeroExit);
    }

    [TestMethod]
    [TestCategory("HardwareInspectionProcessAcceptance")]
    public async Task RealBoundaryMapsOutputOverflowAndKillsProcess()
    {
        using VerifiedPackagedToolFixture fixture = CreateFixture("large-output");

        LlmFitHardwareEvidence evidence = await CreateProvider().CaptureAsync(
            fixture.Tool,
            CancellationToken.None);

        AssertUnavailable(evidence, LlmFitDiagnosticCode.SystemOutputLimitExceeded);
    }

    [TestMethod]
    [TestCategory("HardwareInspectionProcessAcceptance")]
    public async Task RealBoundaryTimesOutAndKillsRootProcess()
    {
        using VerifiedPackagedToolFixture fixture = CreateFixture("sleep");

        LlmFitHardwareEvidence evidence = await CreateProvider().CaptureAsync(
            fixture.Tool,
            CancellationToken.None);

        AssertUnavailable(evidence, LlmFitDiagnosticCode.SystemTimedOut);
        await AssertRecordedProcessExitedAsync(
            Path.Combine(fixture.ControlRoot, "owned-root-ready.txt"));
    }

    [TestMethod]
    [TestCategory("HardwareInspectionProcessAcceptance")]
    public async Task RealBoundaryCancellationPropagatesAndKillsRootProcess()
    {
        using VerifiedPackagedToolFixture fixture = CreateFixture("sleep");
        using CancellationTokenSource cancellation = new();
        Task<LlmFitHardwareEvidence> capture = CreateProvider().CaptureAsync(
            fixture.Tool,
            cancellation.Token);
        string marker = Path.Combine(fixture.ControlRoot, "owned-root-ready.txt");
        int processId = await WaitForProcessIdAsync(marker);

        cancellation.Cancel();
        OperationCanceledException error = await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            async () => await capture);

        Assert.AreEqual(cancellation.Token, error.CancellationToken);
        await AssertProcessExitedAsync(processId);
    }

    private static VerifiedPackagedToolFixture CreateFixture(string mode) =>
        VerifiedPackagedToolFixture.CreateLlmFit(mode, useProductionIdentity: true);

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
            try
            {
                if (File.Exists(marker) &&
                    int.TryParse(await File.ReadAllTextAsync(marker), out int processId))
                {
                    return processId;
                }
            }
            catch (IOException)
            {
                // The fixture has created the marker but has not released its write handle yet.
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
