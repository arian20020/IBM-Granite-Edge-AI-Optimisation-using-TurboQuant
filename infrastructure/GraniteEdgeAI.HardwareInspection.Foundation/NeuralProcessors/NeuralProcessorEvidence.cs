using System.Collections.ObjectModel;
using GraniteEdgeAI.HardwareInspection.Foundation.Validation;

namespace GraniteEdgeAI.HardwareInspection.Foundation.NeuralProcessors;

public enum NeuralProcessorEvidenceState
{
    Present,
    NotPresent,
    DetectionUnavailable,
}

public enum NeuralProcessorDiagnosticCode
{
    EnumerationMechanismNotApproved,
}

public sealed class NeuralProcessorEvidence
{
    private NeuralProcessorEvidence(
        NeuralProcessorEvidenceState state,
        string? name,
        DateTimeOffset capturedAtUtc,
        IReadOnlyList<NeuralProcessorDiagnosticCode> diagnostics)
    {
        State = state;
        Name = name;
        CapturedAtUtc = capturedAtUtc;
        Diagnostics = diagnostics;
    }

    public NeuralProcessorEvidenceState State { get; }

    public string? Name { get; }

    public DateTimeOffset CapturedAtUtc { get; }

    public IReadOnlyList<NeuralProcessorDiagnosticCode> Diagnostics { get; }

    public static NeuralProcessorEvidence Present(string name, DateTimeOffset capturedAtUtc)
    {
        HardwareText.Validate(name, 256, nameof(name));
        ValidateCaptureTime(capturedAtUtc);
        return new(
            NeuralProcessorEvidenceState.Present,
            name,
            capturedAtUtc,
            EmptyDiagnostics());
    }

    public static NeuralProcessorEvidence NotPresent(DateTimeOffset capturedAtUtc)
    {
        ValidateCaptureTime(capturedAtUtc);
        return new(
            NeuralProcessorEvidenceState.NotPresent,
            null,
            capturedAtUtc,
            EmptyDiagnostics());
    }

    public static NeuralProcessorEvidence DetectionUnavailable(
        NeuralProcessorDiagnosticCode diagnostic,
        DateTimeOffset capturedAtUtc)
    {
        if (!Enum.IsDefined(diagnostic))
        {
            throw new ArgumentOutOfRangeException(nameof(diagnostic));
        }

        ValidateCaptureTime(capturedAtUtc);
        return new(
            NeuralProcessorEvidenceState.DetectionUnavailable,
            null,
            capturedAtUtc,
            Array.AsReadOnly([diagnostic]));
    }

    private static ReadOnlyCollection<NeuralProcessorDiagnosticCode> EmptyDiagnostics() =>
        Array.AsReadOnly(Array.Empty<NeuralProcessorDiagnosticCode>());

    private static void ValidateCaptureTime(DateTimeOffset capturedAtUtc)
    {
        if (capturedAtUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Capture time must use the UTC offset.", nameof(capturedAtUtc));
        }
    }
}
