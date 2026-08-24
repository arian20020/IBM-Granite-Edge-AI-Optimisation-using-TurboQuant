using GraniteEdgeAI.Features.HardwareInspection.Application;
using GraniteEdgeAI.Features.HardwareInspection.Orchestration;
using GraniteEdgeAI.Features.HardwareInspection.Resolution;
using GraniteEdgeAI.HardwareInspection.Foundation.TrustedTools;
using GraniteEdgeAI.UnitTests.Features.HardwareInspection.Resolution;
using GraniteEdgeAI.UnitTests.Features.HardwareInspection.Support;

namespace GraniteEdgeAI.UnitTests.Features.HardwareInspection.Orchestration;

[TestClass]
[TestCategory("HardwareInspection")]
[TestCategory("HardwareInspectionGate7Acceptance")]
public sealed class HardwareInspectionServiceTests
{
    [TestMethod]
    public async Task RunAsync_RejectsInvalidArgumentsAndPreCancellationHasNoEffects()
    {
        ScriptedAcquisition acquisition = new(() => throw new InvalidOperationException());
        ScriptedCoordinator coordinator = new();
        ScriptedResolver resolver = new(CreateResolution());
        HardwareInspectionService service = new(acquisition, coordinator, resolver);
        RecordingProgress progress = new();

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.RunAsync(Guid.Empty, progress, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => service.RunAsync(Guid.NewGuid(), null!, CancellationToken.None));
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();
        Guid id = Guid.NewGuid();
        HardwareInspectionRunResult result = await service.RunAsync(id, progress, cancellation.Token);

        Assert.AreEqual(HardwareInspectionOutcome.Cancelled, result.Outcome);
        Assert.AreEqual(id, result.InspectionId);
        Assert.AreEqual(0, acquisition.Calls);
        Assert.AreEqual(0, coordinator.Calls);
        Assert.AreEqual(0, resolver.Calls);
        Assert.HasCount(0, progress.Events);
    }

    [TestMethod]
    public async Task RunAsync_EmitsExactSequenceResolvesOnceAndKeepsCustodyAlive()
    {
        using ToolLeaseFixture tools = new();
        HardwareEvidenceResolutionResult resolution = CreateResolution();
        ScriptedAcquisition acquisition = new(() => HardwareToolAcquisitionResult.Success(tools.Lease));
        ScriptedCoordinator coordinator = new();
        ScriptedResolver resolver = new(resolution)
        {
            OnResolve = () =>
            {
                Assert.IsFalse(tools.LlmFit.IsDisposed);
                Assert.IsFalse(tools.LlamaCpp.IsDisposed);
            },
        };
        HardwareInspectionService service = new(acquisition, coordinator, resolver);
        RecordingProgress progress = new();
        Guid id = Guid.NewGuid();

        HardwareInspectionRunResult result = await service.RunAsync(
            id, progress, CancellationToken.None);

        Assert.AreEqual(HardwareInspectionOutcome.CompletedWithWarnings, result.Outcome);
        Assert.IsNotNull(result.Snapshot);
        Assert.IsNotNull(result.Handoff);
        Assert.AreEqual(1, acquisition.Calls);
        Assert.AreEqual(1, coordinator.Calls);
        Assert.AreEqual(1, resolver.Calls);
        Assert.IsTrue(tools.LlmFit.IsDisposed);
        Assert.IsTrue(tools.LlamaCpp.IsDisposed);
        CollectionAssert.AreEqual(
            Enum.GetValues<HardwareInspectionRunStage>(),
            progress.Events.Select(item => item.Stage).ToArray());
        CollectionAssert.AreEqual(
            Enumerable.Range(1, 7).Select(value => (long)value).ToArray(),
            progress.Events.Select(item => item.Sequence).ToArray());
        Assert.IsTrue(progress.Events.All(item => item.InspectionId == id));
    }

