using GraniteEdgeAI.Features.HardwareInspection.Orchestration;
using GraniteEdgeAI.HardwareInspection.Foundation.TrustedTools;
using GraniteEdgeAI.UnitTests.Features.HardwareInspection.Resolution;
using System.Collections.ObjectModel;

namespace GraniteEdgeAI.UnitTests.Features.HardwareInspection.Orchestration;

[TestClass]
[TestCategory("HardwareInspectionGate7Acceptance")]
public sealed class HardwareOrchestrationContractTests
{
    [TestMethod]
    public void ClosedEnums_ExposeOnlyApprovedGate7States()
    {
        CollectionAssert.AreEqual(
            new[]
            {
                "ToolNotAvailable",
                "ToolIntegrityFailure",
                "PackagedProbeUnavailable",
            },
            Enum.GetNames<HardwareToolAcquisitionDiagnosticCode>());
        CollectionAssert.AreEqual(
            new[]
            {
                "ProviderUnavailable",
                "OrchestrationFailure",
                "ProgressCallbackFailure",
            },
            Enum.GetNames<HardwareEvidenceCollectionFailureCode>());
    }

    [TestMethod]
    public void AcquisitionResult_RejectsUndefinedFailureAndExposesExclusiveState()
    {
        HardwareToolAcquisitionResult failure = HardwareToolAcquisitionResult.Failure(
            HardwareToolAcquisitionDiagnosticCode.ToolIntegrityFailure);

        Assert.IsFalse(failure.IsSuccess);
        Assert.IsNull(failure.Lease);
        Assert.AreEqual(
            HardwareToolAcquisitionDiagnosticCode.ToolIntegrityFailure,
            failure.Diagnostic);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            HardwareToolAcquisitionResult.Failure(
                (HardwareToolAcquisitionDiagnosticCode)99));
        Assert.Throws<ArgumentNullException>(() =>
            HardwareToolAcquisitionResult.Success(null!));
    }

    [TestMethod]
    public void ToolLease_DisposesBothVerifiedToolsExactlyOnce()
    {
        CountingDisposable llmFitCustody = new();
        CountingDisposable llamaCppCustody = new();
        VerifiedTrustedTool llmFit = Tool("llmfit", llmFitCustody);
        VerifiedTrustedTool llamaCpp = Tool("llama", llamaCppCustody);
        HardwareToolLease lease = new(llmFit, llamaCpp);

        HardwareToolAcquisitionResult result = HardwareToolAcquisitionResult.Success(lease);
        Assert.IsTrue(result.IsSuccess);
        Assert.AreSame(lease, result.Lease);
        Assert.IsNull(result.Diagnostic);

        lease.Dispose();
        lease.Dispose();

        Assert.AreEqual(1, llmFitCustody.DisposeCount);
        Assert.AreEqual(1, llamaCppCustody.DisposeCount);
    }

    [TestMethod]
    public void ToolLease_AllowsOnlyVerifiedAbsenceToOmitOptionalLlmFitCustody()
    {
        using VerifiedTrustedTool llamaCpp = Tool("llama", new CountingDisposable());

        using HardwareToolLease degraded = new(
            llmFit: null,
            llamaCpp: llamaCpp,
            llmFitDiagnostic: HardwareToolAcquisitionDiagnosticCode.ToolNotAvailable);

        Assert.IsNull(degraded.LlmFit);
        Assert.AreEqual(
            HardwareToolAcquisitionDiagnosticCode.ToolNotAvailable,
            degraded.LlmFitDiagnostic);
        Assert.Throws<ArgumentException>(() => new HardwareToolLease(
            llmFit: null,
            llamaCpp: llamaCpp,
            llmFitDiagnostic: HardwareToolAcquisitionDiagnosticCode.ToolIntegrityFailure));
        Assert.Throws<ArgumentException>(() => new HardwareToolLease(
            llmFit: null,
            llamaCpp: llamaCpp,
            llmFitDiagnostic: HardwareToolAcquisitionDiagnosticCode.PackagedProbeUnavailable));
    }

    [TestMethod]
    public void CollectionResult_ExposesEitherEvidenceOrClosedFailure()
    {
        HardwareEvidenceCollectionResult success =
            HardwareEvidenceCollectionResult.Success(
                HardwareResolutionTestData.CompleteEvidence());
        HardwareEvidenceCollectionResult failure =
            HardwareEvidenceCollectionResult.Failure(
                HardwareEvidenceCollectionFailureCode.ProviderUnavailable);

        Assert.IsTrue(success.IsSuccess);
        Assert.IsNotNull(success.Evidence);
        Assert.IsNull(success.FailureCode);
        Assert.IsFalse(failure.IsSuccess);
        Assert.IsNull(failure.Evidence);
        Assert.AreEqual(
            HardwareEvidenceCollectionFailureCode.ProviderUnavailable,
            failure.FailureCode);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            HardwareEvidenceCollectionResult.Failure(
                (HardwareEvidenceCollectionFailureCode)99));
        Assert.Throws<ArgumentNullException>(() =>
            HardwareEvidenceCollectionResult.Success(null!));
    }

    private static VerifiedTrustedTool Tool(
        string id,
        IDisposable custody) => new(
            id,
            "1",
            "package",
            "tool.exe",
            TrustedToolPackageDisposition.AcceptedForFunctionalEvaluation,
            new ReadOnlyDictionary<string, TrustedToolCommand>(
                new Dictionary<string, TrustedToolCommand>()),
            custody);

    private sealed class CountingDisposable : IDisposable
    {
        internal int DisposeCount { get; private set; }

        public void Dispose() => DisposeCount++;
    }
}
