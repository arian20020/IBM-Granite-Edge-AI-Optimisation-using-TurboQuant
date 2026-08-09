using System;
using System.Threading;
using System.Threading.Tasks;
using GraniteEdgeAI.Features.ModelInspection.Contracts;

namespace GraniteEdgeAI.Features.ModelInspection.Runtime;

/// <summary>
/// Defines the application-owned boundary for the one approved lightweight
/// GGUF LLamaSharp/llama.cpp inspection path.
/// </summary>
internal interface ILlamaModelProbe
{
    Task<ModelInspectionProbeResult> InspectAsync(
        ModelInspectionRequest request,
        IProgress<ModelInspectionProgress>? progress,
        CancellationToken cancellationToken);
}