    [TestMethod]
    public async Task RunAsync_MapsEveryAcquisitionAndCollectionFailureExactly()
    {
        foreach ((HardwareToolAcquisitionDiagnosticCode code, string expected) in new[]
        {
            (HardwareToolAcquisitionDiagnosticCode.ToolNotAvailable, "HI-TOOL-NOT-AVAILABLE"),
            (HardwareToolAcquisitionDiagnosticCode.ToolIntegrityFailure, "HI-TOOL-INTEGRITY"),
            (HardwareToolAcquisitionDiagnosticCode.PackagedProbeUnavailable, "HI-RUNTIME-PACKAGE"),
        })
        {
            HardwareInspectionService service = new(
                new ScriptedAcquisition(() => HardwareToolAcquisitionResult.Failure(code)),
                new ScriptedCoordinator(),
                new ScriptedResolver(CreateResolution()));
            HardwareInspectionRunResult result = await service.RunAsync(
                Guid.NewGuid(), new RecordingProgress(), CancellationToken.None);
            Assert.AreEqual(expected, result.SafeDiagnosticCode);
            Assert.IsNull(result.Handoff);
        }

        foreach ((HardwareEvidenceCollectionFailureCode code, string expected) in new[]
        {
            (HardwareEvidenceCollectionFailureCode.ProviderUnavailable, "HI-PROVIDER-UNAVAILABLE"),
            (HardwareEvidenceCollectionFailureCode.OrchestrationFailure, "HI-ORCHESTRATION-FAILED"),
            (HardwareEvidenceCollectionFailureCode.ProgressCallbackFailure, "HI-PROGRESS-CALLBACK"),
        })
        {
            using ToolLeaseFixture tools = new();
            ScriptedCoordinator coordinator = new()
            {
                Result = HardwareEvidenceCollectionResult.Failure(code),
            };
            HardwareInspectionService service = new(
                new ScriptedAcquisition(() => HardwareToolAcquisitionResult.Success(tools.Lease)),
                coordinator,
                new ScriptedResolver(CreateResolution()));
            HardwareInspectionRunResult result = await service.RunAsync(
                Guid.NewGuid(), new RecordingProgress(), CancellationToken.None);
            Assert.AreEqual(expected, result.SafeDiagnosticCode);
            Assert.IsNull(result.Handoff);
        }
    }

    [TestMethod]
    public async Task RunAsync_MapsCleanWarningAndFailedResolutionToTruthfulOutcomes()
    {
        HardwareEvidenceResolutionResult warning = CreateResolution();
        HardwareEvidenceResolutionResult clean = HardwareEvidenceResolutionResult.Success(
            warning.Snapshot!, warning.Evidence, []);
        HardwareEvidenceResolutionResult failed = HardwareEvidenceResolutionResult.Failure(
            warning.Evidence, [HardwareResolutionDiagnosticCode.NormalizationInvalid]);

        foreach ((HardwareEvidenceResolutionResult resolution, HardwareInspectionOutcome outcome, string? code) in new[]
        {
            (clean, HardwareInspectionOutcome.Completed, (string?)null),
            (warning, HardwareInspectionOutcome.CompletedWithWarnings, (string?)null),
            (failed, HardwareInspectionOutcome.Failed, "HI-EVIDENCE-UNRESOLVED"),
        })
        {
            using ToolLeaseFixture tools = new();
            HardwareInspectionService service = new(
                new ScriptedAcquisition(() => HardwareToolAcquisitionResult.Success(tools.Lease)),
                new ScriptedCoordinator(),
                new ScriptedResolver(resolution));
            HardwareInspectionRunResult result = await service.RunAsync(
                Guid.NewGuid(), new RecordingProgress(), CancellationToken.None);
            Assert.AreEqual(outcome, result.Outcome);
            Assert.AreEqual(code, result.SafeDiagnosticCode);
            Assert.AreEqual(outcome != HardwareInspectionOutcome.Failed, result.Handoff is not null);
        }
    }

    [TestMethod]
    public async Task RunAsync_CallbackFailureStopsLifecycleAndReturnsClosedFailure()
    {
        using ToolLeaseFixture tools = new();
        ScriptedResolver resolver = new(CreateResolution());
        HardwareInspectionService service = new(
            new ScriptedAcquisition(() => HardwareToolAcquisitionResult.Success(tools.Lease)),
            new ScriptedCoordinator(),
            resolver);
        RecordingProgress progress = new(HardwareInspectionRunStage.NormalisingHardwareInformation);

        HardwareInspectionRunResult result = await service.RunAsync(
            Guid.NewGuid(), progress, CancellationToken.None);

        Assert.AreEqual("HI-PROGRESS-CALLBACK", result.SafeDiagnosticCode);
        Assert.AreEqual(0, resolver.Calls);
        Assert.AreEqual(HardwareInspectionRunStage.NormalisingHardwareInformation, progress.Events[^1].Stage);
        Assert.IsFalse(progress.Events.Any(item => item.Stage == HardwareInspectionRunStage.CreatingHardwareReport));
    }

