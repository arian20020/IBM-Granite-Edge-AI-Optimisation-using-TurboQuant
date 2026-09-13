using GraniteEdgeAI.Features.OpenVinoRoute.Conversion;
using GraniteEdgeAI.Features.Prompting;
using GraniteEdgeAI.ModelInspection.WorkerClient.ProtectedWorker;
using GraniteEdgeAI.OpenVino.Contracts;

namespace GraniteEdgeAI.Features.OpenVinoRoute.Optimization;

internal sealed class SealedOpenVinoOptimizationPipeline : IOpenVinoOptimizationPipeline
{
    private readonly SealedOpenVinoConversionPipeline conversionPipeline;
    private readonly SealedOpenVinoConversionPipeline? turboQuantConversionPipeline;

    internal SealedOpenVinoOptimizationPipeline(
        string converterRoot,
        string expectedManifestSha256,
        OpenVinoRouteService routeService,
        OpenVinoRouteService? turboQuantRouteService = null,
        IProtectedWorkerSessionFactory? sessionFactory = null)
    {
        conversionPipeline = new SealedOpenVinoConversionPipeline(
            converterRoot,
            expectedManifestSha256,
            routeService,
            sessionFactory);
        if (turboQuantRouteService is not null)
        {
            turboQuantConversionPipeline = new SealedOpenVinoConversionPipeline(
                converterRoot,
                expectedManifestSha256,
                turboQuantRouteService,
                sessionFactory);
        }
    }

    public async Task<OpenVinoOptimizationCompletion> OptimizeAsync(
        OpenVinoOptimizationInvocation invocation,
        CancellationToken cancellationToken)
    {
        string precision = invocation.Candidate.PersistentArtifact.WeightPrecision switch
        {
            OpenVinoWeightPrecision.Fp16 => "fp16",
            OpenVinoWeightPrecision.EightBit => "int8",
            OpenVinoWeightPrecision.FourBit => "int4",
            OpenVinoWeightPrecision.MxFp4 => "mxfp4",
            _ => throw new OpenVinoOptimizationException(
                OpenVinoSupportCode.OptimizationUnsupported)
        };
        try
        {
            OpenVinoConverterCompletion completion =
                await conversionPipeline.OptimizePackageAsync(
                    invocation.OperationId,
                    invocation.SourceDirectory,
                    invocation.StagingDirectory,
                    invocation.SourceManifestSha256,
                    precision,
                    cancellationToken).ConfigureAwait(false);
            return new OpenVinoOptimizationCompletion(
                invocation.Candidate.PersistentArtifact.WeightPrecision,
                completion.Versions);
        }
        catch (OpenVinoConversionException failure)
        {
            throw new OpenVinoOptimizationException(failure.SupportCode);
        }
    }

    public async Task<OpenVinoOptimizationValidation> ValidateAsync(
        string stagingDirectory,
        OpenVinoOptimizationCandidate candidate,
        CancellationToken cancellationToken)
    {
        try
        {
            OpenVinoConversionValidation validation = await RuntimePipeline(candidate).ValidateAsync(
                stagingDirectory,
                cancellationToken).ConfigureAwait(false);
            return new OpenVinoOptimizationValidation(
                validation.ValidationRunId,
                validation);
        }
        catch (OpenVinoConversionException failure)
        {
            throw new OpenVinoOptimizationException(failure.SupportCode);
        }
    }

    public async Task<OpenVinoRuntimeOptimizationEvidence> SmokeAsync(
        OpenVinoOptimizationValidation validation,
        string stagingDirectory,
        OpenVinoOptimizationCandidate candidate,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        OpenVinoRuntimeTechnicalConfiguration.From(candidate).ValidateSupported();
        using IDisposable retained = validation.ConsumeLease();
        if (retained is not OpenVinoConversionValidation nativeValidation)
        {
            throw new OpenVinoOptimizationException(
                OpenVinoSupportCode.ConversionOutputInvalid);
        }
        try
        {
            SealedOpenVinoConversionPipeline smokePipeline = RuntimePipeline(candidate);
            OpenVinoRuntimeOptions requested = candidate.Runtime.KvCachePrecision switch
            {
                OpenVinoKvCachePrecision.ReleasedDefault =>
                    OpenVinoRuntimeOptions.ReleasedDefault,
                OpenVinoKvCachePrecision.U8 => OpenVinoRuntimeOptions.U8,
                OpenVinoKvCachePrecision.U4 => OpenVinoRuntimeOptions.U4,
                OpenVinoKvCachePrecision.Tbq4 => OpenVinoRuntimeOptions.Tbq4,
                OpenVinoKvCachePrecision.Tbq3 => OpenVinoRuntimeOptions.Tbq3,
                _ => throw new OpenVinoOptimizationException(
                    OpenVinoSupportCode.OptimizationUnsupported)
            };
            (SessionStartedEvent startup, var turn) = await smokePipeline.SmokeAsync(
                nativeValidation,
                stagingDirectory,
                requested,
                cancellationToken).ConfigureAwait(false);
            bool generationPassed = turn.Status == PromptTurnStatus.Completed &&
                turn.GeneratedTokenCount > 0;
            bool qualityPassed = OpenVinoSmokeQualityRubric.Passes(
                turn,
                SealedOpenVinoConversionPipeline.SmokeRequestedTokens);
            return new OpenVinoRuntimeOptimizationEvidence(
                startup.ActualExecutionDevices.Single(),
                startup.ActualKvCachePrecision switch
                {
                    "u8" => OpenVinoKvCachePrecision.U8,
                    "u4" => OpenVinoKvCachePrecision.U4,
                    "tbq4" => OpenVinoKvCachePrecision.Tbq4,
                    "tbq3" => OpenVinoKvCachePrecision.Tbq3,
                    "released-default" =>
                        OpenVinoKvCachePrecision.ReleasedDefault,
                    _ => throw new OpenVinoOptimizationException(
                        OpenVinoSupportCode.RuntimeIntegrityFailed)
                },
                generationPassed ? "passed" : "failed",
                qualityPassed ? "passed" : "failed");
        }
        catch (OpenVinoConversionException failure)
        {
            throw new OpenVinoOptimizationException(failure.SupportCode);
        }
    }

    public Task<Guid> ReinspectPublishedAsync(
        string destinationDirectory,
        CancellationToken cancellationToken) =>
        conversionPipeline.ReinspectPublishedAsync(destinationDirectory, cancellationToken);

    private SealedOpenVinoConversionPipeline RuntimePipeline(
        OpenVinoOptimizationCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        OpenVinoRuntimeTechnicalConfiguration.From(candidate).ValidateSupported();
        return candidate.Runtime.KvCachePrecision is
            OpenVinoKvCachePrecision.Tbq4 or OpenVinoKvCachePrecision.Tbq3
            ? turboQuantConversionPipeline ?? throw new OpenVinoOptimizationException(
                OpenVinoSupportCode.OptimizationUnsupported)
            : conversionPipeline;
    }
}

internal static class OpenVinoSmokeQualityRubric
{
    internal static bool Passes(PromptTurnResult turn, long requestedTokens) =>
        requestedTokens > 0 &&
        turn.Status == PromptTurnStatus.Completed &&
        turn.Failure is null &&
        turn.PromptTokenCount > 0 &&
        turn.GeneratedTokenCount > 0 &&
        turn.GeneratedTokenCount <= requestedTokens &&
        !turn.Text.Contains('\uFFFD') &&
        turn.Text.Any(char.IsLetterOrDigit) &&
        turn.Text.All(static character => character is '\r' or '\n' or '\t' ||
            !char.IsControl(character));
}
