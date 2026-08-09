using GraniteEdgeAI.Features.ModelInspection.Contracts;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace GraniteEdgeAI.Features.ModelInspection.Services;

/// <summary>
/// Runs the complete application-owned Model Inspection use case.
/// </summary>
internal interface IModelInspectionService
{
    Task<ModelInspectionExecutionResult> InspectAsync(
        ModelInspectionRequest request,
        IProgress<ModelInspectionProgress>? progress,
        CancellationToken cancellationToken);
}
