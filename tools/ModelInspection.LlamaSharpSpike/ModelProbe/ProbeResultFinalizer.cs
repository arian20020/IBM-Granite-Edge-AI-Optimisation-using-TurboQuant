namespace GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike.ModelProbe;

/// <summary>
/// Contains the final operation status and failure after file-integrity
/// precedence has been applied.
/// </summary>
public sealed record ProbeCompletionResolution(
    VocabOnlyProbeCompletionStatus Status,
    ProbeFailure? Failure);

/// <summary>
/// Applies final precedence rules after probing and post-probe integrity
/// capture have completed.
/// </summary>
public static class ProbeResultFinalizer
{
    /// <summary>
    /// Ensures a changed or unverifiable model overrides an otherwise
    /// successful or cancelled operation.
    /// </summary>
    public static ProbeCompletionResolution Resolve(
        VocabOnlyProbeCompletionStatus status,
        ProbeFailure? failure,
        ModelFileIntegrityComparison? integrity,
        string? integrityErrorType,
        string? integrityErrorMessage)
    {
        bool integrityApplies = status is
            VocabOnlyProbeCompletionStatus.Succeeded or
            VocabOnlyProbeCompletionStatus.Cancelled;

        if (integrityApplies &&
            integrity is not null &&
            !integrity.IsPreserved)
        {
            return new ProbeCompletionResolution(
                VocabOnlyProbeCompletionStatus.Failed,
                new ProbeFailure(
                    "MI-OP-MODEL-INTEGRITY-CHANGED",
                    typeof(IOException).FullName,
                    "The selected model changed during the VocabOnly probe."));
        }

        if (integrityApplies && integrity is null)
        {
            return new ProbeCompletionResolution(
                VocabOnlyProbeCompletionStatus.Failed,
                new ProbeFailure(
                    "MI-OP-MODEL-INTEGRITY-VERIFICATION-FAILED",
                    integrityErrorType,
                    integrityErrorMessage ??
                        "Post-probe model integrity could not be verified."));
        }

        return new ProbeCompletionResolution(status, failure);
    }
}
