using System;
#if MODEL_INSPECTION_X64
using GraniteEdgeAI.Features.ModelInspection.Infrastructure;
#endif

namespace GraniteEdgeAI.Features.ModelInspection.Services;

/// <summary>
/// Provides the page-facing service entry point without making UI code own
/// worker or protocol composition. The supported production route is x64.
/// </summary>
internal static class ModelInspectionServiceComposition
{
    internal static IModelInspectionService CreateDefault()
    {
#if MODEL_INSPECTION_X64
        return ModelInspectionWorkerComposition.CreateDefaultService();
#else
        throw new PlatformNotSupportedException(
            "Model Inspection is currently available only for Windows x64.");
#endif
    }
}
