using GraniteEdgeAI.Features.HardwareInspection.Application;
using GraniteEdgeAI.Features.HardwareInspection.Orchestration;
using GraniteEdgeAI.Features.HardwareInspection.Resolution;
using GraniteEdgeAI.HardwareInspection.Foundation.Windows;
using GraniteEdgeAI.UnitTests.Features.HardwareInspection.Support;

namespace GraniteEdgeAI.UnitTests.Features.HardwareInspection.Orchestration;

[TestClass]
[TestCategory("HardwareInspection")]
public sealed class HardwareEvidenceCollectionCoordinatorTests
{
    [TestMethod]
    public async Task CollectAsync_ReportsCategoryStagesAndBuildsOneExactAggregate()
    {
        using ToolLeaseFixture tools = new();
        HardwareEvidenceCaptureTestDouble capture = new();
        RecordingProgress progress = new();
        HardwareEvidenceCollectionCoordinator coordinator = new(capture, TimeProvider.System, 4, 1);

        HardwareEvidenceCollectionResult result = await coordinator.CollectAsync(
            tools.Lease, progress, CancellationToken.None);

        Assert.IsTrue(result.IsSuccess);
        Assert.IsNull(result.FailureCode);
        CollectedHardwareEvidence evidence = result.Evidence!;
        Assert.AreSame(capture.Processor, evidence.WindowsProcessor);
        Assert.AreSame(capture.System, evidence.WindowsSystem.Snapshot);
        Assert.AreSame(capture.Storage, evidence.Storage);
        Assert.AreSame(capture.Graphics, evidence.Graphics);
        Assert.AreSame(capture.NeuralProcessor, evidence.NeuralProcessor);
        Assert.AreSame(capture.LlmFit, evidence.LlmFit);
        Assert.AreSame(capture.LlamaCpp, evidence.LlamaCpp);
        CollectionAssert.AreEqual(new[] { 1, 1, 1, 1, 1, 1, 1 }, capture.Calls);
        CollectionAssert.AreEqual(
            new[]
            {
                HardwareInspectionRunStage.ReadingProcessorInformation,
                HardwareInspectionRunStage.ReadingSystemMemory,
                HardwareInspectionRunStage.DetectingGraphicsHardware,
                HardwareInspectionRunStage.CheckingLocalInferenceRuntimes,
            },
            progress.Stages);
    }

    [TestMethod]
    public async Task CollectAsync_EnforcesFourNativeOneExternalAndAllowsLaneOverlap()
    {
        using ToolLeaseFixture tools = new();
        HardwareEvidenceCaptureTestDouble capture = new() { Hold = true };
        HardwareEvidenceCollectionCoordinator coordinator = new(capture, TimeProvider.System, 4, 1);

        Task<HardwareEvidenceCollectionResult> pending = coordinator.CollectAsync(
            tools.Lease, new RecordingProgress(), CancellationToken.None);
        await capture.WaitForStartsAsync(native: 4, external: 1);
        Assert.AreEqual(4, capture.MaximumNative);
        Assert.AreEqual(1, capture.MaximumExternal);
        Assert.IsTrue(capture.LanesOverlapped);
        capture.Release();

        HardwareEvidenceCollectionResult result = await pending;
        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(5, capture.NativeStarted);
        Assert.AreEqual(2, capture.ExternalStarted);
        Assert.AreEqual(7, capture.Completed);
        Assert.AreEqual(4, capture.MaximumNative);
        Assert.AreEqual(1, capture.MaximumExternal);
    }

    [TestMethod]
    public async Task CollectAsync_CallerCancellationCancelsSiblingsAndAwaitsCleanup()
    {
        using ToolLeaseFixture tools = new();
        HardwareEvidenceCaptureTestDouble capture = new() { Hold = true };
        HardwareEvidenceCollectionCoordinator coordinator = new(capture, TimeProvider.System, 4, 1);
        using CancellationTokenSource cancellation = new();
        Task<HardwareEvidenceCollectionResult> pending = coordinator.CollectAsync(
            tools.Lease, new RecordingProgress(), cancellation.Token);
        await capture.WaitForStartsAsync(native: 4, external: 1);

        cancellation.Cancel();

        OperationCanceledException error = await Assert.ThrowsAsync<OperationCanceledException>(
            async () => await pending);
        Assert.AreEqual(cancellation.Token, error.CancellationToken);
        Assert.AreEqual(capture.NativeStarted + capture.ExternalStarted, capture.Completed);
    }

