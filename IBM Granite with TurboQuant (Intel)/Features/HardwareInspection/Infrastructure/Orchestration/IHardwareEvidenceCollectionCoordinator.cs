using GraniteEdgeAI.Features.HardwareInspection.Application;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace GraniteEdgeAI.Features.HardwareInspection.Orchestration;

internal interface IHardwareEvidenceCollectionCoordinator
{
    Task<HardwareEvidenceCollectionResult> CollectAsync(
        HardwareToolLease tools,
        IProgress<HardwareInspectionRunStage> progress,
        CancellationToken token);
}
