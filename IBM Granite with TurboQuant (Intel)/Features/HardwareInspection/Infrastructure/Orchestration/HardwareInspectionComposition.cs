using GraniteEdgeAI.Features.HardwareInspection.Application;
using GraniteEdgeAI.Features.HardwareInspection.Resolution;
using System;

namespace GraniteEdgeAI.Features.HardwareInspection.Orchestration;

internal static class HardwareInspectionComposition
{
    internal static IHardwareInspectionService CreateProduction()
    {
        IHardwareToolAcquisition tools = FixedHardwareToolAcquisition.CreateProduction();
        IHardwareEvidenceCapture capture = new FoundationHardwareEvidenceCapture();
        IHardwareEvidenceCollectionCoordinator collector =
            new HardwareEvidenceCollectionCoordinator(capture, TimeProvider.System);
        IHardwareEvidenceResolver resolver = new HardwareEvidenceResolver(TimeProvider.System);
        return new HardwareInspectionService(tools, collector, resolver);
    }
}
