namespace GraniteEdgeAI.HardwareInspection.Foundation.NeuralProcessors;

public sealed class UnavailableNeuralProcessorProbe : INeuralProcessorProbe
{
    private readonly TimeProvider _timeProvider;

    public UnavailableNeuralProcessorProbe()
        : this(TimeProvider.System)
    {
    }

    internal UnavailableNeuralProcessorProbe(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public ValueTask<NeuralProcessorEvidence> CaptureAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(NeuralProcessorEvidence.DetectionUnavailable(
            NeuralProcessorDiagnosticCode.EnumerationMechanismNotApproved,
            _timeProvider.GetUtcNow().ToUniversalTime()));
    }
}
