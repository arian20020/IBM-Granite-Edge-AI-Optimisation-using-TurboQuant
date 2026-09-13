using GraniteEdgeAI.Features.HardwareInspection.Application;
using GraniteEdgeAI.Features.HardwareInspection.Orchestration;
using GraniteEdgeAI.Features.HardwareInspection.Resolution;
using GraniteEdgeAI.HardwareInspection.Foundation.LlmFit;
using GraniteEdgeAI.HardwareInspection.Foundation.Windows;
using GraniteEdgeAI.UnitTests.Features.HardwareInspection.Resolution;
using GraniteEdgeAI.UnitTests.Features.HardwareInspection.Support;

namespace GraniteEdgeAI.UnitTests.Features.HardwareInspection.Orchestration;

[TestClass]
[DoNotParallelize]
[TestCategory("HardwareInspection")]
[TestCategory("HardwareInspectionGate7Acceptance")]
public sealed class HardwareEvidenceCollectionCoordinatorTests
{
    [TestMethod]
    [TestCategory("EstimatedProgress")]
    public async Task OutOfOrderGroupCompletionAndCancellationKeepTheirOwnIdentity()
    {
        using ToolLeaseFixture tools = new();
        using var cancellation = new CancellationTokenSource();
        var capture = new HardwareEvidenceCaptureTestDouble { FailingProvider = "processor" };
        var progress = new MeasuredProgress { OnGroup = _ => cancellation.Cancel() };
        var coordinator = new HardwareEvidenceCollectionCoordinator(capture, TimeProvider.System, 4, 1);
        try
        {
            await coordinator.CollectAsync(tools.Lease, progress, cancellation.Token).WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Fail("Cancellation must not produce a successful collection.");
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
        Assert.AreEqual(1, progress.Groups.Count);
        Assert.AreNotEqual(HardwareInspectionRunStage.ReadingProcessorInformation, progress.Groups[0].Group);
        Assert.AreEqual(1, progress.Groups[0].Completed);
        Assert.AreEqual(2, progress.Groups[0].Total);
        Assert.AreEqual(capture.NativeStarted + capture.ExternalStarted, capture.Completed);
    }

    [TestMethod]
    [TestCategory("ChatTextInteractionsMeasured")]
    public async Task SuccessfulChecksReportMeasuredCompletionCounts()
    {
        using ToolLeaseFixture tools = new();
        var progress = new MeasuredProgress();
        var capture = new HardwareEvidenceCaptureTestDouble();
        var coordinator = new HardwareEvidenceCollectionCoordinator(capture, TimeProvider.System, 4, 1);
        var result = await coordinator.CollectAsync(tools.Lease, progress, CancellationToken.None);
        Assert.IsTrue(result.IsSuccess);
        CollectionAssert.AreEqual(new[] { 1, 2, 3, 4, 5, 6, 7 }, progress.Counts.Select(item => item.Completed).ToArray());
        Assert.IsTrue(progress.Counts.All(item => item.Total == 7));
        Assert.AreEqual(7, capture.Completed);
        Assert.AreEqual(7, progress.Groups.Count, "Each settled check must report its own group.");
        Assert.AreEqual(1, progress.Groups.Last(value => value.Group == HardwareInspectionRunStage.ReadingProcessorInformation).Completed);
        foreach (var group in new[] { HardwareInspectionRunStage.ReadingSystemMemory, HardwareInspectionRunStage.DetectingGraphicsHardware, HardwareInspectionRunStage.CheckingLocalInferenceRuntimes })
        {
            Assert.AreEqual(2, progress.Groups.Last(value => value.Group == group).Completed);
            Assert.IsTrue(progress.Groups.Where(value => value.Group == group).All(value => value.Total == 2));
        }
    }

    [TestMethod]
    [TestCategory("ChatTextInteractionsMeasured")]
    [DataRow(false)]
    [DataRow(true)]
    public async Task MeasuredCallbackFailureCancelsAndDrainsOutstandingChecks(bool grouped)
    {
        using ToolLeaseFixture tools = new();
        var progress = new MeasuredProgress { Throw = !grouped, ThrowGroup = grouped };
        var capture = new HardwareEvidenceCaptureTestDouble { FailingProvider = "processor" };
        var coordinator = new HardwareEvidenceCollectionCoordinator(capture, TimeProvider.System, 4, 1);
        var result = await coordinator.CollectAsync(tools.Lease, progress, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(HardwareEvidenceCollectionFailureCode.ProgressCallbackFailure, result.FailureCode);
        Assert.IsTrue(capture.NativeStarted > 0);
        Assert.AreEqual(capture.NativeStarted + capture.ExternalStarted, capture.Completed,
            "The held processor check must settle before collection returns.");
        int completed = capture.Completed;
        await Task.Delay(50);
        Assert.AreEqual(completed, capture.Completed, "No evidence task may continue after callback failure returns.");
    }

    private sealed class MeasuredProgress : IProgress<HardwareInspectionRunStage>, IProgress<(int Completed, int Total)>,
        IProgress<(HardwareInspectionRunStage Group, int Completed, int Total)>
    {
        internal List<(int Completed, int Total)> Counts { get; } = [];
        internal List<(HardwareInspectionRunStage Group, int Completed, int Total)> Groups { get; } = [];
        public void Report((HardwareInspectionRunStage Group, int Completed, int Total) value)
        {
            if (ThrowGroup) throw new InvalidOperationException("Injected group callback failure.");
            Groups.Add(value);
            OnGroup?.Invoke(value);
        }
        internal bool ThrowGroup { get; init; }
        internal Action<(HardwareInspectionRunStage Group, int Completed, int Total)>? OnGroup { get; init; }
        internal bool Throw { get; init; }
        public void Report(HardwareInspectionRunStage stage) { }
        public void Report((int Completed, int Total) value)
        {
            if (Throw) throw new InvalidOperationException("Injected measurement failure.");
            Counts.Add(value);
        }
    }

    [TestMethod]
    public async Task CollectAsync_MissingOptionalLlmFitPreservesWindowsFactsAndSkipsItsProcess()
    {
        using VerifiedPackagedToolFixture llamaCpp =
            VerifiedPackagedToolFixture.CreateLlamaCpp("success");
        using HardwareToolLease tools = new(
            llmFit: null,
            llamaCpp.Tool,
            HardwareToolAcquisitionDiagnosticCode.ToolNotAvailable);
        HardwareEvidenceCaptureTestDouble capture = new();
        HardwareEvidenceCollectionCoordinator coordinator = new(
            capture,
            new HardwareResolutionTestData.FixedTimeProvider(
                HardwareEvidenceCaptureTestDouble.CapturedAtUtc),
            4,
            1);

        HardwareEvidenceCollectionResult result = await coordinator.CollectAsync(
            tools, new RecordingProgress(), CancellationToken.None);

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(LlmFitEvidenceState.Unavailable, result.Evidence!.LlmFit.State);
        CollectionAssert.AreEqual(
            new[] { LlmFitDiagnosticCode.ToolNotAvailable },
            result.Evidence.LlmFit.Diagnostics.ToArray());
        Assert.AreSame(capture.Processor, result.Evidence.WindowsProcessor);
        Assert.AreSame(capture.System, result.Evidence.WindowsSystem.Snapshot);
        CollectionAssert.AreEqual(new[] { 1, 1, 1, 1, 1, 0, 1 }, capture.Calls);
    }

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
            FailFirstNative = true,
        };
        HardwareEvidenceCollectionCoordinator coordinator = new(capture, TimeProvider.System, 4, 1);
        Task<HardwareEvidenceCollectionResult> pending = coordinator.CollectAsync(
            tools.Lease, new RecordingProgress(), CancellationToken.None);
        await capture.WaitForStartsAsync(native: 4, external: 1);

        Assert.IsTrue(capture.FailingProviderStarted);
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
            HardwareEvidenceCollectionCoordinator coordinator = new(
                capture,
                new HardwareResolutionTestData.FixedTimeProvider(
                    HardwareEvidenceCaptureTestDouble.CapturedAtUtc),
                4,
                1);

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
