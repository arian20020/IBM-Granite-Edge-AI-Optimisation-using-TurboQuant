namespace GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike;

/// <summary>
/// Contains the deliberately small command-line configuration for Slice 1.
/// </summary>
/// <param name="OutputPath">Local JSON evidence path.</param>
/// <param name="ShowHelp">Whether help should be printed instead of probing.</param>
public sealed record SpikeOptions(
    string OutputPath,
    bool ShowHelp)
{
    /// <summary>
    /// Gets the ignored local artifact path used when no output is specified.
    /// </summary>
    public const string DefaultOutputPath =
        "artifacts/model-inspection/llamasharp/runtime-smoke.json";
}

/// <summary>
/// Contains either valid spike options or one controlled argument error.
/// </summary>
/// <param name="Options">Parsed options when parsing succeeds.</param>
/// <param name="ErrorMessage">Readable argument error when parsing fails.</param>
public sealed record SpikeOptionsParseResult(
    SpikeOptions? Options,
    string? ErrorMessage)
{
    /// <summary>
    /// Gets whether parsing produced usable options.
    /// </summary>
    public bool Succeeded =>
        Options is not null && string.IsNullOrWhiteSpace(ErrorMessage);
}

/// <summary>
/// Parses the Slice 1 command line without adding a third-party parser.
/// </summary>
public static class SpikeOptionsParser
{
    /// <summary>
    /// Parses zero arguments, <c>--help</c>, or
    /// <c>--output &lt;path&gt;</c>.
    /// </summary>
    public static SpikeOptionsParseResult Parse(
        IReadOnlyList<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        if (arguments.Count == 0)
        {
            return Success(
                new SpikeOptions(
                    SpikeOptions.DefaultOutputPath,
                    ShowHelp: false));
        }

        if (arguments.Count == 1 && IsHelp(arguments[0]))
        {
            return Success(
                new SpikeOptions(
                    SpikeOptions.DefaultOutputPath,
                    ShowHelp: true));
        }

        if (string.Equals(
            arguments[0],
            "--output",
            StringComparison.OrdinalIgnoreCase))
        {
            if (arguments.Count != 2 ||
                string.IsNullOrWhiteSpace(arguments[1]))
            {
                return Failure(
                    "The --output option requires a path.");
            }

            return Success(
                new SpikeOptions(
                    arguments[1],
                    ShowHelp: false));
        }

        return Failure(
            $"Unknown argument: {arguments[0]}");
    }

    /// <summary>
    /// Gets the user-facing Slice 1 command help.
    /// </summary>
    public static string HelpText =>
        "Model Inspection LLamaSharp native-backend smoke\n\n" +
        "Usage:\n" +
        "  ModelInspection.LlamaSharpSpike [--output <json-path>]\n" +
        "  ModelInspection.LlamaSharpSpike --help\n\n" +
        "Slice 1 verifies only the published CPU native backend. " +
        "It does not accept or inspect a model file.";

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
