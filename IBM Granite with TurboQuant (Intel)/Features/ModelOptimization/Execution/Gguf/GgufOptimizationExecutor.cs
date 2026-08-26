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
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.Gguf;

namespace GraniteEdgeAI.Features.ModelOptimization.Execution.Gguf;

internal sealed class GgufOptimizationExecutor : IOptimizationExecutor
{
    private const int ExecutorContractVersion = 3;
    private readonly OptimizationOutputRegistry _outputs;
    private readonly IGgufQuantizationRunner _runner;
    private readonly TimeProvider _timeProvider;

    internal GgufOptimizationExecutor(
        OptimizationOutputRegistry outputs,
        IGgufQuantizationRunner runner,
        TimeProvider? timeProvider = null)
    {
        _outputs = outputs ?? throw new ArgumentNullException(nameof(outputs));
        _runner = runner ?? throw new ArgumentNullException(nameof(runner));
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
            foreach (OptimizationProgressStage stage in Enum.GetValues<OptimizationProgressStage>())
            {
                Report(progress, plan, context, stage, 1d);
            }
            return OptimizationExecutionResult.Succeeded(
                plan,
                $"gguf-profile-{plan.OptimizationPlanId:N}",
                plan.ConfigurationSha256,
                0,
                sourceUnchanged: true,
                _timeProvider.GetUtcNow());
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

        Report(progress, plan, context, OptimizationProgressStage.Validate, 0.7);
        if (!context.Source.RehashMatches())
        {
            return Replan(plan, OptimizationSupportCode.SourceIdentityMismatch, context);
        }
        Report(progress, plan, context, OptimizationProgressStage.SmokeTest, 0.8);
        Report(progress, plan, context, OptimizationProgressStage.Reinspect, 0.9);
        SealedOptimizationCandidate candidate = output.Seal(outputToken);
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
            _timeProvider.GetUtcNow());
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
}
