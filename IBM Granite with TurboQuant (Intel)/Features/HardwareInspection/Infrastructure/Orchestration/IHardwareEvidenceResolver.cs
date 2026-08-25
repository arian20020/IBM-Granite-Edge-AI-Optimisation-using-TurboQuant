using GraniteEdgeAI.Features.HardwareInspection.Resolution;
using System;

namespace GraniteEdgeAI.Features.HardwareInspection.Orchestration;

internal interface IHardwareEvidenceResolver
{
    HardwareEvidenceResolutionResult Resolve(
        Guid inspectionId,
        CollectedHardwareEvidence evidence);
}
