using System.Globalization;

namespace GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike;

/// <summary>
/// Contains command-line configuration for the native smoke or VocabOnly model
/// probe.
/// </summary>
public sealed record SpikeOptions(
    string OutputPath,
    bool ShowHelp,
    string? ModelPath,
    int? CancelAfterMilliseconds,
    int? CancelNativeAfterMilliseconds)
{
    public const string NativeSmokeDefaultOutputPath =
        "artifacts/model-inspection/llamasharp/runtime-smoke.json";

    public const string VocabOnlyModelProbeDefaultOutputPath =
        "artifacts/model-inspection/llamasharp/vocab-only-model-probe.json";

    /// <summary>
    /// Gets whether the command requests a model probe rather than a native
    /// library-only smoke.
    /// </summary>
    public bool RunsModelProbe =>
        !string.IsNullOrWhiteSpace(ModelPath);
}

/// <summary>
/// Contains either valid spike options or one controlled argument error.
/// </summary>
public sealed record SpikeOptionsParseResult(
    SpikeOptions? Options,
    string? ErrorMessage)
{
    public bool Succeeded =>
        Options is not null && string.IsNullOrWhiteSpace(ErrorMessage);
}

/// <summary>
/// Parses the small feasibility command line without a third-party parser.
/// </summary>
public static class SpikeOptionsParser
{
    /// <summary>
    /// Parses native-smoke and VocabOnly model-probe options in any order.
    /// </summary>
    public static SpikeOptionsParseResult Parse(
        IReadOnlyList<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        if (arguments.Count == 0)
        {
            return Success(
                new SpikeOptions(
                    SpikeOptions.NativeSmokeDefaultOutputPath,
                    ShowHelp: false,
                    ModelPath: null,
                    CancelAfterMilliseconds: null,
                    CancelNativeAfterMilliseconds: null));
        }

        if (arguments.Count == 1 && IsHelp(arguments[0]))
        {
            return Success(
                new SpikeOptions(
                    SpikeOptions.NativeSmokeDefaultOutputPath,
                    ShowHelp: true,
                    ModelPath: null,
                    CancelAfterMilliseconds: null,
                    CancelNativeAfterMilliseconds: null));
        }

        string? modelPath = null;
        string? outputPath = null;
        int? cancelAfterMilliseconds = null;
        int? cancelNativeAfterMilliseconds = null;
        var seenOptions = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);

        for (int index = 0; index < arguments.Count; index++)
        {
            string argument = arguments[index];

            if (IsHelp(argument))
            {
                return Failure(
                    "The help option must be used by itself.");
            }

            if (!seenOptions.Add(argument))
            {
                return Failure(
                    $"The {argument} option was specified more than once.");
            }

            switch (argument.ToLowerInvariant())
            {
                case "--model":
                    if (!TryReadValue(
                        arguments,
                        ref index,
                        out modelPath))
                    {
                        return Failure(
                            "The --model option requires a path.");
                    }

                    break;

                case "--output":
                    if (!TryReadValue(
                        arguments,
                        ref index,
                        out outputPath))
                    {
                        return Failure(
                            "The --output option requires a path.");
                    }

                    break;

                case "--cancel-after-ms":
                    if (!TryReadPositiveMilliseconds(
                        arguments,
                        ref index,
                        "--cancel-after-ms",
                        out cancelAfterMilliseconds,
                        out string? cancellationError))
                    {
                        return Failure(cancellationError!);
                    }

                    break;

                case "--cancel-native-after-ms":
                    if (!TryReadPositiveMilliseconds(
                        arguments,
                        ref index,
                        "--cancel-native-after-ms",
                        out cancelNativeAfterMilliseconds,
                        out string? nativeCancellationError))
                    {
                        return Failure(nativeCancellationError!);
                    }

                    break;

                default:
                    return Failure($"Unknown argument: {argument}");
            }
        }

        if (cancelAfterMilliseconds.HasValue &&
            string.IsNullOrWhiteSpace(modelPath))
        {
            return Failure(
                "The --cancel-after-ms option requires --model.");
        }

        if (cancelNativeAfterMilliseconds.HasValue &&
            string.IsNullOrWhiteSpace(modelPath))
        {
            return Failure(
                "The --cancel-native-after-ms option requires --model.");
        }

        if (cancelAfterMilliseconds.HasValue &&
            cancelNativeAfterMilliseconds.HasValue)
        {
            return Failure(
                "The --cancel-after-ms and --cancel-native-after-ms options " +
                "are mutually exclusive.");
        }

        string selectedOutputPath = outputPath ??
            (string.IsNullOrWhiteSpace(modelPath)
                ? SpikeOptions.NativeSmokeDefaultOutputPath
                : SpikeOptions.VocabOnlyModelProbeDefaultOutputPath);

        return Success(
            new SpikeOptions(
                selectedOutputPath,
                ShowHelp: false,
                modelPath,
                cancelAfterMilliseconds,
                cancelNativeAfterMilliseconds));
    }

    /// <summary>
    /// Gets user-facing command help for both feasibility modes.
    /// </summary>
    public static string HelpText =>
        "Model Inspection LLamaSharp feasibility tool\n\n" +
        "Native CPU backend smoke:\n" +
        "  ModelInspection.LlamaSharpSpike [--output <json-path>]\n\n" +
        "CPU VocabOnly model probe:\n" +
        "  ModelInspection.LlamaSharpSpike --model <gguf-path> " +
        "[--output <json-path>] " +
        "[--cancel-after-ms <positive-integer> | " +
        "--cancel-native-after-ms <positive-integer>]\n\n" +
        "Help:\n" +
        "  ModelInspection.LlamaSharpSpike --help\n\n" +
        "The model probe is read-only, uses zero GPU layers, does not create " +
        "a context, and does not run inference.";

    private static bool TryReadPositiveMilliseconds(
        IReadOnlyList<string> arguments,
        ref int index,
        string optionName,
        out int? milliseconds,
        out string? errorMessage)
    {
        milliseconds = null;

        if (!TryReadValue(
            arguments,
            ref index,
            out string? delayText))
        {
            errorMessage =
                $"The {optionName} option requires a value.";
            return false;
        }

        if (!int.TryParse(
                delayText,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out int parsedDelay) ||
            parsedDelay <= 0)
        {
            errorMessage =
                $"The {optionName} value must be a positive whole number.";
            return false;
        }

        milliseconds = parsedDelay;
        errorMessage = null;
        return true;
    }

    private static bool TryReadValue(
        IReadOnlyList<string> arguments,
        ref int index,
        out string? value)
    {
        int valueIndex = index + 1;

        if (valueIndex >= arguments.Count ||
            string.IsNullOrWhiteSpace(arguments[valueIndex]) ||
            arguments[valueIndex].StartsWith(
                "--",
                StringComparison.Ordinal))
        {
            value = null;
            return false;
        }

        value = arguments[valueIndex];
        index = valueIndex;
        return true;
    }

    private static bool IsHelp(string argument)
    {
        return string.Equals(
                   argument,
                   "--help",
                   StringComparison.OrdinalIgnoreCase) ||
               string.Equals(
                   argument,
                   "-h",
                   StringComparison.OrdinalIgnoreCase);
    }

    private static SpikeOptionsParseResult Success(SpikeOptions options)
    {
        return new SpikeOptionsParseResult(
            options,
            ErrorMessage: null);
    }

    private static SpikeOptionsParseResult Failure(string message)
    {
        return new SpikeOptionsParseResult(
            Options: null,
            message);
    }
}
