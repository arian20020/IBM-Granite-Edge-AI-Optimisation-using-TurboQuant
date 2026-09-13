using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GraniteEdgeAI.Features.ModelOptimization.Journey;
using GraniteEdgeAI.Features.ModelOptimization.Storage;
using GraniteEdgeAI.GgufQuantization.Capabilities;
using GraniteEdgeAI.GgufQuantization.Contracts;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Execution;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Evidence;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.Gguf;

namespace GraniteEdgeAI.Features.ModelOptimization.Execution.Gguf;

internal sealed class GgufOptimizationExecutor : IOptimizationExecutor
{
    private const int ExecutorContractVersion = 3;
    private const string Bf16Q3EvidenceId = "GGUF-V5-BF16-Q3-CPU-F16-01";
    private const string Bf16Q3OutputSha256 =
        "77eeab5d868bee624e283f141d5b6be3fa4f4a59b7c78b0163c3a28c7a80a3ca";
    private readonly OptimizationOutputRegistry _outputs;
    private readonly IGgufQuantizationRunner _runner;
    private readonly IGgufOptimizationOutputValidator _validator;
    private readonly TimeProvider _timeProvider;

    internal GgufOptimizationExecutor(
        OptimizationOutputRegistry outputs,
        IGgufQuantizationRunner runner,
        IGgufOptimizationOutputValidator validator,
        TimeProvider? timeProvider = null)
    {
        _outputs = outputs ?? throw new ArgumentNullException(nameof(outputs));
        _runner = runner ?? throw new ArgumentNullException(nameof(runner));
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public OptimizationRoute Route => OptimizationRoute.Gguf;

    public async Task<OptimizationExecutionResult> ExecuteAsync(
        OptimizationExecutionPlan plan,
        OptimizationAttemptContext context,
        IProgress<OptimizationProgress> progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(progress);

        if (plan.Route != Route
            || !plan.IsExecutableBy(ExecutorContractVersion)
            || plan.ExecutionPayload.Gguf is not { } payload)
        {
            return Replan(plan, OptimizationSupportCode.ToolNotAdmitted, context);
        }

        Report(progress, plan, context, OptimizationProgressStage.Preflight, 0.05);
        if (!context.Source.RehashMatches()
            || !plan.MatchesSource(
                context.Source.SourceSha256,
                context.Source.SourceLengthBytes))
        {
            return Replan(plan, OptimizationSupportCode.SourceIdentityMismatch, context);
        }

        if (!payload.RequiresPersistentConversion)
        {
            var runtimeValidationProgress = CreateValidationProgress(
                progress,
                plan,
                context);
            try
            {
                using FileStream source = context.Source.OpenReadVerified();
                OptimizationSupportCode runtimeValidation =
                    await _validator.ValidateRuntimeProfileAsync(
                    plan,
                    source.Name,
                    runtimeValidationProgress,
                    cancellationToken).ConfigureAwait(false);
                bool sourceMatches = context.Source.RehashMatches();
                if (!sourceMatches)
                {
                    return Replan(
                        plan,
                        OptimizationSupportCode.SourceIdentityMismatch,
                        context);
                }
                if (runtimeValidation != OptimizationSupportCode.None)
                {
                    return runtimeValidation is
                        OptimizationSupportCode.SourceIdentityMismatch
                            or OptimizationSupportCode.ToolNotAdmitted
                        ? Replan(plan, runtimeValidation, context)
                        : OptimizationExecutionResult.Failed(
                            plan,
                            runtimeValidation,
                            sourceUnchanged: true,
                            _timeProvider.GetUtcNow());
                }
                return OptimizationExecutionResult.Succeeded(
                    plan,
                    $"gguf-profile-{plan.OptimizationPlanId:N}",
                    plan.ConfigurationSha256,
                    0,
                    sourceUnchanged: true,
                    _timeProvider.GetUtcNow());
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception) when (exception is
                ArgumentException or IOException or InvalidDataException
                    or InvalidOperationException or UnauthorizedAccessException)
            {
                return OptimizationExecutionResult.ReplanRequired(
                    plan,
                    OptimizationSupportCode.ToolNotAdmitted,
                    sourceUnchanged: false,
                    _timeProvider.GetUtcNow());
            }
        }

        if (payload.Quantiser is not { } quantiser
            || payload.ConversionSource is not { } sourceBinding
            || !string.Equals(
                quantiser.ExecutableSha256,
                _runner.ExecutableSha256,
                StringComparison.Ordinal))
        {
            return Replan(plan, OptimizationSupportCode.ToolNotAdmitted, context);
        }
        GgufQuantizationFormat sourceFormat = ToContract(sourceBinding.Precision);
        GgufQuantizationFormat targetFormat =
            GgufQuantizerFormatMap.ToContract(payload.PersistentTargetWeightFormat);
        GgufAdmittedConfiguration? admittedTarget =
            plan.CapabilitySnapshot.Gguf?.Admitted.SingleOrDefault(candidate =>
                string.Equals(candidate.EvidenceId, plan.Candidate.EvidenceId, StringComparison.Ordinal)
                && candidate.Weights == payload.PersistentTargetWeightFormat);
        if (admittedTarget is null)
        {
            return Replan(plan, OptimizationSupportCode.CapabilityDrift, context);
        }

        string? authorizationSha256 = null;
        if (GgufRequantizationAuthorization.IsRequired(sourceFormat, targetFormat))
        {
            authorizationSha256 = GgufRequantizationAuthorization.Create(
                plan.OptimizationPlanId,
                plan.ConfigurationSha256,
                plan.Binding.ModelSha256,
                plan.Binding.ModelLengthBytes,
                sourceFormat,
                targetFormat,
                _runner.ManifestSha256,
                GgufQuantizationProtocol.RequantizationPolicyVersion,
                admittedTarget,
                payload.RequantisationPolicy).Sha256;
        }

        string outputToken = $"gguf-output-{context.Generation}";
        GgufQuantizationCommand command = GgufQuantizationCommand.Create(
            Guid.NewGuid(),
            plan.OptimizationPlanId,
            plan.ConfigurationSha256,
            context.Source.SealedSnapshotIdentity,
            outputToken,
            sourceFormat,
            targetFormat,
            _runner.ManifestSha256,
            GgufQuantizationProtocol.RequantizationPolicyVersion,
            authorizationSha256);

        using OptimizationOutputLease output =
            _outputs.CreateLease(plan, context.Generation);
        string outputPath = output.CreatePendingFilePath("model.gguf");
        string sourcePath;
        using (FileStream source = context.Source.OpenReadVerified())
        {
            sourcePath = source.Name;
        }

        Report(progress, plan, context, OptimizationProgressStage.PrepareStaging, 0.15);
        Report(progress, plan, context, OptimizationProgressStage.Optimise, 0.35);
        GgufQuantizationEvent terminal = await _runner.RunAsync(
            command,
            sourcePath,
            context.Source.SourceSha256,
            context.Source.SourceLengthBytes,
            outputPath,
            cancellationToken).ConfigureAwait(false);
        if (terminal.Kind != GgufQuantizationEventKind.Completed
            || !string.Equals(terminal.OutputToken, outputToken, StringComparison.Ordinal))
        {
            return OptimizationExecutionResult.Failed(
                plan,
                MapFailure(terminal.SupportCode),
                context.Source.RehashMatches(),
                _timeProvider.GetUtcNow());
        }

        if (!context.Source.RehashMatches())
        {
            return Replan(plan, OptimizationSupportCode.SourceIdentityMismatch, context);
        }
        IProgress<GgufOptimizationOutputValidationPhase> validationProgress =
            CreateValidationProgress(progress, plan, context);
        GgufOptimizationOutputValidationResult validation =
            await _validator.ValidateAsync(
                plan,
                outputPath,
                validationProgress,
                cancellationToken).ConfigureAwait(false);
        if (!validation.Succeeded)
        {
            if (!context.Source.RehashMatches())
            {
                return Replan(
                    plan,
                    OptimizationSupportCode.SourceIdentityMismatch,
                    context);
            }
            return OptimizationExecutionResult.Failed(
                plan,
                validation.SupportCode,
                sourceUnchanged: true,
                _timeProvider.GetUtcNow());
        }
        if (validation.ValidatedTarget != payload.PersistentTargetWeightFormat
            || validation.OutputSha256 is null
            || validation.OutputLengthBytes == 0)
        {
            return OptimizationExecutionResult.Failed(
                plan,
                OptimizationSupportCode.ReinspectionFailed,
                context.Source.RehashMatches(),
                _timeProvider.GetUtcNow());
        }
        if (!MatchesMeasuredOutputIdentity(plan, validation))
        {
            return OptimizationExecutionResult.Failed(
                plan,
                OptimizationSupportCode.ReinspectionFailed,
                context.Source.RehashMatches(),
                _timeProvider.GetUtcNow());
        }
        if (!context.Source.RehashMatches())
        {
            return Replan(plan, OptimizationSupportCode.SourceIdentityMismatch, context);
        }
        SealedOptimizationCandidate candidate;
        try
        {
            candidate = output.Seal(
                outputToken,
                validation.OutputSha256,
                validation.OutputLengthBytes);
        }
        catch (Exception exception) when (exception is
            ArgumentException or IOException or InvalidDataException
                or InvalidOperationException)
        {
            return OptimizationExecutionResult.Failed(
                plan,
                OptimizationSupportCode.ReinspectionFailed,
                context.Source.RehashMatches(),
                _timeProvider.GetUtcNow());
        }
        Report(progress, plan, context, OptimizationProgressStage.Publish, 0.95);
        OptimizationCommitReceipt receipt = await _outputs.AdmitAsync(
            plan,
            context.Generation,
            context.Source,
            sourceUnchanged: true,
            candidate,
            cancellationToken).ConfigureAwait(false);
        Report(progress, plan, context, OptimizationProgressStage.Publish, 1d);

        return OptimizationExecutionResult.Succeeded(
            plan,
            receipt.Key.OutputIdentity,
            receipt.Key.OutputManifestSha256,
            receipt.OutputSizeBytes,
            sourceUnchanged: true,
            _timeProvider.GetUtcNow(),
            receipt.Key.ExecutionId);
    }

    internal static bool MatchesMeasuredOutputIdentity(
        OptimizationExecutionPlan plan,
        GgufOptimizationOutputValidationResult validation)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(validation);
        return !string.Equals(
                plan.Candidate.EvidenceId,
                Bf16Q3EvidenceId,
                StringComparison.Ordinal)
            || (string.Equals(
                    validation.OutputSha256,
                    Bf16Q3OutputSha256,
                    StringComparison.Ordinal)
                && validation.OutputLengthBytes
                    == VerifiedGgufOptimizationEvidence.Bf16Q3OutputLengthBytes);
    }

