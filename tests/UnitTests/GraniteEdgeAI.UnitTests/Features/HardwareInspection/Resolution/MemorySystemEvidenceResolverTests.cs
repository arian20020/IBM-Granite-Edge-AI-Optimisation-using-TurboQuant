using GraniteEdgeAI.Features.HardwareInspection.Domain;
using GraniteEdgeAI.Features.HardwareInspection.Resolution;
using GraniteEdgeAI.HardwareInspection.Foundation.LlmFit;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.UnitTests.Features.HardwareInspection.Resolution;

[TestClass]
public sealed class MemorySystemEvidenceResolverTests
{
    private static readonly string[] ExpectedFields =
    [
        "memory.installedBytes",
        "memory.osUsableBytes",
        "memory.availableBytes",
        "os.name",
        "os.version",
        "os.architecture",
    ];

    [TestMethod]
    [TestCategory("Unit")]
    public void Resolve_ValidEvidencePreservesCanonicalWindowsMemoryAndOperatingSystem()
    {
        MemorySystemResolution result = Resolve(
            HardwareResolutionTestData.LlmFit(),
            HardwareResolutionTestData.WindowsSystem());

        Assert.IsFalse(result.HasCriticalFailure);
        Assert.IsNotNull(result.Memory);
        Assert.IsNotNull(result.OperatingSystem);
        Assert.AreEqual(32 * HardwareResolutionTestData.GiB,
            result.Memory.PhysicallyInstalledBytes);
        Assert.AreEqual(31 * HardwareResolutionTestData.GiB,
            result.Memory.OsUsablePhysicalBytes);
        Assert.AreEqual(20 * HardwareResolutionTestData.GiB,
            result.Memory.AvailablePhysicalBytes);
        Assert.AreEqual(HardwareResolutionTestData.Now,
            result.Memory.AvailableCapturedAtUtc);
        Assert.AreEqual("Windows 11", result.OperatingSystem.Name);
        Assert.AreEqual("10.0.26100", result.OperatingSystem.Version);
        Assert.AreEqual("x64", result.OperatingSystem.Architecture);
        AssertFields(result);
        AssertStates(
            result,
            EvidenceResolutionState.ResolvedPrimary,
            EvidenceResolutionState.ResolvedCorroborated,
            EvidenceResolutionState.ResolvedCorroborated,
            EvidenceResolutionState.ResolvedPrimary,
            EvidenceResolutionState.ResolvedPrimary,
            EvidenceResolutionState.ResolvedPrimary);
        Assert.AreEqual(0, result.Diagnostics.Count);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void Resolve_UnavailableLlmFitUsesTruthfulWindowsCanonicalFacts()
    {
        LlmFitHardwareEvidence unavailable = LlmFitHardwareEvidence.Unavailable(
            LlmFitCommandContract.ToolId,
            LlmFitCommandContract.Version,
            HardwareResolutionTestData.Now,
            LlmFitDiagnosticCode.SystemStartFailed);

        MemorySystemResolution result = Resolve(
            unavailable,
            HardwareResolutionTestData.WindowsSystem());

        Assert.IsFalse(result.HasCriticalFailure);
        Assert.IsNotNull(result.Memory);
        Assert.IsNotNull(result.OperatingSystem);
        AssertStates(
            result,
            EvidenceResolutionState.ResolvedPrimary,
            EvidenceResolutionState.ResolvedPrimary,
            EvidenceResolutionState.ResolvedPrimary,
            EvidenceResolutionState.ResolvedPrimary,
            EvidenceResolutionState.ResolvedPrimary,
            EvidenceResolutionState.ResolvedPrimary);
        CollectionAssert.AreEqual(
            new[] { HardwareResolutionDiagnosticCode.LlmFitUnavailable },
            result.Diagnostics.ToArray());
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void Resolve_TotalMemoryAcceptsExactOneGiBToleranceAndRejectsOneByteBeyond()
    {
        double acceptedTotal = BytesToGiB(32 * HardwareResolutionTestData.GiB);
        double rejectedTotal = BytesToGiB(32 * HardwareResolutionTestData.GiB + 1);

        MemorySystemResolution accepted = Resolve(
            AvailableLlmFit(acceptedTotal, 20),
            HardwareResolutionTestData.WindowsSystem());
        MemorySystemResolution rejected = Resolve(
            AvailableLlmFit(rejectedTotal, 20),
            HardwareResolutionTestData.WindowsSystem());

        Assert.IsFalse(accepted.HasCriticalFailure);
        AssertCriticalFailure(
            rejected,
            HardwareResolutionDiagnosticCode.TotalMemoryConflict,
            entryIndex: 1,
            "total-memory.conflict");
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void Resolve_AvailableMemoryUsesExactTenPercentToleranceBoundary()
    {
        ulong tolerance = HardwareEvidenceNormalizer.GetAvailableMemoryTolerance(
            31 * HardwareResolutionTestData.GiB);
        ulong acceptedBytes = 20 * HardwareResolutionTestData.GiB + tolerance;
        ulong rejectedBytes = acceptedBytes + 1;

        MemorySystemResolution accepted = Resolve(
            AvailableLlmFit(31, BytesToGiB(acceptedBytes)),
            HardwareResolutionTestData.WindowsSystem());
        MemorySystemResolution rejected = Resolve(
            AvailableLlmFit(31, BytesToGiB(rejectedBytes)),
            HardwareResolutionTestData.WindowsSystem());

        Assert.IsFalse(accepted.HasCriticalFailure);
        AssertCriticalFailure(
            rejected,
            HardwareResolutionDiagnosticCode.AvailableMemoryConflict,
            entryIndex: 2,
            "available-memory.conflict");
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void Resolve_GiBMidpointsUseMidpointToEvenBeforeToleranceComparison()
    {
        double halfByte = 0.5 / HardwareResolutionTestData.GiB;
        double acceptedTotal = 32 + halfByte;
        double rejectedTotal = 32 + (3 * halfByte);

        Assert.IsFalse(Resolve(
            AvailableLlmFit(acceptedTotal, 20),
            HardwareResolutionTestData.WindowsSystem()).HasCriticalFailure);
        AssertCriticalFailure(
            Resolve(
                AvailableLlmFit(rejectedTotal, 20),
                HardwareResolutionTestData.WindowsSystem()),
            HardwareResolutionDiagnosticCode.TotalMemoryConflict,
            entryIndex: 1,
            "total-memory.conflict");
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void Resolve_UnavailableWindowsObservationIsCriticalAndProducesNoFacts()
    {
        WindowsSystemEvidenceObservation windows =
            WindowsSystemEvidenceObservation.Unavailable(
                HardwareResolutionTestData.Now,
                WindowsSystemObservationDiagnosticCode.MemoryUnavailable);

        MemorySystemResolution result = MemorySystemEvidenceResolver.Resolve(
            HardwareResolutionTestData.LlmFit(),
            windows,
            HardwareResolutionTestData.Now);

        Assert.IsTrue(result.HasCriticalFailure);
        Assert.IsNull(result.Memory);
        Assert.IsNull(result.OperatingSystem);
        AssertFields(result);
        Assert.IsTrue(result.Entries.All(static entry =>
            entry.Resolution == EvidenceResolutionState.Unavailable));
        CollectionAssert.Contains(
            result.Diagnostics.ToArray(),
            HardwareResolutionDiagnosticCode.WindowsSystemUnavailable);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void Resolve_WindowsDynamicClockBoundariesAreEnforced()
    {
        Assert.IsFalse(Resolve(
            HardwareResolutionTestData.LlmFit(),
            HardwareResolutionTestData.WindowsSystem(
                HardwareResolutionTestData.Now - HardwareResolutionPolicy.DynamicMemoryMaximumAge))
            .HasCriticalFailure);
        Assert.IsFalse(Resolve(
            HardwareResolutionTestData.LlmFit(),
            HardwareResolutionTestData.WindowsSystem(
                HardwareResolutionTestData.Now + HardwareResolutionPolicy.FutureClockSkew))
            .HasCriticalFailure);

        AssertCriticalClockFailure(
            HardwareResolutionTestData.Now - HardwareResolutionPolicy.DynamicMemoryMaximumAge -
                TimeSpan.FromTicks(1),
            HardwareResolutionDiagnosticCode.EvidenceStale);
        AssertCriticalClockFailure(
            HardwareResolutionTestData.Now + HardwareResolutionPolicy.FutureClockSkew +
                TimeSpan.FromTicks(1),
            HardwareResolutionDiagnosticCode.ClockFuture);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void Resolve_UnusableLlmFitClockFallsBackWithoutDiscardingWindowsFacts()
    {
        (DateTimeOffset CapturedAt, HardwareResolutionDiagnosticCode Diagnostic)[] cases =
        [
            (HardwareResolutionTestData.Now - HardwareResolutionPolicy.StaticEvidenceMaximumAge -
                TimeSpan.FromTicks(1), HardwareResolutionDiagnosticCode.EvidenceStale),
            (HardwareResolutionTestData.Now + HardwareResolutionPolicy.FutureClockSkew +
                TimeSpan.FromTicks(1), HardwareResolutionDiagnosticCode.ClockFuture),
        ];

        foreach ((DateTimeOffset capturedAt, HardwareResolutionDiagnosticCode diagnostic) in cases)
        {
            MemorySystemResolution result = Resolve(
                AvailableLlmFit(31, 20, capturedAt),
                HardwareResolutionTestData.WindowsSystem());

            Assert.IsFalse(result.HasCriticalFailure);
            Assert.IsNotNull(result.Memory);
            AssertStates(
                result,
                EvidenceResolutionState.ResolvedPrimary,
                EvidenceResolutionState.ResolvedPrimary,
                EvidenceResolutionState.ResolvedPrimary,
                EvidenceResolutionState.ResolvedPrimary,
                EvidenceResolutionState.ResolvedPrimary,
                EvidenceResolutionState.ResolvedPrimary);
            CollectionAssert.Contains(result.Diagnostics.ToArray(), diagnostic);
        }
    }

    private static MemorySystemResolution Resolve(
        LlmFitHardwareEvidence llmFit,
        GraniteEdgeAI.HardwareInspection.Foundation.Windows.WindowsSystemSnapshot windows) =>
        MemorySystemEvidenceResolver.Resolve(
            llmFit,
            WindowsSystemEvidenceObservation.Available(windows),
            HardwareResolutionTestData.Now);

    private static LlmFitHardwareEvidence AvailableLlmFit(
        double totalGiB,
        double availableGiB,
        DateTimeOffset? capturedAtUtc = null) =>
        LlmFitHardwareEvidence.Available(
            LlmFitCommandContract.ToolId,
            LlmFitCommandContract.Version,
            capturedAtUtc ?? HardwareResolutionTestData.Now,
            "Intel Core Ultra 7 155H",
            22,
            totalGiB,
            availableGiB,
            LlmFitGpuDetectionState.NotReported,
            [],
            new string('0', 64));

    private static double BytesToGiB(ulong bytes) =>
        bytes / (double)HardwareResolutionTestData.GiB;

    private static void AssertFields(MemorySystemResolution result) =>
        CollectionAssert.AreEqual(
            ExpectedFields,
            result.Entries.Select(static entry => entry.CanonicalField).ToArray());

    private static void AssertStates(
        MemorySystemResolution result,
        params EvidenceResolutionState[] expected) =>
        CollectionAssert.AreEqual(
            expected,
            result.Entries.Select(static entry => entry.Resolution).ToArray());

    private static void AssertCriticalFailure(
        MemorySystemResolution result,
        HardwareResolutionDiagnosticCode diagnostic,
        int entryIndex,
        string token)
    {
        Assert.IsTrue(result.HasCriticalFailure);
        Assert.IsNull(result.Memory);
        Assert.IsNull(result.OperatingSystem);
        Assert.AreEqual(EvidenceResolutionState.Conflict, result.Entries[entryIndex].Resolution);
        Assert.AreEqual(token, result.Entries[entryIndex].SafeDiagnosticCode);
        CollectionAssert.Contains(result.Diagnostics.ToArray(), diagnostic);
    }

    private static void AssertCriticalClockFailure(
        DateTimeOffset capturedAt,
        HardwareResolutionDiagnosticCode diagnostic)
    {
        MemorySystemResolution result = Resolve(
            HardwareResolutionTestData.LlmFit(),
            HardwareResolutionTestData.WindowsSystem(capturedAt));
        Assert.IsTrue(result.HasCriticalFailure);
        Assert.IsNull(result.Memory);
        Assert.IsNull(result.OperatingSystem);
        CollectionAssert.Contains(result.Diagnostics.ToArray(), diagnostic);
    }
}