    [TestMethod]
    public async Task RunAsync_CancellationAfterResolutionWinsBeforeFinalProgress()
    {
        using ToolLeaseFixture tools = new();
        using CancellationTokenSource cancellation = new();
        ScriptedResolver resolver = new(CreateResolution()) { OnResolve = cancellation.Cancel };
        HardwareInspectionService service = new(
            new ScriptedAcquisition(() => HardwareToolAcquisitionResult.Success(tools.Lease)),
            new ScriptedCoordinator(),
            resolver);
        RecordingProgress progress = new();

        HardwareInspectionRunResult result = await service.RunAsync(
            Guid.NewGuid(), progress, cancellation.Token);

        Assert.AreEqual(HardwareInspectionOutcome.Cancelled, result.Outcome);
        Assert.AreEqual(1, resolver.Calls);
        Assert.IsFalse(progress.Events.Any(item => item.Stage == HardwareInspectionRunStage.CreatingHardwareReport));
        Assert.IsNull(result.Handoff);
    }

    [TestMethod]
    public async Task RunAsync_UnexpectedExceptionBecomesSafeOrchestrationFailure()
    {
        HardwareInspectionService service = new(
            new ScriptedAcquisition(() => throw new InvalidOperationException("username path secret")),
            new ScriptedCoordinator(),
            new ScriptedResolver(CreateResolution()));

        HardwareInspectionRunResult result = await service.RunAsync(
            Guid.NewGuid(), new RecordingProgress(), CancellationToken.None);

        Assert.AreEqual("HI-ORCHESTRATION-FAILED", result.SafeDiagnosticCode);
        Assert.IsFalse(result.SafeDiagnosticCode!.Contains("secret", StringComparison.OrdinalIgnoreCase));
        Assert.IsNull(result.Handoff);
    }

    private static HardwareEvidenceResolutionResult CreateResolution() =>
        new HardwareEvidenceResolver(
            new HardwareResolutionTestData.FixedTimeProvider(HardwareResolutionTestData.Now))
        .Resolve(Guid.NewGuid(), HardwareResolutionTestData.CompleteEvidence());

    private sealed class ScriptedAcquisition(Func<HardwareToolAcquisitionResult> acquire) : IHardwareToolAcquisition
    {
        internal int Calls { get; private set; }
        public HardwareToolAcquisitionResult Acquire() { Calls++; return acquire(); }
    }

    private sealed class ScriptedCoordinator : IHardwareEvidenceCollectionCoordinator
    {
        internal int Calls { get; private set; }
        internal HardwareEvidenceCollectionResult Result { get; set; } =
            HardwareEvidenceCollectionResult.Success(HardwareResolutionTestData.CompleteEvidence());
        public Task<HardwareEvidenceCollectionResult> CollectAsync(
            HardwareToolLease tools,
            IProgress<HardwareInspectionRunStage> progress,
            CancellationToken token)
        {
            Calls++;
            foreach (HardwareInspectionRunStage stage in Enum.GetValues<HardwareInspectionRunStage>()[1..5])
            {
                token.ThrowIfCancellationRequested();
                progress.Report(stage);
            }
            return Task.FromResult(Result);
        }
    }

    private sealed class ScriptedResolver(HardwareEvidenceResolutionResult result) : IHardwareEvidenceResolver
    {
        internal int Calls { get; private set; }
        internal Action? OnResolve { get; set; }
        public HardwareEvidenceResolutionResult Resolve(Guid inspectionId, CollectedHardwareEvidence evidence)
        {
            Calls++;
            OnResolve?.Invoke();
            return result;
        }
    }

    private sealed class RecordingProgress(HardwareInspectionRunStage? failureStage = null)
        : IProgress<HardwareInspectionRunProgress>
    {
        internal List<HardwareInspectionRunProgress> Events { get; } = [];
        public void Report(HardwareInspectionRunProgress value)
        {
            Events.Add(value);
            if (value.Stage == failureStage) throw new InvalidOperationException("private callback text");
        }
    }

    private sealed class ToolLeaseFixture : IDisposable
    {
        private readonly VerifiedPackagedToolFixture _llmFit = VerifiedPackagedToolFixture.CreateLlmFit("success");
        private readonly VerifiedPackagedToolFixture _llamaCpp = VerifiedPackagedToolFixture.CreateLlamaCpp("success");
        internal ToolLeaseFixture()
        {
            LlmFit = _llmFit.Tool;
            LlamaCpp = _llamaCpp.Tool;
            Lease = new(LlmFit, LlamaCpp);
        }
        internal HardwareToolLease Lease { get; }
        internal VerifiedTrustedTool LlmFit { get; }
        internal VerifiedTrustedTool LlamaCpp { get; }
        public void Dispose()
        {
            Lease.Dispose();
            _llamaCpp.Dispose();
            _llmFit.Dispose();
        }
    }
}
