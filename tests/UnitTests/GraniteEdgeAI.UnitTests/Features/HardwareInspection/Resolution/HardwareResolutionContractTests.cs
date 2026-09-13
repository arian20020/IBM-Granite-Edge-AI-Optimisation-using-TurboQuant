using GraniteEdgeAI.Features.HardwareInspection.Domain;
using GraniteEdgeAI.Features.HardwareInspection.Resolution;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.UnitTests.Features.HardwareInspection.Resolution;

[TestClass]
public sealed class HardwareResolutionContractTests
{
    [TestMethod]
    [TestCategory("Unit")]
    public void WindowsSystemObservation_PreservesExactlyOneValidState()
    {
        var snapshot = HardwareResolutionTestData.WindowsSystem();

        WindowsSystemEvidenceObservation available =
            WindowsSystemEvidenceObservation.Available(snapshot);
        WindowsSystemEvidenceObservation unavailable =
            WindowsSystemEvidenceObservation.Unavailable(
                HardwareResolutionTestData.Now,
                WindowsSystemObservationDiagnosticCode.MemoryUnavailable);

        Assert.AreEqual(WindowsSystemObservationState.Available, available.State);
        Assert.AreSame(snapshot, available.Snapshot);
        Assert.AreEqual(snapshot.CapturedAtUtc, available.AttemptedAtUtc);
        Assert.IsNull(available.Diagnostic);
        Assert.AreEqual(WindowsSystemObservationState.Unavailable, unavailable.State);
        Assert.IsNull(unavailable.Snapshot);
        Assert.AreEqual(HardwareResolutionTestData.Now, unavailable.AttemptedAtUtc);
        Assert.AreEqual(
            WindowsSystemObservationDiagnosticCode.MemoryUnavailable,
            unavailable.Diagnostic);
        Assert.Throws<ArgumentException>(() =>
            WindowsSystemEvidenceObservation.Unavailable(
                HardwareResolutionTestData.Now.ToOffset(TimeSpan.FromHours(1)),
                WindowsSystemObservationDiagnosticCode.MemoryUnavailable));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            WindowsSystemEvidenceObservation.Unavailable(
                HardwareResolutionTestData.Now,
                (WindowsSystemObservationDiagnosticCode)int.MaxValue));
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void CollectedEvidence_RejectsEveryMissingSource()
    {
        var llmFit = HardwareResolutionTestData.LlmFit();
        var processor = HardwareResolutionTestData.WindowsProcessor();
        var system = WindowsSystemEvidenceObservation.Available(
            HardwareResolutionTestData.WindowsSystem());
        var storage = HardwareResolutionTestData.Storage();
        var graphics = HardwareResolutionTestData.Dxgi();
        var npu = HardwareResolutionTestData.NeuralProcessor();
        var runtime = HardwareResolutionTestData.LlamaCpp();

        Assert.Throws<ArgumentNullException>(() =>
            new CollectedHardwareEvidence(null!, processor, system, storage, graphics, npu, runtime));
        Assert.Throws<ArgumentNullException>(() =>
            new CollectedHardwareEvidence(llmFit, null!, system, storage, graphics, npu, runtime));
        Assert.Throws<ArgumentNullException>(() =>
            new CollectedHardwareEvidence(llmFit, processor, null!, storage, graphics, npu, runtime));
        Assert.Throws<ArgumentNullException>(() =>
            new CollectedHardwareEvidence(llmFit, processor, system, null!, graphics, npu, runtime));
        Assert.Throws<ArgumentNullException>(() =>
            new CollectedHardwareEvidence(llmFit, processor, system, storage, null!, npu, runtime));
        Assert.Throws<ArgumentNullException>(() =>
            new CollectedHardwareEvidence(llmFit, processor, system, storage, graphics, null!, runtime));
        Assert.Throws<ArgumentNullException>(() =>
            new CollectedHardwareEvidence(llmFit, processor, system, storage, graphics, npu, null!));

        CollectedHardwareEvidence complete =
            new(llmFit, processor, system, storage, graphics, npu, runtime);
        Assert.AreSame(llmFit, complete.LlmFit);
        Assert.AreSame(processor, complete.WindowsProcessor);
        Assert.AreSame(system, complete.WindowsSystem);
        Assert.AreSame(storage, complete.Storage);
        Assert.AreSame(graphics, complete.Graphics);
        Assert.AreSame(npu, complete.NeuralProcessor);
        Assert.AreSame(runtime, complete.LlamaCpp);
        Assert.AreEqual(WindowsSystemObservationState.Available, complete.WindowsSystem.State);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void Failure_CopiesSortsAndDeduplicatesClosedDiagnostics()
    {
        List<HardwareResolutionDiagnosticCode> supplied =
        [
            HardwareResolutionDiagnosticCode.StorageUnavailable,
            HardwareResolutionDiagnosticCode.ClockFuture,
            HardwareResolutionDiagnosticCode.StorageUnavailable,
        ];

        HardwareEvidenceResolutionResult result =
            HardwareEvidenceResolutionResult.Failure(
                new HardwareEvidenceManifest([]),
                supplied);
        supplied.Clear();

        CollectionAssert.AreEqual(
            new[]
            {
                HardwareResolutionDiagnosticCode.ClockFuture,
                HardwareResolutionDiagnosticCode.StorageUnavailable,
            },
            result.Diagnostics.ToArray());
        Assert.IsNull(result.Snapshot);
        Assert.IsFalse(result.IsResolved);
        Assert.Throws<ArgumentException>(() =>
            HardwareEvidenceResolutionResult.Failure(
                new HardwareEvidenceManifest([]),
                Enumerable.Repeat(
                    HardwareResolutionDiagnosticCode.ClockFuture,
                    Enum.GetValues<HardwareResolutionDiagnosticCode>().Length + 1)));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            HardwareEvidenceResolutionResult.Failure(
                new HardwareEvidenceManifest([]),
                [(HardwareResolutionDiagnosticCode)int.MaxValue]));
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void Success_RequiresUsableSnapshotAndRetainsNoCallerDiagnostics()
    {
        HardwareSnapshot usable =
            HardwareInspectionContractTests.CreateUsableSnapshotForPresentation();
        var diagnostics = new List<HardwareResolutionDiagnosticCode>();

        HardwareEvidenceResolutionResult result =
            HardwareEvidenceResolutionResult.Success(
                usable,
                usable.Evidence,
                diagnostics);
        diagnostics.Add(HardwareResolutionDiagnosticCode.ClockFuture);

        Assert.IsTrue(result.IsResolved);
        Assert.AreSame(usable, result.Snapshot);
        Assert.AreSame(usable.Evidence, result.Evidence);
        Assert.AreEqual(0, result.Diagnostics.Count);

        HardwareSnapshot notUsable = new(
            usable.SnapshotId,
            usable.CapturedAtUtc,
            usable.SchemaVersion,
            usable.PolicyVersion,
            usable.Processor,
            usable.Memory,
            usable.GraphicsAdapters,
            usable.NeuralProcessor,
            usable.Storage,
            usable.OperatingSystem,
            usable.LocalRuntime,
            usable.Evidence,
            HardwareSnapshotUsability.NotUsable);
        Assert.Throws<ArgumentException>(() =>
            HardwareEvidenceResolutionResult.Success(
                notUsable,
                notUsable.Evidence,
                []));
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void DiagnosticTokens_AreClosedBoundedAndPrivacySafe()
    {
        foreach (HardwareResolutionDiagnosticCode diagnostic in
                 Enum.GetValues<HardwareResolutionDiagnosticCode>())
        {
            string token = HardwareResolutionDiagnosticTokens.Get(diagnostic);
            Assert.IsTrue(token.Length is >= 1 and <= 96);
            Assert.IsTrue(token.All(character =>
                character is >= 'a' and <= 'z'
                || character is >= '0' and <= '9'
                || character is '.' or '-'));
        }

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            HardwareResolutionDiagnosticTokens.Get(
                (HardwareResolutionDiagnosticCode)int.MaxValue));
    }
}
