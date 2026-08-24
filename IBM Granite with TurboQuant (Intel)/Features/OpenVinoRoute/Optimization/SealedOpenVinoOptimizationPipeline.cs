using GraniteEdgeAI.Features.OpenVinoRoute.Conversion;
using GraniteEdgeAI.Features.Prompting;
using GraniteEdgeAI.ModelInspection.WorkerClient.ProtectedWorker;
using GraniteEdgeAI.OpenVino.Contracts;

namespace GraniteEdgeAI.Features.OpenVinoRoute.Optimization;

internal sealed class SealedOpenVinoOptimizationPipeline : IOpenVinoOptimizationPipeline
{
    private readonly SealedOpenVinoConversionPipeline conversionPipeline;

    internal SealedOpenVinoOptimizationPipeline(
        string converterRoot,
        string expectedManifestSha256,
        OpenVinoRouteService routeService,
        IProtectedWorkerSessionFactory? sessionFactory = null)
    {
        conversionPipeline = new SealedOpenVinoConversionPipeline(
            converterRoot,
            expectedManifestSha256,
            routeService,
            sessionFactory);
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
        CancellationToken cancellationToken)
    {
        try
        {
            OpenVinoConversionValidation validation = await conversionPipeline.ValidateAsync(
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
            OpenVinoRuntimeOptions requested = candidate.Runtime.KvCachePrecision switch
            {
                OpenVinoKvCachePrecision.ReleasedDefault =>
                    OpenVinoRuntimeOptions.ReleasedDefault,
                OpenVinoKvCachePrecision.U8 => OpenVinoRuntimeOptions.U8,
                _ => throw new OpenVinoOptimizationException(
                    OpenVinoSupportCode.OptimizationUnsupported)
            };
            (SessionStartedEvent startup, var turn) = await conversionPipeline.SmokeAsync(
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
                startup.ActualKvCachePrecision == "u8"
                    ? OpenVinoKvCachePrecision.U8
                    : OpenVinoKvCachePrecision.ReleasedDefault,
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
}

internal static class OpenVinoSmokeQualityRubric
{
    internal static bool Passes(PromptTurnResult turn, long requestedTokens) =>
        turn.Failure is null &&
        turn.PromptTokenCount > 0 &&
        turn.GeneratedTokenCount == requestedTokens &&
        !turn.Text.Contains('\uFFFD') &&
        turn.Text.All(static character => character is '\r' or '\n' or '\t' ||
            !char.IsControl(character));
}
