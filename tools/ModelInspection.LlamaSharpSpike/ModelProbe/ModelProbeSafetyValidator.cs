namespace GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike.ModelProbe;

/// <summary>
/// Contains the result of validating model-probe output safety.
/// </summary>
/// <param name="Succeeded">Whether the output path is safe.</param>
/// <param name="ErrorMessage">Controlled error when validation fails.</param>
public sealed record ModelProbeSafetyValidationResult(
    bool Succeeded,
    string? ErrorMessage);

/// <summary>
/// Protects the selected model from accidental evidence overwrite.
/// </summary>
public static class ModelProbeSafetyValidator
{
    /// <summary>
    /// Verifies that the evidence output does not resolve to the selected model.
    /// </summary>
    public static ModelProbeSafetyValidationResult ValidateOutputPath(
        string modelPath,
        string outputPath)
    {
        if (string.IsNullOrWhiteSpace(modelPath))
        {
            return Failure("The model path is required.");
        }

        if (string.IsNullOrWhiteSpace(outputPath))
        {
            return Failure("The evidence output path is required.");
        }

        string fullModelPath;
        string fullOutputPath;

        try
        {
            fullModelPath = Path.GetFullPath(modelPath);
            fullOutputPath = Path.GetFullPath(outputPath);
        }
        catch (Exception exception)
            when (exception is ArgumentException or
                  NotSupportedException or
                  PathTooLongException)
        {
            return Failure(
                "The model or evidence output path is invalid: " +
                exception.Message);
        }

        StringComparison comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        if (string.Equals(
            fullModelPath,
            fullOutputPath,
            comparison))
        {
            return Failure(
                "The evidence output must not overwrite the selected model.");
        }

        return new ModelProbeSafetyValidationResult(
            Succeeded: true,
            ErrorMessage: null);
    }

    private static ModelProbeSafetyValidationResult Failure(string message)
    {
        return new ModelProbeSafetyValidationResult(
            Succeeded: false,
            ErrorMessage: message);
    }
}
