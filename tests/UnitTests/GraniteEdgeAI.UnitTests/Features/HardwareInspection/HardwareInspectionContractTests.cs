using GraniteEdgeAI.Features.HardwareInspection.Application;
using GraniteEdgeAI.Features.HardwareInspection.Domain;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Reflection;

namespace GraniteEdgeAI.UnitTests.Features.HardwareInspection;

[TestClass]
public sealed class HardwareInspectionContractTests
{
    private static readonly DateTimeOffset CapturedAtUtc =
        new(2026, 8, 20, 0, 0, 0, TimeSpan.Zero);

    [TestMethod]
    [TestCategory("Unit")]
    public void PublicEnums_ExposeOnlyApprovedSemanticValues()
    {
        CollectionAssert.AreEqual(
            new[] { "Completed", "CompletedWithWarnings", "Failed", "Cancelled" },
            Enum.GetNames<HardwareInspectionOutcome>());
        CollectionAssert.AreEqual(
            new[] { "Present", "NotPresent", "DetectionUnavailable" },
            Enum.GetNames<NpuDetectionState>());
        CollectionAssert.AreEqual(
            new[] { "Usable", "NotUsable" },
            Enum.GetNames<HardwareSnapshotUsability>());
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void Snapshot_PreservesDistinctMemoryAndGraphicsSemantics()
    {
        HardwareSnapshot snapshot = CreateUsableSnapshot();

        Assert.AreEqual(32UL * 1024 * 1024 * 1024, snapshot.Memory.PhysicallyInstalledBytes);
        Assert.AreEqual(31UL * 1024 * 1024 * 1024, snapshot.Memory.OsUsablePhysicalBytes);
        Assert.AreEqual(20UL * 1024 * 1024 * 1024, snapshot.Memory.AvailablePhysicalBytes);
        Assert.AreEqual(CapturedAtUtc, snapshot.Memory.AvailableCapturedAtUtc);

        GraphicsAdapterFacts graphics = snapshot.GraphicsAdapters.Single();
        Assert.AreEqual(8UL * 1024 * 1024 * 1024, graphics.DedicatedVideoMemoryBytes);
        Assert.AreEqual(0UL, graphics.DedicatedSystemMemoryBytes);
        Assert.AreEqual(16UL * 1024 * 1024 * 1024, graphics.SharedSystemMemoryBytes);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void Snapshot_CopiesProviderOwnedCollections()
    {
        List<string> instructionSets = ["avx2"];
        List<GraphicsAdapterFacts> graphics =
        [
            new("Intel Arc Graphics", 8, 0, 16),
        ];
        List<HardwareEvidenceEntry> evidence = CreateRequiredEvidence().ToList();

        HardwareSnapshot snapshot = CreateSnapshot(instructionSets, graphics, evidence);
        instructionSets.Add("mutated");
        graphics.Clear();
        evidence.Clear();

        CollectionAssert.AreEqual(new[] { "avx2" }, snapshot.Processor.InstructionSets.ToArray());
        Assert.AreEqual(1, snapshot.GraphicsAdapters.Count);
        Assert.AreEqual(4, snapshot.Evidence.Entries.Count);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void Snapshot_RejectsFalseUsableAndInvalidDynamicMemory()
    {
        List<HardwareEvidenceEntry> incomplete = CreateRequiredEvidence().Take(3).ToList();

        Assert.Throws<ArgumentException>(() =>
            CreateSnapshot(["avx2"], [], incomplete));

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new MemoryFacts(
                physicallyInstalledBytes: 16,
                osUsablePhysicalBytes: 15,
                availablePhysicalBytes: 16,
                availableCapturedAtUtc: CapturedAtUtc));

        Assert.Throws<ArgumentException>(() =>
            new AvailableMemorySnapshot(1, DateTimeOffset.Now));
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void EvidenceManifest_RejectsDuplicatesAndUnsafeDiagnosticText()
    {
        HardwareEvidenceEntry first = CreateRequiredEvidence()[0];

        Assert.Throws<ArgumentException>(() =>
            new HardwareEvidenceManifest([first, first]));

        Assert.Throws<ArgumentException>(() =>
            new HardwareEvidenceEntry(
                "processor.name",
                EvidenceSourceKind.Windows,
                EvidenceResolutionState.ResolvedCorroborated,
                CapturedAtUtc,
                EvidenceConfidence.High,
                @"C:\Users\private\raw.log"));
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void Handoff_AllowsOnlyActionableOutcomesAndUsableSnapshot()
    {
        Guid inspectionId = Guid.NewGuid();
        HardwareSnapshot usable = CreateUsableSnapshot();

        HardwareInspectionHandoff completed = HardwareInspectionHandoff.Create(
            inspectionId,
            HardwareInspectionOutcome.Completed,
            usable);
        HardwareInspectionHandoff warning = HardwareInspectionHandoff.Create(
            inspectionId,
            HardwareInspectionOutcome.CompletedWithWarnings,
            usable);

        Assert.AreEqual(inspectionId, completed.InspectionId);
        Assert.AreSame(usable, completed.Snapshot);
        Assert.AreSame(usable, warning.Snapshot);
        Assert.Throws<ArgumentException>(() => HardwareInspectionHandoff.Create(
            inspectionId,
            HardwareInspectionOutcome.Failed,
            usable));
        Assert.Throws<ArgumentException>(() => HardwareInspectionHandoff.Create(
            inspectionId,
            HardwareInspectionOutcome.Cancelled,
            usable));
        Assert.Throws<ArgumentException>(() => HardwareInspectionHandoff.Create(
            Guid.Empty,
            HardwareInspectionOutcome.Completed,
            usable));
        Assert.Throws<ArgumentException>(() => HardwareInspectionHandoff.Create(
            inspectionId,
            HardwareInspectionOutcome.Completed,
            CreateSnapshot(
                ["avx2"],
                [],
                CreateRequiredEvidence(),
                HardwareSnapshotUsability.NotUsable)));
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void Handoff_HasExactlyTwoPublicPropertiesAndNoModelOrPrivacySurface()
    {
        PropertyInfo[] properties = typeof(HardwareInspectionHandoff)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance);

        CollectionAssert.AreEquivalent(
            new[] { "InspectionId", "Snapshot" },
            properties.Select(property => property.Name).ToArray());

        string[] bannedFragments =
        [
            "Path", "FileName", "Host", "User", "Model", "Compatibility",
            "Raw", "Command", "Output", "Credential",
        ];
        Type[] publicTypes = typeof(HardwareSnapshot).Assembly.GetExportedTypes()
            .Where(type => type.Namespace?.StartsWith(
                "GraniteEdgeAI.Features.HardwareInspection",
                StringComparison.Ordinal) == true)
            .ToArray();

        foreach (Type type in publicTypes)
        {
            foreach (MemberInfo member in type.GetMembers(BindingFlags.Public | BindingFlags.Instance))
            {
                Assert.IsFalse(
                    bannedFragments.Any(fragment => member.Name.Contains(fragment, StringComparison.OrdinalIgnoreCase)),
                    $"Public member {type.FullName}.{member.Name} exposes banned contract vocabulary.");
            }
        }
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void AvailableMemoryProvider_UsesFreshTimestampedValueContract()
    {
        MethodInfo method = typeof(IAvailableMemoryProvider).GetMethod("CaptureAsync")!;
        Assert.AreEqual(typeof(ValueTask<AvailableMemorySnapshot>), method.ReturnType);
        CollectionAssert.AreEqual(
            new[] { typeof(CancellationToken) },
            method.GetParameters().Select(parameter => parameter.ParameterType).ToArray());

        AvailableMemorySnapshot value = new(1234, CapturedAtUtc);
        Assert.AreEqual(1234UL, value.AvailablePhysicalBytes);
        Assert.AreEqual(CapturedAtUtc, value.CapturedAtUtc);
    }

    private static HardwareSnapshot CreateUsableSnapshot() =>
        CreateSnapshot(["avx2"],
            [new GraphicsAdapterFacts("Intel Arc Graphics", 8UL * 1024 * 1024 * 1024, 0, 16UL * 1024 * 1024 * 1024)],
            CreateRequiredEvidence());

    private static HardwareSnapshot CreateSnapshot(
        IEnumerable<string> instructionSets,
        IEnumerable<GraphicsAdapterFacts> graphics,
        IEnumerable<HardwareEvidenceEntry> evidence,
        HardwareSnapshotUsability usability = HardwareSnapshotUsability.Usable) =>
        new(
            snapshotId: Guid.NewGuid(),
            capturedAtUtc: CapturedAtUtc,
            schemaVersion: 1,
            policyVersion: "hardware-policy-v1",
            processor: new ProcessorFacts("Intel Core Ultra 7 155H", "x64", 16, 22, instructionSets),
            memory: new MemoryFacts(
                32UL * 1024 * 1024 * 1024,
                31UL * 1024 * 1024 * 1024,
                20UL * 1024 * 1024 * 1024,
                CapturedAtUtc),
            graphicsAdapters: graphics,
            neuralProcessor: new NeuralProcessorFacts(NpuDetectionState.Present, "Intel AI Boost"),
            storage: new StorageFacts(1_000_000_000_000, 500_000_000_000),
            operatingSystem: new OperatingSystemFacts("Windows 11", "10.0.26100", "x64"),
            localRuntime: new LocalRuntimeCapabilities(
                "llama.cpp-b123",
                [LocalRuntimeBackend.Cpu, LocalRuntimeBackend.Sycl],
                ["Intel Arc Graphics"]),
            evidence: new HardwareEvidenceManifest(evidence),
            usability: usability);

    private static HardwareEvidenceEntry[] CreateRequiredEvidence() =>
    [
        Evidence("processor.name", EvidenceSourceKind.LlmFit),
        Evidence("memory.installedBytes", EvidenceSourceKind.LlmFit),
        Evidence("memory.osUsableBytes", EvidenceSourceKind.Windows),
        Evidence("memory.availableBytes", EvidenceSourceKind.Windows),
    ];

    private static HardwareEvidenceEntry Evidence(string field, EvidenceSourceKind source) =>
        new(
            field,
            source,
            EvidenceResolutionState.ResolvedCorroborated,
            CapturedAtUtc,
            EvidenceConfidence.High,
            null);
}
