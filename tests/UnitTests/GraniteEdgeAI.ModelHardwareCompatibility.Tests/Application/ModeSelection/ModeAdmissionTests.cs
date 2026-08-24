using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Candidates;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Estimation;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.FitAssessment;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.ModeSelection;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.Gguf;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Tests.Application.ModeSelection;

[TestClass]
public sealed class ModeAdmissionTests
{
    private static EvaluatedCandidate Candidate(
        CompatibilityFitState state = CompatibilityFitState.Safe,
        EvidenceGrade evidence = EvidenceGrade.Estimated,
        EstimationStatus estimateStatus = EstimationStatus.Established)
    {
        CompatibilityCandidate candidate = CompatibilityCandidate.Create(
            GgufRouteConfiguration.Create(
                GgufWeightFormat.Imported,
                GgufKvCacheFormat.F16,
                CompatibilityBackend.Cpu,
                DeviceRouteId.Cpu,
                GpuOffloadLevel.None),
            ContextTokenCount.FromTokens(4096),
            CandidatePreparation.None,
            supportEntryId: "entry-1",
            isExperimental: false,
            isBaseline: true);

        ResourceEstimate estimate = estimateStatus == EstimationStatus.Established
            ? ResourceEstimate.Established(
                [
                    ResourceComponent.Create(
                        ResourceComponentKind.Weights,
                        ResourceTarget.SystemMemory,
                        ByteCount.FromBytes(1024),
                        new HashSet<LifecyclePhase> { LifecyclePhase.Load })
                ],
                new HashSet<EstimationLimitation>())
            : ResourceEstimate.NotEstablished(
                EstimationUnavailableReason.UnknownArchitecture);

        return EvaluatedCandidate.Create(
            candidate,
            estimate,
            ResourcePhaseComposer.Compose(estimate.Components),
            new FitAssessment(
                state,
                FitLimitingReason.None,
                ByteCount.FromBytes(8192),
                ByteCount.FromBytes(1024),
                ByteCount.FromBytes(7168),
                PressureRatio: 0.125m),
            WeightQuantisation.Q4_K_M,
            evidence,
            PerformanceIndicator.NotEstablished(),
            ByteCount.Zero);
    }

    [TestMethod]
    [DataRow(nameof(CompatibilityFitState.Safe), true)]
    [DataRow(nameof(CompatibilityFitState.Narrow), true)]
    [DataRow(nameof(CompatibilityFitState.DoesNotFit), false)]
    [DataRow(nameof(CompatibilityFitState.Unsupported), false)]
    [DataRow(nameof(CompatibilityFitState.NotEstablished), false)]
    public void IsAdmitted_OnlyAdmitsSafeOrNarrow(string state, bool expected)
    {
        bool admitted = ModeAdmission.IsAdmitted(
            Candidate(Enum.Parse<CompatibilityFitState>(state)),
            entryRequiresEvidence: false,
            out ModeAdmissionReason reason);

        Assert.AreEqual(expected, admitted);

        if (!expected)
        {
            Assert.AreEqual(
                nameof(ModeAdmissionReason.FitStateNotSafeOrNarrow), reason.ToString());
        }
    }

    [TestMethod]
    public void IsAdmitted_RefusesWhenAnEntryNeedsEvidenceAndOnlyAnEstimateExists()
    {
        // Section 11: where an entry declares a threshold but no evidence exists
        // to test it, the candidate is unsupported rather than optimistically
        // admitted on a calculated number.
        bool admitted = ModeAdmission.IsAdmitted(
            Candidate(evidence: EvidenceGrade.Estimated),
            entryRequiresEvidence: true,
            out ModeAdmissionReason reason);

        Assert.IsFalse(admitted);
        Assert.AreEqual(
            nameof(ModeAdmissionReason.EvidenceBelowAdmissionLevel), reason.ToString());
    }

    [TestMethod]
    [DataRow(nameof(EvidenceGrade.Measured))]
    [DataRow(nameof(EvidenceGrade.Verified))]
    public void IsAdmitted_AdmitsAnEvidenceRequiringEntryOnceMeasured(string grade)
    {
        Assert.IsTrue(ModeAdmission.IsAdmitted(
            Candidate(evidence: Enum.Parse<EvidenceGrade>(grade)),
            entryRequiresEvidence: true,
            out _));
    }

    [TestMethod]
    public void IsAdmitted_DoesNotRequireEvidenceWhenTheEntryDeclaresNoThreshold()
    {
        Assert.IsTrue(ModeAdmission.IsAdmitted(
            Candidate(evidence: EvidenceGrade.Estimated),
            entryRequiresEvidence: false,
            out _));
    }

    [TestMethod]
    public void IsAdmitted_RefusesAnUnestablishedEstimate()
    {
        // Without an estimate there is no requirement to compare, so the fit
        // verdict beside it cannot mean anything.
        bool admitted = ModeAdmission.IsAdmitted(
            Candidate(estimateStatus: EstimationStatus.NotEstablished),
            entryRequiresEvidence: false,
            out ModeAdmissionReason reason);

        Assert.IsFalse(admitted);
        Assert.AreEqual(
            nameof(ModeAdmissionReason.EstimateNotEstablished), reason.ToString());
    }

    [TestMethod]
    public void IsAdmitted_ReportsNoReasonWhenItAdmits()
    {
        ModeAdmission.IsAdmitted(Candidate(), false, out ModeAdmissionReason reason);

        Assert.AreEqual(nameof(ModeAdmissionReason.None), reason.ToString());
    }

    [TestMethod]
    public void IsAdmitted_ChecksTheEstimateBeforeTheFitState()
    {
        // An unestablished estimate produces a NotEstablished fit state too. The
        // more specific reason is the useful one to show a user.
        ModeAdmission.IsAdmitted(
            Candidate(
                state: CompatibilityFitState.NotEstablished,
                estimateStatus: EstimationStatus.NotEstablished),
            entryRequiresEvidence: false,
            out ModeAdmissionReason reason);

        Assert.AreEqual(
            nameof(ModeAdmissionReason.EstimateNotEstablished), reason.ToString());
    }
}
