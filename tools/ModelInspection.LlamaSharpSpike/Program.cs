namespace GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike;

/// <summary>
/// Provides the command-line entry point for the isolated native-backend smoke
/// gate.
/// </summary>
public static class Program
{
    /// <summary>
    /// Parses options, performs one dry run and writes local JSON evidence.
    /// </summary>
    public static async Task<int> Main(string[] args)
    {
        SpikeOptionsParseResult parseResult =
            SpikeOptionsParser.Parse(args);

        if (!parseResult.Succeeded || parseResult.Options is null)
        {
            Console.Error.WriteLine(
                "MI-OP-INVALID-SPIKE-ARGUMENTS: " +
                parseResult.ErrorMessage);
            Console.Error.WriteLine();
            Console.Error.WriteLine(SpikeOptionsParser.HelpText);
            return 2;
        }

        if (parseResult.Options.ShowHelp)
        {
            Console.WriteLine(SpikeOptionsParser.HelpText);
            return 0;
        }

        NativeBackendSmokeResult result =
            new NativeBackendSmokeProbe().Run();

        try
        {
            string outputPath = await new SmokeEvidenceWriter().WriteAsync(
                result,
                parseResult.Options.OutputPath,
                CancellationToken.None);

            Console.WriteLine(
                result.Succeeded
                    ? "LLamaSharp CPU backend dry run succeeded."
                    : "LLamaSharp CPU backend dry run failed.");
            Console.WriteLine($"Evidence: {outputPath}");

            if (!result.Succeeded)
            {
                Console.Error.WriteLine(
                    $"{result.FailureCode}: {result.FailureMessage}");
            }

            return result.Succeeded ? 0 : 1;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(
                "MI-OP-EVIDENCE-WRITE-FAILED: " +
                exception.Message);
            return 1;
        }
    }
}
