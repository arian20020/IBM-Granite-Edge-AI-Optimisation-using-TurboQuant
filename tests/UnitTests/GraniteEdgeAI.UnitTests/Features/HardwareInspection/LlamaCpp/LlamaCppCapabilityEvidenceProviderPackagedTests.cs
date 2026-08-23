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

        Task<LlamaCppCapabilityEvidence> capture = CreateProvider().CaptureAsync(
            fixture.Tool,
            CancellationToken.None);
        using Process? observedProcess = mode == "sleep"
            ? await WaitForLiveProcessAsync(
                Path.Combine(fixture.ControlRoot, "owned-root-ready.txt"),
                capture)
            : null;
        LlamaCppCapabilityEvidence evidence = await capture;

        AssertUnavailable(evidence, diagnostic);
        CollectionAssert.AreEquivalent(
            inventoryBefore,
            Directory.GetFiles(fixture.PackageRoot, "*", SearchOption.AllDirectories));
        if (mode == "sleep")
        {
            await AssertProcessExitedAsync(observedProcess!);
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
        Process childProcess;
        try
        {
            childProcess = await WaitForLiveProcessAsync(
                Path.Combine(fixture.ControlRoot, "spawn-child-ready.txt"),
                capture);
        }
        catch
        {
            cancellation.Cancel();
            try
            {
                await capture;
            }
            catch (OperationCanceledException)
            {
                // Preserve the original readiness failure after bounded cleanup.
            }

            throw;
        }

        using (childProcess)
        {
            cancellation.Cancel();
            OperationCanceledException error = await Assert.ThrowsExactlyAsync<OperationCanceledException>(
                async () => await capture);

            Assert.AreEqual(cancellation.Token, error.CancellationToken);
            await AssertProcessExitedAsync(childProcess);
        }
    }

    [TestMethod]
    [TestCategory("HardwareInspectionProcessAcceptance")]
    public async Task JobCustodyKillsChildAfterNormalParentExit()
    {
        using VerifiedPackagedToolFixture fixture = CreateFixture("spawn-child-exit");

        Task<LlamaCppCapabilityEvidence> capture = CreateProvider().CaptureAsync(
            fixture.Tool,
            CancellationToken.None);
        using Process childProcess = await WaitForLiveProcessAsync(
            Path.Combine(fixture.ControlRoot, "spawn-child-ready.txt"),
            capture);
        LlamaCppCapabilityEvidence evidence = await capture;

        Assert.AreEqual(LlamaCppCapabilityEvidenceState.Available, evidence.State);
        await AssertProcessExitedAsync(childProcess);
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

    private static async Task<Process> WaitForLiveProcessAsync(
        string marker,
        Task<LlamaCppCapabilityEvidence>? capture = null)
    {
        string errorMarker = Path.Combine(
            Path.GetDirectoryName(marker)!,
            "spawn-child-error.txt");
        Stopwatch elapsed = Stopwatch.StartNew();
        // Capture performs the bounded identity probe before starting the
        // capabilities process. Allow both cold starts on constrained guests.
        while (elapsed.Elapsed < TimeSpan.FromSeconds(30))
        {
            if (capture?.IsCompleted == true)
            {
                LlamaCppCapabilityEvidence completed = await capture;
                Assert.Fail(
                    "Capability capture completed before the child marker: " +
                    $"state={completed.State}; diagnostic={completed.Diagnostic?.ToString() ?? "none"}.");
            }

            try
            {
                if (File.Exists(marker) && int.TryParse(await File.ReadAllTextAsync(marker), out int processId))
                {
                    Process process = Process.GetProcessById(processId);
                    if (!process.HasExited)
                    {
                        return process;
                    }

                    process.Dispose();
                    Assert.Fail("The harmless fixture process exited before it could be observed.");
                }
            }
            catch (IOException)
            {
                // The fixture created the marker but has not released its write handle yet.
            }

            if (File.Exists(errorMarker))
            {
                Assert.Fail(
                    $"The harmless fixture child could not start: {await File.ReadAllTextAsync(errorMarker)}");
            }

            await Task.Delay(20);
        }

        Assert.Fail("The harmless fixture did not publish its bounded child marker.");
        throw new InvalidOperationException("Unreachable after Assert.Fail.");
    }

    private static async Task AssertProcessExitedAsync(Process process)
    {
        Stopwatch elapsed = Stopwatch.StartNew();
        while (elapsed.Elapsed < TimeSpan.FromSeconds(5))
        {
            process.Refresh();
            if (process.HasExited)
            {
                return;
            }

            await Task.Delay(20);
        }

        Assert.Fail("A harmless fixture process remained after bounded cleanup.");
    }
}