    [TestMethod]
    public async Task CollectAsync_UnexpectedFailureCancelsSiblingsAwaitsCleanupAndReturnsClosedCode()
    {
        using ToolLeaseFixture tools = new();
        HardwareEvidenceCaptureTestDouble capture = new()
        {
            Hold = true,
            FailingProvider = "processor",
        };
        HardwareEvidenceCollectionCoordinator coordinator = new(capture, TimeProvider.System, 4, 1);
        Task<HardwareEvidenceCollectionResult> pending = coordinator.CollectAsync(
            tools.Lease, new RecordingProgress(), CancellationToken.None);
        await capture.WaitForStartsAsync(native: 4, external: 1);

        capture.Fail();

        HardwareEvidenceCollectionResult result = await pending;
        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(HardwareEvidenceCollectionFailureCode.OrchestrationFailure, result.FailureCode);
        Assert.IsNull(result.Evidence);
        Assert.AreEqual(capture.NativeStarted + capture.ExternalStarted, capture.Completed);
    }

    [TestMethod]
    public async Task CollectAsync_ProgressFailureCancelsStartedWorkAndReturnsClosedCode()
    {
        using ToolLeaseFixture tools = new();
        HardwareEvidenceCaptureTestDouble capture = new() { Hold = true };
        HardwareEvidenceCollectionCoordinator coordinator = new(capture, TimeProvider.System, 4, 1);
        ThrowingProgress progress = new(HardwareInspectionRunStage.DetectingGraphicsHardware);

        HardwareEvidenceCollectionResult result = await coordinator.CollectAsync(
            tools.Lease, progress, CancellationToken.None);

        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(HardwareEvidenceCollectionFailureCode.ProgressCallbackFailure, result.FailureCode);
        Assert.AreEqual(capture.NativeStarted + capture.ExternalStarted, capture.Completed);
        CollectionAssert.AreEqual(
            new[]
            {
                HardwareInspectionRunStage.ReadingProcessorInformation,
                HardwareInspectionRunStage.ReadingSystemMemory,
                HardwareInspectionRunStage.DetectingGraphicsHardware,
            },
            progress.Stages);
    }

    [TestMethod]
    public async Task CollectAsync_ConvertsEveryClosedWindowsSystemFailureWithoutReadingMessage()
    {
        foreach (WindowsSystemSnapshotDiagnosticCode diagnostic in Enum.GetValues<WindowsSystemSnapshotDiagnosticCode>())
        {
            using ToolLeaseFixture tools = new();
            HardwareEvidenceCaptureTestDouble capture = new() { SystemFailure = diagnostic };
            HardwareEvidenceCollectionCoordinator coordinator = new(capture, TimeProvider.System, 4, 1);

            HardwareEvidenceCollectionResult result = await coordinator.CollectAsync(
                tools.Lease, new RecordingProgress(), CancellationToken.None);

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(WindowsSystemObservationState.Unavailable, result.Evidence!.WindowsSystem.State);
            Assert.AreEqual(Map(diagnostic), result.Evidence.WindowsSystem.Diagnostic);
            Assert.AreEqual(HardwareEvidenceCaptureTestDouble.CapturedAtUtc, result.Evidence.WindowsSystem.AttemptedAtUtc);
        }
    }

    private static WindowsSystemObservationDiagnosticCode Map(WindowsSystemSnapshotDiagnosticCode code) => code switch
    {
        WindowsSystemSnapshotDiagnosticCode.MemoryUnavailable => WindowsSystemObservationDiagnosticCode.MemoryUnavailable,
        WindowsSystemSnapshotDiagnosticCode.MemoryOverflow => WindowsSystemObservationDiagnosticCode.MemoryOverflow,
        WindowsSystemSnapshotDiagnosticCode.MemoryInconsistent => WindowsSystemObservationDiagnosticCode.MemoryInconsistent,
        _ => throw new ArgumentOutOfRangeException(nameof(code)),
    };

    private sealed class RecordingProgress : IProgress<HardwareInspectionRunStage>
    {
        internal List<HardwareInspectionRunStage> Stages { get; } = [];
        public void Report(HardwareInspectionRunStage value) => Stages.Add(value);
    }

    private sealed class ThrowingProgress(HardwareInspectionRunStage failureStage) : IProgress<HardwareInspectionRunStage>
    {
        internal List<HardwareInspectionRunStage> Stages { get; } = [];
        public void Report(HardwareInspectionRunStage value)
        {
            Stages.Add(value);
            if (value == failureStage) throw new InvalidOperationException("private callback text");
        }
    }

    private sealed class ToolLeaseFixture : IDisposable
    {
        private readonly VerifiedPackagedToolFixture _llmFit = VerifiedPackagedToolFixture.CreateLlmFit("success");
        private readonly VerifiedPackagedToolFixture _llamaCpp = VerifiedPackagedToolFixture.CreateLlamaCpp("success");
        internal ToolLeaseFixture() => Lease = new(_llmFit.Tool, _llamaCpp.Tool);
        internal HardwareToolLease Lease { get; }
        public void Dispose()
        {
            Lease.Dispose();
            _llamaCpp.Dispose();
            _llmFit.Dispose();
        }
    }
}
