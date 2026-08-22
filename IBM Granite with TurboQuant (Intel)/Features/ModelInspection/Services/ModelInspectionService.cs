using GraniteEdgeAI.Features.ModelInspection.Classification;
using GraniteEdgeAI.Features.ModelInspection.Contracts;
using GraniteEdgeAI.Features.ModelInspection.Runtime;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace GraniteEdgeAI.Features.ModelInspection.Services;

/// <summary>
/// Orchestrates the isolated probe and classifies only its trusted completed
/// evidence.
/// </summary>
internal sealed class ModelInspectionService : IModelInspectionService
{
    private readonly ILlamaModelProbe probe;
    private readonly IModelInspectionClassifier classifier;
    private readonly TimeProvider timeProvider;

    internal ModelInspectionService(
        ILlamaModelProbe probe,
        IModelInspectionClassifier classifier)
        : this(probe, classifier, TimeProvider.System)
    {
    }

    internal ModelInspectionService(
        ILlamaModelProbe probe,
        IModelInspectionClassifier classifier,
        TimeProvider timeProvider)
    {
        this.probe = probe ?? throw new ArgumentNullException(nameof(probe));
        this.classifier = classifier ??
            throw new ArgumentNullException(nameof(classifier));
        this.timeProvider = timeProvider ??
            throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task<ModelInspectionExecutionResult> InspectAsync(
        ModelInspectionRequest request,
        IProgress<ModelInspectionProgress>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        DateTimeOffset startedAtUtc = timeProvider.GetUtcNow();
        ModelInspectionProbeResult probeResult = await probe.InspectAsync(
            request,
            progress,
            cancellationToken).ConfigureAwait(false);

        return probeResult.Status switch
        {
            ModelInspectionProbeStatus.Completed =>
                Complete(probeResult.Evidence!, startedAtUtc),
            ModelInspectionProbeStatus.Cancelled =>
                ModelInspectionExecutionResult.Cancelled(cooperative: true),
            ModelInspectionProbeStatus.OperationalFailure =>
                ModelInspectionExecutionResult.OperationalFailure(
                    probeResult.Failure!),
            _ => throw new InvalidOperationException(
                "The model inspection probe returned an unknown terminal state.")
        };
    }

    private ModelInspectionExecutionResult Complete(
        ModelInspectionEvidence evidence,
        DateTimeOffset startedAtUtc)
    {
        DateTimeOffset completedAtUtc = timeProvider.GetUtcNow();
        ModelInspectionResult result = classifier.Classify(
            evidence,
            startedAtUtc,
            completedAtUtc);

        return ModelInspectionExecutionResult.Completed(result);
    }
}
