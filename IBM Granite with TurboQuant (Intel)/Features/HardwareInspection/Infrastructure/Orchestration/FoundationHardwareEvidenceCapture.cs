using GraniteEdgeAI.HardwareInspection.Foundation.Dxgi;
using GraniteEdgeAI.HardwareInspection.Foundation.LlamaCpp;
using GraniteEdgeAI.HardwareInspection.Foundation.LlmFit;
using GraniteEdgeAI.HardwareInspection.Foundation.NeuralProcessors;
using GraniteEdgeAI.HardwareInspection.Foundation.Processes;
using GraniteEdgeAI.HardwareInspection.Foundation.TrustedTools;
using GraniteEdgeAI.HardwareInspection.Foundation.Windows;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace GraniteEdgeAI.Features.HardwareInspection.Orchestration;

internal sealed class FoundationHardwareEvidenceCapture : IHardwareEvidenceCapture
{
    private readonly Func<CancellationToken, ValueTask<WindowsProcessorEvidence>> _captureProcessor;
    private readonly Func<CancellationToken, ValueTask<WindowsSystemSnapshot>> _captureSystem;
    private readonly Func<CancellationToken, ValueTask<WindowsStorageEvidence>> _captureStorage;
    private readonly Func<CancellationToken, ValueTask<DxgiGraphicsEvidence>> _captureGraphics;
    private readonly Func<CancellationToken, ValueTask<NeuralProcessorEvidence>> _captureNeuralProcessor;
    private readonly Func<VerifiedTrustedTool, CancellationToken, Task<LlmFitHardwareEvidence>> _captureLlmFit;
    private readonly Func<VerifiedTrustedTool, CancellationToken, Task<LlamaCppCapabilityEvidence>> _captureLlamaCpp;

    internal FoundationHardwareEvidenceCapture()
    {
        IExternalProcessRunner processRunner = new ExternalProcessRunner();
        _captureProcessor = new WindowsProcessorEvidenceProvider().CaptureAsync;
        _captureSystem = new WindowsSystemSnapshotProvider().CaptureAsync;
        _captureStorage = new WindowsStorageEvidenceProvider().CaptureAsync;
        _captureGraphics = new DxgiGraphicsEvidenceProvider().CaptureAsync;
        _captureNeuralProcessor = new UnavailableNeuralProcessorProbe().CaptureAsync;
        _captureLlmFit = new LlmFitHardwareEvidenceProvider(processRunner).CaptureAsync;
        _captureLlamaCpp = new LlamaCppCapabilityEvidenceProvider(processRunner).CaptureAsync;
    }

    internal FoundationHardwareEvidenceCapture(
        Func<CancellationToken, ValueTask<WindowsProcessorEvidence>> captureProcessor,
        Func<CancellationToken, ValueTask<WindowsSystemSnapshot>> captureSystem,
        Func<CancellationToken, ValueTask<WindowsStorageEvidence>> captureStorage,
        Func<CancellationToken, ValueTask<DxgiGraphicsEvidence>> captureGraphics,
        Func<CancellationToken, ValueTask<NeuralProcessorEvidence>> captureNeuralProcessor,
        Func<VerifiedTrustedTool, CancellationToken, Task<LlmFitHardwareEvidence>> captureLlmFit,
        Func<VerifiedTrustedTool, CancellationToken, Task<LlamaCppCapabilityEvidence>> captureLlamaCpp)
    {
        _captureProcessor = captureProcessor ?? throw new ArgumentNullException(nameof(captureProcessor));
        _captureSystem = captureSystem ?? throw new ArgumentNullException(nameof(captureSystem));
        _captureStorage = captureStorage ?? throw new ArgumentNullException(nameof(captureStorage));
        _captureGraphics = captureGraphics ?? throw new ArgumentNullException(nameof(captureGraphics));
        _captureNeuralProcessor = captureNeuralProcessor ??
            throw new ArgumentNullException(nameof(captureNeuralProcessor));
        _captureLlmFit = captureLlmFit ?? throw new ArgumentNullException(nameof(captureLlmFit));
        _captureLlamaCpp = captureLlamaCpp ?? throw new ArgumentNullException(nameof(captureLlamaCpp));
    }

    public ValueTask<WindowsProcessorEvidence> CaptureProcessorAsync(CancellationToken token) =>
        _captureProcessor(token);

    public ValueTask<WindowsSystemSnapshot> CaptureSystemAsync(CancellationToken token) =>
        _captureSystem(token);

    public ValueTask<WindowsStorageEvidence> CaptureStorageAsync(CancellationToken token) =>
        _captureStorage(token);

    public ValueTask<DxgiGraphicsEvidence> CaptureGraphicsAsync(CancellationToken token) =>
        _captureGraphics(token);

    public ValueTask<NeuralProcessorEvidence> CaptureNeuralProcessorAsync(CancellationToken token) =>
        _captureNeuralProcessor(token);

    public Task<LlmFitHardwareEvidence> CaptureLlmFitAsync(
        VerifiedTrustedTool tool,
        CancellationToken token) =>
        _captureLlmFit(tool, token);

    public Task<LlamaCppCapabilityEvidence> CaptureLlamaCppAsync(
        VerifiedTrustedTool tool,
        CancellationToken token) =>
        _captureLlamaCpp(tool, token);
}