    private static void Report(
        IProgress<OptimizationProgress> progress,
        OptimizationExecutionPlan plan,
        OptimizationAttemptContext context,
        OptimizationProgressStage stage,
        double fraction) =>
        progress.Report(new OptimizationProgress(
            context.Generation,
            plan.OptimizationPlanId,
            plan.ConfigurationSha256,
            stage,
            fraction,
            fraction >= 1d
                ? OptimizationProgressStatusKey.Completed
                : OptimizationProgressStatusKey.Active));

    private static IProgress<GgufOptimizationOutputValidationPhase>
        CreateValidationProgress(
            IProgress<OptimizationProgress> progress,
            OptimizationExecutionPlan plan,
            OptimizationAttemptContext context) =>
        new InlineValidationProgress(phase =>
            Report(
                progress,
                plan,
                context,
                phase switch
                {
                    GgufOptimizationOutputValidationPhase.Validate =>
                        OptimizationProgressStage.Validate,
                    GgufOptimizationOutputValidationPhase.SmokeTest =>
                        OptimizationProgressStage.SmokeTest,
                    GgufOptimizationOutputValidationPhase.Reinspect =>
                        OptimizationProgressStage.Reinspect,
                    _ => throw new ArgumentOutOfRangeException(nameof(phase)),
                },
                phase switch
                {
                    GgufOptimizationOutputValidationPhase.Validate => 0.7,
                    GgufOptimizationOutputValidationPhase.SmokeTest => 0.8,
                    GgufOptimizationOutputValidationPhase.Reinspect => 0.9,
                    _ => throw new ArgumentOutOfRangeException(nameof(phase)),
                }));

