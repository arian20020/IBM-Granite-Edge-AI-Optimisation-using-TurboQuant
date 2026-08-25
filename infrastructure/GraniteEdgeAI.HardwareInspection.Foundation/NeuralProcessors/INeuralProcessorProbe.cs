namespace GraniteEdgeAI.HardwareInspection.Foundation.NeuralProcessors;

public interface INeuralProcessorProbe
{
    ValueTask<NeuralProcessorEvidence> CaptureAsync(CancellationToken cancellationToken);
}
