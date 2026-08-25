using GraniteEdgeAI.HardwareInspection.Foundation.Dxgi;
using GraniteEdgeAI.HardwareInspection.Foundation.LlamaCpp;
using GraniteEdgeAI.HardwareInspection.Foundation.LlmFit;
using GraniteEdgeAI.HardwareInspection.Foundation.NeuralProcessors;
using GraniteEdgeAI.HardwareInspection.Foundation.Windows;
using System;

namespace GraniteEdgeAI.Features.HardwareInspection.Resolution;

internal sealed class CollectedHardwareEvidence
{
    internal CollectedHardwareEvidence(
        LlmFitHardwareEvidence llmFit,
        WindowsProcessorEvidence windowsProcessor,
        WindowsSystemEvidenceObservation windowsSystem,
        WindowsStorageEvidence storage,
        DxgiGraphicsEvidence graphics,
        NeuralProcessorEvidence neuralProcessor,
        LlamaCppCapabilityEvidence llamaCpp)
    {
        LlmFit = llmFit ?? throw new ArgumentNullException(nameof(llmFit));
        WindowsProcessor = windowsProcessor ??
            throw new ArgumentNullException(nameof(windowsProcessor));
        WindowsSystem = windowsSystem ?? throw new ArgumentNullException(nameof(windowsSystem));
        Storage = storage ?? throw new ArgumentNullException(nameof(storage));
        Graphics = graphics ?? throw new ArgumentNullException(nameof(graphics));
        NeuralProcessor = neuralProcessor ?? throw new ArgumentNullException(nameof(neuralProcessor));
        LlamaCpp = llamaCpp ?? throw new ArgumentNullException(nameof(llamaCpp));
    }

    internal LlmFitHardwareEvidence LlmFit { get; }

    internal WindowsProcessorEvidence WindowsProcessor { get; }

    internal WindowsSystemEvidenceObservation WindowsSystem { get; }

    internal WindowsStorageEvidence Storage { get; }

    internal DxgiGraphicsEvidence Graphics { get; }

    internal NeuralProcessorEvidence NeuralProcessor { get; }

    internal LlamaCppCapabilityEvidence LlamaCpp { get; }
}
