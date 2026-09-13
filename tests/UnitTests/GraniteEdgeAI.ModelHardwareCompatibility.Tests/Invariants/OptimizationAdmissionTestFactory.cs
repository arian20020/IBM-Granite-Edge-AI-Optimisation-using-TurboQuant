using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Tests.Invariants;

internal static class OptimizationAdmissionTestFactory
{
    internal static OptimizationCandidate Admit(
        OptimizationCandidate candidate,
        OptimizationCapabilitySnapshot snapshot,
        OptimizationWorkload workload,
        OptimizationJourneyBinding binding,
        SupportLevel level,
        bool requiresEvidence = false)
    {
        IReadOnlySet<string> optedIn = requiresEvidence
            || level == SupportLevel.Experimental
            ? new HashSet<string> { candidate.EvidenceId }
            : new HashSet<string>();
        OptimizationAdmissionProof proof = OptimizationAdmissionProof.Create(
            snapshot, workload, binding, candidate, level, requiresEvidence, optedIn,
            OptimizationHardwareAuthorityTestData.Issuance(candidate));
        return OptimizationCandidate.AttachAdmissionProof(candidate, proof);
    }
}
