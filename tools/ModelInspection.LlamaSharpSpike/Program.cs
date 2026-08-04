using GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike.ModelProbe;

namespace GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike;

/// <summary>
/// Provides the command-line entry point for the isolated LLamaSharp
/// feasibility gates.
/// </summary>
public static class Program
{
    /// <summary>
    /// Parses options and runs either the native smoke or the read-only
    /// VocabOnly model probe.
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

        SpikeOptions options = parseResult.Options;

        if (options.ShowHelp)
        {
            Console.WriteLine(SpikeOptionsParser.HelpText);
            return 0;
        }

        if (options.RunsModelProbe)
        {
            return await RunModelProbeAsync(options);
        }

        return await RunNativeSmokeAsync(options);
    }

    private static async Task<int> RunNativeSmokeAsync(
        SpikeOptions options)
    {
        NativeBackendSmokeResult result =
            new NativeBackendSmokeProbe().Run();

        try
        {
            string outputPath = await new JsonEvidenceWriter().WriteAsync(
                result,
                options.OutputPath,
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

    private static async Task<int> RunModelProbeAsync(
        SpikeOptions options)
    {
        string modelPath = options.ModelPath!;

        ModelProbeSafetyValidationResult safetyResult =
            ModelProbeSafetyValidator.ValidateOutputPath(
                modelPath,
                options.OutputPath);

        if (!safetyResult.Succeeded)
        {
            Console.Error.WriteLine(
                "MI-OP-INVALID-SPIKE-ARGUMENTS: " +
                safetyResult.ErrorMessage);
            return 2;
        }

        using var cancellationSource = new CancellationTokenSource();

        if (options.CancelAfterMilliseconds.HasValue)
        {
            cancellationSource.CancelAfter(
                options.CancelAfterMilliseconds.Value);
        }

        ConsoleCancelEventHandler cancelHandler = (_, eventArguments) =>
        {
            eventArguments.Cancel = true;
            cancellationSource.Cancel();
        };

        Console.CancelKeyPress += cancelHandler;

        VocabOnlyModelProbeResult result;

        try
        {
            result = await new VocabOnlyModelProbe().RunAsync(
                modelPath,
                cancellationSource.Token);
        }
        finally
        {
            Console.CancelKeyPress -= cancelHandler;
        }

        try
        {
            string outputPath = await new JsonEvidenceWriter().WriteAsync(
                result,
                options.OutputPath,
                CancellationToken.None);

            Console.WriteLine(
                $"VocabOnly model probe: {result.CompletionStatus}");
            Console.WriteLine($"Evidence: {outputPath}");

            if (!result.Succeeded)
            {
                Console.Error.WriteLine(
                    $"{result.FailureCode}: {result.FailureMessage}");
            }

            return result.CompletionStatus switch
            {
                VocabOnlyProbeCompletionStatus.Succeeded => 0,
                VocabOnlyProbeCompletionStatus.Cancelled => 3,
                _ => 1
            };
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
