using GraniteEdgeAI.Features.HardwareInspection.Domain;
using GraniteEdgeAI.Features.HardwareInspection.Resolution;
using GraniteEdgeAI.HardwareInspection.Foundation.LlmFit;
using GraniteEdgeAI.HardwareInspection.Foundation.Windows;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.UnitTests.Features.HardwareInspection.Resolution;

[TestClass]
public sealed class ProcessorEvidenceResolverTests
{
    private static readonly string[] ExpectedFields =
    [
        "processor.name",
        "processor.architecture",
        "processor.physicalCores",
        "processor.logicalProcessors",
        "processor.instructionSets",
    ];

    [TestMethod]
    [TestCategory("Unit")]
    public void Resolve_AgreementUsesLlmFitValuesAndWindowsTopology()
    {
        ComponentResolution<ProcessorFacts> result = ProcessorEvidenceResolver.Resolve(
            HardwareResolutionTestData.LlmFit(),
            HardwareResolutionTestData.WindowsProcessor(),
            HardwareResolutionTestData.Now);

        Assert.IsFalse(result.HasCriticalFailure);
        Assert.IsNotNull(result.Value);
        Assert.AreEqual("Intel Core Ultra 7 155H", result.Value.Name);
        Assert.AreEqual("x64", result.Value.Architecture);
        Assert.AreEqual(16, result.Value.PhysicalCoreCount);
        Assert.AreEqual(22, result.Value.LogicalProcessorCount);
        Assert.AreEqual(0, result.Value.InstructionSets.Count);
        AssertFields(result);
        AssertEntry(result, 0, EvidenceSourceKind.LlmFit,
            EvidenceResolutionState.ResolvedCorroborated, EvidenceConfidence.High, null);
        AssertEntry(result, 1, EvidenceSourceKind.Windows,
            EvidenceResolutionState.ResolvedPrimary, EvidenceConfidence.High, null);
        AssertEntry(result, 2, EvidenceSourceKind.Windows,
            EvidenceResolutionState.ResolvedPrimary, EvidenceConfidence.High, null);
        AssertEntry(result, 3, EvidenceSourceKind.LlmFit,
            EvidenceResolutionState.ResolvedCorroborated, EvidenceConfidence.High, null);
        AssertEntry(result, 4, EvidenceSourceKind.Windows,
            EvidenceResolutionState.Unavailable, EvidenceConfidence.Low,
            "instruction-sets.unavailable");
        CollectionAssert.AreEqual(
            new[] { HardwareResolutionDiagnosticCode.InstructionSetsUnavailable },
            result.Diagnostics.ToArray());
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void Resolve_CaseOnlyNameDifferenceCorroboratesAndPreservesPrimarySpelling()
    {
        LlmFitHardwareEvidence llmFit = AvailableLlmFit("INTEL CORE ULTRA 7 155H", 22);

        ComponentResolution<ProcessorFacts> result = ProcessorEvidenceResolver.Resolve(
            llmFit,
            HardwareResolutionTestData.WindowsProcessor(),
            HardwareResolutionTestData.Now);

        Assert.IsFalse(result.HasCriticalFailure);
        Assert.AreEqual("INTEL CORE ULTRA 7 155H", result.Value!.Name);
        Assert.AreEqual(EvidenceResolutionState.ResolvedCorroborated, result.Entries[0].Resolution);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void Resolve_MaterialNameMismatchIsCriticalConflict()
    {
        ComponentResolution<ProcessorFacts> result = ProcessorEvidenceResolver.Resolve(
            AvailableLlmFit("Different Processor", 22),
            HardwareResolutionTestData.WindowsProcessor(),
            HardwareResolutionTestData.Now);

        AssertCriticalFailure(result, HardwareResolutionDiagnosticCode.ProcessorNameConflict);
        Assert.AreEqual(EvidenceResolutionState.Conflict, result.Entries[0].Resolution);
        Assert.AreEqual("processor-name.conflict", result.Entries[0].SafeDiagnosticCode);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void Resolve_LogicalProcessorMismatchIsCriticalConflict()
    {
        ComponentResolution<ProcessorFacts> result = ProcessorEvidenceResolver.Resolve(
            AvailableLlmFit("Intel Core Ultra 7 155H", 24),
            HardwareResolutionTestData.WindowsProcessor(),
            HardwareResolutionTestData.Now);

        AssertCriticalFailure(result, HardwareResolutionDiagnosticCode.LogicalProcessorConflict);
        Assert.AreEqual(EvidenceResolutionState.Conflict, result.Entries[3].Resolution);
        Assert.AreEqual("logical-processors.conflict", result.Entries[3].SafeDiagnosticCode);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void Resolve_UnavailableOrInvalidLlmFitUsesWindowsFallbackWithoutPartialFacts()
    {
        LlmFitHardwareEvidence[] unusablePrimary =
        [
            LlmFitHardwareEvidence.Unavailable(
                LlmFitCommandContract.ToolId,
                LlmFitCommandContract.Version,
                HardwareResolutionTestData.Now,
                LlmFitDiagnosticCode.SystemStartFailed),
            LlmFitHardwareEvidence.Invalid(
                LlmFitCommandContract.ToolId,
                LlmFitCommandContract.Version,
                HardwareResolutionTestData.Now,
                "Do Not Use This Partial Name",
                24,
                31,
                20,
                LlmFitGpuDetectionState.Invalid,
                [],
                new string('0', 64),
                [LlmFitDiagnosticCode.JsonInvalid]),
        ];

        foreach (LlmFitHardwareEvidence llmFit in unusablePrimary)
        {
            ComponentResolution<ProcessorFacts> result = ProcessorEvidenceResolver.Resolve(
                llmFit,
                HardwareResolutionTestData.WindowsProcessor(),
                HardwareResolutionTestData.Now);

            Assert.IsFalse(result.HasCriticalFailure);
            Assert.AreEqual("Intel Core Ultra 7 155H", result.Value!.Name);
            Assert.AreEqual(22, result.Value.LogicalProcessorCount);
            Assert.AreEqual(EvidenceResolutionState.ResolvedFallback, result.Entries[0].Resolution);
            Assert.AreEqual(EvidenceResolutionState.ResolvedFallback, result.Entries[3].Resolution);
            CollectionAssert.AreEqual(
                new[]
                {
                    HardwareResolutionDiagnosticCode.LlmFitUnavailable,
                    HardwareResolutionDiagnosticCode.InstructionSetsUnavailable,
                },
                result.Diagnostics.ToArray());
        }
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void Resolve_UnavailableWindowsEvidenceIsCritical()
    {
        WindowsProcessorEvidence windows = WindowsProcessorEvidence.Unavailable(
            WindowsProcessorDiagnosticCode.NativeApiUnavailable,
            HardwareResolutionTestData.Now);

        ComponentResolution<ProcessorFacts> result = ProcessorEvidenceResolver.Resolve(
            HardwareResolutionTestData.LlmFit(),
            windows,
            HardwareResolutionTestData.Now);

        AssertCriticalFailure(result, HardwareResolutionDiagnosticCode.WindowsProcessorUnavailable);
        Assert.AreEqual(EvidenceResolutionState.Unavailable, result.Entries[1].Resolution);
        Assert.AreEqual(EvidenceResolutionState.Unavailable, result.Entries[2].Resolution);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void Resolve_StaleOrFutureLlmFitUsesWindowsFallbackWithClockDiagnostic()
    {
        (DateTimeOffset CapturedAt, HardwareResolutionDiagnosticCode Diagnostic)[] cases =
        [
            (HardwareResolutionTestData.Now - HardwareResolutionPolicy.StaticEvidenceMaximumAge - TimeSpan.FromTicks(1),
                HardwareResolutionDiagnosticCode.EvidenceStale),
            (HardwareResolutionTestData.Now + HardwareResolutionPolicy.FutureClockSkew + TimeSpan.FromTicks(1),
                HardwareResolutionDiagnosticCode.ClockFuture),
        ];

        foreach ((DateTimeOffset capturedAt, HardwareResolutionDiagnosticCode diagnostic) in cases)
        {
            ComponentResolution<ProcessorFacts> result = ProcessorEvidenceResolver.Resolve(
                AvailableLlmFit("Intel Core Ultra 7 155H", 22, capturedAt),
                HardwareResolutionTestData.WindowsProcessor(),
                HardwareResolutionTestData.Now);

            Assert.IsFalse(result.HasCriticalFailure);
            Assert.IsNotNull(result.Value);
            Assert.AreEqual(EvidenceResolutionState.ResolvedFallback, result.Entries[0].Resolution);
            Assert.AreEqual(EvidenceResolutionState.ResolvedFallback, result.Entries[3].Resolution);
            CollectionAssert.Contains(result.Diagnostics.ToArray(), diagnostic);
        }
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void Resolve_StaleOrFutureWindowsEvidenceIsCritical()
    {
        (DateTimeOffset CapturedAt, HardwareResolutionDiagnosticCode Diagnostic)[] cases =
        [
            (HardwareResolutionTestData.Now - HardwareResolutionPolicy.StaticEvidenceMaximumAge - TimeSpan.FromTicks(1),
                HardwareResolutionDiagnosticCode.EvidenceStale),
            (HardwareResolutionTestData.Now + HardwareResolutionPolicy.FutureClockSkew + TimeSpan.FromTicks(1),
                HardwareResolutionDiagnosticCode.ClockFuture),
        ];

        foreach ((DateTimeOffset capturedAt, HardwareResolutionDiagnosticCode diagnostic) in cases)
        {
            ComponentResolution<ProcessorFacts> result = ProcessorEvidenceResolver.Resolve(
                HardwareResolutionTestData.LlmFit(),
                HardwareResolutionTestData.WindowsProcessor(capturedAt),
                HardwareResolutionTestData.Now);

            AssertCriticalFailure(result, diagnostic);
            Assert.AreEqual(EvidenceResolutionState.Unavailable, result.Entries[1].Resolution);
            Assert.AreEqual(EvidenceResolutionState.Unavailable, result.Entries[2].Resolution);
        }
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void Resolve_CrossSourceTopologyContradictionIsCritical()
    {
        WindowsProcessorEvidence windows = WindowsProcessorEvidence.Available(
            "Intel Core Ultra 7 155H",
            WindowsProcessorArchitecture.X64,
            physicalCoreCount: 16,
            logicalProcessorCount: 18,
            HardwareResolutionTestData.Now);

        ComponentResolution<ProcessorFacts> result = ProcessorEvidenceResolver.Resolve(
            AvailableLlmFit("Intel Core Ultra 7 155H", 12),
            windows,
            HardwareResolutionTestData.Now);

        AssertCriticalFailure(result, HardwareResolutionDiagnosticCode.ProcessorTopologyConflict);
        Assert.AreEqual(EvidenceResolutionState.Conflict, result.Entries[2].Resolution);
        Assert.AreEqual("processor-topology.conflict", result.Entries[2].SafeDiagnosticCode);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void Resolve_MapsEverySupportedWindowsArchitectureExactly()
    {
        (WindowsProcessorArchitecture Architecture, string Expected)[] cases =
        [
            (WindowsProcessorArchitecture.X86, "x86"),
            (WindowsProcessorArchitecture.X64, "x64"),
            (WindowsProcessorArchitecture.Arm64, "arm64"),
        ];

        foreach ((WindowsProcessorArchitecture architecture, string expected) in cases)
        {
            WindowsProcessorEvidence windows = WindowsProcessorEvidence.Available(
                "Intel Core Ultra 7 155H",
                architecture,
                16,
                22,
                HardwareResolutionTestData.Now);

            ComponentResolution<ProcessorFacts> result = ProcessorEvidenceResolver.Resolve(
                HardwareResolutionTestData.LlmFit(),
                windows,
                HardwareResolutionTestData.Now);

            Assert.AreEqual(expected, result.Value!.Architecture);
        }
    }

    private static LlmFitHardwareEvidence AvailableLlmFit(
        string name,
        int logicalProcessorCount,
        DateTimeOffset? capturedAtUtc = null) =>
        LlmFitHardwareEvidence.Available(
            LlmFitCommandContract.ToolId,
            LlmFitCommandContract.Version,
            capturedAtUtc ?? HardwareResolutionTestData.Now,
            name,
            logicalProcessorCount,
            31,
            20,
            LlmFitGpuDetectionState.NotReported,
            [],
            new string('0', 64));

    private static void AssertFields(ComponentResolution<ProcessorFacts> result) =>
        CollectionAssert.AreEqual(
            ExpectedFields,
            result.Entries.Select(static entry => entry.CanonicalField).ToArray());

    private static void AssertEntry(
        ComponentResolution<ProcessorFacts> result,
        int index,
        EvidenceSourceKind source,
        EvidenceResolutionState resolution,
        EvidenceConfidence confidence,
        string? diagnostic)
    {
        HardwareEvidenceEntry entry = result.Entries[index];
        Assert.AreEqual(source, entry.Source);
        Assert.AreEqual(resolution, entry.Resolution);
        Assert.AreEqual(confidence, entry.Confidence);
        Assert.AreEqual(diagnostic, entry.SafeDiagnosticCode);
        Assert.AreEqual(HardwareResolutionTestData.Now, entry.CapturedAtUtc);
    }

    private static void AssertCriticalFailure(
        ComponentResolution<ProcessorFacts> result,
        HardwareResolutionDiagnosticCode expectedDiagnostic)
    {
        Assert.IsTrue(result.HasCriticalFailure);
        Assert.IsNull(result.Value);
        CollectionAssert.Contains(result.Diagnostics.ToArray(), expectedDiagnostic);
        AssertFields(result);
    }
}
