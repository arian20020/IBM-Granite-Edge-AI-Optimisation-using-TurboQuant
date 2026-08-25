using GraniteEdgeAI.Features.HardwareInspection.Application;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace GraniteEdgeAI.Features.HardwareInspection.Orchestration;

internal sealed class HardwareInspectionService : IHardwareInspectionService
{
    private readonly IHardwareToolAcquisition _toolAcquisition;
    private readonly IHardwareEvidenceCollectionCoordinator _evidenceCollection;
    private readonly IHardwareEvidenceResolver _evidenceResolver;

    internal HardwareInspectionService(
        IHardwareToolAcquisition toolAcquisition,
        IHardwareEvidenceCollectionCoordinator evidenceCollection,
        IHardwareEvidenceResolver evidenceResolver)
    {
        _toolAcquisition = toolAcquisition
            ?? throw new ArgumentNullException(nameof(toolAcquisition));
        _evidenceCollection = evidenceCollection
            ?? throw new ArgumentNullException(nameof(evidenceCollection));
        _evidenceResolver = evidenceResolver
            ?? throw new ArgumentNullException(nameof(evidenceResolver));
    }

    public async Task<HardwareInspectionRunResult> RunAsync(
        Guid inspectionId,
        IProgress<HardwareInspectionRunProgress> progress,
        CancellationToken cancellationToken)
    {
        if (inspectionId == Guid.Empty)
        {
            throw new ArgumentException(
                "Inspection identity cannot be empty.",
                nameof(inspectionId));
        }

        ArgumentNullException.ThrowIfNull(progress);
        if (cancellationToken.IsCancellationRequested)
        {
            return HardwareInspectionRunResult.CreateCancelled(inspectionId);
        }

        ProgressRelay progressRelay = new(inspectionId, progress);
        try
        {
            progressRelay.Report(HardwareInspectionRunStage.StartingHardwareInspection);
            HardwareToolAcquisitionResult acquisition = _toolAcquisition.Acquire();
            if (!acquisition.IsSuccess)
            {
                return HardwareInspectionOutcomePolicy.FromAcquisitionFailure(
                    inspectionId,
                    acquisition.Diagnostic!.Value);
            }

            using HardwareToolLease tools = acquisition.Lease!;
            if (cancellationToken.IsCancellationRequested)
            {
                return HardwareInspectionRunResult.CreateCancelled(inspectionId);
            }

            HardwareEvidenceCollectionResult collection =
                await _evidenceCollection.CollectAsync(
                    tools,
                    progressRelay,
                    cancellationToken);
            if (!collection.IsSuccess)
            {
                return HardwareInspectionOutcomePolicy.FromCollectionFailure(
                    inspectionId,
                    collection.FailureCode!.Value);
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return HardwareInspectionRunResult.CreateCancelled(inspectionId);
            }

            progressRelay.Report(HardwareInspectionRunStage.NormalisingHardwareInformation);
            var resolution = _evidenceResolver.Resolve(
                inspectionId,
                collection.Evidence!);
            if (cancellationToken.IsCancellationRequested)
            {
                return HardwareInspectionRunResult.CreateCancelled(inspectionId);
            }

            progressRelay.Report(HardwareInspectionRunStage.CreatingHardwareReport);
            return HardwareInspectionOutcomePolicy.FromResolution(inspectionId, resolution);
        }
        catch (ProgressCallbackException)
        {
            return HardwareInspectionOutcomePolicy.FromCollectionFailure(
                inspectionId,
                HardwareEvidenceCollectionFailureCode.ProgressCallbackFailure);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return HardwareInspectionRunResult.CreateCancelled(inspectionId);
        }
        catch (Exception exception) when (IsRecoverable(exception))
        {
            return HardwareInspectionOutcomePolicy.FromCollectionFailure(
                inspectionId,
                HardwareEvidenceCollectionFailureCode.OrchestrationFailure);
        }
    }

    private static bool IsRecoverable(Exception exception) => exception is not
        (OutOfMemoryException or StackOverflowException or AccessViolationException);

    private sealed class ProgressRelay(
        Guid inspectionId,
        IProgress<HardwareInspectionRunProgress> destination)
        : IProgress<HardwareInspectionRunStage>
    {
        private long _sequence;

        public void Report(HardwareInspectionRunStage stage)
        {
            HardwareInspectionRunProgress update = new(
                inspectionId,
                checked(++_sequence),
                stage);
            try
            {
                destination.Report(update);
            }
            catch (Exception exception) when (IsRecoverable(exception))
            {
                throw new ProgressCallbackException(exception);
            }
        }
    }

    private sealed class ProgressCallbackException(Exception innerException)
        : Exception("The inspection progress callback failed.", innerException);
}
