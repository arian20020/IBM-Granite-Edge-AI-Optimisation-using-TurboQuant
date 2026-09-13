using LLama.Exceptions;

namespace GraniteEdgeAI.ModelInspection.LlamaSharp.ModelProbe;

/// <summary>
/// Converts managed runtime and file exceptions into stable feasibility
/// diagnostics. It does not classify a final application model outcome.
/// </summary>
public static class ProbeFailureMapper
{
    /// <summary>
    /// Maps one exception to a project-owned diagnostic record.
    /// </summary>
    public static ProbeFailure Map(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        string? type = exception.GetType().FullName;

        return exception switch
        {
            ModelFileContinuityException =>
                new ProbeFailure(
                    "MI-OP-MODEL-CONTINUITY-MISMATCH",
                    type,
                    "The selected model changed after it was prepared for inspection."),

            FileNotFoundException =>
                new ProbeFailure(
                    "MI-OP-MODEL-FILE-NOT-FOUND",
                    type,
                    exception.Message),

            UnauthorizedAccessException =>
                new ProbeFailure(
                    "MI-OP-MODEL-FILE-ACCESS-DENIED",
                    type,
                    exception.Message),

            BadImageFormatException =>
                new ProbeFailure(
                    "MI-OP-RUNTIME-ARCHITECTURE-MISMATCH",
                    type,
                    exception.Message),

            DllNotFoundException =>
                new ProbeFailure(
                    "MI-OP-RUNTIME-UNAVAILABLE",
                    type,
                    exception.Message),

            LoadWeightsFailedException =>
                new ProbeFailure(
                    "MI-PROBE-MODEL-LOAD-FAILED",
                    type,
                    exception.Message),

            IOException =>
                new ProbeFailure(
                    "MI-OP-MODEL-FILE-IO",
                    type,
                    exception.Message),

            TypeInitializationException
                { InnerException: DllNotFoundException inner } =>
                new ProbeFailure(
                    "MI-OP-RUNTIME-UNAVAILABLE",
                    inner.GetType().FullName,
                    inner.Message),

            TypeInitializationException
                { InnerException: BadImageFormatException inner } =>
                new ProbeFailure(
                    "MI-OP-RUNTIME-ARCHITECTURE-MISMATCH",
                    inner.GetType().FullName,
                    inner.Message),

            _ =>
                new ProbeFailure(
                    "MI-OP-RUNTIME-INSPECTION-FAILED",
                    type,
                    exception.Message)
        };
    }
}
