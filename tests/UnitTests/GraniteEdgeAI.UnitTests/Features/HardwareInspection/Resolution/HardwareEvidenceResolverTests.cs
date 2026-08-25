using GraniteEdgeAI.Features.HardwareInspection.Domain;
using GraniteEdgeAI.Features.HardwareInspection.Resolution;
using GraniteEdgeAI.HardwareInspection.Foundation.Dxgi;
using GraniteEdgeAI.HardwareInspection.Foundation.LlamaCpp;
using GraniteEdgeAI.HardwareInspection.Foundation.LlmFit;
using GraniteEdgeAI.HardwareInspection.Foundation.NeuralProcessors;
using GraniteEdgeAI.HardwareInspection.Foundation.Windows;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.UnitTests.Features.HardwareInspection.Resolution;

[TestClass]
public sealed class HardwareEvidenceResolverTests
{
    private static readonly string[] ExpectedFields =
    [
        "processor.name",
        "processor.architecture",
        "processor.physicalCores",
        "processor.logicalProcessors",
        "processor.instructionSets",
        "memory.installedBytes",
        "memory.osUsableBytes",
        "memory.availableBytes",
        "graphics.adapters",
        "graphics.memory",
        "npu.state",
        "storage.systemVolumeCapacityBytes",
        "storage.systemVolumeAvailableBytes",
        "os.name",
        "os.version",
        "os.architecture",
        "runtime.buildIdentity",
        "runtime.backends",
        "runtime.visibleDevices",
    ];

    private static readonly string[] ConstructionCriticalFields =
    [
        "processor.name",
        "processor.architecture",
        "processor.physicalCores",
        "processor.logicalProcessors",
        "memory.installedBytes",
        "memory.osUsableBytes",
        "memory.availableBytes",
        "storage.systemVolumeCapacityBytes",
        "storage.systemVolumeAvailableBytes",
        "os.name",
        "os.version",
        "os.architecture",
        "runtime.buildIdentity",
        "runtime.backends",
        "runtime.visibleDevices",
    ];

