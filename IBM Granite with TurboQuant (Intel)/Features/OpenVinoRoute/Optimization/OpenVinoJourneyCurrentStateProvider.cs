using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;
using GraniteEdgeAI.Features.ModelHardwareCompatibility.Infrastructure;
using GraniteEdgeAI.OpenVino.Contracts;

namespace GraniteEdgeAI.Features.OpenVinoRoute.Optimization;

internal sealed class OpenVinoJourneyCurrentStateProvider :
    IOpenVinoOptimizationCurrentStateProvider
{
    private readonly OptimizationExecutionPlan _plan;
    private readonly OptimizationCapabilitySnapshot _capabilities;
    private readonly Func<(bool Valid, OpenVinoBuildEvidence? Builds)> _modelState;
    private readonly Func<bool> _hardwareState;

    internal OpenVinoJourneyCurrentStateProvider(
        OptimizationExecutionPlan plan,
        OptimizationCapabilitySnapshot capabilities,
        Func<(bool Valid, OpenVinoBuildEvidence? Builds)> modelState,
        Func<bool> hardwareState)
    {
        _plan = plan ?? throw new ArgumentNullException(nameof(plan));
        _capabilities = capabilities ?? throw new ArgumentNullException(nameof(capabilities));
        _modelState = modelState ?? throw new ArgumentNullException(nameof(modelState));
        _hardwareState = hardwareState ?? throw new ArgumentNullException(nameof(hardwareState));
    }

    public ValueTask<OpenVinoOptimizationCurrentState> GetCurrentStateAsync(
        OpenVinoOptimizationCheckpoint checkpoint,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        (bool valid, OpenVinoBuildEvidence? builds) = _modelState();
        if (!valid || builds is null || !_hardwareState())
        {
            OpenVinoOptimizationCapabilityEvidence expected =
                OpenVinoOfficialCapabilityEvidenceFactory.Create(
                    new OpenVinoBuildEvidence(
                        _plan.ExecutionPayload.OpenVino!.BuildIdentity.RuntimeBuild,
                        _plan.ExecutionPayload.OpenVino.BuildIdentity.GenAiBuild,
                        _plan.ExecutionPayload.OpenVino.BuildIdentity.TokenizersBuild,
                        _plan.ExecutionPayload.OpenVino.BuildIdentity.WorkerManifestDigest));
            return ValueTask.FromResult(new OpenVinoOptimizationCurrentState(
                _capabilities,
                expected,
                "stale-model-run",
                "stale-model-handoff",
                "stale-hardware-run",
                _plan.Binding.HardwareSnapshotSha256));
        }

        OpenVinoOptimizationCapabilityEvidence evidence =
            OpenVinoOfficialCapabilityEvidenceFactory.Create(builds);
        OpenVinoCapabilityPayload payload =
            OpenVinoOptimizationCapabilityProjector.Project(evidence);
        OptimizationCapabilitySnapshot currentCapabilities =
            OptimizationCapabilitySnapshot.ForOpenVino(
                _capabilities.SnapshotId,
                OpenVinoCompatibilityEvaluator.CapabilityDigest(payload),
                payload);
        return ValueTask.FromResult(new OpenVinoOptimizationCurrentState(
            currentCapabilities,
            evidence,
            _plan.Binding.ModelInspectionRunId,
            _plan.Binding.ModelInspectionHandoffId,
            _plan.Binding.ProductHardwareRunId,
            _plan.Binding.HardwareSnapshotSha256));
    }
}
