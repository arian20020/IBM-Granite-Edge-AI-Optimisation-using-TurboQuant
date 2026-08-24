using GraniteEdgeAI.Features.HardwareInspection.Application;
using GraniteEdgeAI.Features.HardwareInspection.Orchestration;
using GraniteEdgeAI.Features.HardwareInspection.Resolution;
using GraniteEdgeAI.UnitTests.Features.HardwareInspection.Resolution;

namespace GraniteEdgeAI.UnitTests.Features.HardwareInspection.Orchestration;

[TestClass]
public sealed class HardwareInspectionOutcomePolicyTests
{
    [TestMethod]
    public void FromResolution_MapsCleanWarningAndFailureWithoutLeakingDiagnostics()
    {
        Guid inspectionId = Guid.NewGuid();
        HardwareEvidenceResolutionResult baseline = ResolvedBaseline();
        HardwareEvidenceResolutionResult clean = HardwareEvidenceResolutionResult.Success(
            baseline.Snapshot!,
            baseline.Evidence,
            []);
        HardwareEvidenceResolutionResult warning = HardwareEvidenceResolutionResult.Success(
            baseline.Snapshot!,
            baseline.Evidence,
            [HardwareResolutionDiagnosticCode.InstructionSetsUnavailable]);
        HardwareEvidenceResolutionResult failed = HardwareEvidenceResolutionResult.Failure(
            baseline.Evidence,
            [HardwareResolutionDiagnosticCode.TotalMemoryConflict]);

        HardwareInspectionRunResult completed =
            HardwareInspectionOutcomePolicy.FromResolution(inspectionId, clean);
        HardwareInspectionRunResult completedWithWarnings =
            HardwareInspectionOutcomePolicy.FromResolution(inspectionId, warning);
        HardwareInspectionRunResult failure =
            HardwareInspectionOutcomePolicy.FromResolution(inspectionId, failed);

        Assert.AreEqual(HardwareInspectionOutcome.Completed, completed.Outcome);
        Assert.IsNotNull(completed.Handoff);
        Assert.AreEqual(
            HardwareInspectionOutcome.CompletedWithWarnings,
            completedWithWarnings.Outcome);
        Assert.IsNotNull(completedWithWarnings.Handoff);
        Assert.AreEqual(HardwareInspectionOutcome.Failed, failure.Outcome);
        Assert.AreEqual(HardwareInspectionFailureKind.CriticalEvidence, failure.FailureKind);
        Assert.AreEqual("HI-EVIDENCE-UNRESOLVED", failure.SafeDiagnosticCode);
        Assert.IsNull(failure.Snapshot);
        Assert.IsNull(failure.Handoff);
    }

    [TestMethod]
    public void FromAcquisitionFailure_MapsOnlyClosedApplicationRepairCodes()
    {
        Guid inspectionId = Guid.NewGuid();
        var cases = new[]
        {
            (HardwareToolAcquisitionDiagnosticCode.ToolNotAvailable,
                "HI-TOOL-NOT-AVAILABLE"),
            (HardwareToolAcquisitionDiagnosticCode.ToolIntegrityFailure,
                "HI-TOOL-INTEGRITY"),
            (HardwareToolAcquisitionDiagnosticCode.PackagedProbeUnavailable,
                "HI-RUNTIME-PACKAGE"),
        };

        foreach ((HardwareToolAcquisitionDiagnosticCode diagnostic, string code) in cases)
        {
            HardwareInspectionRunResult result =
                HardwareInspectionOutcomePolicy.FromAcquisitionFailure(
                    inspectionId,
                    diagnostic);

            Assert.AreEqual(HardwareInspectionOutcome.Failed, result.Outcome);
            Assert.AreEqual(
                HardwareInspectionFailureKind.ApplicationRepairRequired,
                result.FailureKind);
            Assert.AreEqual(code, result.SafeDiagnosticCode);
            Assert.IsNull(result.Handoff);
        }

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            HardwareInspectionOutcomePolicy.FromAcquisitionFailure(
                inspectionId,
                (HardwareToolAcquisitionDiagnosticCode)99));
    }

    [TestMethod]
    public void FromCollectionFailure_MapsOnlyClosedFailureClassesAndCodes()
    {
        Guid inspectionId = Guid.NewGuid();
        var cases = new[]
        {
            (HardwareEvidenceCollectionFailureCode.ProviderUnavailable,
                HardwareInspectionFailureKind.TransientOperation,
                "HI-PROVIDER-UNAVAILABLE"),
            (HardwareEvidenceCollectionFailureCode.OrchestrationFailure,
                HardwareInspectionFailureKind.ApplicationRepairRequired,
                "HI-ORCHESTRATION-FAILED"),
            (HardwareEvidenceCollectionFailureCode.ProgressCallbackFailure,
                HardwareInspectionFailureKind.ApplicationRepairRequired,
                "HI-PROGRESS-CALLBACK"),
        };

        foreach ((HardwareEvidenceCollectionFailureCode failure,
                     HardwareInspectionFailureKind kind,
                     string code) in cases)
        {
            HardwareInspectionRunResult result =
                HardwareInspectionOutcomePolicy.FromCollectionFailure(
                    inspectionId,
                    failure);

            Assert.AreEqual(HardwareInspectionOutcome.Failed, result.Outcome);
            Assert.AreEqual(kind, result.FailureKind);
            Assert.AreEqual(code, result.SafeDiagnosticCode);
            Assert.IsNull(result.Snapshot);
        }

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            HardwareInspectionOutcomePolicy.FromCollectionFailure(
                inspectionId,
                (HardwareEvidenceCollectionFailureCode)99));
    }

    private static HardwareEvidenceResolutionResult ResolvedBaseline()
    {
        HardwareEvidenceResolver resolver = new(
            new HardwareResolutionTestData.FixedTimeProvider(
                HardwareResolutionTestData.Now));
        HardwareEvidenceResolutionResult result = resolver.Resolve(
            Guid.NewGuid(),
            HardwareResolutionTestData.CompleteEvidence());
        Assert.IsTrue(result.IsResolved);
        return result;
    }
}
