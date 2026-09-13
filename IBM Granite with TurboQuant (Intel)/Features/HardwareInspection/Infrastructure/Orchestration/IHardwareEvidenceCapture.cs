using GraniteEdgeAI.HardwareInspection.Foundation.Dxgi;
using GraniteEdgeAI.HardwareInspection.Foundation.LlamaCpp;
using GraniteEdgeAI.HardwareInspection.Foundation.LlmFit;
using GraniteEdgeAI.HardwareInspection.Foundation.NeuralProcessors;
using GraniteEdgeAI.HardwareInspection.Foundation.TrustedTools;
using GraniteEdgeAI.HardwareInspection.Foundation.Windows;
using System.Threading;
using System.Threading.Tasks;

namespace GraniteEdgeAI.Features.HardwareInspection.Orchestration;

internal interface IHardwareEvidenceCapture
{
    ValueTask<WindowsProcessorEvidence> CaptureProcessorAsync(CancellationToken token);

    ValueTask<WindowsSystemSnapshot> CaptureSystemAsync(CancellationToken token);

    ValueTask<WindowsStorageEvidence> CaptureStorageAsync(CancellationToken token);

    ValueTask<DxgiGraphicsEvidence> CaptureGraphicsAsync(CancellationToken token);

    ValueTask<NeuralProcessorEvidence> CaptureNeuralProcessorAsync(CancellationToken token);

    Task<LlmFitHardwareEvidence> CaptureLlmFitAsync(
        VerifiedTrustedTool tool,
        CancellationToken token);

    Task<LlamaCppCapabilityEvidence> CaptureLlamaCppAsync(
        VerifiedTrustedTool tool,
        CancellationToken token);
}