    private OptimizationExecutionResult Replan(
        OptimizationExecutionPlan plan,
        OptimizationSupportCode code,
        OptimizationAttemptContext context) =>
        OptimizationExecutionResult.ReplanRequired(
            plan,
            code,
            context.Source.RehashMatches(),
            _timeProvider.GetUtcNow());

    private static OptimizationSupportCode MapFailure(
        GgufQuantizationSupportCode code) => code switch
        {
            GgufQuantizationSupportCode.PackageVerificationFailed =>
                OptimizationSupportCode.ToolNotAdmitted,
            GgufQuantizationSupportCode.SourceChanged =>
                OptimizationSupportCode.SourceIdentityMismatch,
            GgufQuantizationSupportCode.OutputInvalid =>
                OptimizationSupportCode.ValidationFailed,
            GgufQuantizationSupportCode.Cancelled =>
                OptimizationSupportCode.CancelledByUser,
            _ => OptimizationSupportCode.ConversionFailed,
        };

    private static GgufQuantizationFormat ToContract(
        WeightQuantisation precision) => precision switch
        {
            WeightQuantisation.F32 => GgufQuantizationFormat.F32,
            WeightQuantisation.BF16 => GgufQuantizationFormat.BF16,
            WeightQuantisation.F16 => GgufQuantizationFormat.F16,
            WeightQuantisation.Q8_0 => GgufQuantizationFormat.Q8_0,
            WeightQuantisation.Q6_K => GgufQuantizationFormat.Q6K,
            WeightQuantisation.Q5_K_M => GgufQuantizationFormat.Q5KM,
            WeightQuantisation.Q4_K_M => GgufQuantizationFormat.Q4KM,
            WeightQuantisation.Q3_K_M => GgufQuantizationFormat.Q3KM,
            WeightQuantisation.Q2_K => GgufQuantizationFormat.Q2K,
            _ => throw new ArgumentOutOfRangeException(
                nameof(precision), precision, "The source format cannot be executed."),
        };

    private sealed class InlineValidationProgress(
        Action<GgufOptimizationOutputValidationPhase> report)
        : IProgress<GgufOptimizationOutputValidationPhase>
    {
        public void Report(GgufOptimizationOutputValidationPhase value) =>
            report(value);
    }
}
