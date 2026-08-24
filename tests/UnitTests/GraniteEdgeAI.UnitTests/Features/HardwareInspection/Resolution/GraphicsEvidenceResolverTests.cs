using GraniteEdgeAI.Features.HardwareInspection.Domain;
using GraniteEdgeAI.Features.HardwareInspection.Resolution;
using GraniteEdgeAI.HardwareInspection.Foundation.Dxgi;
using GraniteEdgeAI.HardwareInspection.Foundation.LlmFit;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.UnitTests.Features.HardwareInspection.Resolution;

[TestClass]
public sealed class GraphicsEvidenceResolverTests
{
    [TestMethod]
    [TestCategory("Unit")]
    public void Resolve_DxgiIsCanonicalAndPreservesOrdinalAndSeparateMemoryCategories()
    {
        DxgiGraphicsEvidence dxgi = DxgiGraphicsEvidence.Available(
            [
                Adapter("Remote Adapter", DxgiAdapterKind.Remote, 300, 30, 31, 2),
                Adapter("Hardware Adapter", DxgiAdapterKind.Hardware, 100, 10, 11, 0),
                Adapter("Software Adapter", DxgiAdapterKind.Software, 200, 20, 21, 1),
            ],
            HardwareResolutionTestData.Now);

        ComponentResolution<IReadOnlyList<GraphicsAdapterFacts>> result =
            GraphicsEvidenceResolver.Resolve(
                dxgi,
                ReportedLlmFit([new LlmFitReportedGpu("Unrelated GPU", 1)]),
                HardwareResolutionTestData.Now);

        Assert.IsFalse(result.HasCriticalFailure);
        Assert.AreEqual(3, result.Value!.Count);
        CollectionAssert.AreEqual(
            new[] { "Hardware Adapter", "Software Adapter", "Remote Adapter" },
            result.Value.Select(static adapter => adapter.Name).ToArray());
        AssertMemory(result.Value[0], 100, 10, 11);
        AssertMemory(result.Value[1], 200, 20, 21);
        AssertMemory(result.Value[2], 300, 30, 31);
        AssertEntries(result,
            EvidenceSourceKind.Dxgi,
            EvidenceResolutionState.ResolvedPrimary,
            EvidenceResolutionState.ResolvedPrimary);
        Assert.AreEqual(0, result.Diagnostics.Count);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void Resolve_AvailableEmptyDxgiRemainsCanonicalWhenLlmFitReportsGraphics()
    {
        ComponentResolution<IReadOnlyList<GraphicsAdapterFacts>> result =
            GraphicsEvidenceResolver.Resolve(
                DxgiGraphicsEvidence.Available([], HardwareResolutionTestData.Now),
                ReportedLlmFit([new LlmFitReportedGpu("Reported Elsewhere", 3)]),
                HardwareResolutionTestData.Now);

        Assert.AreEqual(0, result.Value!.Count);
        AssertEntries(result,
            EvidenceSourceKind.Dxgi,
            EvidenceResolutionState.ResolvedPrimary,
            EvidenceResolutionState.ResolvedPrimary);
        Assert.AreEqual(0, result.Diagnostics.Count);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void Resolve_UnavailableDxgiUsesExactLlmFitNamesAndCountsWithoutInventedMemory()
    {
        ComponentResolution<IReadOnlyList<GraphicsAdapterFacts>> result =
            GraphicsEvidenceResolver.Resolve(
                UnavailableDxgi(),
                ReportedLlmFit(
                [
                    new LlmFitReportedGpu("Intel Arc Graphics", 2),
                    new LlmFitReportedGpu("Discrete GPU", 1),
                ]),
                HardwareResolutionTestData.Now);

        Assert.IsFalse(result.HasCriticalFailure);
        CollectionAssert.AreEqual(
            new[] { "Intel Arc Graphics", "Intel Arc Graphics", "Discrete GPU" },
            result.Value!.Select(static adapter => adapter.Name).ToArray());
        Assert.IsTrue(result.Value!.All(static adapter =>
            adapter.DedicatedVideoMemoryBytes is null &&
            adapter.DedicatedSystemMemoryBytes is null &&
            adapter.SharedSystemMemoryBytes is null));
        AssertEntries(result,
            EvidenceSourceKind.LlmFit,
            EvidenceResolutionState.ResolvedFallback,
            EvidenceResolutionState.Unavailable);
        CollectionAssert.Contains(
            result.Diagnostics.ToArray(),
            HardwareResolutionDiagnosticCode.DxgiUnavailable);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void Resolve_BothSourcesUnavailableReturnsTruthfulEmptyOptionalResult()
    {
        LlmFitHardwareEvidence llmFit = LlmFitHardwareEvidence.Unavailable(
            LlmFitCommandContract.ToolId,
            LlmFitCommandContract.Version,
            HardwareResolutionTestData.Now,
            LlmFitDiagnosticCode.SystemStartFailed);

        ComponentResolution<IReadOnlyList<GraphicsAdapterFacts>> result =
            GraphicsEvidenceResolver.Resolve(
                UnavailableDxgi(),
                llmFit,
                HardwareResolutionTestData.Now);

        Assert.IsFalse(result.HasCriticalFailure);
        Assert.IsNotNull(result.Value);
        Assert.AreEqual(0, result.Value.Count);
        AssertEntries(result,
            EvidenceSourceKind.Dxgi,
            EvidenceResolutionState.Unavailable,
            EvidenceResolutionState.Unavailable);
        CollectionAssert.AreEqual(
            new[]
            {
                HardwareResolutionDiagnosticCode.LlmFitUnavailable,
                HardwareResolutionDiagnosticCode.DxgiUnavailable,
                HardwareResolutionDiagnosticCode.GraphicsUnresolved,
            },
            result.Diagnostics.ToArray());
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void Resolve_StaleOrFutureDxgiFallsBackToFreshLlmFit()
    {
        foreach (DateTimeOffset capturedAt in RejectedStaticTimes())
        {
            ComponentResolution<IReadOnlyList<GraphicsAdapterFacts>> result =
                GraphicsEvidenceResolver.Resolve(
                    DxgiGraphicsEvidence.Available([Adapter("DXGI", 1, 2, 3, 0)], capturedAt),
                    ReportedLlmFit([new LlmFitReportedGpu("Fallback", 1)]),
                    HardwareResolutionTestData.Now);

            Assert.AreEqual("Fallback", result.Value!.Single().Name);
            Assert.AreEqual(EvidenceResolutionState.ResolvedFallback, result.Entries[0].Resolution);
            CollectionAssert.Contains(
                result.Diagnostics.ToArray(),
                capturedAt < HardwareResolutionTestData.Now
                    ? HardwareResolutionDiagnosticCode.EvidenceStale
                    : HardwareResolutionDiagnosticCode.ClockFuture);
        }
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void Resolve_UnavailableDxgiRejectsStaleOrFutureLlmFitFallback()
    {
        foreach (DateTimeOffset capturedAt in RejectedStaticTimes())
        {
            ComponentResolution<IReadOnlyList<GraphicsAdapterFacts>> result =
                GraphicsEvidenceResolver.Resolve(
                    UnavailableDxgi(),
                    ReportedLlmFit([new LlmFitReportedGpu("Do Not Use", 1)], capturedAt),
                    HardwareResolutionTestData.Now);

            Assert.AreEqual(0, result.Value!.Count);
            Assert.AreEqual(EvidenceResolutionState.Unavailable, result.Entries[0].Resolution);
            CollectionAssert.Contains(
                result.Diagnostics.ToArray(),
                HardwareResolutionDiagnosticCode.GraphicsUnresolved);
        }
    }

    private static DxgiAdapterEvidence Adapter(
        string name,
        ulong dedicatedVideo,
        ulong dedicatedSystem,
        ulong shared,
        int ordinal) =>
        Adapter(name, DxgiAdapterKind.Hardware, dedicatedVideo, dedicatedSystem, shared, ordinal);

    private static DxgiAdapterEvidence Adapter(
        string name,
        DxgiAdapterKind kind,
        ulong dedicatedVideo,
        ulong dedicatedSystem,
        ulong shared,
        int ordinal) =>
        new(
            name,
            kind,
            vendorId: 0x8086,
            deviceId: checked((uint)(100 + ordinal)),
            dedicatedVideo,
            dedicatedSystem,
            shared,
            ordinal);

    private static DxgiGraphicsEvidence UnavailableDxgi() =>
        DxgiGraphicsEvidence.Unavailable(
            DxgiGraphicsDiagnosticCode.NativeApiUnavailable,
            HardwareResolutionTestData.Now);

    private static LlmFitHardwareEvidence ReportedLlmFit(
        IEnumerable<LlmFitReportedGpu> gpus,
        DateTimeOffset? capturedAtUtc = null) =>
        LlmFitHardwareEvidence.Available(
            LlmFitCommandContract.ToolId,
            LlmFitCommandContract.Version,
            capturedAtUtc ?? HardwareResolutionTestData.Now,
            "Intel Core Ultra 7 155H",
            22,
            31,
            20,
            LlmFitGpuDetectionState.Reported,
            gpus,
            new string('0', 64));

    private static DateTimeOffset[] RejectedStaticTimes() =>
    [
        HardwareResolutionTestData.Now - HardwareResolutionPolicy.StaticEvidenceMaximumAge -
            TimeSpan.FromTicks(1),
        HardwareResolutionTestData.Now + HardwareResolutionPolicy.FutureClockSkew +
            TimeSpan.FromTicks(1),
    ];

    private static void AssertMemory(
        GraphicsAdapterFacts adapter,
        ulong dedicatedVideo,
        ulong dedicatedSystem,
        ulong shared)
    {
        Assert.AreEqual(dedicatedVideo, adapter.DedicatedVideoMemoryBytes);
        Assert.AreEqual(dedicatedSystem, adapter.DedicatedSystemMemoryBytes);
        Assert.AreEqual(shared, adapter.SharedSystemMemoryBytes);
    }

    private static void AssertEntries(
        ComponentResolution<IReadOnlyList<GraphicsAdapterFacts>> result,
        EvidenceSourceKind adapterSource,
        EvidenceResolutionState adapterState,
        EvidenceResolutionState memoryState)
    {
        CollectionAssert.AreEqual(
            new[] { "graphics.adapters", "graphics.memory" },
            result.Entries.Select(static entry => entry.CanonicalField).ToArray());
        Assert.AreEqual(adapterSource, result.Entries[0].Source);
        Assert.AreEqual(adapterState, result.Entries[0].Resolution);
        Assert.AreEqual(memoryState, result.Entries[1].Resolution);
    }
}