    [TestMethod]
    [TestCategory("Unit")]
    public void Resolve_CompleteEvidenceBuildsExactCanonicalSnapshotAndManifest()
    {
        Guid snapshotId = Guid.Parse("d731ff0d-cf68-43af-a9bb-f2d49df71f03");
        HardwareEvidenceResolutionResult result = Resolver().Resolve(
            snapshotId,
            HardwareResolutionTestData.CompleteEvidence());

        Assert.IsTrue(result.IsResolved);
        Assert.IsNotNull(result.Snapshot);
        HardwareSnapshot snapshot = result.Snapshot;
        Assert.AreEqual(snapshotId, snapshot.SnapshotId);
        Assert.AreEqual(HardwareResolutionTestData.Now, snapshot.CapturedAtUtc);
        Assert.AreEqual(HardwareResolutionPolicy.SchemaVersion, snapshot.SchemaVersion);
        Assert.AreEqual(HardwareResolutionPolicy.PolicyVersion, snapshot.PolicyVersion);
        Assert.AreEqual(HardwareSnapshotUsability.Usable, snapshot.Usability);
        Assert.AreSame(result.Evidence, snapshot.Evidence);
        Assert.AreEqual("Intel Core Ultra 7 155H", snapshot.Processor.Name);
        Assert.AreEqual("x64", snapshot.Processor.Architecture);
        Assert.AreEqual(16, snapshot.Processor.PhysicalCoreCount);
        Assert.AreEqual(22, snapshot.Processor.LogicalProcessorCount);
        Assert.AreEqual(32 * HardwareResolutionTestData.GiB,
            snapshot.Memory.PhysicallyInstalledBytes);
        Assert.AreEqual("Intel Arc Graphics", snapshot.GraphicsAdapters.Single().Name);
        Assert.AreEqual(NpuDetectionState.DetectionUnavailable, snapshot.NeuralProcessor.State);
        Assert.AreEqual(1_000_000_000_000UL, snapshot.Storage.SystemVolumeCapacityBytes);
        Assert.AreEqual("Windows 11", snapshot.OperatingSystem.Name);
        Assert.AreEqual(
            "LLamaSharp/0.27.0;LLamaSharp.Backend.Cpu/0.27.0;" +
                "llama.cpp/3f7c29d318e317b63f54c558bc69803963d7d88c;win-x64",
            snapshot.LocalRuntime.BuildIdentity);

        CollectionAssert.AreEqual(
            ExpectedFields,
            result.Evidence.Entries.Select(static entry => entry.CanonicalField).ToArray());
        Assert.AreEqual(19, result.Evidence.Entries.Count);
        foreach (string field in ConstructionCriticalFields)
        {
            Assert.IsTrue(result.Evidence.Entries.Single(entry =>
                entry.CanonicalField == field).IsResolved, field);
        }

        AssertDefaultProvenance(result.Evidence.Entries);
        CollectionAssert.AreEqual(
            new[]
            {
                HardwareResolutionDiagnosticCode.NeuralProcessorUnavailable,
                HardwareResolutionDiagnosticCode.InstructionSetsUnavailable,
            },
            result.Diagnostics.ToArray());
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void Resolve_CopiesProviderCollectionsBeforeCallerMutation()
    {
        List<LlmFitReportedGpu> llmGpus = [new("LLM GPU", 1)];
        List<DxgiAdapterEvidence> dxgiAdapters =
        [
            new("Canonical GPU", DxgiAdapterKind.Hardware, 1, 2, 3, 4, 5, 0),
        ];
        List<LlamaCppBackend> backends = [LlamaCppBackend.Cpu];
        List<LlamaCppVisibleDevice> devices = [new(0, "CPU")];
        CollectedHardwareEvidence evidence = Evidence(
            llmFit: LlmFitHardwareEvidence.Available(
                LlmFitCommandContract.ToolId,
                LlmFitCommandContract.Version,
                HardwareResolutionTestData.Now,
                "Intel Core Ultra 7 155H",
                22,
                31,
                20,
                LlmFitGpuDetectionState.Reported,
                llmGpus,
                new string('0', 64)),
            graphics: DxgiGraphicsEvidence.Available(
                dxgiAdapters,
                HardwareResolutionTestData.Now),
            llamaCpp: LlamaCppCapabilityEvidence.Available(
                LlamaCppRuntimeIdentity.PinnedCpu,
                HardwareResolutionTestData.Now,
                backends,
                devices));

        llmGpus.Clear();
        dxgiAdapters.Clear();
        backends.Clear();
        devices.Clear();

        HardwareSnapshot snapshot = Resolver().Resolve(Guid.NewGuid(), evidence).Snapshot!;
        Assert.AreEqual("Canonical GPU", snapshot.GraphicsAdapters.Single().Name);
        CollectionAssert.AreEqual(
            new[] { LocalRuntimeBackend.Cpu },
            snapshot.LocalRuntime.SupportedBackends.ToArray());
        CollectionAssert.AreEqual(
            new[] { "CPU" },
            snapshot.LocalRuntime.VisibleDevices.ToArray());
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void Resolve_CriticalConflictStaleFutureOrUnavailableEvidenceFailsClosed()
    {
        LlmFitHardwareEvidence nameConflict = LlmFitHardwareEvidence.Available(
            LlmFitCommandContract.ToolId,
            LlmFitCommandContract.Version,
            HardwareResolutionTestData.Now,
            "Different Processor",
            22,
            31,
            20,
            LlmFitGpuDetectionState.NotReported,
            [],
            new string('0', 64));
        CollectedHardwareEvidence[] cases =
        [
            Evidence(llmFit: nameConflict),
            Evidence(windowsSystem: WindowsSystemEvidenceObservation.Unavailable(
                HardwareResolutionTestData.Now,
                WindowsSystemObservationDiagnosticCode.MemoryUnavailable)),
            Evidence(storage: HardwareResolutionTestData.Storage(
                HardwareResolutionTestData.Now + HardwareResolutionPolicy.FutureClockSkew +
                    TimeSpan.FromTicks(1))),
            Evidence(llamaCpp: HardwareResolutionTestData.LlamaCpp(
                HardwareResolutionTestData.Now - HardwareResolutionPolicy.StaticEvidenceMaximumAge -
                    TimeSpan.FromTicks(1))),
            Evidence(storage: WindowsStorageEvidence.Unavailable(
                WindowsStorageDiagnosticCode.NativeApiUnavailable,
                HardwareResolutionTestData.Now)),
        ];

        foreach (CollectedHardwareEvidence evidence in cases)
        {
            HardwareEvidenceResolutionResult result = Resolver().Resolve(Guid.NewGuid(), evidence);
            Assert.IsFalse(result.IsResolved);
            Assert.IsNull(result.Snapshot);
            Assert.AreEqual(19, result.Evidence.Entries.Count);
            Assert.IsTrue(result.Diagnostics.Count > 0);
        }
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void Resolve_EnforcesExactCaptureSpanBoundary()
    {
        HardwareEvidenceResolutionResult accepted = Resolver().Resolve(
            Guid.NewGuid(),
            Evidence(llmFit: HardwareResolutionTestData.LlmFit(
                HardwareResolutionTestData.Now - HardwareResolutionPolicy.MaximumCaptureSpan)));
        HardwareEvidenceResolutionResult rejected = Resolver().Resolve(
            Guid.NewGuid(),
            Evidence(llmFit: HardwareResolutionTestData.LlmFit(
                HardwareResolutionTestData.Now - HardwareResolutionPolicy.MaximumCaptureSpan -
                    TimeSpan.FromTicks(1))));

        Assert.IsTrue(accepted.IsResolved);
        Assert.IsFalse(rejected.IsResolved);
        CollectionAssert.Contains(
            rejected.Diagnostics.ToArray(),
            HardwareResolutionDiagnosticCode.CaptureSpanExceeded);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void Resolve_IgnoresUnavailableOptionalObservationInCaptureSpan()
    {
        NeuralProcessorEvidence oldUnavailable = NeuralProcessorEvidence.DetectionUnavailable(
            NeuralProcessorDiagnosticCode.EnumerationMechanismNotApproved,
            HardwareResolutionTestData.Now - HardwareResolutionPolicy.StaticEvidenceMaximumAge -
                TimeSpan.FromTicks(1));

        HardwareEvidenceResolutionResult result = Resolver().Resolve(
            Guid.NewGuid(),
            Evidence(neuralProcessor: oldUnavailable));

        Assert.IsTrue(result.IsResolved);
        Assert.AreEqual(NpuDetectionState.DetectionUnavailable, result.Snapshot!.NeuralProcessor.State);
        CollectionAssert.DoesNotContain(
            result.Diagnostics.ToArray(),
            HardwareResolutionDiagnosticCode.CaptureSpanExceeded);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void Resolve_AllowsExplicitlyUnavailableOptionalFields()
    {
        LlmFitHardwareEvidence unavailableLlmFit = LlmFitHardwareEvidence.Unavailable(
            LlmFitCommandContract.ToolId,
            LlmFitCommandContract.Version,
            HardwareResolutionTestData.Now,
            LlmFitDiagnosticCode.SystemStartFailed);
        CollectedHardwareEvidence evidence = Evidence(
            llmFit: unavailableLlmFit,
            graphics: DxgiGraphicsEvidence.Unavailable(
                DxgiGraphicsDiagnosticCode.NativeApiUnavailable,
                HardwareResolutionTestData.Now));

        HardwareEvidenceResolutionResult result = Resolver().Resolve(Guid.NewGuid(), evidence);

        Assert.IsTrue(result.IsResolved);
        Assert.AreEqual(0, result.Snapshot!.GraphicsAdapters.Count);
        Assert.IsFalse(result.Evidence.Entries.Single(entry =>
            entry.CanonicalField == "graphics.adapters").IsResolved);
        Assert.IsFalse(result.Evidence.Entries.Single(entry =>
            entry.CanonicalField == "processor.instructionSets").IsResolved);
        Assert.IsFalse(result.Evidence.Entries.Single(entry =>
            entry.CanonicalField == "npu.state").IsResolved);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void Resolve_RejectsEmptySnapshotIdentity()
    {
        Assert.Throws<ArgumentException>(() =>
            Resolver().Resolve(Guid.Empty, HardwareResolutionTestData.CompleteEvidence()));
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void CreateManifest_RejectsDuplicateMissingAndOutOfOrderInventory()
    {
        HardwareEvidenceEntry[] valid = Resolver().Resolve(
            Guid.NewGuid(),
            HardwareResolutionTestData.CompleteEvidence()).Evidence.Entries.ToArray();
        HardwareEvidenceEntry[] duplicate = valid.ToArray();
        duplicate[^1] = valid[0];
        HardwareEvidenceEntry[] reordered = valid.ToArray();
        (reordered[0], reordered[1]) = (reordered[1], reordered[0]);

        Assert.Throws<ArgumentException>(() => HardwareEvidenceResolver.CreateManifest(duplicate));
        Assert.Throws<ArgumentException>(() => HardwareEvidenceResolver.CreateManifest(valid[..^1]));
        Assert.Throws<ArgumentException>(() => HardwareEvidenceResolver.CreateManifest(reordered));
        Assert.AreEqual(19, HardwareEvidenceResolver.CreateManifest(valid).Entries.Count);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void Resolve_DiagnosticsAreDeterministicAcrossProviderDiagnosticOrder()
    {
        LlmFitDiagnosticCode[] first =
        [LlmFitDiagnosticCode.GpuShapeMissing, LlmFitDiagnosticCode.JsonInvalid];
        LlmFitDiagnosticCode[] second = first.Reverse().ToArray();

        HardwareResolutionDiagnosticCode[] firstResult = Resolver().Resolve(
            Guid.NewGuid(),
            Evidence(llmFit: InvalidLlmFit(first))).Diagnostics.ToArray();
        HardwareResolutionDiagnosticCode[] secondResult = Resolver().Resolve(
            Guid.NewGuid(),
            Evidence(llmFit: InvalidLlmFit(second))).Diagnostics.ToArray();

        CollectionAssert.AreEqual(firstResult, secondResult);
    }

    private static HardwareEvidenceResolver Resolver() =>
        new(new HardwareResolutionTestData.FixedTimeProvider(HardwareResolutionTestData.Now));

    private static CollectedHardwareEvidence Evidence(
        LlmFitHardwareEvidence? llmFit = null,
        WindowsProcessorEvidence? windowsProcessor = null,
        WindowsSystemEvidenceObservation? windowsSystem = null,
        WindowsStorageEvidence? storage = null,
        DxgiGraphicsEvidence? graphics = null,
        NeuralProcessorEvidence? neuralProcessor = null,
        LlamaCppCapabilityEvidence? llamaCpp = null) =>
        new(
            llmFit ?? HardwareResolutionTestData.LlmFit(),
            windowsProcessor ?? HardwareResolutionTestData.WindowsProcessor(),
            windowsSystem ?? WindowsSystemEvidenceObservation.Available(
                HardwareResolutionTestData.WindowsSystem()),
            storage ?? HardwareResolutionTestData.Storage(),
            graphics ?? HardwareResolutionTestData.Dxgi(),
            neuralProcessor ?? HardwareResolutionTestData.NeuralProcessor(),
            llamaCpp ?? HardwareResolutionTestData.LlamaCpp());

    private static LlmFitHardwareEvidence InvalidLlmFit(
        IEnumerable<LlmFitDiagnosticCode> diagnostics) =>
        LlmFitHardwareEvidence.Invalid(
            LlmFitCommandContract.ToolId,
            LlmFitCommandContract.Version,
            HardwareResolutionTestData.Now,
            cpuName: null,
            cpuLogicalProcessorCount: null,
            totalRamGiB: null,
            availableRamGiB: null,
            LlmFitGpuDetectionState.Invalid,
            [],
            new string('0', 64),
            diagnostics);

    private static void AssertDefaultProvenance(IReadOnlyList<HardwareEvidenceEntry> entries)
    {
        EvidenceSourceKind[] sources =
        [
            EvidenceSourceKind.LlmFit,
            EvidenceSourceKind.Windows,
            EvidenceSourceKind.Windows,
            EvidenceSourceKind.LlmFit,
            EvidenceSourceKind.Windows,
            EvidenceSourceKind.Windows,
            EvidenceSourceKind.Windows,
            EvidenceSourceKind.Windows,
            EvidenceSourceKind.Dxgi,
            EvidenceSourceKind.Dxgi,
            EvidenceSourceKind.NeuralProcessorProbe,
            EvidenceSourceKind.Windows,
            EvidenceSourceKind.Windows,
            EvidenceSourceKind.Windows,
            EvidenceSourceKind.Windows,
            EvidenceSourceKind.Windows,
            EvidenceSourceKind.LlamaCpp,
            EvidenceSourceKind.LlamaCpp,
            EvidenceSourceKind.LlamaCpp,
        ];
        for (int index = 0; index < entries.Count; index++)
        {
            Assert.AreEqual(sources[index], entries[index].Source, ExpectedFields[index]);
            Assert.AreEqual(HardwareResolutionTestData.Now, entries[index].CapturedAtUtc,
                ExpectedFields[index]);
        }

        Assert.AreEqual(EvidenceResolutionState.Unavailable, entries[4].Resolution);
        Assert.AreEqual("instruction-sets.unavailable", entries[4].SafeDiagnosticCode);
        Assert.AreEqual(EvidenceConfidence.Low, entries[4].Confidence);
        Assert.AreEqual(EvidenceResolutionState.Unavailable, entries[10].Resolution);
        Assert.AreEqual("neural-processor.unavailable", entries[10].SafeDiagnosticCode);
        Assert.AreEqual(EvidenceConfidence.Low, entries[10].Confidence);
        Assert.IsTrue(entries.Where((_, index) => index is not 4 and not 10)
            .All(static entry => entry.IsResolved && entry.SafeDiagnosticCode is null));
    }
}
