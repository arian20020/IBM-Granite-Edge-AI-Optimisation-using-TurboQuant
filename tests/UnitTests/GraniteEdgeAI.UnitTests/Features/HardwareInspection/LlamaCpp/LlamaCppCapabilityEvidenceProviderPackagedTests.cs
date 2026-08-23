using System.Diagnostics;
using GraniteEdgeAI.HardwareInspection.Foundation.LlamaCpp;
using GraniteEdgeAI.HardwareInspection.Foundation.Processes;
using GraniteEdgeAI.UnitTests.Features.HardwareInspection.Support;

namespace GraniteEdgeAI.UnitTests.Features.HardwareInspection.LlamaCpp;

[TestClass]
[DoNotParallelize]
public sealed class LlamaCppCapabilityEvidenceProviderPackagedTests
{
    [TestMethod]
    [TestCategory("HardwareInspectionProcessAcceptance")]
    public async Task RealBoundaryMapsCpuSuccess()
    {
        using VerifiedPackagedToolFixture fixture = CreateFixture("success");

        LlamaCppCapabilityEvidence evidence = await CreateProvider().CaptureAsync(
            fixture.Tool,
            CancellationToken.None);

        Assert.AreEqual(LlamaCppCapabilityEvidenceState.Available, evidence.State);
        Assert.AreSame(LlamaCppRuntimeIdentity.PinnedCpu, evidence.RuntimeIdentity);
        CollectionAssert.AreEqual(new[] { LlamaCppBackend.Cpu }, evidence.Backends.ToArray());
        CollectionAssert.AreEqual(
            new[] { new LlamaCppVisibleDevice(0, "Fixture CPU Buffer") },
            evidence.VisibleDevices.ToArray());
        Assert.IsNull(evidence.Diagnostic);
    }

    [TestMethod]
    [TestCategory("HardwareInspectionProcessAcceptance")]
    public Task RealBoundaryRejectsIdentityMismatch() =>
        AssertFailsClosedAsync("identity-mismatch", LlamaCppCapabilityDiagnosticCode.IdentityMismatch);

    [TestMethod]
    [TestCategory("HardwareInspectionProcessAcceptance")]
    public Task RealBoundaryRejectsInvalidCapabilityJson() =>
        AssertFailsClosedAsync("invalid-json", LlamaCppCapabilityDiagnosticCode.CapabilityOutputInvalid);

    [TestMethod]
    [TestCategory("HardwareInspectionProcessAcceptance")]
    public Task RealBoundaryMapsCapabilityNonzeroExit() =>
        AssertFailsClosedAsync("nonzero", LlamaCppCapabilityDiagnosticCode.CapabilityProcessFailed);

    [TestMethod]
    [TestCategory("HardwareInspectionProcessAcceptance")]
    public Task RealBoundaryMapsCapabilityOutputOverflow() =>
        AssertFailsClosedAsync("large-output", LlamaCppCapabilityDiagnosticCode.CapabilityOutputLimitExceeded);

    [TestMethod]
    [TestCategory("HardwareInspectionProcessAcceptance")]
    public Task RealBoundaryMapsCapabilityTimeout() =>
        AssertFailsClosedAsync("sleep", LlamaCppCapabilityDiagnosticCode.CapabilityTimedOut);

    private static async Task AssertFailsClosedAsync(
        string mode,
        LlamaCppCapabilityDiagnosticCode diagnostic)
    {
        using VerifiedPackagedToolFixture fixture = CreateFixture(mode);
        string[] inventoryBefore = Directory.GetFiles(
            fixture.PackageRoot,
            "*",
            SearchOption.AllDirectories);

        LlamaCppCapabilityEvidence evidence = await CreateProvider().CaptureAsync(
            fixture.Tool,
            CancellationToken.None);

        AssertUnavailable(evidence, diagnostic);
        CollectionAssert.AreEquivalent(
            inventoryBefore,
            Directory.GetFiles(fixture.PackageRoot, "*", SearchOption.AllDirectories));
        if (mode == "sleep")
        {
            int processId = await WaitForProcessIdAsync(
                Path.Combine(fixture.ControlRoot, "owned-root-ready.txt"));
            await AssertProcessExitedAsync(processId);
        }
    }

    [TestMethod]
    [TestCategory("HardwareInspectionProcessAcceptance")]
    public async Task RealBoundaryRunsInsideJobCustody()
    {
        using VerifiedPackagedToolFixture fixture = CreateFixture("assert-in-job");

        LlamaCppCapabilityEvidence evidence = await CreateProvider().CaptureAsync(
            fixture.Tool,
            CancellationToken.None);

        Assert.AreEqual(LlamaCppCapabilityEvidenceState.Available, evidence.State);
    }

    [TestMethod]
    [TestCategory("HardwareInspectionProcessAcceptance")]
    public async Task CancellationKillsEntireCapabilityProcessTree()
    {
        using VerifiedPackagedToolFixture fixture = CreateFixture("spawn-child");
        using CancellationTokenSource cancellation = new();
        Task<LlamaCppCapabilityEvidence> capture = CreateProvider().CaptureAsync(
            fixture.Tool,
            cancellation.Token);
        int childId = await WaitForProcessIdAsync(Path.Combine(fixture.ControlRoot, "spawn-child-ready.txt"));

        cancellation.Cancel();
        OperationCanceledException error = await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            async () => await capture);

        Assert.AreEqual(cancellation.Token, error.CancellationToken);
        await AssertProcessExitedAsync(childId);
    }

    [TestMethod]
    [TestCategory("HardwareInspectionProcessAcceptance")]
    public async Task JobCustodyKillsChildAfterNormalParentExit()
    {
        using VerifiedPackagedToolFixture fixture = CreateFixture("spawn-child-exit");

        LlamaCppCapabilityEvidence evidence = await CreateProvider().CaptureAsync(
            fixture.Tool,
            CancellationToken.None);

        Assert.AreEqual(LlamaCppCapabilityEvidenceState.Available, evidence.State);
        int childId = await WaitForProcessIdAsync(Path.Combine(fixture.ControlRoot, "spawn-child-ready.txt"));
        await AssertProcessExitedAsync(childId);
    }

    private static VerifiedPackagedToolFixture CreateFixture(string mode) =>
        VerifiedPackagedToolFixture.CreateLlamaCpp(mode);

    private static LlamaCppCapabilityEvidenceProvider CreateProvider() =>
        new(new ExternalProcessRunner());

    private static void AssertUnavailable(
        LlamaCppCapabilityEvidence evidence,
        LlamaCppCapabilityDiagnosticCode diagnostic)
    {
        Assert.AreEqual(LlamaCppCapabilityEvidenceState.Unavailable, evidence.State);
        Assert.AreEqual(diagnostic, evidence.Diagnostic);
        Assert.IsNull(evidence.RuntimeIdentity);
        Assert.HasCount(0, evidence.Backends);
        Assert.HasCount(0, evidence.VisibleDevices);
    }

    private static async Task<int> WaitForProcessIdAsync(string marker)
    {
        Stopwatch elapsed = Stopwatch.StartNew();
        while (elapsed.Elapsed < TimeSpan.FromSeconds(5))
        {
            try
            {
                if (File.Exists(marker) && int.TryParse(await File.ReadAllTextAsync(marker), out int processId))
                {
                    return processId;
                }
            }
            catch (IOException)
            {
                // The fixture created the marker but has not released its write handle yet.
            }

            await Task.Delay(20);
        }

        Assert.Fail("The harmless fixture did not publish its bounded child marker.");
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
